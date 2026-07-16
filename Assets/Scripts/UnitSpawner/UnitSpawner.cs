using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// 생산 대기열 한 항목의 상태(어떤 유닛을, 얼마나 더 기다려야 하는지)
[System.Serializable]
public class ProductionData
{
    public int UnitID;
    public float RemainTime;
    public float TotalTime;

    // 0~1 사이의 진행률 (프로그레스 바 표시용)
    public float Progress => 1f - (RemainTime / TotalTime);

    public ProductionData(int unitID, float productionTime)
    {
        UnitID = unitID;
        RemainTime = productionTime;
        TotalTime = productionTime;
    }
}

// 건물에 부착되어 유닛 생산 대기열을 관리하고, 시간이 다 되면 실제로 유닛을 스폰하는 컴포넌트.
public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private UnitDataSO database;

    private const int MaxQueueSize = 5;

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

    // 지정한 유닛 ID를 생산 대기열에 추가한다. 대기열이 5개 이상이면 무시된다.
    // (자원 소모는 호출측인 RTSUnitController.TryProduceUnit에서 먼저 처리된 뒤 호출됨)
    public void Enqueue(int unitID)
    {
        Debug.Log($"database : {database}");

        if (database == null)
        {
            Debug.LogError("UnitDataSO is not assigned.");
            return;
        }

        Debug.Log($"unitData : {database.unitData}");

        if (productionQueue.Count >= MaxQueueSize)
            return;

        int index = database.unitData.FindIndex(d =>
        {
            Debug.Log($"Compare: {d.ID} == {unitID}");
            return d.ID == unitID;
        });
        //int index = database.unitData.FindIndex(d => d.ID == unitID);

        if (index == -1)
        {
            Debug.LogError($"Could not find UnitData for ID {unitID}.");
            return;
        }

        UnitData data = database.unitData[index];

        productionQueue.Add(
            new ProductionData(data.ID, data.productionTime)
        );

        PrintQueue();
    }

    // 대기열이 가득 찼는지(자원 소모 전에 미리 확인하기 위함, RTSUnitController.TryProduceUnit에서 사용)
    public bool IsQueueFull() => productionQueue.Count >= MaxQueueSize;

    // 생산이 완료된 유닛을 실제로 Instantiate하고, 스포너 위치에서 랠리 포인트로 이동을 명령한다.
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

    // 매 프레임 대기열 맨 앞(index 0) 항목의 남은 시간을 줄이고, 0 이하가 되면 스폰을 실행한다.
    // 대기열은 항상 맨 앞의 한 항목만 진행되는 순차(FIFO) 생산 방식이다.
    private void Produce()
    {
        if (productionQueue.Count == 0)
            return;

        if (buildingController != null && !TerritoryManager.IsInsideAlliedTerritory(transform.position))
            return; // 영토 밖이면 타이머가 그 자리에서 멈춘다 (대기열은 유지, 리셋하지 않음)

        ProductionData current = productionQueue[0];

        current.RemainTime -= Time.deltaTime;

        if (current.RemainTime > 0)
            return;

        int unitID = current.UnitID;

        // 완료된 큐 항목 제거
        productionQueue.RemoveAt(0);

        // 유닛 스폰 실행
        Spawn(unitID);
    }

    // 대기열의 특정 인덱스 항목을 취소(제거)하고, 환불에 쓸 수 있도록 그 유닛ID를 반환한다 (유효하지 않으면 -1).
    // (UI의 대기열 슬롯 클릭 시 호출)
    public int Cancel(int index)
    {
        if (index < 0 || index >= productionQueue.Count)
            return -1;

        int unitID = productionQueue[index].UnitID;
        productionQueue.RemoveAt(index);
        PrintQueue();
        return unitID;
    }

    // 건물이 파괴될 때 호출: 대기열에 남아있던 항목 전체를 반환(제거)한다 - 환불 자체는 호출측(RTSUnitController)이 처리.
    public List<ProductionData> ClearQueue()
    {
        List<ProductionData> remaining = new List<ProductionData>(productionQueue);
        productionQueue.Clear();
        return remaining;
    }
   

    /// <summary>
    /// 콘솔 디버그용 대기열 출력
    /// </summary>
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

    // 대기열 반환 (읽기 전용 - UI 표시용)
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        return productionQueue;
    }

    /// <summary>
    /// 현재 생산 중인 항목의 진행률(0~1) 반환
    /// </summary>
    public float GetProductionProgress()
    {
        if (productionQueue.Count == 0)
            return 0f;

        return productionQueue[0].Progress;
    }
}
