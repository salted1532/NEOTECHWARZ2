using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using FischlWorks_FogWar;

// 적 유닛 컨트롤러. 예전엔 EnemyController(선택 표시/스탯/사망 처리만 담당, AI 없음)였던 것을
// 이동/전투 AI까지 합쳐서 이름을 바꿨다 (doc/0231) - 플레이어의 UnitController에 대응하는 적 진영 버전이지만
// 기능은 훨씬 단순하다: 자동 교전(사거리 내 감지), 이동, 공격-이동 세 가지만 지원한다
// (지정 대상 강제 추격, 건설/채집, 특성 스킬, 포탑/레이저 연동 등은 없음 - 필요해지면 그때 추가).
public class EnemyUnitController : MonoBehaviour, IDestructible, IAttackRangeUnit
{
    [SerializeField]
    private GameObject enemyMarker;

    [SerializeField]
    private Sprite icon; // Info_panel에 표시할 아이콘

    private string infoDescription; // Info_panel에 표시할 설명 (UnitData.infoDescription, doc/0476)

    // 미니맵에 표시하는 y40대 스프라이트 마커(자식 오브젝트, 인스펙터에서 연결). 이 프로젝트의 안개(csFogWar)는
    // 실제 3D Plane(Y≈1)으로 구현돼 있어 이렇게 Y가 높은 오브젝트는 깊이 테스트로 가려지지 않는다 - 그래서
    // Update()에서 안개 상태를 직접 조회해 이 렌더러를 켜고 끈다 (doc/0356).
    [SerializeField]
    private SpriteRenderer minimapIcon;

    [SerializeField]
    private int minimapFogVisibilityMargin = 1; // UserControl.fogVisibilityMargin과 동일한 목적

    private csFogWar fogWar;

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

    // 공격 전달 방식 (UnitController와 동일한 필드, doc/0290). Projectile인데 ProjectileAttack 컴포넌트가
    // 안 붙어있으면 Attack()에서 자동으로 Hitscan으로 폴백한다.
    [SerializeField] private AttackDeliveryType attackDelivery = AttackDeliveryType.Hitscan;

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
    private TurretController turretController; // 차량형 유닛의 포탑(자식에 붙어있으면 세팅, 없으면 null - UnitController와 동일한 옵셔널 연동)

    private Coroutine flashRoutine;
    private RTSUnitController rtsController;
    private HealthManager healthManager; // 피격 시 공격받은 위치로 반격하러 가기 위한 이벤트 구독용
    private UnitEffects unitEffects;         // 공격 이펙트 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private UnitAudio unitAudio;             // 공격 SFX 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private LaserBeamAttack laserBeamAttack; // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트
    private ProjectileAttack projectileAttack; // 투사체 발사 유닛만 붙어있는 옵셔널 컴포넌트
    private MeleeBodySlamAttack meleeBodySlamAttack; // 근접 유닛(Ripfang 등) 몸 파츠에만 붙어있는 옵셔널 컴포넌트

