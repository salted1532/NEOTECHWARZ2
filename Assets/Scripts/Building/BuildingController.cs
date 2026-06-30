using UnityEngine;

public class BuildingController : MonoBehaviour
{
    [SerializeField]
    private GameObject buildingMarker;

    private UnitSpawner UnitSpawner;

    private Vector3 RallyPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();

        controller.BuildingList.Add(this);

        UnitSpawner = GetComponentInChildren<UnitSpawner>();

        RallyPosition = transform.position + new Vector3(0, 0, -2f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SelectBuilding()
    {
        //Debug.Log(name + " 선택");
        buildingMarker.SetActive(true);
    }

    public void DeselecBuilding()
    {
        //Debug.Log(name + " 선택 해제");
        buildingMarker.SetActive(false);
    }

    public void SetRallyPosition(Vector3 position)
    {
        RallyPosition = position;
    }

    public void SpawnUnit(int unitID)
    {
        UnitSpawner.Enqueue(unitID);
    }

    public Vector3 GetRallyPos()
    {
        return RallyPosition;
    }
}
