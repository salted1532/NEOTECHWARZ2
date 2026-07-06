using System.Collections;
using System.Net;
using System.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using static UnityEditor.PlayerSettings;
using static UnityEngine.GraphicsBuffer;

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
    [SerializeField] private float airSeparationRadius = 1.2f; // 두 콜라이더 반경 합 정도
    [SerializeField] private float airSeparationSpeed = 4f;    // 밀려나는 속도(초당)

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

    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            defaultAgentRadius = navMeshAgent.radius;
        }
        else
        {
            targetPosition = transform.position + Vector3.up * 5f;
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
    }

    // Update is called once per frame
    void Update()
    {
        //공중 유닛 일 경우
        if (isAirUnit && isMovingAirUnit)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            Vector3 dir = targetPosition - transform.position;
            dir.y = 0;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
            }

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                isMovingAirUnit = false;

                // 지정 추격 대상(적/아군)이 살아있는 동안은 잠깐 따라잡아도 Idle로 전환하지 않는다 (계속 추격/교전 유지)
                if (orderedTarget == null && friendlyTarget == null)
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

            if (dist > 0.001f && dist < airSeparationRadius)
            {
                float overlap = airSeparationRadius - dist;
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
        CancelGatheringForNewCommand();
        CancelAttackOrder();

        arrived = false;
        patrolling = false;
        UnitcurrentState = UnitState.Move;

        MoveAgentTo(end);
    }

    // 지상/공중 유닛 이동 로직을 한 곳으로 모은 헬퍼 (공격 명령 추적/재개 로직에서 반복 사용하기 위함)
    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
        }
        else
        {
            targetPosition = destination + Vector3.up * 5f;
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
    }

    // ======================
    // 공격 명령 (우클릭 적 지정 / A 모드)
    // ======================

    // 특정 적 유닛을 추격하여 공격한다 (우클릭 적 클릭 / A 모드에서 적 클릭).
    // 대상이 살아있는 한 매 프레임 최신 위치를 쫓아가고(AttackOrderTick), 사거리 안에 들어오면
    // AttackRange가 자동으로 공격을 실행한다.
    public void AttackUnitTarget(EnemyController target)
    {
        CancelGatheringForNewCommand();

        orderedTarget = target;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        attackMoveDestination = target.transform.position;

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position);
    }

    // 특정 지점으로 공격-이동한다 (A 모드에서 땅 클릭).
    // 이동 중 사거리에 적이 들어오면 교전하고, 교전이 끝나면(AttackOrderTick) 다시 이 지점으로 이동을 재개한다.
    public void AttackMoveTo(Vector3 destination)
    {
        CancelGatheringForNewCommand();

        orderedTarget = null;
        friendlyTarget = null;
        attackMoveDestination = destination;

        arrived = false;
        UnitcurrentState = UnitState.Idle; // Idle 상태여야 AttackRange가 사거리 내 적을 자동으로 교전한다

        MoveAgentTo(destination);
    }

    // 아군 유닛/건물을 강제로 공격한다 (A 모드에서 아군 좌클릭). target은 UnitController 또는 BuildingController.
    // 적 추격과 달리 "시야 이탈" 개념이 없다: 대상이 죽어서 파괴되기 전까지는 거리에 상관없이 끝까지 쫓아간다
    // (FriendlyAttackTick에서 매 프레임 갱신).
    public void AttackFriendlyTarget(MonoBehaviour target)
    {
        CancelGatheringForNewCommand();

        orderedTarget = null;
        attackMoveDestination = null;
        friendlyTarget = target;
        hasFriendlyOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position);
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
            Attack(friendlyTarget.transform.position, attackRange.AttackDamage, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
        else
        {
            MoveAgentTo(friendlyTarget.transform.position); // 사거리 밖: 거리 상관없이 끝까지 추격
        }
    }

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
            targetPosition = pos + Vector3.up * 5f;
            isMovingAirUnit = true;
        }
    }

    public void Attack(Vector3 end, int damage, GameObject enemy)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = transform.position + Vector3.up * 5f;
            isMovingAirUnit = false;
        }

        RotateYOnly(end);


        if (alreadyAttacked)
            return;

        Debug.Log("공격성공!");
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            targetHealth.GetDamage(damage);
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
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
        CancelGatheringForNewCommand();
        CancelAttackOrder();

        UnitcurrentState = UnitState.Idle;

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = transform.position + Vector3.up * 5f;
            isMovingAirUnit = false;
        }
 

    }
    public void PatrolUnit(Vector3 end)
    {
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
            targetPosition = endPoint + Vector3.up * 5f;
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

        bool arrivedAir =
            isAirUnit &&
            (targetPosition - transform.position).sqrMagnitude < 0.5f;

        if (!arrivedGround && !arrivedAir)
            return;

        arrived = false; // 🔥 다음 이동 준비

        if (goingToEnd)
        {
            goingToEnd = false;

            if (!isAirUnit)
                navMeshAgent.SetDestination(startPoint);
            else
                targetPosition = startPoint;
        }
        else
        {
            goingToEnd = true;

            if (!isAirUnit)
                navMeshAgent.SetDestination(endPoint);
            else
                targetPosition = endPoint + Vector3.up * 5f;
        }
    }

    public void HoldUnit()
    {
        CancelGatheringForNewCommand();
        CancelAttackOrder();

        UnitcurrentState = UnitState.Attack;

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            targetPosition = transform.position + Vector3.up * 5f;
            isMovingAirUnit = false;
        }
    }

    // ===== 외부에서 호출하는 유일한 진입점 =====
    public void Gather(ResourceNode node)
    {
        if (!isWorker)
        {
            MoveTo(node.transform.position); // 전투 유닛은 그냥 이동 명령으로 처리
            return;
        }

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
            if (node == null || node == exclude || node.IsDepleted || node.IsCrowded)
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

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);
        controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록

        Destroy(gameObject);
    }

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => UnitcurrentState == UnitState.Idle;
    public bool IsMove() => UnitcurrentState == UnitState.Move;
    public bool IsAttack() => UnitcurrentState == UnitState.Attack;

    public Sprite GetIcon() => icon;
    public int GetUnitID() => unitID;
}