    private void Awake()
    {
        attackRange = GetComponentInChildren<EnemyAttackRange>();
        turretController = GetComponentInChildren<TurretController>();
        healthManager = GetComponent<HealthManager>();
        unitEffects = GetComponent<UnitEffects>();
        unitAudio = GetComponent<UnitAudio>();
        laserBeamAttack = GetComponent<LaserBeamAttack>();
        TryGetComponent(out projectileAttack);
        meleeBodySlamAttack = GetComponentInChildren<MeleeBodySlamAttack>(); // 몸 모델(자식 오브젝트)에 붙는 컴포넌트라 turretController와 동일하게 자식까지 탐색

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }
        else
        {
            // 시작 시점에 "자기 위치로 이동" 명령을 내린 것과 동일하게 처리한다 - destinationIsAirborne을
            // 기본값(false)으로 둬서 현재 XZ의 지면 높이 + airCruiseAltitude로 목적지를 계산하고,
            // isMovingAirUnit을 true로 켜서 Update()가 실제로 그 높이까지 떠오르게 한다 (UnitController.Awake()와
            // 동일한 패턴). 씬에 바닥 위치로 배치해둔 테스트 오브젝트도 시작하자마자 자동으로 떠오른다.
            targetPosition = AirTargetPosition(transform.position);
            isMovingAirUnit = true;
        }
    }

    private void OnEnable()
    {
        if (healthManager != null)
            healthManager.OnDamaged += HandleAttacked;
    }

    private void OnDisable()
    {
        if (healthManager != null)
            healthManager.OnDamaged -= HandleAttacked;
    }

    // 감지 범위 밖(사거리가 긴 원거리 유닛 등)에서 공격받으면 공격이 어디서 왔는지 모른 채 가만히 있던
    // 문제를 해결한다 - 데미지 이벤트에 실려오는 attackerPosition으로 공격-이동해서 반격하러 간다
    // (플레이어의 "A + 클릭"과 동일한 AttackMoveTo를 그대로 재사용, 이동 중 마주치는 다른 대상과도 자동 교전).
    private void HandleAttacked(int damage, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isEnemyAttacker)
            return; // 공격자가 같은 진영(OC)이면 반응하지 않음 - 플레이어에게 공격받았을 때만 반격하러 간다

        if (attackRange != null && attackRange.HasTargetInRange)
            return; // 이미 사거리 안에서 교전 중이면 그대로 둔다(겹쳐서 새 이동 명령을 내리지 않음)

        AttackMoveTo(attackerPosition);
    }

    private void Start()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(false);

        rtsController = FindFirstObjectByType<RTSUnitController>();
        fogWar = FindFirstObjectByType<csFogWar>(); // 안개가 없는 씬(테스트 씬 등)에서는 null - Update()에서 그 경우 마커를 항상 켜둠

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
                // ResetPath()는 호출하지 않는다 - NavMeshAgent는 도착하면(또는 도달 불가능한 대상이라
                // 갈 수 있는 데까지만 간 채) 스스로 정지하므로 불필요하고, 오히려 hasPath를 false로
                // 만들어서 MoveAgentTo의 목적지 캐시(doc/0386)를 매 프레임 무효화시킨다 - 자동교전
                // (EnemyAttackRange → ChaseTarget)이 매 프레임 다시 호출되는 동안 도착 판정이 계속
                // ResetPath()를 부르면 이미 도착한 뒤에도 매 프레임 경로가 재계산되어 미세하게 계속
                // 흔들리는 문제가 있었다 (doc/0387).
                // isStopped는 true로 건다 - 안 그러면 destination이 여전히 이 지점을 가리키는 채로
                // 남아서, 도착 후 다른 유닛에게 밀려나면 NavMeshAgent가 스스로 원래 자리로 되돌아가려
                // 한다(doc/0399). MoveAgentTo가 다음 명령 때 항상 isStopped = false로 풀어준다.
                navMeshAgent.isStopped = true;
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }

        AttackMoveTick();
        UpdateFogVisibility();
    }

    // 미니맵 마커 토글(doc/0356, 마커가 Y40대라 안개 Plane 깊이 테스트로는 안 가려지는 문제의 대안)과
    // 선택 해제(doc/0360, 선택 중인 적이 안개 속으로 들어가면 선택을 풀어줌)가 같은 안개 조회 결과를
    // 공유한다 - 매 프레임 두 번 물어볼 필요 없음. 안개 조회 로직 자체는 공용 헬퍼로 뽑음 (doc/0358).
    private void UpdateFogVisibility()
    {
        bool revealed = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);

        if (minimapIcon != null)
            minimapIcon.enabled = revealed;

        if (!revealed)
            rtsController?.ClearSelectedEnemyIfMatches(this);
    }

    // ======================
    // 이동 / 공격-이동 (외부(AI 관제소/미션 스크립트)에서 호출하는 진입점)
    // ======================

    public void MoveTo(Vector3 destination)
    {
        arrived = false;
        attackMoveDestination = null;
        currentState = EnemyState.Move;

        unitEffects?.StopAttackEffects(); // 공격 중이었다면 이동 명령으로 전환되므로 재생 중인 공격 이펙트를 즉시 정지

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
    // 반환값 true면 "도착했는데 대상이 그 자리 그대로 있어서 더는 다가갈 수 없다"는 뜻 - 호출자가
    // 이 대상을 포기하고 다른 대상을 찾아야 한다 (doc/0398, UnitController.UpdateUnreachableChase와
    // 동일한 판단 구조 - doc/0397).
    // 마지막 재탐색에서 도달 불가로 판정됐는지 - 이 값에 따라 ChaseTarget()이 두 모드로 나뉜다
    // (doc/0415): 도달 가능 모드는 게이트 없이 매 프레임 실시간 추적, 도달 불가 모드는 가장 가까운
    // 위치로 이동하는 동안 재탐색을 쉬었다가 도착 시에만 재확인한다.
    private bool chaseIsUnreachable;

    public bool ChaseTarget(Vector3 pos)
    {
        arrived = false;
        currentState = EnemyState.Idle;

        if (isAirUnit)
        {
            MoveAgentTo(pos);
            return false;
        }

        if (chaseIsUnreachable)
        {
            // 도달 불가 모드: 가장 가까운 위치로 이동하는 동안은(아직 도착 전) 재탐색하지 않는다 (doc/0391).
            if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                if (!navMeshAgent.hasPath)
                    MoveAgentTo(pos); // 아직 이동을 시작 안 했으면 최초 탐색
                return false;
            }

            // 도착(또는 더 갈 수 없어 멈춤) - 여기서만 재탐색(도달 가능 여부 재확인)한다.
            bool reachableOnArrival = IsPositionReachable(pos);
            if (reachableOnArrival)
            {
                chaseIsUnreachable = false;
                MoveAgentTo(pos);
                return false;
            }

            bool targetMoved = !lastMoveAgentToDestination.HasValue ||
                (lastMoveAgentToDestination.Value - pos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

            if (!targetMoved)
                return true; // 대상도 그 자리 그대로 - 도달 불가로 최종 판정, 포기

            MoveAgentTo(pos); // 새 위치 기준으로 가장 가까운 위치로 다시 이동
            return false;
        }

        // 도달 가능 모드: 게이트 없이 매 프레임 실시간으로 계속 추적/재확인한다 (doc/0415).
        bool reachableNow = IsPositionReachable(pos);
        if (!reachableNow)
        {
            chaseIsUnreachable = true; // 방금 도달 불가로 전환
        }

        MoveAgentTo(pos);
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

    // UnitController.MoveAgentTo와 동일한 fallback (doc/0375) - 경사로 없이 끊긴 언덕 등으로
    // SetDestination이 실패하면 더 넓은 반경으로 가장 가까운 NavMesh 지점을 찾아 재시도한다.
    private const float UnreachableDestinationSampleRadius = 20f;

    // UnitController.MoveAgentTo와 동일한 캐싱(doc/0386) - 목적지가 직전과 사실상 같으면 재요청하지 않는다.
    private const float RedundantDestinationEpsilon = 0.5f;
    private Vector3? lastMoveAgentToDestination;

    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;

            if (navMeshAgent.hasPath &&
                lastMoveAgentToDestination.HasValue &&
                (lastMoveAgentToDestination.Value - destination).sqrMagnitude < RedundantDestinationEpsilon * RedundantDestinationEpsilon)
            {
                return;
            }

            if (navMeshAgent.SetDestination(destination))
            {
                lastMoveAgentToDestination = destination;
                return;
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas) &&
                navMeshAgent.SetDestination(hit.position))
            {
                lastMoveAgentToDestination = destination;
                return;
            }

            // 실패했더라도 "이 위치로 시도했다"는 기록은 남긴다 - 안 그러면 다음 판정에서 대상이
            // 실제로는 안 움직였는데도 "직전 기록이 없으니 움직인 것"으로 잘못 판정해서 완전히
            // 도달 불가능한 대상에게 매 프레임 재시도를 반복하게 된다 (doc/0415).
            lastMoveAgentToDestination = destination;
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
            // 공격 중에도 목표 고도(airCruiseAltitude)까지는 계속 상승한다 - 생성되자마자 근처에 상대가
            // 있어서 뜨기도 전에 공격을 시작하면 그대로 바닥에 눌러붙은 채로 싸우는 문제가 있었다 (doc/0241).
            // 수평 이동 없이(현재 XZ 그대로) 수직으로만 계속 목표 고도로 수렴시킨다.
            float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
            float desiredY = groundBelow + airCruiseAltitude;
            targetPosition = new Vector3(transform.position.x, desiredY, transform.position.z);

            // 이미 목표 고도에 도달했으면 "이동 중"으로 다시 켜지 않는다 - 여기서 매 프레임 무조건 true로
            // 켜면 공격이 지속되는 내내 IsCurrentlyMoving()이 true가 되어 이동 이펙트(엔진 트레일)가
            // 멈추지 않는다(doc/0252). 임계값은 Update()의 도착 판정(arrivedVertically)과 동일하게 0.1.
            if (Mathf.Abs(transform.position.y - desiredY) >= 0.1f)
                isMovingAirUnit = true;
        }

        if (turretController == null)
            RotateYOnly(end); // 포탑 유닛(turretController != null)은 몸체를 안 돌린다 - 포탑이 대신 조준한다 (UnitController.Attack()과 동일한 패턴)

        if (alreadyAttacked)
            return;

        bool targetIsAir = IsAirborne(target);
        if (!CanAttackDomain(targetIsAir))
            return;

        if (target.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(target);
            int finalDamage = CalculateFinalDamage(target, targetArmor);

            // Projectile이면 즉시 데미지를 넣지 않고 투사체가 명중했을 때 처음 적용한다 (doc/0290,
            // UnitController.Attack()과 동일한 훅 지점). ProjectileAttack이 안 붙어있으면 Hitscan으로 폴백.
            // 공격자는 항상 적 진영(EnemyUnitController)이므로 isEnemyAttacker=true (doc/0292).
            if (attackDelivery == AttackDeliveryType.Projectile && projectileAttack != null)
                projectileAttack.Fire(target.transform, targetHealth, finalDamage, attackType, isEnemyAttacker: true);
            else
                targetHealth.GetDamage(finalDamage, transform.position, attackType, isEnemyAttacker: true);

            unitEffects?.PlayAttack();
            unitAudio?.PlayAttackSFX();
            laserBeamAttack?.Fire(target.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
            meleeBodySlamAttack?.Slam(); // 근접 유닛만 붙어있는 옵셔널 컴포넌트 - 몸통박치기 애니메이션
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

        // 구조된 아군 OC(AllyController)도 공격 대상이 될 수 있으므로(doc/0452) AllyController의
        // 대칭 메소드와 동일하게 조회한다 (doc/0493 - 이 분기 누락으로 항상 기본값이 나가던 버그 수정).
        if (target.TryGetComponent<AllyController>(out var allyUnit))
            return allyUnit.GetArmor();

        return 0;
    }

    // 공격 대상의 크기 타입 조회 (건물 등 타입 정보가 없는 대상은 Medium → 배율 100%로 영향 없음).
    private SizeType GetTargetSizeType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.GetSizeType();

        if (target.TryGetComponent<AllyController>(out var allyUnit))
            return allyUnit.GetSizeType();

        return SizeType.Medium;
    }

    // 공격 대상의 장갑 타입 조회 (건물 등은 고유 보너스 대상이 아니므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.GetArmorType();

        if (target.TryGetComponent<AllyController>(out var allyUnit))
            return allyUnit.GetArmorType();

        return ArmorType.Light;
    }

    // 공격 대상이 "지금" 공중 상태인지 조회 (건물은 이/착륙으로 실시간 바뀔 수 있어 매번 다시 확인).
    private bool IsAirborne(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var playerUnit))
            return playerUnit.IsAirUnit();

        if (target.TryGetComponent<AllyController>(out var allyUnit))
            return allyUnit.IsAirUnit();

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
    // currentState는 Idle/Move만 쓰고 Attack은 쓰지 않는다(doc/0236 - Attack에 멈추면 교전 종료 후
    // 추적 재개가 안 됨). 대신 "지금 실제 사거리 안에 대상이 있어서 공격 중인가"를 EnemyAttackRange에
    // 직접 물어본다 - UnitAnimatorDriver가 Fire 애니메이션을 켤 때 이 값을 쓴다(doc/0253).
    public bool IsAttack() => attackRange != null && attackRange.HasTargetInAttackRange;
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
    public string GetDescription() => infoDescription;
    public string GetEnemyName() => enemyName;
    public HealthManager GetHealthManager() => healthManager;
    public int GetEnemyUnitID() => enemyUnitID;

    // TurretController가 조준 대상을 물어볼 때 쓰는 EnemyAttackRange 접근자 (UnitController.GetAttackRange()와 동일한 용도).
    public EnemyAttackRange GetAttackRange() => attackRange;
    public int GetAttackDamage() => attackDamage;
    public int GetArmor() => armor;
    public AttackEffectType GetAttackType() => attackType;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
    public bool GetCanAttackGround() => canAttackGround;
    public bool GetCanAttackAir() => canAttackAir;

    // 공격 1회당 동시에 나가는 투사체 개수 (UnitController.GetShotCount()와 동일한 패턴, doc/0291/0293).
    public int GetShotCount() =>
        attackDelivery == AttackDeliveryType.Projectile && projectileAttack != null
            ? projectileAttack.GetFirePointCount()
            : 1;

    // 생산/스폰 시점에 EnemyUnitDataSO(OC 데이터)의 값으로 스탯을 덮어쓴다. UnitController.ApplyUnitData와
    // 동일한 패턴 (doc/0230의 OC Unit Data SO를 나중에 "AI 관제소"/스포너가 이 메서드로 흘려보낼 수 있음).
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        infoDescription = LocalizationManager.GetTextOrFallback($"unit.oc.{data.ID}.info", data.infoDescription);
        enemyName = LocalizationManager.GetTextOrFallback($"unit.oc.{data.ID}.name", data.unitName);
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

    // 사망 처리: 선택 목록에서 제거하고 게임오브젝트를 파괴한다 (HealthManager의 IDestructible 구현체로 호출됨).
    public void Die()
    {
        rtsController?.selectedEnemyList.Remove(this);

        Destroy(gameObject);
    }
}
