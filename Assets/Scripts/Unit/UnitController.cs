using System.Net;
using System.Resources;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using static UnityEditor.PlayerSettings;
using static UnityEngine.GraphicsBuffer;

public class UnitController : MonoBehaviour
{
    [SerializeField]
    private GameObject unitMarker;

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

    private enum GatherState { None, MovingToResource, Gathering, MovingToBase, Depositing }
    private GatherState gatherState = GatherState.None;

    private int amountPerTrip = 5; //자원 채취량
    private float gatherDuration = 3f; //자원 채취 시간

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

    private void Awake()
    {
        isWorker = CompareTag("Worker");

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
                UnitcurrentState = UnitState.Idle;
                Debug.Log("공중유닛 도착 !");
            }
        }
        //지상 유닛 일 경우
        if (!isAirUnit)
        {
            if (!arrived &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                navMeshAgent.ResetPath();
                UnitcurrentState = UnitState.Idle;
            }
        }

        GatherTick();
        PatrolTick();
    }

    public void SelectUnit()
    {
        unitMarker.SetActive(true);
    }

    public void DeselectUnit()
    {
        unitMarker.SetActive(false);
    }

    public void MoveTo(Vector3 end)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        patrolling = false;
        UnitcurrentState = UnitState.Move;

        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(end);
        }
        else
        {
            targetPosition = end + Vector3.up * 5f;
            isMovingAirUnit = true;
        }
    }

    public void AttackToGround(Vector3 end)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        //idle 모드로 변경(적 발견시 바로 공격)
        UnitcurrentState = UnitState.Idle;
        if (isAirUnit == false)
        {
            navMeshAgent.isStopped = false;
            if (navMeshAgent != null)
            {
                navMeshAgent.SetDestination(end);
            }
        }
        else
        {
            targetPosition = end + Vector3.up * 5f;
            isMovingAirUnit = true;
        }
    }

    public void AttackToUnit(Vector3 end)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        UnitcurrentState = UnitState.Attack;
        if (isAirUnit == false)
        {
            navMeshAgent.isStopped = false;
            if (navMeshAgent != null)
            {
                navMeshAgent.SetDestination(end);
            }
        }
        else
        {
            targetPosition = end + Vector3.up * 5f; ;
            isMovingAirUnit = true;
        }



    }
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

        patrolling = false;
        gatherTargetNode = node;
        MoveTo(node.transform.position);
        gatherState = GatherState.MovingToResource;
    }

    private bool IsCarryingResource() => DepositOre.activeSelf || DepositGas.activeSelf;

    // 채취 중단 + 그 자리에 멈춰서 Idle로 (반납 건물이 없거나, 채취 중이던 노드가 파괴된 경우 등)
    private void CancelGathering()
    {
        gatherState = GatherState.None;
        StopUnit();
    }

    // 이동/공격/정지 등 다른 명령이 들어와서 채취를 중단시킬 때 호출 (반경만 원상복구, Idle 전환은 각 명령이 알아서 함)
    private void CancelGatheringForNewCommand()
    {
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

        // 채취 도중 노드가 고갈되어 파괴된 경우(다른 유닛이 마저 캐간 경우 등) 방어
        if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.Gathering)
            && gatherTargetNode == null)
        {
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

        if (gatherTargetNode == null || gatherTargetNode.IsDepleted)
        {
            CancelGathering(); // 캐던 노드가 없어졌으면(고갈/파괴) 그 자리에 멈춰서 Idle로
            return;
        }

        MoveTo(gatherTargetNode.transform.position);
        gatherState = GatherState.MovingToResource;
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
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);

        Destroy(gameObject);
    }

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => UnitcurrentState == UnitState.Idle;
    public bool IsMove() => UnitcurrentState == UnitState.Move;
    public bool IsAttack() => UnitcurrentState == UnitState.Attack;
}
