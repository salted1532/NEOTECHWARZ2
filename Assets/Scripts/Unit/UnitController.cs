using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 공격 수단의 종류. 피격 이펙트를 공격자에 따라 다르게 재생하기 위해 HealthManager.GetDamage에 실어 보낸다
// (UnitEffects가 이 값으로 총기/폭발형/레이저/화염 중 어떤 피격 이펙트를 재생할지 고른다).
public enum AttackEffectType { Bullet, Explosive, Laser, Flame }

// 액티브 스킬 발동 시 함께 넘어가는 대상 정보 (doc/0323). trait.targetType(None/SingleUnit/AreaGround)에 따라
// unitTarget 또는 groundPoint 중 하나만 의미 있고, 자기 자신에게 쓰는 논타겟 스킬(None)은 Self를 그대로 쓴다.
public readonly struct SkillActivationContext
{
    public readonly GameObject unitTarget;
    public readonly Vector3 groundPoint;

    public static readonly SkillActivationContext Self = new SkillActivationContext(null, default);

    public SkillActivationContext(GameObject unitTarget, Vector3 groundPoint)
    {
        this.unitTarget = unitTarget;
        this.groundPoint = groundPoint;
    }
}

// 고급유닛이 특성(트레이트) 선택으로 얻은 액티브 스킬의 실제 효과를 구현하는 컴포넌트가 구현하는 인터페이스.
// 유닛 프리팹에 이 인터페이스를 구현한 MonoBehaviour(예: GoliathSkill.cs)를 붙이기만 하면
// UnitController.UseTraitSkill()이 자동으로 찾아서 호출한다 - UnitController/RTSUnitController를
// 건드리지 않고도 유닛별 스킬을 새로 추가/교체할 수 있게 하기 위한 연결점 (doc/0228).
public interface IUnitSkill
{
    // traitData: 지금 발동되는 스킬의 UnitTraitOption 자체(쿨다운/사거리/범위반경 등 수치를 그대로 참조할 수 있음).
    void Activate(UnitController unit, RTSUnitController.TraitChoice trait, UnitTraitOption traitData, SkillActivationContext context);
}

