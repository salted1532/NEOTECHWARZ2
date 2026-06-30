using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class ProductionData
{
    public int UnitID;
    public float RemainTime;

    public ProductionData(int unitID, float productionTime)
    {
        UnitID = unitID;
        RemainTime = productionTime;
    }
}

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private UnitDataSO database;

    private List<ProductionData> productionQueue = new();

    private BuildingController buildingController;

    void Start()
    {
        buildingController = GetComponentInParent<BuildingController>();    
    }

    private void Update()
    {
        Produce();
    }

    public void Enqueue(int unitID)
    {
        Debug.Log($"database : {database}");

        if (database == null)
        {
            Debug.LogError("UnitDataSO가 할당되지 않았습니다.");
            return;
        }

        Debug.Log($"unitData : {database.unitData}");

        if (productionQueue.Count >= 5)
            return;

        int index = database.unitData.FindIndex(d =>
        {
            Debug.Log($"비교 : {d.ID} == {unitID}");
            return d.ID == unitID;
        });
        //int index = database.unitData.FindIndex(d => d.ID == unitID);

        if (index == -1)
        {
            Debug.LogError($"ID {unitID}의 UnitData를 찾을 수 없습니다.");
            return;
        }

        UnitData data = database.unitData[index];

        productionQueue.Add(
            new ProductionData(data.ID, data.productionTime)
        );

        PrintQueue();
    }

    private void Spawn(int unitID)
    {
        int index = database.unitData.FindIndex(d => d.ID == unitID);

        if (index == -1)
            return;

        UnitData data = database.unitData[index];
        

        Vector3 spawnPos = transform.position + new Vector3(0, 0, -2f);

        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        spawnunit.GetComponent<UnitController>().MoveTo(buildingController.GetRallyPos());
        
        PrintQueue();

    }

    private void Produce()
    {
        if (productionQueue.Count == 0)
            return;

        ProductionData current = productionQueue[0];

        current.RemainTime -= Time.deltaTime;

        if (current.RemainTime > 0)
            return;

        int unitID = current.UnitID;

        // 먼저 큐에서 제거
        productionQueue.RemoveAt(0);

        // 그 다음 생성
        Spawn(unitID);
    }
    public void Cancel(int index)
    {
        if (index < 0 || index >= productionQueue.Count)
            return;

        productionQueue.RemoveAt(index);
    }

    private void PrintQueue()
    {
        string log = "생산 대기열 : ";

        if (productionQueue.Count == 0)
        {
            log += "비어있음";
        }
        else
        {
            for (int i = 0; i < productionQueue.Count; i++)
            {
                log += $"[{i}] ID:{productionQueue[i].UnitID} ({productionQueue[i].RemainTime:F1}s) ";

                if (i < productionQueue.Count - 1)
                    log += "-> ";
            }
        }

        Debug.Log(log);
    }
}
