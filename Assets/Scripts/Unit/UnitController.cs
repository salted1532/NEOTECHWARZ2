using System.Collections;
using System.Net;
using System.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using static UnityEditor.PlayerSettings;
using static UnityEngine.GraphicsBuffer;

// 공격 수단의 종류. 피격 이펙트를 공격자에 따라 다르게 재생하기 위해 HealthManager.GetDamage에 실어 보낸다
// (UnitEffects가 이 값으로 총기/폭발형/레이저/화염 중 어떤 피격 이펙트를 재생할지 고른다).
public enum AttackEffectType { Bullet, Explosive, Laser, Flame }

// 개별 유닛(일꾼/전투유닛/공중유닛 포함)의 이동, 전투, 순찰, 자원 채취(일꾼 전용) 상태머신을 담당하는 핵심 컴포넌트.
// NavMeshAgent 기반 지상 이동과 직접 좌표 보간 기반 공중 이동을 모두 지원하며,
// AttackRange가 사거리 내 적을 감지하면 이 컴포넌트의 Attack/ChaseTarget을 호출한다.
public class UnitController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject unitMarker;

    [SerializeField]
    private Sprite icon; // Squad_panel 등 선택 UI에 표시할 아이콘

    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;

    // ===== 전투 스탯 (공격력/방어력) =====
    // 공격력은 기존 AttackRange.AttackDamage였던 것을 이곳으로 옮겨 UnitController가 함께 관리한다.
    // Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값이기도 하다.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
    // 이 유닛의 공격 수단 (총기 든 유닛은 Bullet, 탱크류는 Explosive 등) - 피격 이펙트 선택에 사용됨
    [SerializeField] private AttackEffectType attackType = AttackEffectType.Bullet;

    // 이 유닛이 "공격받을 때" 적용되는 분류 (DamageMultiplierTableSO/고유 보너스 판정에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;

    // 이 유닛이 "공격할 때" 적용되는 제한 - 지상/공중 유닛을 각각 공격할 수 있는지
    // (UnitDataSO.canAttackGround/canAttackAir가 ApplyUnitData에서 그대로 반영됨). 둘 다 기본 true(제한 없음).
    [SerializeField] private bool canAttackGround = true;
    [SerializeField] private bool canAttackAir = true;

    [Header("고유 추가 데미지 (해당 없으면 Percent를 0으로 둘 것)")]
    [Tooltip("이 유닛이 특정 장갑 타입 상대로만 추가 데미지를 줄 때 설정 (예: 저격수 = Heavy, 80)")]
    [SerializeField] private ArmorType bonusVersusArmorType = ArmorType.Light;
    [SerializeField] private float bonusVersusArmorPercent = 0f;

    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private float moveSpeed = 10f;
    [SerializeField]
    private float arriveDistance = 0.5f;
    [SerializeField]
    private float stuckTimer; //갇히거나 행동 실행 불가시 타이머

    private Vector3 targetPosition;
    [SerializeField]
    private bool isMovingAirUnit = false;
    [SerializeField]
    private bool isAirUnit;
    // 공중 유닛이 지면으로부터 띄워서 날아다니는 높이. 목적지 자체가 이미 공중에 뜬 대상(다른 공중유닛/이륙한
    // 건물)의 좌표일 땐 여기에 또 더하지 않는다(AirTargetPosition의 destinationIsAirborne 참고) - 안 그러면
    // 고도가 중첩(예: 5+5=10)돼서 그 대상 머리 위로 솟구쳐버린다.
    [SerializeField]
    private float airCruiseAltitude = 5f;

    // 공중 유닛이 이동 중 실시간으로 자기 발밑 지면 높이를 알아내기 위한 레이어(지형/땅). 비워두면(Nothing)
    // 지면 높이 추적 없이 목적지 고도로 곧장 직선 이동한다(이 경우 언덕을 완전히 넘기 전에 미리 하강해서
    // 언덕에 파묻히듯 스칠 수 있음 - 아래 SampleGroundHeight 주석 참고).
    [SerializeField]
    private LayerMask airGroundLayer;

    // ===== 상태 하나로 통합 =====
    private enum UnitState
    {
        Idle,
        Move,
        Attack
    }

    [SerializeField]
    private UnitState UnitcurrentState = UnitState.Idle;

    private bool arrived = false;
    public bool alreadyAttacked = false;
    public float timeBetweenAttacks;

    private bool patrolling = false;
    private bool goingToEnd = true;

    private Vector3 startPoint;
    private Vector3 endPoint;

    // ===== 필드 추가 =====
    private bool isWorker;

    private enum GatherState { None, MovingToResource, WaitingInQueue, Gathering, MovingToBase, Depositing }
    private GatherState gatherState = GatherState.None;

    private int amountPerTrip = 5; //자원 채취량
    private float gatherDuration = 3f; //자원 채취 시간

    private const float alternateResourceSearchRadius = 10f; // 목표 자원 대기열이 꽉 찼을 때 대체 자원을 찾는 반경

    private ResourceNode gatherTargetNode;
    private float gatherTimer;
    private int carryingAmount;
    private ResourceType carryingType;

    // 차량형 유닛의 포탑(자식 오브젝트에 TurretController가 붙어있으면 세팅됨, 없으면 null - 일반 유닛은 영향 없음).
    // 있으면 Attack()이 몸체 회전(RotateYOnly)을 건너뛰고 포탑이 대신 조준하며, 데미지가 들어갈 때 반동을 재생한다.
    private TurretController turretController;

    private RTSUnitController rtsController;   // 기존 Start()에서 지역변수였던 것을 필드로 승격

    [SerializeField] 
    private GameObject DepositOre;
    [SerializeField] 
    private GameObject DepositGas;

    [SerializeField] private float gatherInteractRange = 2f; // 장애물 특성상 arriveDistance보다 넉넉하게

    private Transform depositTargetTransform; // Gathering 단계에서만 쓰던 지역변수를 필드로 승격

    [SerializeField] private float gatherAgentRadius = 0.1f; // 채취 중 서로 부딪히는 것 방지용 축소 반경
    private float defaultAgentRadius;

    // ===== 공중 유닛 겹침 분리 (이동 중엔 통과 허용, 정지/공격 중엔 분리) =====
    // 이 유닛 자신이 차지하는 "절반의 분리 반경". 두 유닛 사이에 필요한 분리 거리는 항상
    // (this.airUnitRadius + other.airUnitRadius)로 계산되므로, 큰 유닛일수록 이 값을 키우면
    // 그 유닛이 낀 모든 페어가 자동으로 더 멀리 떨어져서 풀린다 (유닛 크기에 비례한 분리).
    [SerializeField] private float airUnitRadius = 0.6f;    // 기본값 0.6 = 기존 고정 분리거리(1.2)와 동일한 결과
    [SerializeField] private float airSeparationSpeed = 4f; // 밀려나는 속도(초당)

    // ===== 공격 명령 (우클릭 적 지정 / A 모드) =====
    [SerializeField] private float chaseLoseSightRange = 20f; // 지정 추격 대상과 이 거리 이상 벌어지면 "시야 이탈"로 간주

    private EnemyController orderedTarget;   // 명시적으로 지정된 추격 대상 (없으면 null)
    private Vector3? attackMoveDestination;  // 공격-이동 목적지 / 추격 중 마지막으로 확인된 위치 (교전 후 복귀할 지점)
    private AttackRange attackRange;         // 사거리 내 교전 대상 존재 여부 조회용 (자식 컴포넌트)
    // 지정 추격 대상과 한 번이라도 사거리 안에서 접촉했는지. 접촉 전(예: 맵 반대편의 먼 적을 지정한 직후)에는
    // 아무리 멀어도 "시야 이탈"로 취급하지 않고 무조건 계속 쫓아간다 - 그래야 이동 도중 우연히 지나치는
    // 다른 적에게 한눈팔지 않고 지정한 대상까지 끝까지 간다. 접촉 이후에만 chaseLoseSightRange가 적용된다.
    private bool hasEngagedOrderedTarget;

    // 아군 강제 공격 대상 (A 모드에서 아군 유닛/건물 좌클릭). MonoBehaviour로 두어 UnitController(유닛)와
    // BuildingController(건물) 둘 다 받을 수 있게 한다 (둘 다 .transform/.gameObject로 충분).
    // 적과 달리 시야 개념 없이 죽을 때까지 끝까지 추격/공격한다 (건물은 이동하지 않으므로 추격은 사실상 접근만 함).
    private MonoBehaviour friendlyTarget;
    // friendlyTarget이 죽어서(파괴되어) 이번 프레임에 Unity의 fake-null로 바뀌었는지 판별하기 위한 플래그.
    // (파괴된 순간부터 friendlyTarget == null이 즉시 true가 되므로, 이 플래그 없이는 "막 끝났다"는
    //  전이 시점을 알 수 없어 유닛이 정지된 채로 영원히 멈춰버린다.)
    private bool hasFriendlyOrder;

    private Coroutine markerFlashRoutine; // 공격 대상 지정 피드백 깜빡임 (Enemy/ResourceNode와 동일한 패턴)
    [SerializeField] private float markerFlashInterval = 0.3f;
    [SerializeField] private int markerFlashCount = 3;

    // ===== 아군 유닛 우클릭 = 계속 따라다니기 (공격 명령 아님, Idle 상태 유지) =====
    // Attack 상태가 아니라 Idle로 유지해야 AttackRange가 사거리 내 적을 자동으로 교전한다 (AttackMoveTo와 동일한 이유).
    private UnitController followTarget;
    private bool hasFollowOrder;
    // 지상 유닛이 정지할 때 실제 몸체 반경(this.navMeshAgent.radius + 대상의 반경, 대상이 지상유닛일 때만)에
    // 더해줄 여유 거리. 고정된 정지거리를 쓰면 유닛 크기에 따라 두 NavMeshAgent 반경 합보다 짧아질 수 있는데,
    // 그러면 NavMeshAgent가 서로의 반경 안쪽 자리를 계속 점유하려고 들어서 밀어붙이는 문제가 생긴다 -
    // 그래서 고정 거리가 아니라 "두 유닛 반경 합 + 여유값"으로 대상 크기에 맞춰 정지 거리가 늘어나게 한다.
    [SerializeField] private float followStopMargin = 1f;
    // 공중 유닛이 정지할 때 실제 몸체 반경(this.airUnitRadius + 대상의 airUnitRadius, 대상이 공중유닛일 때만)에
    // 더해줄 여유 거리. 고정값(예전엔 airFollowStopDistance 4로 고정)으로 두면 작은 유닛끼리는 여유롭게 멈추지만
    // 큰 유닛(예: airUnitRadius가 큰 유닛)은 그 문턱보다 실제 반경 합이 더 커서 여전히 서로 밀고 들어가는 문제가
    // 있었다 - 그래서 고정 거리가 아니라 "두 유닛 반경 합 + 여유값"으로 대상 크기에 맞춰 정지 거리가 늘어나게 한다.
    // 목표 도착 판정이 MoveTowards 기반이라 문턱 부근에서 위치가 살짝만 흔들려도(대상이 다른 유닛에 밀려 미세하게
    // 움직이는 등) 정지→재이동을 반복하며 튕겨 들어가는 현상을 줄이기 위한 여유값이기도 하다.
    [SerializeField] private float airFollowStopMargin = 1f;

    // ===== 건설 이동 (건설모드에서 위치 클릭 시 일꾼이 그 자리로 이동 후 완공) =====
    [SerializeField] private float buildInteractRange = 2f; // 건설 위치 도착 판정 거리 (gatherInteractRange와 동일한 이유)
    private Vector3 buildDestination;
    private System.Action onBuildArrived;
    private System.Action onBuildCancelled;
    private bool hasBuildOrder;

    // ===== 건설 진행 (BaseStructure에 붙어서 건설 중일 때는 다른 명령을 받을 수 없다) =====
    private BaseStructure attachedStructure;
    private bool isConstructing;

    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();
        turretController = GetComponentInChildren<TurretController>();

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            defaultAgentRadius = navMeshAgent.radius;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position);
            isMovingAirUnit = true;
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        unitMarker.SetActive(false);
        if (isWorker)
        {
            DepositOre.SetActive(false);
            DepositGas.SetActive(false);
        }

        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);

        // 생산 큐를 거쳤든 씬에 직접 배치됐든, 어떤 경로로 만들어진 인스턴스든 항상 자기 unitID로
        // UnitDataSO를 조회해서 스스로 스탯을 적용한다 (UnitSpawner가 밖에서 push하던 방식 대체).
        ApplyUnitData(rtsController.GetUnitData(unitID));
    }

    // Update is called once per frame
    void Update()
    {
        //공중 유닛 일 경우
        if (isAirUnit && isMovingAirUnit)
        {
            // 수평(X/Z)은 목적지를 향해, 수직(Y)은 "지금 발밑 지면 + airCruiseAltitude"를 매 프레임 다시 재서
            // 각각 독립적으로 수렴시킨다 - 그래야 언덕 위를 지나는 동안은 그만큼 떠 있다가, 언덕을 실제로 벗어나
            // 발밑 지형이 낮아지는 순간에 맞춰 고도도 자연스럽게 낮아진다.
            Vector3 pos = transform.position;

            Vector3 horizontalTarget = new Vector3(targetPosition.x, pos.y, targetPosition.z);
            pos = Vector3.MoveTowards(pos, horizontalTarget, moveSpeed * Time.deltaTime);

            // 도착 판정은 미리 계산해둔 targetPosition.y가 아니라 "지금 이 프레임에 실제로 향하고 있는" 고도
            // (desiredY)와 비교해야 한다. targetPosition.y는 명령을 내린 시점에 한 번 계산된 값이라 실제 지형
            // (레이캐스트로 잰 값)과 완전히 일치한다는 보장이 없는데, 예전처럼 targetPosition.y와 비교하면 그
            // 미세한 차이 때문에 도착 판정이 영원히 안 나서 계속 제자리에서 맴도는 문제가 있었다.
            float groundBelow = SampleGroundHeight(pos, targetPosition.y - airCruiseAltitude);
            float desiredY = groundBelow + airCruiseAltitude;
            pos.y = Mathf.MoveTowards(pos.y, desiredY, moveSpeed * Time.deltaTime);

            transform.position = pos;

            Vector3 dir = targetPosition - transform.position;
            dir.y = 0;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
            }

            bool arrivedHorizontally = Mathf.Abs(pos.x - targetPosition.x) < 0.1f && Mathf.Abs(pos.z - targetPosition.z) < 0.1f;
            bool arrivedVertically = Mathf.Abs(pos.y - desiredY) < 0.1f;

            if (arrivedHorizontally && arrivedVertically)
            {
                isMovingAirUnit = false;

                // 지정 추격 대상(적/아군)이 살아있는 동안은 잠깐 따라잡아도 Idle로 전환하지 않는다 (계속 추격/교전 유지)
                if (orderedTarget == null && friendlyTarget == null && followTarget == null)
                {
                    UnitcurrentState = UnitState.Idle;
                    attackMoveDestination = null;
                }

                Debug.Log("공중유닛 도착 !");
            }
        }
        //지상 유닛 일 경우
        if (!isAirUnit)
        {
            if (!arrived &&
                orderedTarget == null &&
                friendlyTarget == null &&
                followTarget == null &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                navMeshAgent.ResetPath();
                UnitcurrentState = UnitState.Idle;
                attackMoveDestination = null;
            }
        }

        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
        FollowTick();
        BuildTick();

        if (isAirUnit)
            SeparateFromOverlappingAirUnits();
    }

    // 이동 중이 아닌 공중 유닛끼리만 서로 겹친 만큼 수평으로 밀어낸다.
    // isMovingAirUnit이 곧 "지금 서로 통과해도 되는가"의 기준이라 StopUnit/Attack/HoldUnit/도착 처리가
    // 전부 이 값을 false로 내리는 것만으로 정지/공격 케이스가 자동으로 커버된다.
    private void SeparateFromOverlappingAirUnits()
    {
        if (isMovingAirUnit)
            return;

        Vector3 push = Vector3.zero;

        foreach (UnitController other in rtsController.UnitList)
        {
            if (other == this || other == null || !other.isAirUnit)
                continue;
            if (other.isMovingAirUnit)
                continue; // 상대가 지나가는 중이면 통과시켜줌

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f; // 고도는 건드리지 않고 수평으로만 분리
            float dist = diff.magnitude;

            // 필요한 분리 거리 = 두 유닛 각자의 반경 합 (큰 유닛이 낀 페어일수록 더 멀리 떨어짐)
            float requiredDist = airUnitRadius + other.airUnitRadius;

            if (dist > 0.001f && dist < requiredDist)
            {
                float overlap = requiredDist - dist;
                push += diff.normalized * overlap;
            }
        }

        if (push.sqrMagnitude > 0.0001f)
        {
            Vector3 step = push.normalized * Mathf.Min(push.magnitude, airSeparationSpeed * Time.deltaTime);
            transform.position += step;
        }
    }

    public void SelectUnit()
    {
        unitMarker.SetActive(true);
    }

    public void DeselectUnit()
    {
        unitMarker.SetActive(false);
    }

    // 공격 명령(아군 강제 공격 등) 대상으로 지정됐을 때 "이 유닛이 대상"임을 피드백으로 마커를 짧게 깜빡인다.
    // 좌클릭 선택 마커와 같은 오브젝트를 사용하므로, 끝나면 실제 선택 상태에 맞춰 복원한다.
    public void FlashMarker()
    {
        if (unitMarker == null)
            return;

        if (markerFlashRoutine != null)
            StopCoroutine(markerFlashRoutine);

        markerFlashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(markerFlashInterval);

        for (int i = 0; i < markerFlashCount; i++)
        {
            unitMarker.SetActive(true);
            yield return wait;
            unitMarker.SetActive(false);
            yield return wait;
        }

        // 깜빡이는 도중 선택된 상태였다면(드문 경우) 꺼진 채로 두지 않고 선택 마커 상태로 복원
        bool isSelected = rtsController != null && rtsController.selectedUnitList.Contains(this);
        unitMarker.SetActive(isSelected);

        markerFlashRoutine = null;
    }

    public void MoveTo(Vector3 end)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        arrived = false;
        patrolling = false;
        UnitcurrentState = UnitState.Move;

        MoveAgentTo(end);
    }

    // 공중 유닛의 비행 목표 좌표를 계산한다.
    // destinationIsAirborne이 false(기본값)면 destination을 "지면 좌표"로 보고 그 지점의 지면 높이(Y) 기준으로
    // airCruiseAltitude만큼 띄운다 - 그래야 언덕/저지대처럼 지형 높이가 다른 곳도 정확히 "그 지점 + 5"로 날아간다.
    // destinationIsAirborne이 true면 destination이 이미 공중에 뜬 대상(다른 공중유닛/이륙한 건물/자기 자신의 현재
    // 위치)의 좌표라는 뜻이므로 다시 더하지 않고 그대로 쓴다 - 안 그러면 이미 반영된 고도에 또 더해져서(예: 5+5=10)
    // 그 대상 머리 위로 솟구쳐버린다.
    private Vector3 AirTargetPosition(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (destinationIsAirborne)
            return destination;

        return new Vector3(destination.x, destination.y + airCruiseAltitude, destination.z);
    }

    // friendlyTarget(아군 강제공격 대상)은 UnitController 또는 BuildingController 둘 다 될 수 있어서,
    // 실제로 지금 공중에 떠 있는 상태인지 타입별로 확인해야 AirTargetPosition에 정확히 알려줄 수 있다.
    private static bool IsAirborne(MonoBehaviour target)
    {
        if (target is UnitController unit)
            return unit.isAirUnit;
        if (target is BuildingController building)
            return building.IsLifted();
        return false;
    }

    // xzPosition의 X/Z 바로 아래에 있는 지면(airGroundLayer) 높이를 레이캐스트로 알아낸다. 못 찾으면 fallback을 쓴다.
    // 공중 유닛이 "지금 자기 발밑" 지형을 매 프레임 다시 확인하는 데 쓴다 - 목적지 고도를 미리 계산해서 그쪽으로만
    // 직선 이동하면, 언덕 위에서 출발해 저지대로 이동할 때 언덕을 채 벗어나기도 전에 미리 하강을 시작해서 언덕
    // 지형에 파묻히듯 스치는 문제가 생긴다. 매 프레임 발밑 지형을 다시 재는 방식이라야 "언덕을 실제로 벗어나는
    // 순간"에 맞춰 고도가 자연스럽게 바뀐다.
    private float SampleGroundHeight(Vector3 xzPosition, float fallback)
    {
        if (airGroundLayer == 0)
            return fallback;

        Vector3 rayOrigin = new Vector3(xzPosition.x, 1000f, xzPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, airGroundLayer))
            return hit.point.y;

        return fallback;
    }

    // 지상/공중 유닛 이동 로직을 한 곳으로 모은 헬퍼 (공격 명령 추적/재개 로직에서 반복 사용하기 위함).
    // destinationIsAirborne: destination이 이미 공중에 뜬 대상의 좌표인지 (공중 유닛에만 의미 있음).
    private void MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
        }
    }

    // 명시 공격 명령(추격 대상/아군 강제공격 대상/공격-이동 목적지) 취소: 다른 종류의 명령이 새로 들어올 때 호출
    private void CancelAttackOrder()
    {
        orderedTarget = null;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        hasFriendlyOrder = false;
        attackMoveDestination = null;
        followTarget = null;
        hasFollowOrder = false;

        GetComponent<UnitEffects>()?.StopAttackEffects(); // 이동/정지 등 다른 명령으로 공격이 취소되면 재생 중인 공격 이펙트도 즉시 정지

        CancelBuildOrder();
    }

    // ======================
    // 공격 명령 (우클릭 적 지정 / A 모드)
    // ======================

    // 특정 적 유닛을 추격하여 공격한다 (우클릭 적 클릭 / A 모드에서 적 클릭).
    // 대상이 살아있는 한 매 프레임 최신 위치를 쫓아가고(AttackOrderTick), 사거리 안에 들어오면
    // AttackRange가 자동으로 공격을 실행한다.
    // 참고: EnemyController에는 아직 공중 여부 개념이 없어(모든 적이 암묵적으로 지상 취급) 여기서는
    // canAttackAir 차단을 적용하지 않는다. EnemyController에 isAirUnit이 추가되면(추후 작업)
    // AttackFriendlyTarget과 동일한 패턴으로 CanAttackDomain(target.IsAirUnit()) 체크를 추가할 것.
    public void AttackUnitTarget(EnemyController target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();

        orderedTarget = target;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        attackMoveDestination = target.transform.position;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position);
    }

    // 특정 지점으로 공격-이동한다 (A 모드에서 땅 클릭).
    // 이동 중 사거리에 적이 들어오면 교전하고, 교전이 끝나면(AttackOrderTick) 다시 이 지점으로 이동을 재개한다.
    public void AttackMoveTo(Vector3 destination)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();

        orderedTarget = null;
        friendlyTarget = null;
        attackMoveDestination = destination;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        arrived = false;
        UnitcurrentState = UnitState.Idle; // Idle 상태여야 AttackRange가 사거리 내 적을 자동으로 교전한다

        MoveAgentTo(destination);
    }

    // 아군 유닛/건물을 강제로 공격한다 (A 모드에서 아군 좌클릭). target은 UnitController 또는 BuildingController.
    // 적 추격과 달리 "시야 이탈" 개념이 없다: 대상이 죽어서 파괴되기 전까지는 거리에 상관없이 끝까지 쫓아간다
    // (FriendlyAttackTick에서 매 프레임 갱신).
    public void AttackFriendlyTarget(MonoBehaviour target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        bool targetIsAir = IsAirborne(target);
        if (!CanAttackDomain(targetIsAir))
        {
            Debug.Log($"{name}: 이 유닛은 {(targetIsAir ? "공중" : "지상")} 대상을 공격할 수 없습니다.");
            return;
        }

        CancelGatheringForNewCommand();

        orderedTarget = null;
        attackMoveDestination = null;
        friendlyTarget = target;
        hasFriendlyOrder = true;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position, targetIsAir);
    }

    // 아군 강제 공격을 매 프레임 갱신한다: 사거리 안이면 공격하고, 아니면 거리 제한 없이 계속 추격한다.
    // 대상이 죽어서 파괴되면 정지 상태를 풀고 Idle로 복귀한다.
    // (AttackRange는 "Enemy" 태그만 감지하므로 아군 대상 전투는 여기서 직접 처리한다.)
    private void FriendlyAttackTick()
    {
        if (!hasFriendlyOrder)
            return;

        if (friendlyTarget == null)
        {
            // 대상이 죽어서 파괴됨: 정지된 채로 남지 않도록 여기서 직접 마무리 처리
            hasFriendlyOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();

            UnitcurrentState = UnitState.Idle;
            return;
        }

        float distance = Vector3.Distance(transform.position, friendlyTarget.transform.position);

        if (attackRange != null && distance <= attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
        else
        {
            MoveAgentTo(friendlyTarget.transform.position, IsAirborne(friendlyTarget)); // 사거리 밖: 거리 상관없이 끝까지 추격
        }
    }

    public void FollowUnit(UnitController target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        followTarget = target;
        hasFollowOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Idle; // Idle 유지 - AttackRange가 사거리 내 적을 자동으로 교전하게 함

        MoveAgentTo(target.transform.position, target.isAirUnit);
    }

    // 따라다니기 명령을 매 프레임 갱신한다: 대상이 죽으면 그 자리에 멈추고, 교전 중(AttackRange가 정지시킨 상태)이면
    // 이동 명령을 덮어쓰지 않으며, 두 유닛의 반경 합(+여유값) 이내로 가까워지면 정지한다(그래야 지상 유닛이 대상을
    // 계속 밀어붙이거나, 공중 유닛이 계속 "이동 중" 상태로 남아 겹침 분리가 안 되는 문제가 없다). 그 외에는 대상의
    // 최신 위치로 계속 이동한다. 대상이 다시 멀어지면 다음 프레임에 거리 재확인으로 자동으로 다시 쫓아간다.
    private void FollowTick()
    {
        if (!hasFollowOrder)
            return;

        if (followTarget == null)
        {
            hasFollowOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();
            else
                isMovingAirUnit = false;
            return;
        }

        if (attackRange != null && attackRange.HasEnemyInRange)
            return; // 교전 중이면 그대로 둔다 (AttackRange가 정지시킨 상태 유지)

        float stopDistance;
        if (isAirUnit)
        {
            float combinedRadius = airUnitRadius + (followTarget.isAirUnit ? followTarget.airUnitRadius : 0f);
            stopDistance = combinedRadius + airFollowStopMargin;
        }
        else
        {
            float combinedRadius = navMeshAgent.radius + (followTarget.isAirUnit ? 0f : followTarget.navMeshAgent.radius);
            stopDistance = combinedRadius + followStopMargin;
        }

        float sqrDist = (followTarget.transform.position - transform.position).sqrMagnitude;
        if (sqrDist <= stopDistance * stopDistance)
        {
            if (!isAirUnit)
                navMeshAgent.isStopped = true;
            else
                isMovingAirUnit = false;
            return;
        }

        MoveAgentTo(followTarget.transform.position, followTarget.isAirUnit);
    }

    // 건설모드에서 건물 위치를 클릭했을 때 PlacementSystem이 호출한다.
    // destination에 도착하면 onArrived(실제 건물 스폰)를, 도착 전에 다른 명령으로 취소되면 onCancelled(그리드 예약 해제)를 실행한다.
    public void GoBuild(Vector3 destination, System.Action onArrived, System.Action onCancelled)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder(); // 이전 건설 이동이 있었다면 여기서 먼저 취소 콜백이 실행됨

        buildDestination = destination;
        onBuildArrived = onArrived;
        onBuildCancelled = onCancelled;
        hasBuildOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Move;
        MoveAgentTo(destination);
    }

    // 진행 중이던 건설 이동을 취소하고(다른 명령으로 대체됨) 취소 콜백을 실행한다.
    private void CancelBuildOrder()
    {
        if (!hasBuildOrder)
            return;

        hasBuildOrder = false;
        System.Action cancelled = onBuildCancelled;
        onBuildArrived = null;
        onBuildCancelled = null;

        cancelled?.Invoke();
    }

    // 건설 이동을 매 프레임 갱신한다: 목적지 근접 반경 안에 들어오면 도착 콜백을 실행하고 Idle로 전환한다.
    private void BuildTick()
    {
        if (!hasBuildOrder)
            return;

        if (Vector3.Distance(transform.position, buildDestination) > buildInteractRange)
            return;

        hasBuildOrder = false;

        if (!isAirUnit)
            navMeshAgent.ResetPath();

        arrived = true;
        UnitcurrentState = UnitState.Idle;

        System.Action arrivedCallback = onBuildArrived;
        onBuildArrived = null;
        onBuildCancelled = null;

        arrivedCallback?.Invoke();
    }

    // BaseStructure에 도착해서 건설을 시작(또는 재개)할 때 호출된다(GoBuild의 onArrived에서 호출).
    // structure가 이미 파괴된 경우(도착 전에 다른 일꾼이 먼저 완공한 경우 등)는 그냥 아무 것도 하지 않고 자유 상태로 남는다.
    public void BeginConstruction(BaseStructure structure)
    {
        if (structure == null)
            return;

        attachedStructure = structure;
        isConstructing = true;

        structure.AttachBuilder(this);
    }

    // 건설이 끝나거나(완공) 다른 일꾼으로 교체되어 담당에서 풀렸을 때 BaseStructure가 호출한다.
    public void FinishConstruction()
    {
        isConstructing = false;
        attachedStructure = null;
    }

    public bool IsConstructing() => isConstructing;

    // 명시적 공격 명령을 매 프레임 갱신한다.
    // - 지정 대상과 한 번도 접촉(사거리 진입)한 적이 없다면, 아무리 멀어도 "시야 이탈"로 보지 않고
    //   무조건 그 대상만 쫓아간다 (맵 반대편의 먼 적을 지정해도 도중의 다른 적에게 한눈팔지 않는다).
    // - 한 번이라도 접촉한 뒤에는, 대상이 죽거나(파괴) chaseLoseSightRange 밖으로 벗어나면
    //   마지막으로 확인한 위치로 공격-이동 전환한다 (그 뒤로는 도중에 만나는 다른 적과 교전해도 된다).
    // - 공격-이동 중 근처에 교전 상대가 없는데 정지된 채로 남아있다면(전투 종료 직후 등)
    //   원래 목적지로 이동을 재개한다.
    private void AttackOrderTick()
    {
        if (orderedTarget != null)
        {
            float sqrDist = (transform.position - orderedTarget.transform.position).sqrMagnitude;

            bool inAttackRange = attackRange != null && sqrDist <= (float)attackRange.UnitRange * attackRange.UnitRange;
            if (inAttackRange)
                hasEngagedOrderedTarget = true; // 한 번이라도 사거리 안에서 접촉했다면 이후 "시야 이탈" 판정을 적용

            if (hasEngagedOrderedTarget && sqrDist > chaseLoseSightRange * chaseLoseSightRange)
            {
                // 시야 이탈: 마지막으로 확인된 위치로 "공격-이동" 전환 (추격 대상은 포기)
                // Idle 상태로 바꿔야 그 길에 새로 마주치는 다른 적도 AttackRange가 자동으로 교전해준다.
                attackMoveDestination = orderedTarget.transform.position;
                orderedTarget = null;
                hasEngagedOrderedTarget = false;
                UnitcurrentState = UnitState.Idle;
            }
            else
            {
                attackMoveDestination = orderedTarget.transform.position;

                // 다른 적이 근처에 있어도 그건 무시하고, 오직 "지정한 대상"과의 거리로만 교전 여부를 판단한다
                // (attackRange.HasEnemyInRange를 쓰면 무관한 다른 적 때문에 추격이 멈춰버릴 수 있음).
                if (!inAttackRange)
                    MoveAgentTo(attackMoveDestination.Value); // 사거리 밖: 계속 추격 이동

                return;
            }
        }

        if (attackMoveDestination == null)
            return;

        if (attackRange != null && attackRange.HasEnemyInRange)
            return; // 아직 교전 중이면 그대로 둔다 (AttackRange가 정지시킨 상태 유지)

        bool groundStopped = !isAirUnit && navMeshAgent.isStopped;
        bool airStopped = isAirUnit && !isMovingAirUnit;

        if (groundStopped || airStopped)
        {
            arrived = false;
            MoveAgentTo(attackMoveDestination.Value); // 교전 종료 → 원래 목적지로 이동 재개
        }
    }

    public EnemyController GetOrderedTarget() => orderedTarget;

    // TurretController가 조준 대상을 물어볼 때 쓰는 AttackRange 접근자.
    public AttackRange GetAttackRange() => attackRange;

    // 아군 강제공격 중인지 (AttackRange가 다른 적으로 대상을 가로채지 않도록 확인하는 데 쓴다).
    public bool HasFriendlyOrder => hasFriendlyOrder;

    // ======================
    // 추적 (공격 준비 이동)
    // ======================
    public void ChaseTarget(Vector3 pos)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        UnitcurrentState = UnitState.Idle;
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(pos);
        }
        else
        {
            targetPosition = AirTargetPosition(pos);
            isMovingAirUnit = true;
        }
    }

    public void Attack(Vector3 end, GameObject enemy)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position, true); // 제자리 정지 - 현재 고도를 그대로 유지
            isMovingAirUnit = false;
        }

        if (turretController == null)
            RotateYOnly(end); // 포탑 유닛(turretController != null)은 몸체를 안 돌린다 - 포탑이 대신 조준한다 (doc/0219)


        if (alreadyAttacked)
            return;

        bool targetIsAir = IsTargetAirborne(enemy);
        if (!CanAttackDomain(targetIsAir))
        {
            // 쿨다운(alreadyAttacked)은 건드리지 않는다 - 대상이 다시 공격 가능한 도메인으로 돌아오면(예: 건물 착륙)
            // 대기 없이 바로 다음 프레임에 공격을 재개할 수 있어야 하기 때문.
            Debug.Log($"{name}: 이 유닛은 {(targetIsAir ? "공중" : "지상")} 대상을 공격할 수 없습니다.");
            return;
        }

        Debug.Log("공격성공!");
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(enemy);
            int finalDamage = CalculateFinalDamage(enemy, targetArmor);
            targetHealth.GetDamage(finalDamage, transform.position, attackType); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
    }

    // 공격방식×대상크기 배율(DamageMultiplierTableSO)과 이 유닛의 고유 장갑타입 보너스를 곱연산으로 적용한 뒤,
    // 대상의 고정 방어력을 감산해 최종 데미지를 계산한다. 최소 1은 항상 보장.
    private int CalculateFinalDamage(GameObject target, int targetArmor)
    {
        SizeType targetSize = GetTargetSizeType(target);
        ArmorType targetArmorType = GetTargetArmorType(target);

        DamageMultiplierTableSO table = rtsController != null ? rtsController.DamageMultiplierTable : null;
        float sizeMultiplier = table != null ? table.GetMultiplier(attackType, targetSize) : 1f;

        float bonusMultiplier = (bonusVersusArmorPercent != 0f && targetArmorType == bonusVersusArmorType)
            ? 1f + bonusVersusArmorPercent / 100f
            : 1f;

        int scaledAttack = Mathf.RoundToInt(GetAttackDamage() * sizeMultiplier * bonusMultiplier);
        return Mathf.Max(1, scaledAttack - targetArmor);
    }

    // 공격 대상의 방어력을 조회한다 (아군 유닛이면 연구 보너스가 반영된 GetArmor(), 적 유닛이면 EnemyController의 armor, 그 외(건물/자원)는 0).
    private int GetTargetArmor(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmor();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmor();

        return 0;
    }

    // 공격 대상의 크기 타입을 조회한다 (건물/자원 등 타입 정보가 없는 대상은 Medium → 배율 100%로 영향 없음).
    private SizeType GetTargetSizeType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetSizeType();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetSizeType();

        return SizeType.Medium;
    }

    // 공격 대상의 장갑 타입을 조회한다 (건물/자원 등은 고유 보너스가 적용될 일이 없으므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmorType();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmorType();

        return ArmorType.Light;
    }

    // 공격 대상이 "지금" 공중 상태인지 조회한다. 건물은 이/착륙으로 실시간 바뀔 수 있어(BuildingController.IsLifted)
    // 매 공격 사이클마다 다시 확인해야 한다 - 명령을 내린 시점에 캐싱해둔 값을 계속 쓰면 안 된다.
    // EnemyController는 아직 공중 개념이 없어(doc/0213) 항상 지상(false)으로 취급한다.
    private bool IsTargetAirborne(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.IsAirUnit();

        if (target.TryGetComponent<BuildingController>(out var building))
            return building.IsLifted();

        return false;
    }

    //공격 리셋
    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    // ======================
    // Y축 회전
    // ======================
    private void RotateYOnly(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            rot,
            Time.deltaTime * 10f
        );
    }

    public void StopUnit()
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        UnitcurrentState = UnitState.Idle;

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position, true); // 제자리 정지 - 현재 고도를 그대로 유지
            isMovingAirUnit = false;
        }


    }
    public void PatrolUnit(Vector3 end)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        UnitcurrentState = UnitState.Idle;

        startPoint = transform.position;
        endPoint = end;

        patrolling = true;
        goingToEnd = true;

        arrived = false;   // 🔥 중요 (버그 방지)

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(endPoint);
        }
        else
        {
            targetPosition = AirTargetPosition(endPoint);
            isMovingAirUnit = true;
        }
    }

    void PatrolTick()
    {
        if (!patrolling)
            return;

        bool arrivedGround =
            !isAirUnit &&
            !navMeshAgent.pathPending &&
            navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;

        // 수평(X/Z) 거리만 본다 - 고도는 Update()에서 매 프레임 발밑 지형을 따라 계속 조정되는 값이라
        // targetPosition.y와 정확히 일치한다는 보장이 없어서, 3D 거리로 비교하면 도착 판정이 영원히 안 날 수 있다.
        Vector3 horizontalDiff = targetPosition - transform.position;
        horizontalDiff.y = 0;
        bool arrivedAir =
            isAirUnit &&
            horizontalDiff.sqrMagnitude < 0.5f;

        if (!arrivedGround && !arrivedAir)
            return;

        arrived = false; // 🔥 다음 이동 준비

        if (goingToEnd)
        {
            goingToEnd = false;

            if (!isAirUnit)
                navMeshAgent.SetDestination(startPoint);
            else
                targetPosition = AirTargetPosition(startPoint, true); // startPoint는 순찰 시작 시 현재(이미 공중) 위치를 그대로 캡처한 값
        }
        else
        {
            goingToEnd = true;

            if (!isAirUnit)
                navMeshAgent.SetDestination(endPoint);
            else
                targetPosition = AirTargetPosition(endPoint);
        }
    }

    public void HoldUnit()
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        UnitcurrentState = UnitState.Attack;

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position, true); // 제자리 정지 - 현재 고도를 그대로 유지
            isMovingAirUnit = false;
        }
    }

    // ===== 외부에서 호출하는 유일한 진입점 =====
    public void Gather(ResourceNode node)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        if (!isWorker)
        {
            MoveTo(node.transform.position); // 전투 유닛은 그냥 이동 명령으로 처리
            return;
        }

        if (!TerritoryManager.IsInsideAlliedTerritory(node.transform.position))
            return; // 영토 밖 자원은 채취 명령 자체를 무시

        // 새 채취 명령이므로, 기존에 대기열에 들어가 있던 노드가 있다면 자리부터 비워준다
        // (gatherTargetNode를 새 목표로 덮어쓰기 전에 반드시 먼저 호출해야 함)
        CancelGatheringForNewCommand();

        // 이미 자원을 들고 있는 상태(Deposit 못 하고 중간에 새 채취 명령을 받은 경우)면
        // 다시 캐러 가지 말고 바로 반납하러 감
        if (IsCarryingResource())
        {
            depositTargetTransform = FindNearestDepositBuilding();
            if (depositTargetTransform == null)
            {
                CancelGathering(); // 반납할 건물이 없으면 그 자리에 멈춰서 Idle로
                return;
            }

            patrolling = false;
            MoveTo(depositTargetTransform.position);
            gatherState = GatherState.MovingToBase;
            return;
        }

        // 대기열 확인은 도착한 뒤에 한다 (일단 이동부터 시작)
        patrolling = false;
        gatherTargetNode = node;
        MoveTo(node.transform.position);
        gatherState = GatherState.MovingToResource;
    }

    // 목표 노드에 도착했지만 대기열이 꽉 찼거나(혹은 목표 노드 자체가 사라졌을) 때,
    // 자신 기준 alternateResourceSearchRadius 이내에서 대기열 여유가 있는 다른 자원 노드를 찾아 그쪽으로 재이동한다.
    // 성공하면 true를 반환하고(이동 시작, 도착하면 다시 대기열을 확인하게 됨), 근처에 대체 자원이 없으면 false를 반환한다.
    private bool TryRedirectToNearbyResource(ResourceNode exclude)
    {
        ResourceNode alt = FindNearestAvailableResourceNode(alternateResourceSearchRadius, exclude);
        if (alt == null)
            return false;

        gatherTargetNode = alt;
        MoveTo(alt.transform.position);
        gatherState = GatherState.MovingToResource;
        return true;
    }

    private ResourceNode FindNearestAvailableResourceNode(float maxDistance, ResourceNode exclude)
    {
        ResourceNode nearest = null;
        float nearestSqrDist = maxDistance * maxDistance;

        foreach (ResourceNode node in rtsController.ResourceNodeList)
        {
            if (node == null || node == exclude || node.IsDepleted || node.IsCrowded
                || !TerritoryManager.IsInsideAlliedTerritory(node.transform.position))
                continue;

            float sqrDist = (node.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = node;
            }
        }

        return nearest;
    }

    private bool IsCarryingResource() => DepositOre.activeSelf || DepositGas.activeSelf;

    // ===== Return Cargo 진입점 (UI "반환" 버튼) =====
    public void ReturnCargo()
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        if (!isWorker || !IsCarryingResource())
            return; // 일꾼이 아니거나 들고 있는 자원이 없으면 아무 것도 안 함

        depositTargetTransform = FindNearestDepositBuilding();
        if (depositTargetTransform == null)
        {
            CancelGathering(); // 반납할 건물이 없으면 그 자리에 멈춰서 Idle로
            return;
        }

        patrolling = false;
        gatherTargetNode = null; // 고정 목적지 없음 신호 → Deposit()이 최근접 노드를 새로 찾게 함
        MoveTo(depositTargetTransform.position);
        gatherState = GatherState.MovingToBase;
    }

    // ===== 건물 우클릭 명령 =====
    // 일꾼이 자원을 들고 있으면 ReturnCargo()(반환 후 최근접 자원 채취)로 처리하고,
    // 그 외(자원 없는 일꾼, 전투 유닛 등)에는 그냥 건물 위치로 이동만 한다.
    public void MoveToBuilding(BuildingController building)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        if (isWorker && IsCarryingResource())
        {
            ReturnCargo();
            return;
        }

        MoveTo(building.transform.position);
    }

    // 채취 중단 + 그 자리에 멈춰서 Idle로 (반납 건물이 없거나, 채취 중이던 노드가 파괴된 경우 등)
    private void CancelGathering()
    {
        gatherState = GatherState.None;
        StopUnit();
    }

    // 이동/공격/정지 등 다른 명령이 들어와서 채취를 중단시킬 때 호출 (반경만 원상복구, Idle 전환은 각 명령이 알아서 함)
    private void CancelGatheringForNewCommand()
    {
        // 대기열 등록은 노드에 "도착한 뒤"(WaitingInQueue)에만 이뤄지므로, MovingToResource 중에는 대기열에 없다.
        // WaitingInQueue(대기 중)나 Gathering(채취 중, 즉 대기열 맨 앞)에서 중단되면 자리를 비워줘야
        // 다음 일꾼이 그 자리를 이어받을 수 있다. MovingToBase 이후에는 이미 GatherTick에서 LeaveQueue가 호출된 상태다.
        if ((gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
            && gatherTargetNode != null)
        {
            gatherTargetNode.LeaveQueue(this);
        }

        gatherState = GatherState.None;

        if (!isAirUnit)
            navMeshAgent.radius = defaultAgentRadius;
    }

    // ===== 채취 상태 머신 =====
    private void GatherTick()
    {
        if (gatherState == GatherState.None)
            return;

        // 채취 중엔 서로 부딪히지 않도록 반경 축소 (다른 명령이 들어오면 CancelGatheringForNewCommand에서 원상복구)
        if (!isAirUnit)
            navMeshAgent.radius = gatherAgentRadius;

        // 채취 중이던(이동/대기/채취 중) 노드가 영토를 잃으면(적에게 거점을 뺏기는 등) 왕복을 끝까지 두지 않고
        // 그 자리에서 즉시 정지시킨다.
        if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
            && gatherTargetNode != null && !TerritoryManager.IsInsideAlliedTerritory(gatherTargetNode.transform.position))
        {
            StopUnit();
            return;
        }

        // 채취 도중(혹은 대기 중) 노드가 고갈되어 파괴된 경우(다른 유닛이 마저 캐간 경우 등) 방어
        // 그냥 멈추지 않고, 자신 기준 10 거리 이내에 대체 자원이 있으면 그쪽으로 재이동한다
        if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
            && gatherTargetNode == null)
        {
            if (!TryRedirectToNearbyResource(null))
                CancelGathering();
            return;
        }

        switch (gatherState)
        {
            case GatherState.MovingToResource:
                if (DistanceToTarget(gatherTargetNode.transform) <= gatherInteractRange)
                {
                    if (!isAirUnit)
                        navMeshAgent.isStopped = true; // 장애물 경계에서 계속 재탐색하며 맴도는 것 방지

                    // 도착했으니 이제 대기열을 확인한다.
                    // 대기열이 혼잡하면(waitWorkerCount 이상) 우선 근처(10 이내)에 더 한가한 자원을 찾아보고,
                    // 대체 자원을 못 찾으면 인원 제한 없이 그냥 이 노드의 대기열에 줄을 선다.
                    if (gatherTargetNode.IsCrowded && TryRedirectToNearbyResource(gatherTargetNode))
                    {
                        break; // 대체 자원으로 재이동 시작 (그쪽에 도착하면 다시 이 로직을 탄다)
                    }

                    gatherTargetNode.JoinQueue(this);
                    gatherState = GatherState.WaitingInQueue;
                }
                break;

            // 대기열에 등록은 됐지만 아직 자기 차례가 아닌 상태 (다른 일꾼이 채취 중)
            case GatherState.WaitingInQueue:
                RotateYOnly(gatherTargetNode.transform.position);

                if (gatherTargetNode.IsTurnToGather(this))
                {
                    gatherTimer = gatherDuration;
                    gatherState = GatherState.Gathering;
                }
                break;

            case GatherState.Gathering:
                RotateYOnly(gatherTargetNode.transform.position);

                gatherTimer -= Time.deltaTime;
                if (gatherTimer <= 0f)
                {
                    carryingType = gatherTargetNode.Type; // 노드가 파괴되기 전에 타입을 미리 캐싱
                    carryingAmount = gatherTargetNode.Extract(amountPerTrip);
                    gatherTargetNode.LeaveQueue(this); // 채취 완료 → 대기열 자리 반납, 다음 일꾼 차례로

                    if (carryingType == ResourceType.Ore)
                        DepositOre.SetActive(true);
                    else
                        DepositGas.SetActive(true);

                    depositTargetTransform = FindNearestDepositBuilding();
                    if (depositTargetTransform == null)
                    {
                        CancelGathering(); // 반납할 건물이 없으면 그 자리에 멈춰서 Idle로
                        return;
                    }

                    MoveTo(depositTargetTransform.position);
                    gatherState = GatherState.MovingToBase;
                }
                break;

            case GatherState.MovingToBase:
                if (DistanceToTarget(depositTargetTransform) <= gatherInteractRange)
                {
                    if (!isAirUnit)
                        navMeshAgent.isStopped = true;

                    gatherState = GatherState.Depositing;
                }
                break;

            case GatherState.Depositing:
                Deposit();
                break;
        }
    }

    private void Deposit()
    {
        // gatherTargetNode는 채취 도중(또는 자신의 채취로) 이미 파괴됐을 수 있어서
        // 타입 판정은 여기서 다시 gatherTargetNode를 참조하지 않고 미리 캐싱해둔 carryingType을 사용
        if (carryingType == ResourceType.Ore)
        {
            rtsController.AddOre(carryingAmount);
            DepositOre.SetActive(false);
        }
        else
        {
            rtsController.AddGas(carryingAmount);
            DepositGas.SetActive(false);
        }

        carryingAmount = 0;

        if (gatherTargetNode != null && !gatherTargetNode.IsDepleted)
        {
            // 원래 캐던 노드가 아직 남아있으면 그대로 복귀한다 (도착하면 다시 대기열을 확인하게 됨)
            MoveTo(gatherTargetNode.transform.position);
            gatherState = GatherState.MovingToResource;
            return;
        }

        // 원래 노드가 고갈됐거나(혹은 ReturnCargo로 목표 없이 반납한 경우) 자신 기준 10 이내에서 새 자원을 찾는다
        if (!TryRedirectToNearbyResource(gatherTargetNode))
        {
            CancelGathering(); // 근처(10 이내)에 캘 자원이 없으면 그 자리에 멈춰서 Idle로
        }
    }

    // 건물처럼 콜라이더가 큰 대상은 피벗(중심)이 아니라 표면(가장 가까운 지점) 기준으로 거리 판정
    private float DistanceToTarget(Transform target)
    {
        if (target.TryGetComponent<Collider>(out var col))
            return Vector3.Distance(transform.position, col.ClosestPoint(transform.position));

        return Vector3.Distance(transform.position, target.position);
    }

    private Transform FindNearestDepositBuilding()
    {
        BuildingController nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (BuildingController building in rtsController.BuildingList)
        {
            if (building == null) continue;
            if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납

            float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = building;
            }
        }

        return nearest != null ? nearest.transform : null;
    }

    public void Die()
    {
        gatherTargetNode?.LeaveQueue(this); // 대기열/채취 중에 사망해도 자리를 비워줌
        CancelBuildOrder(); // 건설 위치로 이동 중(hasBuildOrder)에 사망해도, 다른 명령으로 취소될 때와 동일하게
                             // 그리드 예약 해제 + 건물 가격 환불(onCancelled 콜백, 0089)이 실행되도록 함

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);
        controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
        controller?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환

        Destroy(gameObject);
    }

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => UnitcurrentState == UnitState.Idle;
    public bool IsMove() => UnitcurrentState == UnitState.Move;
    public bool IsAttack() => UnitcurrentState == UnitState.Attack;
    public bool IsAirUnit() => isAirUnit; // HoverBob 등 외부 이펙트 컴포넌트가 폴링용으로 사용(doc/0119)

    // 이동 이펙트(UnitEffects)가 상태머신을 직접 건드리지 않고 매 프레임 폴링으로 이동 여부를 판단할 수 있도록 노출.
    public bool IsCurrentlyMoving()
    {
        if (isAirUnit)
            return isMovingAirUnit;

        return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
    }

    public Sprite GetIcon() => icon;
    public int GetUnitID() => unitID;

    // 연구소 업그레이드로 얻은 전역 보너스를 더해서 반환한다 (RTSUnitController를 거쳐서만 조회 - UpgradeManager는 직접 참조하지 않음).
    public int GetAttackDamage() => attackDamage + (rtsController != null ? rtsController.GlobalAttackBonus : 0);
    public int GetArmor() => armor + (rtsController != null ? rtsController.GlobalArmorBonus : 0);
    public AttackEffectType GetAttackType() => attackType;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;

    // 대상이 공중 유닛인지에 따라 이 유닛이 그 대상을 공격할 수 있는 도메인(지상/공중)인지 판정한다.
    // (AttackUnitTarget/AttackFriendlyTarget의 명령 시점 차단, AttackRange의 자동 감지 필터링 양쪽에서 공용으로 사용)
    public bool CanAttackDomain(bool targetIsAirUnit) => targetIsAirUnit ? canAttackAir : canAttackGround;

    // 생산 시점에 UnitDataSO의 값으로 전투 스탯(체력/공격력/사거리/아이콘/장갑타입/크기타입)을 덮어쓴다.
    // 프리팹 자체에 미리 박아둔 값은 인스펙터 프리뷰/테스트용 기본값 역할만 하고, 실제로 생산되어 스폰된
    // 유닛은 이 메서드를 통해 UnitDataSO 값을 반영받는다 (UnitSpawner.Spawn()에서 호출).
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;
        timeBetweenAttacks = data.attackSpeed;
        canAttackGround = data.canAttackGround;
        canAttackAir = data.canAttackAir;

        if (attackRange != null)
            attackRange.UnitRange = data.attackRange;

        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
}