// 개별 유닛(일꾼/전투유닛/공중유닛 포함)의 이동, 전투, 순찰, 자원 채취(일꾼 전용) 상태머신을 담당하는 핵심 컴포넌트.
// NavMeshAgent 기반 지상 이동과 직접 좌표 보간 기반 공중 이동을 모두 지원하며,
// AttackRange가 사거리 내 적을 감지하면 이 컴포넌트의 Attack/ChaseTarget을 호출한다.
public class UnitController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject unitMarker;

    [SerializeField]
    private Sprite icon; // Squad_panel 등 선택 UI에 표시할 아이콘

    private string infoDescription; // Info_panel에 표시할 설명 (UnitData.infoDescription, doc/0476)

    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;

    // UnitSpawner.Spawn()이 Instantiate 직후(Start()가 돌기 전) true로 표시한다 - 생산 큐를 거친 유닛은
    // 이미 큐잉 시점에 TryProduceUnit()이 인구수를 소모했으므로, Start()에서 또 인구수를 더하면 이중 계산된다.
    // 이 값이 false로 남아있는 유닛(씬에 미리 배치된 시작 유닛 등)만 Start()에서 인구수를 반영한다.
    [System.NonSerialized]
    public bool spawnedByProduction;

    // 영웅 유닛(스토리 등장인물) 전용 - unitID를 0(=UnitDataSO에 없는 값)으로 두면 ApplyUnitData가
    // null 데이터를 받아 아무것도 덮어쓰지 않으므로 attackDamage/armorType/sizeType/HealthManager 값은
    // 인스펙터에 넣은 값이 그대로 유지된다. 이름만 원래 ID 조회 방식이라 별도 필드가 필요해서 추가함 (doc/0304).
    [SerializeField]
    private string heroName;

    // 0(기본값)이면 지금처럼 unitID로 NTA Unit Data SO를 조회한다. 0이 아니면 이 값으로 OC Unit Data SO를
    // 대신 조회해서 스탯(공격력/방어력/체력 등)을 적용한다 - 겉모습은 OC 프리팹 그대로 두고 조종만
    // 플레이어가 가능하게 만드는 "구조 가능한 OC 유닛"에 사용 (doc/0458).
    [SerializeField]
    private int enemyDataUnitID;

    // ===== 구조 가능한 OC 유닛 (doc/0458) =====
    // true면 "구조 전" - 아래 명령 진입점(MoveTo/AttackUnitTarget/... 13곳)이 isConstructing과 같은
    // 자리에서 함께 막힌다. AttackRange의 자동교전(사거리 내 적 자동 공격)은 이 플래그와 무관하게 계속
    // 작동한다 - 구조되기 전에도 스스로 방어는 하되, 플레이어가 직접 명령만 못 내리는 상태로 둔다.
    [SerializeField]
    private bool isRescueUnit;

    // unitMarker(선택 시 켜지는 마커 오브젝트) 자체는 그대로 재사용하고, 그 안에 있는 "Green" 효과만
    // 구조 시 켠다 - 마커의 on/off(선택/해제)는 항상 기존 SelectUnit/DeselectUnit이 그대로 담당하고,
    // 이 필드는 그 마커가 켜졌을 때 초록으로 보이게 하는 효과만 담당한다. 한 번 구조되면 계속 켜진
    // 채로 둔다(다시 꺼지지 않음 - "구조했다"는 사실은 되돌리지 않는다).
    [SerializeField]
    private GameObject rescuedMarker;

    // 구조 전 기본으로 켜져 있는 "Yellow" 효과 - rescuedMarker(Green)와 같은 마커 안에서 서로 배타적으로
    // 보여야 하므로, 구조 시 이것도 함께 꺼준다(Green 켜질 때 Yellow는 반드시 꺼짐).
    [SerializeField]
    private GameObject preRescueMarker;

    // 구조 시 FogRevealerAgent 시야를 이 값으로 되돌린다(구조 전엔 낮은 값으로 설정해둔 상태라고 가정).
    [SerializeField]
    private int rescuedSightRange = 25;

    // 구조 시 미니맵 아이콘 색을 이 값(#19FF00)으로 바꿔서 아직 안 구조된 OC(노란색)와 구분되게 한다.
    [SerializeField]
    private SpriteRenderer miniMapIconRenderer;
    private static readonly Color RescuedMiniMapIconColor = new Color(0.09803922f, 1f, 0f);

    // 구조 완료 순간 마커(초록으로 바뀐 뒤)를 짧게 깜빡여 피드백을 주는 연출 (doc/0465) - 공격 대상 지정
    // 깜빡임(markerFlashCount/Interval)과 의도가 달라 별도 필드로 둔다.
    [SerializeField]
    private float rescueFlashInterval = 0.3f;
    [SerializeField]
    private int rescueFlashCount = 3;
    [SerializeField]
    private SoundClipSet rescueSfx;

    private FogRevealerAgent fogRevealerAgent; // 구조 시 시야 범위를 바꾸는 데 사용 (같은 오브젝트에서 조회)

    // 구조 비콘 등 트리거 콜라이더에 실제로 겹쳐 있는지 판정한다 - 겹친 트리거를 전부 추적해서, 특정
    // 콜라이더(비콘)에 지금 닿아 있는지 IsTouching()으로 물어볼 수 있게 한다(MissionItem과 동일한
    // 패턴, doc/0456/0459 후속 - Stage3Objectives가 거리 대신 실제 트리거 접촉으로 판정하도록 변경).
    private readonly HashSet<Collider> overlappingTriggers = new HashSet<Collider>();

    // AttackRange의 감지용 콜라이더는 Rigidbody가 없는 자식이라 OnTriggerEnter/Exit이 이 유닛(부모의
    // Rigidbody)로도 함께 올라온다 - 그대로 두면 사거리(AttackRange)만 닿아도 비콘에 닿은 것으로
    // 오판된다(doc/0463). 실제 몸체 콜라이더(bodyCollider)의 Bounds와 겹치는 경우만 인정한다.
    private Collider bodyCollider;

    private void OnTriggerEnter(Collider other)
    {
        if (bodyCollider != null && bodyCollider.bounds.Intersects(other.bounds))
            overlappingTriggers.Add(other);
    }

    private void OnTriggerExit(Collider other) => overlappingTriggers.Remove(other);

    public bool IsTouching(Collider other) => other != null && overlappingTriggers.Contains(other);

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

    // 공격 전달 방식 (UnitDataSO.attackDelivery가 ApplyUnitData에서 그대로 반영됨, doc/0290).
    // Projectile인데 ProjectileAttack 컴포넌트가 안 붙어있으면 Attack()에서 자동으로 Hitscan으로 폴백한다.
    [SerializeField] private AttackDeliveryType attackDelivery = AttackDeliveryType.Hitscan;

    [Header("고유 추가 데미지 (해당 없으면 Percent를 0으로 둘 것)")]
    [Tooltip("이 유닛이 특정 장갑 타입 상대로만 추가 데미지를 줄 때 설정 (예: 저격수 = Heavy, 80)")]
    [SerializeField] private ArmorType bonusVersusArmorType = ArmorType.Light;
    [SerializeField] private float bonusVersusArmorPercent = 0f;

    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private float moveSpeed = 10f;
    [SerializeField]
    private float arriveDistance = 0.5f;
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

    private EnemyUnitController orderedTarget;   // 명시적으로 지정된 추격 대상 (없으면 null)
    private Vector3? attackMoveDestination;  // 공격-이동 목적지 / 추격 중 마지막으로 확인된 위치 (교전 후 복귀할 지점)
    private AttackRange attackRange;         // 사거리 내 교전 대상 존재 여부 조회용 (자식 컴포넌트)
    private UnitEffects unitEffects;         // 공격/피격 이펙트 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private UnitAudio unitAudio;             // 공격/채취 SFX 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private HealthManager healthManager;     // Info_panel 표시용 - Awake에서 한 번만 캐싱
    private LaserBeamAttack laserBeamAttack; // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
    private ProjectileAttack projectileAttack; // 투사체 발사 유닛만 붙어있는 옵셔널 컴포넌트
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

    // ===== 건물 우클릭 = 계속 따라다니기 (건물이 리프트로 이동할 수 있으므로 한 번만 이동하지 않고 매 프레임
    // 최신 위치를 쫓아간다 - FollowUnit/FollowTick과 동일한 패턴). =====
    private BuildingController followBuildingTarget;
    private bool hasFollowBuildingOrder;

    // ===== 건설 이동 (건설모드에서 위치 클릭 시 일꾼이 그 자리로 이동 후 완공) =====
    [SerializeField] private float buildInteractRange = 2f; // 건설 위치 도착 판정 거리 (gatherInteractRange와 동일한 이유)
    private Vector3 buildDestination;
    private System.Action onBuildArrived;
    private System.Action onBuildCancelled;
    private bool hasBuildOrder;

    // ===== 건설 진행 (BaseStructure에 붙어서 건설 중일 때는 다른 명령을 받을 수 없다) =====
    private BaseStructure attachedStructure;
    private bool isConstructing;

    // ===== 특성(트레이트) 스킬 - 고급유닛만 해당 (doc/0228) =====
    // RTSUnitController.ChooseTrait()를 거쳐서만 채워짐 (유닛 종류 전체가 공유하는 선택이라 유닛 스스로 정하지 않음)
    private RTSUnitController.TraitChoice currentTrait = RTSUnitController.TraitChoice.None;
    private float skillCooldownRemaining;

    // 공격이 실제로 명중했을 때 발행되는 이벤트 (doc/0323) - 패시브 스킬(예: 스카이 랜서 "공중 강화")이
    // UnitController/RTSUnitController를 건드리지 않고 이 이벤트만 구독해서 자기 효과를 붙일 수 있게 하기 위함.
    public event System.Action<GameObject> OnAttackHit;

    // ===== 은신 (doc/0323) - true면 EnemyAttackRange가 이 유닛을 감지 대상에서 제외한다 =====
    private bool isStealthed;

    // ===== 지정형 액티브 스킬(단일 유닛/범위) 이동-후-발동 대기 상태 (doc/0323) =====
    private bool hasPendingSkillUnitOrder;
    private GameObject pendingSkillUnitTarget;
    private bool hasPendingSkillAreaOrder;
    private Vector3 pendingSkillGroundTarget;
    private UnitTraitOption pendingSkillTraitData;

    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();
        turretController = GetComponentInChildren<TurretController>();
        unitEffects = GetComponent<UnitEffects>();
        unitAudio = GetComponent<UnitAudio>();
        laserBeamAttack = GetComponent<LaserBeamAttack>();
        healthManager = GetComponent<HealthManager>();
        fogRevealerAgent = GetComponent<FogRevealerAgent>();
        bodyCollider = GetComponent<Collider>();
        TryGetComponent(out projectileAttack);

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
        if (rescuedMarker != null)
            rescuedMarker.SetActive(false);
        if (isWorker)
        {
            DepositOre.SetActive(false);
            DepositGas.SetActive(false);
        }

        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);

        // 생산 큐를 거쳤든 씬에 직접 배치됐든, 어떤 경로로 만들어진 인스턴스든 항상 자기 unitID로
        // UnitDataSO를 조회해서 스스로 스탯을 적용한다 (UnitSpawner가 밖에서 push하던 방식 대체).
        // enemyDataUnitID가 지정돼 있으면 NTA 테이블 대신 OC Unit Data SO를 조회한다(doc/0458 - 겉모습은
        // OC 프리팹 그대로 두고 스탯도 OC 그대로 가져오는 "구조 가능한 OC 유닛"용).
        UnitData unitData = enemyDataUnitID > 0
            ? rtsController.GetEnemyUnitData(enemyDataUnitID)
            : rtsController.GetUnitData(unitID);
        ApplyUnitData(unitData);

        // ApplyUnitData 자체는 이름을 안 건드린다(스탯만) - Info Panel 이름은 RTSUnitController가
        // heroName(있으면) 아니면 GetUnitName(unitID)(NTA 테이블)로 별도 조회한다. enemyDataUnitID
        // 경로는 unitID가 0이라 그 조회가 항상 빈 문자열이 되므로, heroName이 비어있으면 OC 데이터의
        // 이름으로 자동 채워준다(doc/0458 - "구조 가능한 OC 유닛"이 이름 없이 뜨는 것 방지).
        if (enemyDataUnitID > 0 && string.IsNullOrEmpty(heroName) && unitData != null)
            heroName = unitData.unitName;

        // 생산 큐를 거치지 않은 유닛(씬에 미리 배치된 시작 유닛 등)만 여기서 인구수를 반영한다.
        // 생산 큐를 거친 유닛은 이미 큐잉 시점(TryProduceUnit)에 인구수가 소모됐으므로 건너뛴다.
        if (!spawnedByProduction)
            rtsController.AddPopulationForExistingUnit(unitID);

        // 이 유닛 종류가 이미 특성을 선택한 상태라면(예전에 다른 개체가 먼저 골랐음) 새로 생산된
        // 이 유닛에도 자동으로 같은 선택을 적용한다 (doc/0228 - "모든 같은 유닛에 적용").
        RTSUnitController.TraitChoice chosenTrait = rtsController.GetChosenTrait(unitID);
        if (chosenTrait != RTSUnitController.TraitChoice.None)
            ApplyTrait(chosenTrait);
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
                // ResetPath() 미호출 이유는 EnemyUnitController와 동일 (doc/0387) - AttackRange의
                // 순수 자동교전(ChaseTarget, 지정 명령 아님) 경로에서 매 프레임 재호출되는 동안 이
                // 도착 판정이 계속 ResetPath()를 부르면 doc/0386 목적지 캐시가 무효화된다.
                // isStopped는 true로 건다 - 안 그러면 destination이 여전히 이 지점을 가리키는 채로
                // 남아서, 도착 후 다른 유닛에게 밀려나면 NavMeshAgent가 스스로 원래 자리로 되돌아가려
                // 한다(doc/0399). MoveAgentTo가 다음 명령 때 항상 isStopped = false로 풀어준다.
                navMeshAgent.isStopped = true;
                UnitcurrentState = UnitState.Idle;
                attackMoveDestination = null;
            }
        }

        if (skillCooldownRemaining > 0f)
            skillCooldownRemaining -= Time.deltaTime;

        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
        SkillOrderTick();
        FollowTick();
        FollowBuildingTick();
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

            if (dist < requiredDist)
            {
                float overlap = requiredDist - dist;
                // 같은 건물을 따라가다 완전히 같은 좌표(dist≈0)에 겹친 경우 diff.normalized가 0벡터라
                // 미는 힘도 영원히 0이 되어 절대 안 풀리는 문제가 있었다 - 이럴 땐 유닛 고유의(항상 같은)
                // 방향으로라도 밀어서 겹침을 깨야 한다.
                Vector3 pushDir = dist > 0.001f ? diff.normalized : StackedNudgeDirection();
                push += pushDir * overlap;
            }
        }

        if (push.sqrMagnitude > 0.0001f)
        {
            Vector3 step = push.normalized * Mathf.Min(push.magnitude, airSeparationSpeed * Time.deltaTime);
            transform.position += step;
        }
    }

    // 완전히 같은 좌표에 겹친 상대에게서 밀려날 때 쓸, 이 유닛 고유의 고정 방향 (인스턴스ID 기반이라 매 프레임 동일).
    private Vector3 StackedNudgeDirection()
    {
        return Quaternion.Euler(0f, GetHashCode() % 360, 0f) * Vector3.forward;
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

        markerFlashRoutine = StartCoroutine(FlashMarkerRoutine(markerFlashCount, markerFlashInterval));
    }

    private IEnumerator FlashMarkerRoutine(int count, float interval)
    {
        WaitForSeconds wait = new WaitForSeconds(interval);

        for (int i = 0; i < count; i++)
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
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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

    // friendlyTarget(아군 강제공격 대상)은 UnitController/BuildingController 외에 아군 OC 유닛
    // (AllyController, doc/0450/0452)도 될 수 있어서, 실제로 지금 공중에 떠 있는 상태인지 타입별로
    // 확인해야 AirTargetPosition에 정확히 알려줄 수 있다.
    private static bool IsAirborne(MonoBehaviour target)
    {
        if (target is UnitController unit)
            return unit.isAirUnit;
        if (target is BuildingController building)
            return building.IsLifted();
        if (target is AllyController allyUnit) // 아군 OC 강제공격 대상(doc/0450) - 더 이상 EnemyUnitController가 아님(doc/0452)
            return allyUnit.IsAirUnit();
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
    // 반환값: 지상 유닛이 목적지로 실제 길을 잡는 데 성공했는지 (NavMeshAgent.SetDestination의 결과 그대로 - 목적지가
    // NavMesh로 연결되지 않은 영역(맵이 끊긴 다른 구역 등)이면 조용히 false를 반환한다). 대부분의 호출부는 이 값을
    // 무시해도 되지만, 실패를 감지 못하면 매 프레임 똑같이 실패하는 SetDestination만 반복하며 겉보기엔 그냥
    // 멈춰있는 것처럼 보이는 경우가 있어(예: GatherTick의 반납 이동), 그런 곳에서는 반드시 확인해야 한다.
    // 목적지 지점 근처에 NavMesh 샘플이 아예 안 잡히는 경우(경사로 없이 끊긴 언덕 위 등)
    // SetDestination이 실패하며 유닛이 조용히 멈춰버리므로, 더 넓은 반경으로 가장 가까운 NavMesh 지점을
    // 찾아 재시도한다. 경사로로 실제 연결된 언덕은 SetDestination이 바로 성공하고 Unity가 알아서
    // PathPartial로 갈 수 있는 데까지만 이동하므로 이 fallback을 타지 않는다 (doc/0375).
    private const float UnreachableDestinationSampleRadius = 20f;

    // AttackOrderTick/FriendlyAttackTick/ChaseTarget처럼 매 프레임 MoveAgentTo를 다시 호출하는 곳에서,
    // 목적지가 사실상 그대로인데도 매번 SetDestination(+실패 시 SamplePosition 재탐색)을 반복하면
    // NavMeshAgent가 경로를 다 계산하기도 전에 매 프레임 다시 리셋되어, 실제로는 목적지 방향으로
    // 계속 재조준(회전)만 하고 거의 전진하지 못하는 문제가 있었다(doc/0386). 직전과 사실상 같은
    // 목적지면 이미 잡혀있는 경로를 그대로 유지하고 재요청하지 않는다.
    private const float RedundantDestinationEpsilon = 0.5f;
    private Vector3? lastMoveAgentToDestination;

    // 도달 불가능할 수 있는 대상(친구 강제공격/명시 추격)을 사거리 밖에서 계속 쫓을 때 쓰는 상태.
    // 매 프레임 실시간 위치로 재탐색하면 [[0391]]처럼 멈칫거리므로, 도착(또는 더 갈 수 없어 멈춤)
    // 이벤트에서만 재확인한다. 도착 시점에 대상이 그 자리 그대로면(안 움직였는데 이 유닛도 더 못 감)
    // 도달 불가로 최종 판정하고 공격 명령을 취소한다. 대상이 그 사이 움직였으면 새 위치로 재탐색하고
    // 계속 쫓는다 - 대상이 계속 움직이는 한 명시 공격 명령은 취소되지 않는다(여러 판정 방식을 검토한
    // 끝에 이 방식이 가장 자연스럽다고 확정, doc/0397). 방금 사거리 안에서 밖으로 벗어난 프레임
    // (공격 중이던 대상이 도망감)은 즉시 재탐색한다.
    private bool chaseWasInAttackRange;

    // 마지막 재탐색에서 도달 불가로 판정됐는지 - 이 값에 따라 UpdateUnreachableChase()가 두 모드로
    // 나뉜다 (doc/0415): 도달 가능 모드는 게이트 없이 매 프레임 실시간 추적, 도달 불가 모드는
    // 가장 가까운 위치로 이동하는 동안 재탐색을 쉬었다가 도착 시에만 재확인한다.
    private bool chaseIsUnreachable;

    // 사거리 밖에서 대상을 계속 쫓을 때 FriendlyAttackTick/AttackOrderTick이 공용으로 쓰는 이동 갱신.
    // 반환값 true면 호출자가 도달 불가로 최종 판정된 것 - CancelAttackOrder() + HaltInPlace()로
    // 마무리해야 한다.
    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit)
        {
            MoveAgentTo(targetPos, destinationIsAirborne);
            return false;
        }

        if (justLeftAttackRange)
        {
            // 방금까지 사거리 안(공격 중)이었는데 대상이 도망가서 벗어남 - 즉시 재탐색
            chaseIsUnreachable = false;
            MoveAgentTo(targetPos, false);
            return false;
        }

        if (chaseIsUnreachable)
        {
            // 도달 불가 모드: 가장 가까운 위치로 이동하는 동안은(아직 도착 전) 재탐색하지 않는다 (doc/0391).
            if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                if (!navMeshAgent.hasPath)
                    MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색
                return false;
            }

            // 도착(또는 더 갈 수 없어 멈춤) - 여기서만 재탐색(도달 가능 여부 재확인)한다.
            bool reachableOnArrival = IsPositionReachable(targetPos);
            if (reachableOnArrival)
            {
                chaseIsUnreachable = false;
                MoveAgentTo(targetPos, false);
                return false;
            }

            bool targetMoved = !lastMoveAgentToDestination.HasValue ||
                (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

            if (!targetMoved)
                return true; // 대상도 그 자리 그대로 - 진짜 도달 불가로 최종 판정

            MoveAgentTo(targetPos, false); // 새 위치 기준으로 가장 가까운 위치로 다시 이동
            return false;
        }

        // 도달 가능 모드: 게이트 없이 매 프레임 실시간으로 계속 추적/재확인한다. MoveAgentTo의
        // 0.5m 캐시([[0386]])가 있어서 대상이 거의 안 움직이면 사실상 공짜 - FollowTick()이 이미
        // 쓰고 있는 것과 같은 패턴 (doc/0415).
        bool reachableNow = IsPositionReachable(targetPos);
        if (!reachableNow)
        {
            chaseIsUnreachable = true; // 방금 도달 불가로 전환
        }

        MoveAgentTo(targetPos, false);
        return false;
    }

    // MoveAgentTo와 달리 에이전트의 실제 경로/이동 상태를 전혀 건드리지 않는 순수 조회 - 그 지점이
    // 지금 이 유닛 기준으로 완전히 도달 가능한지만 확인한다 (doc/0403).
    private NavMeshPath reachabilityProbePath;
    private bool IsPositionReachable(Vector3 pos)
    {
        reachabilityProbePath ??= new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, reachabilityProbePath) &&
            reachabilityProbePath.status == NavMeshPathStatus.PathComplete;
    }

    private bool MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;

            if (navMeshAgent.hasPath &&
                lastMoveAgentToDestination.HasValue &&
                (lastMoveAgentToDestination.Value - destination).sqrMagnitude < RedundantDestinationEpsilon * RedundantDestinationEpsilon)
            {
                return true; // 목적지 변화 없음 - 진행 중인 경로 그대로 유지
            }

            if (navMeshAgent.SetDestination(destination))
            {
                lastMoveAgentToDestination = destination;
                return true;
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas) &&
                navMeshAgent.SetDestination(hit.position))
            {
                lastMoveAgentToDestination = destination;
                return true;
            }

            // 실패했더라도 "이 위치로 시도했다"는 기록은 남긴다 - 안 그러면 다음 판정에서 대상이
            // 실제로는 안 움직였는데도 "직전 기록이 없으니 움직인 것"으로 잘못 판정해서 완전히
            // 도달 불가능한 대상에게 매 프레임 재시도를 반복하게 된다 (doc/0415).
            lastMoveAgentToDestination = destination;
            return false;
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
            return true;
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
        followBuildingTarget = null;
        hasFollowBuildingOrder = false;

        chaseWasInAttackRange = false; // 이전 명령의 상태가 다음 명령으로 새 나가지 않도록 초기화
        chaseIsUnreachable = false; // doc/0415 - 도달 불가 상태도 함께 초기화

        // 이동/공격/스킬 등 다른 명령이 새로 들어오면 지정형 스킬(단일/범위) 이동-후-발동 대기도 함께 취소한다
        // (doc/0323 - 새 명령마다 취소 코드를 따로 추가하지 않고 이 공용 취소 지점 하나만 고치면 전부 커버됨).
        hasPendingSkillUnitOrder = false;
        hasPendingSkillAreaOrder = false;

        unitEffects?.StopAttackEffects(); // 이동/정지 등 다른 명령으로 공격이 취소되면 재생 중인 공격 이펙트도 즉시 정지

        CancelBuildOrder();
    }

    // ======================
    // 공격 명령 (우클릭 적 지정 / A 모드)
    // ======================

    // 특정 적 유닛을 추격하여 공격한다 (우클릭 적 클릭 / A 모드에서 적 클릭).
    // 대상이 살아있는 한 매 프레임 최신 위치를 쫓아가고(AttackOrderTick), 사거리 안에 들어오면
    // AttackRange가 자동으로 공격을 실행한다.
    public void AttackUnitTarget(EnemyUnitController target)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        CancelGatheringForNewCommand();

        orderedTarget = target;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        attackMoveDestination = target.transform.position;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        chaseWasInAttackRange = false; // 새 추격 명령 - 이전 명령의 상태 초기화
        chaseIsUnreachable = false; // doc/0415

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position);
    }

    // 특정 지점으로 공격-이동한다 (A 모드에서 땅 클릭).
    // 이동 중 사거리에 적이 들어오면 교전하고, 교전이 끝나면(AttackOrderTick) 다시 이 지점으로 이동을 재개한다.
    public void AttackMoveTo(Vector3 destination)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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

        chaseWasInAttackRange = false; // 새 강제공격 명령 - 이전 명령의 상태 초기화
        chaseIsUnreachable = false; // doc/0415

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

        Vector3 targetPos = friendlyTarget.transform.position;
        float sqrDistance = (transform.position - targetPos).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(targetPos, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
            chaseWasInAttackRange = true;
            return;
        }

        bool justLeftAttackRange = chaseWasInAttackRange;
        chaseWasInAttackRange = false;

        if (UpdateUnreachableChase(targetPos, IsAirborne(friendlyTarget), justLeftAttackRange))
        {
            // 재탐색을 몇 번 더 해봐도 대상이 계속 그 자리 + 이 유닛도 더 못 감 - 진짜 도달 불가로
            // 판정하고 공격 명령을 취소한다 (doc/0384/0392).
            CancelAttackOrder();
            HaltInPlace();
        }
    }

    public void FollowUnit(UnitController target)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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

        // 대상이 도달 불가 지형에 있어도(가장 가까운 위치로 이동 후 도착 시에만 재확인) 처리되도록
        // 강제공격과 같은 도달 가능/불가 로직을 재사용한다. 강제공격과 동일하게, 재탐색을 거듭해도
        // 계속 도달 불가로 판정되면 따라가기 명령도 포기한다 (doc/0422 - doc/0417의 "끝까지 포기하지
        // 않는다" 설계를 뒤집음).
        if (UpdateUnreachableChase(followTarget.transform.position, followTarget.isAirUnit, false))
        {
            CancelAttackOrder();
            HaltInPlace();
        }
    }

    // ===== 건물 우클릭 = 계속 따라다니기 =====
    // 건물은 리프트로 위치가 바뀔 수 있어서(일반 이동과 달리) 한 번만 이동시키지 않고 FollowUnit/FollowTick과
    // 동일한 패턴으로 매 프레임 최신 위치를 쫓아간다. 자원을 든 일꾼의 반납 리다이렉트(MoveToBuilding 참고)는
    // 이 메서드를 거치지 않는다 - 이건 "그냥 건물을 우클릭"했을 때(자원이 없거나 워커가 아닌 경우)만 쓰인다.
    public void FollowBuilding(BuildingController building)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        followBuildingTarget = building;
        hasFollowBuildingOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Idle; // Idle 유지 - AttackRange가 사거리 내 적을 자동으로 교전하게 함

        MoveAgentTo(GetClosestSurfacePoint(building.transform));
    }

    // 건물 따라다니기를 매 프레임 갱신한다: 건물이 파괴되면 그 자리에 멈추고, 교전 중이면 이동 명령을 덮어쓰지
    // 않으며, 건물 표면과 가까워지면 정지한다. 건물이 리프트로 움직이면 매 프레임 최신 위치로 계속 쫓아간다.
    private void FollowBuildingTick()
    {
        if (!hasFollowBuildingOrder)
            return;

        if (followBuildingTarget == null)
        {
            hasFollowBuildingOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();
            else
                isMovingAirUnit = false;
            return;
        }

        if (attackRange != null && attackRange.HasEnemyInRange)
            return; // 교전 중이면 그대로 둔다 (AttackRange가 정지시킨 상태 유지)

        Vector3 approachPoint = GetClosestSurfacePoint(followBuildingTarget.transform);
        float sqrDist = (transform.position - approachPoint).sqrMagnitude;

        if (sqrDist <= followStopMargin * followStopMargin)
        {
            if (!isAirUnit)
                navMeshAgent.isStopped = true;
            else
                isMovingAirUnit = false;
            return;
        }

        MoveAgentTo(approachPoint);
    }

    // 건설모드에서 건물 위치를 클릭했을 때 PlacementSystem이 호출한다.
    // destination에 도착하면 onArrived(실제 건물 스폰)를, 도착 전에 다른 명령으로 취소되면 onCancelled(그리드 예약 해제)를 실행한다.
    public void GoBuild(Vector3 destination, System.Action onArrived, System.Action onCancelled)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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

        if ((transform.position - buildDestination).sqrMagnitude > buildInteractRange * buildInteractRange)
        {
            // 목적지에 아직 못 왔는데 NavMeshAgent가 갈 수 있는 데까지 다 가서 멈춘 경우
            // (경사로 없는 언덕 위 등 도달 불가능한 위치, doc/0375 fallback으로 가장 가까운 지점까지만
            // 이동한 경우 포함) - 건설 명령을 취소하고 실패 음성을 재생한다 (doc/0382).
            if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                unitAudio?.PlayBuildFailVoice();
                UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.constructionfail"));
                HaltInPlace();
                CancelBuildOrder();
            }
            return;
        }

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
                // 사거리 안일 때의 실제 공격은 AttackRange.cs가 별도로 Attack()을 호출하므로 여기선
                // "방금 사거리 안이었는지" 상태만 갱신한다.
                if (inAttackRange)
                {
                    chaseWasInAttackRange = true;
                }
                else
                {
                    bool justLeftAttackRange = chaseWasInAttackRange;
                    chaseWasInAttackRange = false;

                    if (UpdateUnreachableChase(attackMoveDestination.Value, false, justLeftAttackRange))
                    {
                        // 재탐색을 몇 번 더 해봐도 대상이 계속 그 자리 + 이 유닛도 더 못 감 - 진짜 도달
                        // 불가로 판정 (doc/0384/0392).
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }
                }

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

    public EnemyUnitController GetOrderedTarget() => orderedTarget;

    // TurretController(AttackRange.GetTrackingTarget())가 아군 강제공격 대상을 조회할 때 쓴다.
    // friendlyTarget 자체는 UnitController/BuildingController 겸용 MonoBehaviour라 비공개로 두고 GameObject만 노출.
    public GameObject GetFriendlyTargetObject() => friendlyTarget != null ? friendlyTarget.gameObject : null;

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
        MoveAgentTo(pos); // NavMesh fallback(doc/0375) 재사용 - 도달 불가능한 대상도 가장 가까운 지점까지는 이동한다
    }

    public void Attack(Vector3 end, GameObject enemy)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
            // 공격 중에도 목표 고도(airCruiseAltitude)까지는 계속 상승한다 - 우주공항에서 막 생성되자마자
            // 근처에 적이 있어서 뜨기도 전에 공격을 시작하면 그대로 바닥에 눌러붙은 채로 싸우는 문제가
            // 있었다 (doc/0241). 수평 이동 없이(현재 XZ 그대로) 수직으로만 계속 목표 고도로 수렴시킨다.
            float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
            float desiredY = groundBelow + airCruiseAltitude;
            targetPosition = new Vector3(transform.position.x, desiredY, transform.position.z);

            // 이미 목표 고도에 도달했으면 "이동 중"으로 다시 켜지 않는다 - 여기서 매 프레임 무조건 true로
            // 켜면 공격이 지속되는 내내 IsCurrentlyMoving()이 true가 되어 이동 이펙트(엔진 트레일)가
            // 멈추지 않는다(doc/0252). 임계값은 Update()의 도착 판정과 동일하게 0.1.
            if (Mathf.Abs(transform.position.y - desiredY) >= 0.1f)
                isMovingAirUnit = true;
        }

        if (turretController == null)
            RotateYOnly(end); // 포탑 유닛(turretController != null)은 몸체를 안 돌린다 - 포탑이 대신 조준한다 (doc/0219)


        if (alreadyAttacked)
            return;

        // 대상 종류(아군 유닛 / 적 유닛 / 아군 OC)를 한 번만 조회해서, 아래 도메인 판정/데미지 계산 전체가 이 결과를 공유한다
        // (예전엔 IsTargetAirborne/GetTargetArmor/GetTargetSizeType/GetTargetArmorType이 각자 다시 조회했음).
        enemy.TryGetComponent<UnitController>(out var targetFriendlyUnit);
        enemy.TryGetComponent<EnemyUnitController>(out var targetEnemyUnit);
        enemy.TryGetComponent<AllyController>(out var targetAllyUnit); // 아군 OC 강제공격(doc/0450) 대상 스탯 조회용 (doc/0452)

        bool targetIsAir = IsTargetAirborne(enemy, targetFriendlyUnit, targetEnemyUnit, targetAllyUnit);
        if (!CanAttackDomain(targetIsAir))
        {
            // 쿨다운(alreadyAttacked)은 건드리지 않는다 - 대상이 다시 공격 가능한 도메인으로 돌아오면(예: 건물 착륙)
            // 대기 없이 바로 다음 프레임에 공격을 재개할 수 있어야 하기 때문.
            Debug.Log($"{name}: 이 유닛은 {(targetIsAir ? "공중" : "지상")} 대상을 공격할 수 없습니다.");
            return;
        }

        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(targetFriendlyUnit, targetEnemyUnit, targetAllyUnit);
            int finalDamage = CalculateFinalDamage(targetFriendlyUnit, targetEnemyUnit, targetAllyUnit, targetArmor);

            // Projectile이면 즉시 데미지를 넣지 않고 투사체가 명중했을 때 처음 적용한다 (doc/0290).
            // ProjectileAttack이 안 붙어있으면(설정 실수) 데미지가 아예 안 들어가는 사고를 막기 위해 Hitscan으로 폴백.
            // 공격자는 항상 아군(UnitController)이므로 isEnemyAttacker=false - 아군사격에 "적에게 공격받음"
            // 경고음이 울리지 않도록 하기 위함(doc/0292).
            if (attackDelivery == AttackDeliveryType.Projectile && projectileAttack != null)
                projectileAttack.Fire(enemy.transform, targetHealth, finalDamage, attackType, isEnemyAttacker: false);
            else
                targetHealth.GetDamage(finalDamage, transform.position, attackType, isEnemyAttacker: false); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용

            unitEffects?.PlayAttack();
            unitAudio?.PlayAttackSFX();
            laserBeamAttack?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)

            // 패시브 스킬(예: 스카이 랜서 "공중 강화" 도트)이 구독해서 쓰는 명중 이벤트 (doc/0323).
            // 투사체 공격은 명중이 아니라 발사 시점에 발행되지만(기존 데미지 계산 시점과 동일), 실사용에 문제없다.
            OnAttackHit?.Invoke(enemy);
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
    }

    // 공격방식×대상크기 배율(DamageMultiplierTableSO)과 이 유닛의 고유 장갑타입 보너스를 곱연산으로 적용한 뒤,
    // 대상의 고정 방어력을 감산해 최종 데미지를 계산한다. 최소 1은 항상 보장.
    private int CalculateFinalDamage(UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit, AllyController targetAllyUnit, int targetArmor)
    {
        SizeType targetSize = GetTargetSizeType(targetFriendlyUnit, targetEnemyUnit, targetAllyUnit);
        ArmorType targetArmorType = GetTargetArmorType(targetFriendlyUnit, targetEnemyUnit, targetAllyUnit);

        DamageMultiplierTableSO table = rtsController != null ? rtsController.DamageMultiplierTable : null;
        float sizeMultiplier = table != null ? table.GetMultiplier(attackType, targetSize) : 1f;

        float bonusMultiplier = (bonusVersusArmorPercent != 0f && targetArmorType == bonusVersusArmorType)
            ? 1f + bonusVersusArmorPercent / 100f
            : 1f;

        int scaledAttack = Mathf.RoundToInt(GetAttackDamage() * sizeMultiplier * bonusMultiplier);
        return Mathf.Max(1, scaledAttack - targetArmor);
    }

    // 공격 대상의 방어력을 조회한다 (아군 유닛이면 연구 보너스가 반영된 GetArmor(), 적 유닛/아군 OC(doc/0452)면 각자의
    // armor, 그 외(건물/자원)는 0).
    private int GetTargetArmor(UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit, AllyController targetAllyUnit)
    {
        if (targetFriendlyUnit != null)
            return targetFriendlyUnit.GetArmor();

        if (targetEnemyUnit != null)
            return targetEnemyUnit.GetArmor();

        if (targetAllyUnit != null)
            return targetAllyUnit.GetArmor();

        return 0;
    }

    // 공격 대상의 크기 타입을 조회한다 (건물/자원 등 타입 정보가 없는 대상은 Medium → 배율 100%로 영향 없음).
    private SizeType GetTargetSizeType(UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit, AllyController targetAllyUnit)
    {
        if (targetFriendlyUnit != null)
            return targetFriendlyUnit.GetSizeType();

        if (targetEnemyUnit != null)
            return targetEnemyUnit.GetSizeType();

        if (targetAllyUnit != null)
            return targetAllyUnit.GetSizeType();

        return SizeType.Medium;
    }

    // 공격 대상의 장갑 타입을 조회한다 (건물/자원 등은 고유 보너스가 적용될 일이 없으므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit, AllyController targetAllyUnit)
    {
        if (targetFriendlyUnit != null)
            return targetFriendlyUnit.GetArmorType();

        if (targetEnemyUnit != null)
            return targetEnemyUnit.GetArmorType();

        if (targetAllyUnit != null)
            return targetAllyUnit.GetArmorType();

        return ArmorType.Light;
    }

    // 공격 대상이 "지금" 공중 상태인지 조회한다. 건물은 이/착륙으로 실시간 바뀔 수 있어(BuildingController.IsLifted)
    // 매 공격 사이클마다 다시 확인해야 한다 - 명령을 내린 시점에 캐싱해둔 값을 계속 쓰면 안 된다.
    // EnemyUnitController/AllyController(doc/0452)도 이제 isAirUnit 개념이 있어(doc/0231) 그 값을 그대로 물어본다.
    private bool IsTargetAirborne(GameObject target, UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit, AllyController targetAllyUnit)
    {
        if (targetFriendlyUnit != null)
            return targetFriendlyUnit.IsAirUnit();

        if (targetEnemyUnit != null)
            return targetEnemyUnit.IsAirUnit();

        if (targetAllyUnit != null)
            return targetAllyUnit.IsAirUnit();

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
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        // 자원을 든 채로 정지 명령을 받으면 그 자리에 멈춰 화물을 방치하는 대신 반납을 이어간다
        // (Move/Attack처럼 플레이어가 명시적인 목적지를 지정한 명령과 달리, 정지는 목적지가 없으므로 충돌하지 않는다).
        if (isWorker && IsCarryingResource())
        {
            ReturnCargo();
            return;
        }

        HaltInPlace();
    }

    // 그 자리에 멈춰 Idle로 전환하는 실제 동작만 (StopUnit의 "화물 있으면 반납 재개" 판단 없이).
    // CancelGathering()이 StopUnit()을 직접 부르면, "반납할 건물을 못 찾음"(CancelGathering 진입 조건) →
    // StopUnit의 화물 재반납 리다이렉트 → ReturnCargo가 또 반납할 건물을 못 찾음 → CancelGathering() →
    // StopUnit() → ... 무한 재귀(스택 오버플로우)에 빠지므로, 그 경로는 이 저수준 헬퍼만 사용해야 한다.
    private void HaltInPlace()
    {
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
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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
            {
                navMeshAgent.isStopped = false; // 0399로 도착 시 걸린 정지를 다음 구간 이동 전에 풀어준다 (doc/0402)
                navMeshAgent.SetDestination(startPoint);
            }
            else
                targetPosition = AirTargetPosition(startPoint, true); // startPoint는 순찰 시작 시 현재(이미 공중) 위치를 그대로 캡처한 값
        }
        else
        {
            goingToEnd = true;

            if (!isAirUnit)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(endPoint);
            }
            else
                targetPosition = AirTargetPosition(endPoint);
        }
    }

    public void HoldUnit()
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        CancelGatheringForNewCommand();
        CancelAttackOrder();

        // 자원을 든 채로 홀드 명령을 받으면 그 자리에 멈춰 화물을 방치하는 대신 반납을 이어간다 (StopUnit과 동일한 이유).
        if (isWorker && IsCarryingResource())
        {
            ReturnCargo();
            return;
        }

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
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

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
            gatherTargetNode = node; // 새로 지정한 자원으로 기억을 갱신 - 반납 후 이 자원으로 캐러 감 (doc/0418)
            MoveToDepositTargetOrWait();
            return;
        }

        // 대기열 확인은 도착한 뒤에 한다 (일단 이동부터 시작)
        patrolling = false;
        gatherTargetNode = node;
        MoveTo(GetApproachPoint(node));
        gatherState = GatherState.MovingToResource;
    }

    // 노드 "중심"이 아니라 지금 위치에서 가장 가까운 콜라이더 표면 지점을 목적지로 삼는다
    // (SqrDistanceToTarget/AssignBuilderToStructure와 동일한 패턴). 자원 노드 여러 개가 가까이 붙어있을 때
    // 전부 중심으로 몰리면 노드 사이 좁은 틈에 여러 일꾼이 겹쳐 멈추는 문제가 있었다 - 표면 접근점을 쓰면
    // 각자 접근한 방향(바깥쪽)에서 자연히 멈추게 된다.
    private Vector3 GetApproachPoint(ResourceNode node) => GetClosestSurfacePoint(node.transform);

    // 목표 노드에 도착했지만 대기열이 꽉 찼거나(혹은 목표 노드 자체가 사라졌을) 때,
    // 자신 기준 alternateResourceSearchRadius 이내에서 대기열 여유가 있는 다른 자원 노드를 찾아 그쪽으로 재이동한다.
    // 성공하면 true를 반환하고(이동 시작, 도착하면 다시 대기열을 확인하게 됨), 근처에 대체 자원이 없으면 false를 반환한다.
    private bool TryRedirectToNearbyResource(ResourceNode exclude)
    {
        ResourceNode alt = FindNearestAvailableResourceNode(alternateResourceSearchRadius, exclude);
        if (alt == null)
            return false;

        gatherTargetNode = alt;
        MoveTo(GetApproachPoint(alt));
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

    // depositTargetTransform(FindNearestDepositBuilding으로 이미 정해진 반납 대상)으로 이동을 시작한다.
    // 대상 건물이 지금 리프트 중이면(공중에 떠 있어 NavMesh 위 점이 아님) 그 위치로 SetDestination을 걸지
    // 않는다 - 공중의 한 점으로 길찾기를 시도하면 NavMeshAgent가 목적지를 못 찾고 그대로 멈춰버려서,
    // 이후 다른 명령(자원 우클릭/건물 우클릭 등)을 내려도 다시 같은 방식으로 실패해 계속 안 움직이는
    // 버그가 있었다. 리프트 중엔 그 자리에서 대기만 하고, GatherTick의 MovingToBase 케이스가 착륙을
    // 감지하면 그때 실제 위치로 길을 잡는다.
    private void MoveToDepositTargetOrWait()
    {
        BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
        bool lifted = depositBuilding != null && depositBuilding.IsLifted();

        if (lifted)
        {
            if (!isAirUnit)
                navMeshAgent.isStopped = true;
        }
        else
        {
            MoveTo(GetDepositApproachPoint());
        }

        gatherState = GatherState.MovingToBase;
    }

    // 반납 대상 건물의 "중심(피벗)"이 아니라 표면에서 가장 가까운 지점을 목적지로 삼는다 - GetApproachPoint()와
    // 동일한 이유(doc/0345). 건물엔 대개 NavMeshObstacle이 붙어있어서 피벗 지점 자체가 그 장애물 구멍 안(NavMesh가
    // 없는 영역)인 경우가 흔한데, 표면 지점은 항상 장애물 경계 바로 바깥이라 NavMesh 길찾기가 훨씬 안정적이다.
    private Vector3 GetDepositApproachPoint() => GetClosestSurfacePoint(depositTargetTransform);

    // ===== Return Cargo 진입점 (UI "반환" 버튼) =====
    public void ReturnCargo()
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        if (!isWorker || !IsCarryingResource())
            return; // 일꾼이 아니거나 들고 있는 자원이 없으면 아무 것도 안 함

        depositTargetTransform = FindNearestDepositBuilding();
        if (depositTargetTransform == null)
        {
            CancelGathering(); // 반납할 건물이 없으면 그 자리에 멈춰서 Idle로
            return;
        }

        patrolling = false;
        // gatherTargetNode는 그대로 둔다 - 반납이 끝나면 Deposit()이 원래 캐던 자원으로 돌아간다 (doc/0418)
        MoveToDepositTargetOrWait();
    }

    // ===== 건물 우클릭 명령 =====
    // 메인기지를 우클릭했고 자원을 들고 있으면 그 기지로 직접 반납(후 캐던 자원으로 복귀). 그 외
    // (다른 건물, 또는 메인기지라도 자원이 없음)에는 기존 그대로 건물을 계속 따라다닌다
    // (FollowBuilding, doc/0345, doc/0419).
    public void MoveToBuilding(BuildingController building)
    {
        if (isConstructing || isRescueUnit) return; // 건설 중이거나 구조 전인 유닛은 다른 명령을 받지 않는다 (doc/0458)

        if (building.CompareTag("MainBase") && isWorker && IsCarryingResource())
        {
            ReturnCargoTo(building);
            return;
        }

        FollowBuilding(building);
    }

    // ReturnCargo()와 동일하지만 "가장 가까운 기지"를 다시 찾지 않고 인자로 받은 특정 건물을 그대로
    // 반납 대상으로 삼는다 (메인기지 우클릭으로 명시적으로 지정한 경우 전용).
    private void ReturnCargoTo(BuildingController building)
    {
        depositTargetTransform = building.transform;
        patrolling = false;
        // gatherTargetNode는 그대로 둔다 - 반납이 끝나면 Deposit()이 원래 캐던 자원으로 돌아간다 (doc/0418)
        MoveToDepositTargetOrWait();
    }

    // 채취 중단 + 그 자리에 멈춰서 Idle로 (반납 건물이 없거나, 채취 중이던 노드가 파괴된 경우 등).
    // StopUnit()이 아니라 HaltInPlace()를 직접 호출한다 - 이유는 HaltInPlace() 주석 참고(무한 재귀 방지).
    private void CancelGathering()
    {
        gatherState = GatherState.None;
        CancelAttackOrder();
        HaltInPlace();

        // 반납할 메인기지를 아예 못 찾은 경우(전부 파괴됐거나 하나도 없음) - 화물을 든 채 멈췄다는 뜻이라
        // 재현/원인 파악용으로 남겨둔다.
        if (isWorker && IsCarryingResource())
            Debug.LogWarning($"[GatherDiag] {gameObject.name}: 반납할 메인기지를 찾지 못해 화물을 든 채 정지함", this);
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
                if (SqrDistanceToTarget(gatherTargetNode.transform) <= gatherInteractRange * gatherInteractRange)
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
                    unitAudio?.PlayGatherSFX();
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

                    MoveToDepositTargetOrWait();
                }
                break;

            case GatherState.MovingToBase:
                BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
                if (depositBuilding != null && depositBuilding.IsLifted())
                {
                    // 반납 대상 건물이 이륙했다 - 착륙해 있는 다른 메인기지가 있으면 그쪽으로 갈아탄다.
                    // 착륙한 곳이 하나도 없으면(전부 이륙 중) 기존처럼 제자리에서 대기한다 (doc/0420).
                    Transform alt = FindNearestDepositBuilding();
                    if (alt != null && alt != depositTargetTransform)
                    {
                        depositTargetTransform = alt;
                        MoveToDepositTargetOrWait();
                        break;
                    }

                    if (!isAirUnit)
                        navMeshAgent.isStopped = true;
                    break;
                }

                // 건물이 착륙/재배치 등으로 움직였을 수 있으니, 목적지가 바뀌었을 때만 다시 길을 잡는다.
                // (표면 접근점 기준 - GetDepositApproachPoint() 참고, 건물 피벗은 NavMeshObstacle 구멍 안일 수 있음)
                Vector3 depositApproachPoint = GetDepositApproachPoint();
                if (isAirUnit || (navMeshAgent.destination - depositApproachPoint).sqrMagnitude > 0.01f)
                {
                    if (!MoveAgentTo(depositApproachPoint) && !isAirUnit)
                    {
                        // 표면 접근점으로도 실패하면 진짜로 NavMesh가 끊긴 영역(맵이 끊긴 다른 구역으로 건물이
                        // 재배치된 경우 등)이라는 뜻 - SetDestination이 조용히 실패하고 navMeshAgent.destination이
                        // 자기 위치 근처로 되돌아가버려서, 위의 "목적지가 바뀌었을 때만" 조건이 매 프레임 다시
                        // 참이 되어 똑같이 실패하는 시도를 영원히 반복하며 겉보기엔 그냥 멈춰있는 것처럼 보이는
                        // 버그가 있었다. 실패를 감지하면 무한 재시도 대신 화물을 든 채로 그 자리에 멈춘다
                        // (반납 대상을 아예 못 찾은 경우와 동일 처리).
                        Debug.LogWarning($"[GatherDiag] {gameObject.name}: 반납 목적지에 길을 못 찾음(NavMesh 미연결 추정) target={depositApproachPoint}", this);
                        CancelGathering();
                        return;
                    }
                }

                if (SqrDistanceToTarget(depositTargetTransform) <= gatherInteractRange * gatherInteractRange)
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

    // 건물처럼 콜라이더가 큰 대상은 피벗(중심)이 아니라 표면(가장 가까운 지점) 기준으로 거리 판정 (제곱 거리로 반환)
    private float SqrDistanceToTarget(Transform target)
    {
        return (transform.position - GetClosestSurfacePoint(target)).sqrMagnitude;
    }

    // 콜라이더가 있는 대상은 "중심(피벗)"이 아니라 표면에서 가장 가까운 지점을 돌려준다 - 건물처럼 콜라이더가
    // 큰 대상(및 NavMeshObstacle이 붙어있어 피벗 자체가 장애물 구멍 안일 수 있는 대상)에 대한 이동 목적지/거리
    // 판정을 전부 이 헬퍼 하나로 통일한다 (자원 채취 접근, 반납 이동, 건물 따라다니기 공용).
    private Vector3 GetClosestSurfacePoint(Transform target)
    {
        if (target.TryGetComponent<Collider>(out var col))
            return col.ClosestPoint(transform.position);

        return target.position;
    }

    // 착륙해서 실제로 도달 가능한 메인기지를 우선한다. 전부 공중에 떠 있으면(착륙한 곳이 하나도 없으면)
    // 그중 가장 가까운 곳을 그대로 목표로 잡아, GatherTick의 MovingToBase가 착륙할 때까지 대기하다가
    // 착륙하면 자동으로 반납을 재개하게 한다 (메인기지가 리프트 중이라 반납 자체가 막혀버리는 것 방지).
    private Transform FindNearestDepositBuilding()
    {
        BuildingController nearest = FindNearestMainBase(requireLanded: true);
        if (nearest == null)
            nearest = FindNearestMainBase(requireLanded: false);

        return nearest != null ? nearest.transform : null;
    }

    private BuildingController FindNearestMainBase(bool requireLanded)
    {
        BuildingController nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (BuildingController building in rtsController.BuildingList)
        {
            if (building == null) continue;
            if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납
            if (requireLanded && building.IsLifted()) continue;

            float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = building;
            }
        }

        return nearest;
    }

    public void Die()
    {
        gatherTargetNode?.LeaveQueue(this); // 대기열/채취 중에 사망해도 자리를 비워줌
        CancelBuildOrder(); // 건설 위치로 이동 중(hasBuildOrder)에 사망해도, 다른 명령으로 취소될 때와 동일하게
                             // 그리드 예약 해제 + 건물 가격 환불(onCancelled 콜백, 0089)이 실행되도록 함

        rtsController?.UnitList.Remove(this);
        rtsController?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
        rtsController?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환

        Destroy(gameObject);
    }

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => UnitcurrentState == UnitState.Idle;
    public bool IsMove() => UnitcurrentState == UnitState.Move;
    // 자동교전(AttackMoveTo/FollowUnit/FollowBuilding/패시브 대기 중 사거리 내 적 발견)은 UnitcurrentState를
    // 계속 Idle로 유지하므로(각 명령 지점 주석 참고), 상태값만으로는 "실제로 쏘는 중"을 놓친다 - AttackRange의
    // 실시간 교전 여부도 함께 확인해야 두리번 애니메이션 등이 전투 중을 정확히 인식한다 (doc/0451).
    public bool IsAttack() => UnitcurrentState == UnitState.Attack || (attackRange != null && attackRange.HasEnemyInRange);
    // AttackRange.Update()의 자동교전 게이트 전용 - IsAttack()과 달리 실제 명령 상태만 본다. IsAttack()은
    // 애니메이션용으로 사거리 내 적 존재 여부까지 넓게 판정해서(doc/0451), 이동 명령(Move) 중에도 직전까지
    // 싸우던 적이 감지 범위 안에 남아있으면 true가 되어 MoveTo() 등으로 공격을 끊으려 해도 AttackRange가
    // 매 프레임 다시 Attack()을 호출해 이동을 계속 막는 문제가 있었다(doc/0464).
    public bool IsAttackOrderState() => UnitcurrentState == UnitState.Attack;
    public bool IsAirUnit() => isAirUnit; // HoverBob 등 외부 이펙트 컴포넌트가 폴링용으로 사용(doc/0119)

    // 이동 이펙트(UnitEffects)가 상태머신을 직접 건드리지 않고 매 프레임 폴링으로 이동 여부를 판단할 수 있도록 노출.
    public bool IsCurrentlyMoving()
    {
        if (isAirUnit)
            return isMovingAirUnit;

        return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
    }

    public Sprite GetIcon() => icon;
    public string GetDescription() => infoDescription;
    public int GetUnitID() => unitID;
    public int GetEnemyDataUnitID() => enemyDataUnitID;
    public string GetHeroName() => heroName;

    // 구조 완료 시 Stage3Objectives 등이 호출한다 (doc/0458/0459 후속) - 명령 억제를 풀고, 선택 마커
    // 안의 Green 효과를 영구히 켜면서 Yellow는 꺼서 서로 배타적으로 만들고(마커 자체의 on/off는 그대로
    // SelectUnit/DeselectUnit이 담당), 낮춰뒀던 시야를 원래 범위로 되돌린다.
    public void Rescue()
    {
        if (!isRescueUnit)
            return; // 이미 구조됨 - 중복 호출 방지

        isRescueUnit = false;

        if (preRescueMarker != null)
            preRescueMarker.SetActive(false);
        if (rescuedMarker != null)
            rescuedMarker.SetActive(true);

        fogRevealerAgent?.SetSightRange(rescuedSightRange);

        if (miniMapIconRenderer != null)
            miniMapIconRenderer.color = RescuedMiniMapIconColor;

        // 초록으로 바뀐 마커를 짧게 깜빡여 구조됐다는 피드백을 준다 (doc/0465) - unitMarker 자체를
        // FlashMarker()와 동일한 방식으로 강제로 켰다 끄지만, 지금은 rescuedMarker가 이미 영구히
        // 켜진 상태라 깜빡일 때마다 초록으로 보인다.
        if (unitMarker != null)
        {
            if (markerFlashRoutine != null)
                StopCoroutine(markerFlashRoutine);

            markerFlashRoutine = StartCoroutine(FlashMarkerRoutine(rescueFlashCount, rescueFlashInterval));
        }

        SoundManager.Instance?.PlaySFX(rescueSfx, transform.position);
    }

    // 연구소 업그레이드로 얻은 전역 보너스를 더해서 반환한다 (RTSUnitController를 거쳐서만 조회 - UpgradeManager는 직접 참조하지 않음).
    public int GetAttackDamage() => attackDamage + (rtsController != null ? rtsController.GlobalAttackBonus : 0);
    public int GetArmor() => armor + (rtsController != null ? rtsController.GlobalArmorBonus : 0);
    public AttackEffectType GetAttackType() => attackType;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
    public bool GetCanAttackGround() => canAttackGround;
    public bool GetCanAttackAir() => canAttackAir;

    // 공격 1회당 동시에 나가는 투사체 개수 (Projectile + ProjectileAttack의 firePoints가 여러 개일 때만
    // 1 초과, doc/0291/0293) - 정보 패널 툴팁에서 "공격력 x2" 같은 배수 표기에 사용된다.
    public int GetShotCount() =>
        attackDelivery == AttackDeliveryType.Projectile && projectileAttack != null
            ? projectileAttack.GetFirePointCount()
            : 1;

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
        infoDescription = data.infoDescription;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;
        timeBetweenAttacks = data.attackSpeed;
        canAttackGround = data.canAttackGround;
        canAttackAir = data.canAttackAir;
        attackDelivery = data.attackDelivery;

        if (attackRange != null)
        {
            attackRange.UnitRange = data.attackRange;
            attackRange.EnsureDetectionRadius(); // 감지 반경이 새 사거리보다 좁아지지 않도록 보장 (doc/0239 안전장치)
        }

        healthManager?.InitializeHealth(data.hp);
    }

    public HealthManager GetHealthManager() => healthManager;

    // ======================
    // 특성(트레이트) 스킬 (doc/0228) - 실제 효과는 유닛별 IUnitSkill 구현체에 위임
    // ======================
    public RTSUnitController.TraitChoice GetCurrentTrait() => currentTrait;

    // RTSUnitController.ChooseTrait()가 이 유닛 종류의 선택이 결정될 때(또는 새로 생산된 유닛이 기존 선택을
    // 물려받을 때) 호출한다. 패시브 트레이트의 실제 스탯 보정 수치는 유닛 타입마다 다르므로, 여기서는
    // "지금 어떤 트레이트를 장착했는지"만 기록하고, 구체적인 보정 적용은 스킬이 확정된 뒤 유닛별로 추가한다.
    public void ApplyTrait(RTSUnitController.TraitChoice choice)
    {
        currentTrait = choice;
    }

    // 대기 중인 지정형 스킬 명령(단일 유닛/범위)이 있는지. AttackRange.Update()가 이 값을 확인해서
    // 스킬 사용 명령 중엔 사거리 밖 적 자동교전으로 이동을 가로채지 않게 하는 데 쓴다 (doc/0383).
    public bool HasPendingSkillOrder => hasPendingSkillUnitOrder || hasPendingSkillAreaOrder;

    public bool CanUseSkill() => skillCooldownRemaining <= 0f;
    public void StartSkillCooldown(float cooldown) => skillCooldownRemaining = cooldown;
    public float GetSkillCooldownRemaining() => skillCooldownRemaining; // 스킬 슬롯 쿨다운 원형 이펙트 표시용 (doc/0323 후속)

    // order panel 스킬 버튼(슬롯 6) 클릭/단축키로 호출되는 실제 진입점 (RTSUnitController.ActivateSkill 참고).
    // 이 유닛 프리팹에 IUnitSkill을 구현한 컴포넌트(유닛별 전용 스킬 스크립트)가 붙어있으면 그쪽에 위임하고,
    // 아직 그 유닛의 스킬이 구현되지 않았으면 로그만 남기고 아무 효과도 내지 않는다.
    public void UseTraitSkill(UnitTraitOption traitData, SkillActivationContext context)
    {
        IUnitSkill skill = GetComponent<IUnitSkill>();
        if (skill == null)
        {
            Debug.Log($"{name}: '{currentTrait}' 트레이트 스킬이 아직 구현되지 않았습니다 (IUnitSkill 컴포넌트 없음).");
            return;
        }

        skill.Activate(this, currentTrait, traitData, context);
    }

    // ===== 은신 (doc/0323) =====
    public bool IsStealthed() => isStealthed;
    public void SetStealthed(bool value) => isStealthed = value;

    // ===== 영구 스탯 가산 패시브 (doc/0323) - ApplyTrait()에서 유닛 타입별로 필요할 때 호출 =====
    public void AddAttackDamageBonus(int amount) => attackDamage += amount;
    public void AddArmorBonus(int amount) => armor += amount;
    public void MultiplyAttackInterval(float multiplier) => timeBetweenAttacks *= multiplier; // 1보다 작으면 공격속도 증가

    // ===== 지정형 액티브 스킬(단일 유닛/범위) - doc/0323 =====
    // RTSUnitController.ConfirmSkillUnitTarget()이 호출. 대상이 스킬 전용 사거리(trait.skillRange) 안에
    // 들어올 때까지 이동하다가, 도착하면 SkillOrderTick()이 자동으로 발동시킨다. CancelAttackOrder()를 먼저
    // 호출해 기존 이동/공격/스킬 지시를 정리한 뒤 새로 지정한다.
    public void MoveToUseSkillOnUnit(GameObject target, UnitTraitOption trait)
    {
        CancelAttackOrder();

        hasPendingSkillUnitOrder = true;
        pendingSkillUnitTarget = target;
        pendingSkillTraitData = trait;

        MoveAgentTo(target.transform.position);
    }

    // RTSUnitController.ConfirmSkillAreaTarget()이 호출. point는 이동 중 바뀌지 않는 고정 좌표(범위 지정형).
    public void MoveToUseSkillOnArea(Vector3 point, UnitTraitOption trait)
    {
        CancelAttackOrder();

        hasPendingSkillAreaOrder = true;
        pendingSkillGroundTarget = point;
        pendingSkillTraitData = trait;

        MoveAgentTo(point);
    }

    // 사거리 안에 들어오면 스킬을 발동하고 그 순간 쿨다운을 시작한다(확정 사항 - 지정한 순간이 아니라
    // 실제로 사용한 순간부터 쿨다운을 카운트해야 함).
    private void SkillOrderTick()
    {
        if (hasPendingSkillUnitOrder)
        {
            if (pendingSkillUnitTarget == null) // 대상이 이동 중 파괴됨
            {
                hasPendingSkillUnitOrder = false;
                return;
            }

            float sqrDist = (transform.position - pendingSkillUnitTarget.transform.position).sqrMagnitude;
            if (sqrDist > pendingSkillTraitData.skillRange * pendingSkillTraitData.skillRange)
            {
                MoveAgentTo(pendingSkillUnitTarget.transform.position); // 계속 추격 이동(대상이 움직이는 유닛일 수 있음)
                return;
            }

            StopUnit();
            UseTraitSkill(pendingSkillTraitData, new SkillActivationContext(pendingSkillUnitTarget, pendingSkillUnitTarget.transform.position));
            StartSkillCooldown(pendingSkillTraitData.cooldown);
            hasPendingSkillUnitOrder = false;
            return;
        }

        if (hasPendingSkillAreaOrder)
        {
            float sqrDist = (transform.position - pendingSkillGroundTarget).sqrMagnitude;
            if (sqrDist > pendingSkillTraitData.skillRange * pendingSkillTraitData.skillRange)
                return; // MoveAgentTo로 이미 그 지점으로 이동 중 - 도착할 때까지 대기

            StopUnit();
            UseTraitSkill(pendingSkillTraitData, new SkillActivationContext(null, pendingSkillGroundTarget));
            StartSkillCooldown(pendingSkillTraitData.cooldown);
            hasPendingSkillAreaOrder = false;
        }
    }
}
