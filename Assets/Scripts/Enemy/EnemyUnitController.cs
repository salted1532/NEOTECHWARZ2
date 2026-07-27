using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 적 유닛 컨트롤러. 예전엔 EnemyController(선택 표시/스탯/사망 처리만 담당, AI 없음)였던 것을
// 이동/전투 AI까지 합쳐서 이름을 바꿨다 (doc/0231) - 플레이어의 UnitController에 대응하는 적 진영 버전이지만
// 기능은 훨씬 단순하다: 자동 교전(사거리 내 감지), 이동, 공격-이동 세 가지만 지원한다
// (지정 대상 강제 추격, 건설/채집, 특성 스킬, 포탑/레이저 연동 등은 없음 - 필요해지면 그때 추가).
public class EnemyUnitController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject enemyMarker;

    [SerializeField]
    private Sprite icon; // Info_panel에 표시할 아이콘

    [SerializeField]
    private string enemyName; // Info_panel에 표시할 이름

    // OC Unit Data SO(EnemyUnitDataSO)의 UnitData.ID와 매칭되는 값 - Start()에서 이 ID로 스탯을 조회해
    // ApplyUnitData()로 덮어쓴다 (UnitController.unitID와 동일한 패턴, doc/0232).
    [SerializeField]
    private int enemyUnitID;

    // ===== 전투 스탯 (공격력/방어력) =====
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
    // 이 유닛의 공격 수단 (피격 이펙트 선택에 사용됨) - UnitController와 동일한 필드
    [SerializeField] private AttackEffectType attackType = AttackEffectType.Bullet;
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;

    // 이 유닛이 "공격할 때" 적용되는 제한 - 지상/공중 유닛을 각각 공격할 수 있는지
    [SerializeField] private bool canAttackGround = true;
    [SerializeField] private bool canAttackAir = true;

    [Header("고유 추가 데미지 (해당 없으면 Percent를 0으로 둘 것)")]
    [SerializeField] private ArmorType bonusVersusArmorType = ArmorType.Light;
    [SerializeField] private float bonusVersusArmorPercent = 0f;

    [SerializeField] private float flashInterval = 0.3f; // 공격 명령 피드백 깜빡임 간격
    [SerializeField] private int flashCount = 3;          // 깜빡이는 횟수

    // ===== 이동 =====
    private NavMeshAgent navMeshAgent;

    [SerializeField] private bool isAirUnit;
    private bool isMovingAirUnit;
    private Vector3 targetPosition;
    [SerializeField] private float moveSpeed = 10f; // 공중 유닛 전용 (지상 유닛은 NavMeshAgent 자체 속도를 사용)
    [SerializeField] private float arriveDistance = 0.5f;
    [SerializeField] private float airCruiseAltitude = 5f;
    [SerializeField] private LayerMask airGroundLayer; // 공중 유닛이 발밑 지면 높이를 재는 레이어 (UnitController와 동일한 용도)

    private enum EnemyState { Idle, Move, Attack }
    private EnemyState currentState = EnemyState.Idle;

    private bool arrived = true;
    private bool alreadyAttacked;
    public float timeBetweenAttacks = 1f;

    // 공격-이동 목적지 (교전 후 복귀할 지점). null이면 공격-이동 중이 아님.
    private Vector3? attackMoveDestination;
    private EnemyAttackRange attackRange; // 사거리 내 대상 감지용 (자식 컴포넌트)

    private Coroutine flashRoutine;
    private RTSUnitController rtsController;

    private void Awake()
    {
        attackRange = GetComponentInChildren<EnemyAttackRange>();

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position, true);
        }
    }

    private void Start()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(false);

        rtsController = FindFirstObjectByType<RTSUnitController>();

        // 씬에 직접 배치됐든 나중에 스포너를 거쳐 생성됐든, 항상 자기 enemyUnitID로 OC Unit Data SO를
        // 조회해서 스스로 스탯(체력/공격력/이름 등)을 적용한다 (UnitController.Start()와 동일한 패턴).
        ApplyUnitData(rtsController != null ? rtsController.GetEnemyUnitData(enemyUnitID) : null);
    }

    private void Update()
    {
        if (isAirUnit && isMovingAirUnit)
        {
            Vector3 pos = transform.position;

            Vector3 horizontalTarget = new Vector3(targetPosition.x, pos.y, targetPosition.z);
            pos = Vector3.MoveTowards(pos, horizontalTarget, moveSpeed * Time.deltaTime);

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
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }

        if (!isAirUnit)
        {
            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                navMeshAgent.ResetPath();
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }

        AttackMoveTick();
    }

    // ======================
    // 이동 / 공격-이동 (외부(AI 관제소/미션 스크립트)에서 호출하는 진입점)
    // ======================

    public void MoveTo(Vector3 destination)
    {
        arrived = false;
        attackMoveDestination = null;
        currentState = EnemyState.Move;

        GetComponent<UnitEffects>()?.StopAttackEffects(); // 공격 중이었다면 이동 명령으로 전환되므로 재생 중인 공격 이펙트를 즉시 정지

        MoveAgentTo(destination);
    }

    // 플레이어의 "땅 공격(A + 클릭)"과 동일한 기능: 이동 중 사거리에 상대가 들어오면 교전하고,
    // 교전이 끝나면(AttackMoveTick) 다시 이 지점으로 이동을 재개한다.
    public void AttackMoveTo(Vector3 destination)
    {
        arrived = false;
        attackMoveDestination = destination;
        currentState = EnemyState.Idle; // Idle이어야 EnemyAttackRange가 사거리 내 상대를 자동으로 교전한다

        MoveAgentTo(destination);
    }

    // Idle 상태에서 사거리 밖의 감지된 상대에게 다가갈 때 EnemyAttackRange가 호출한다.
    public void ChaseTarget(Vector3 pos)
    {
        arrived = false;
        currentState = EnemyState.Idle;
        MoveAgentTo(pos);
    }

    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
        }
        else
        {
            targetPosition = AirTargetPosition(destination);
            isMovingAirUnit = true;
        }
    }

    // 공격-이동을 매 프레임 갱신한다: 교전 중이면 그대로 두고, 교전이 끝나 정지된 상태면 원래 목적지로
    // 이동을 재개한다 (UnitController.AttackOrderTick의 공격-이동 부분과 동일한 패턴).
    private void AttackMoveTick()
    {
        if (attackMoveDestination == null)
            return;

        if (attackRange != null && attackRange.HasTargetInRange)
            return; // 교전 중이면 그대로 둔다

        bool groundStopped = !isAirUnit && navMeshAgent.isStopped;
        bool airStopped = isAirUnit && !isMovingAirUnit;

        if (groundStopped || airStopped)
        {
            arrived = false;
            currentState = EnemyState.Idle;
            MoveAgentTo(attackMoveDestination.Value);
        }
    }

    // ======================
    // 공격
    // ======================

    // 사거리 안의 대상을 공격한다 (EnemyAttackRange가 매 프레임 호출). target은 플레이어 유닛(UnitController)
    // 또는 건물(BuildingController) - 둘 다 HealthManager를 갖고 있다.
    public void Attack(Vector3 end, GameObject target)
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

        currentState = EnemyState.Attack;
        RotateYOnly(end);

        if (alreadyAttacked)
            return;

        bool targetIsAir = IsAirborne(target);
        if (!CanAttackDomain(targetIsAir))
            return;

        if (target.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(target);
            int finalDamage = CalculateFinalDamage(target, targetArmor);
            targetHealth.GetDamage(finalDamage, transform.position, attackType);
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(target.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    // 플레이어 UnitController.CalculateFinalDamage와 동일한 공식(공격타입×크기 배율 + 장갑타입 고유 보너스).
    // 단, 연구소(Lab) 전역 공격/방어 보너스는 적용하지 않는다 - OC 쪽 연구 시스템이 아직 없기 때문 (doc/0231).
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

    // 공격 대상의 방어력 조회 (플레이어 유닛이면 UnitController.GetArmor(), 건물 등은 0).
    private int GetTargetArmor(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.GetArmor();

        return 0;
    }

    // 공격 대상의 크기 타입 조회 (건물 등 타입 정보가 없는 대상은 Medium → 배율 100%로 영향 없음).
    private SizeType GetTargetSizeType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.GetSizeType();

        return SizeType.Medium;
    }

    // 공격 대상의 장갑 타입 조회 (건물 등은 고유 보너스 대상이 아니므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.GetArmorType();

        return ArmorType.Light;
    }

    // 공격 대상이 "지금" 공중 상태인지 조회 (건물은 이/착륙으로 실시간 바뀔 수 있어 매번 다시 확인).
    private bool IsAirborne(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.IsAirUnit();

        if (target.TryGetComponent<BuildingController>(out var building))
            return building.IsLifted();

        return false;
    }

    // ======================
    // 공중 유닛 헬퍼 (UnitController와 동일한 방식)
    // ======================

    private Vector3 AirTargetPosition(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (destinationIsAirborne)
            return destination;

        return new Vector3(destination.x, destination.y + airCruiseAltitude, destination.z);
    }

    private float SampleGroundHeight(Vector3 xzPosition, float fallback)
    {
        if (airGroundLayer == 0)
            return fallback;

        Vector3 rayOrigin = new Vector3(xzPosition.x, 1000f, xzPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, airGroundLayer))
            return hit.point.y;

        return fallback;
    }

    private void RotateYOnly(Vector3 target)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
    }

    // ======================
    // 선택 / 마커 (기존 EnemyController와 동일)
    // ======================

    public void SelectEnemy()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(true);
    }

    public void DeselectEnemy()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(false);
    }

    // 공격 명령(우클릭/A 모드)을 받았을 때 "어느 적이 대상인지" 피드백으로 마커를 짧게 깜빡인다.
    public void FlashMarker()
    {
        if (enemyMarker == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(flashInterval);

        for (int i = 0; i < flashCount; i++)
        {
            enemyMarker.SetActive(true);
            yield return wait;
            enemyMarker.SetActive(false);
            yield return wait;
        }

        bool isSelected = rtsController != null && rtsController.selectedEnemyList.Contains(this);
        enemyMarker.SetActive(isSelected);

        flashRoutine = null;
    }

    // ======================
    // 상태 확인용 (EnemyAttackRange용)
    // ======================
    public bool IsIdle() => currentState == EnemyState.Idle;
    public bool IsMove() => currentState == EnemyState.Move;
    public bool IsAttack() => currentState == EnemyState.Attack;
    public bool IsAirUnit() => isAirUnit;

    // 이동 이펙트(UnitEffects)가 상태머신을 직접 건드리지 않고 매 프레임 폴링으로 이동 여부를 판단할 수 있도록 노출
    // (UnitController.IsCurrentlyMoving()과 동일한 패턴, doc/0233).
    public bool IsCurrentlyMoving()
    {
        if (isAirUnit)
            return isMovingAirUnit;

        return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
    }

    public bool CanAttackDomain(bool targetIsAirUnit) => targetIsAirUnit ? canAttackAir : canAttackGround;

    public Sprite GetIcon() => icon;
    public string GetEnemyName() => enemyName;
    public int GetEnemyUnitID() => enemyUnitID;
    public int GetAttackDamage() => attackDamage;
    public int GetArmor() => armor;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;

    // 생산/스폰 시점에 EnemyUnitDataSO(OC 데이터)의 값으로 스탯을 덮어쓴다. UnitController.ApplyUnitData와
    // 동일한 패턴 (doc/0230의 OC Unit Data SO를 나중에 "AI 관제소"/스포너가 이 메서드로 흘려보낼 수 있음).
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        enemyName = data.unitName;
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

    // 사망 처리: 선택 목록에서 제거하고 게임오브젝트를 파괴한다 (HealthManager의 IDestructible 구현체로 호출됨).
    public void Die()
    {
        rtsController?.selectedEnemyList.Remove(this);

        Destroy(gameObject);
    }
}
