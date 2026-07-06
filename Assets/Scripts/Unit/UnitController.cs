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
public class UnitController : MonoBehaviour
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
