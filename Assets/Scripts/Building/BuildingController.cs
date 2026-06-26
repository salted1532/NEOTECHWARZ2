using UnityEngine;

public class BuildingController : MonoBehaviour
{
    [SerializeField]
    private GameObject buildingMarker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();

        controller.BuildingList.Add(this);
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
}
