using UnityEngine;

public class UnitController : MonoBehaviour
{
    [SerializeField]
    private GameObject unitMarker;


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
}
