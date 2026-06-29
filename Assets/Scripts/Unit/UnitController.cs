using System.Net;
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

    private void Awake()
    {
        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
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

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();

        controller.UnitList.Add(this);
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
                navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                if (!navMeshAgent.hasPath || navMeshAgent.velocity.sqrMagnitude < 0f)
                {
                    arrived = true;
                    UnitcurrentState = UnitState.Idle;
                    Debug.Log("지상유닛 도착 !");
                }
            }
        }

        PatrolTick();
    }

    public void SelectUnit()
    {
        //Debug.Log(name + " 선택");
        unitMarker.SetActive(true);
    }

    public void DeselectUnit()
    {
        //Debug.Log(name + " 선택 해제");
        unitMarker.SetActive(false);
    }

    public void MoveTo(Vector3 end)
    {
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

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => UnitcurrentState == UnitState.Idle;
    public bool IsMove() => UnitcurrentState == UnitState.Move;
    public bool IsAttack() => UnitcurrentState == UnitState.Attack;
}
