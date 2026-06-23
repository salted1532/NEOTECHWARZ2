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
    private UnitState currentState = UnitState.Idle;

    private bool arrived = false;
    public bool alreadyAttacked = false;
    public float timeBetweenAttacks;

    private void Awake()
    {
        if (!isAirUnit)
            navMeshAgent = GetComponent<NavMeshAgent>();
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
                currentState = UnitState.Idle;
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
                    currentState = UnitState.Idle;
                    Debug.Log("지상유닛 도착 !");
                }
            }
        }
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
        currentState = UnitState.Move;

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
        currentState = UnitState.Idle;
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
        currentState = UnitState.Attack;
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
        currentState = UnitState.Idle;
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

    // ======================
    // 상태 확인용 (AttackRange용)
    // ======================
    public bool IsIdle() => currentState == UnitState.Idle;

    public bool IsMove() => currentState == UnitState.Move;
    public bool IsAttack() => currentState == UnitState.Attack;
}
