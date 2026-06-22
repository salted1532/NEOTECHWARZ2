using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

public class UnitController : MonoBehaviour
{
    [SerializeField]
    private GameObject unitMarker;

    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private float moveSpeed = 10f;
    private Vector3 targetPosition;
    private bool isMovingAirUnit = false;
    [SerializeField]
    private bool isAirUnit;

    private void Awake()
    {
        if(isAirUnit == false)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }
        else
        {
            MoveAirUnitTo(transform.position);
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
        if (isMovingAirUnit)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                isMovingAirUnit = false;
            }
        }
    }

    public void SelectUnit()
    {
        Debug.Log(name + " 선택");
        unitMarker.SetActive(true);
    }

    public void DeselectUnit()
    {
        Debug.Log(name + " 선택 해제");
        unitMarker.SetActive(false);
    }

    public void MoveTo(Vector3 end)
    {
        if(isAirUnit == false)
        {
            if (navMeshAgent != null)
            {
                navMeshAgent.SetDestination(end);
            }
        }
        else
        {
            MoveAirUnitTo(end);
        }

    }

    public void MoveAirUnitTo(Vector3 end)
    {
        targetPosition = end + Vector3.up * 5f;
        isMovingAirUnit = true;
    }
}
