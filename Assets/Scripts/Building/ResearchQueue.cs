using System.Collections.Generic;
using UnityEngine;

// 연구 대기열 한 항목의 상태 (어떤 종류를, 몇 레벨째 연구 중인지)
[System.Serializable]
public class ResearchData
{
    public ResearchType Type;
    public int Level; // 이 연구가 완료되면 도달하는 레벨 (1, 2, 3)
    public float RemainTime;
    public float TotalTime;

    // 0~1 사이의 진행률 (프로그레스 바 표시용)
    public float Progress => 1f - (RemainTime / TotalTime);

    public ResearchData(ResearchType type, int level, float researchTime)
    {
        Type = type;
        Level = level;
        RemainTime = researchTime;
        TotalTime = researchTime;
    }
}

// 연구소(Lab)에 부착되어 공격력/방어력 연구 대기열(레벨 1~3)을 관리하고, 완료되면 RTSUnitController를
// 통해 전역 보너스를 올리는 컴포넌트. UnitSpawner(생산 대기열)와 동일한 FIFO 타이머 구조를 사용한다.
public class ResearchQueue : MonoBehaviour
{
    public const int MaxLevel = 3;

    // 공격/방어 각각 "다음 레벨" 1개씩만 의미가 있으므로 동시에 최대 2개(둘 다 큐잉)까지만 허용
    private const int MaxQueueSize = 2;

    [Header("레벨별 연구 시간(초) - [0]=1업 [1]=2업 [2]=3업")]
    [SerializeField] private float[] attackResearchTime = { 60f, 90f, 120f };
    [SerializeField] private float[] armorResearchTime = { 60f, 90f, 120f };

    [Header("레벨별 연구 비용 (광물/가스, 공격·방어 공통) - [0]=1업 [1]=2업 [2]=3업")]
    [SerializeField] private int[] researchOreCost = { 100, 200, 250 };
    [SerializeField] private int[] researchGasCost = { 100, 200, 250 };

    [Header("레벨업 1회당 적용되는 보너스량 (누적)")]
    [SerializeField] private int attackBonusPerLevel = 1;
    [SerializeField] private int armorBonusPerLevel = 1;

    private readonly List<ResearchData> researchQueue = new();

    private RTSUnitController rtsController;
    private BuildingController buildingController;

    private int attackLevel; // 0~3
    private int armorLevel;  // 0~3

    void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        buildingController = GetComponentInParent<BuildingController>();
    }

    void Update()
    {
        Research();
    }

    public int GetLevel(ResearchType type) => type == ResearchType.Attack ? attackLevel : armorLevel;

    // 같은 타입이 이미 대기열에 있는지 (공격 1업 연구 중에는 공격 2업을 큐잉할 수 없게 막기 위함)
    private bool IsQueued(ResearchType type) =>
        researchQueue.Exists(r => r.Type == type);

    public bool CanEnqueue(ResearchType type) =>
        GetLevel(type) < MaxLevel && !IsQueued(type) && researchQueue.Count < MaxQueueSize;

    // "현재 레벨+1"을 연구하는 데 드는 비용. 이미 최대 레벨이면 (0,0) 반환.
    public (int ore, int gas) GetCost(ResearchType type)
    {
        int nextLevel = GetLevel(type) + 1;
        if (nextLevel > MaxLevel)
            return (0, 0);

        return (researchOreCost[nextLevel - 1], researchGasCost[nextLevel - 1]);
    }

    // 지정한 타입의 다음 레벨을 대기열에 추가한다. (자원 소모는 호출측 RTSUnitController.TryResearch에서 먼저 처리된 뒤 호출됨)
    public void Enqueue(ResearchType type)
    {
        if (!CanEnqueue(type))
            return;

        int nextLevel = GetLevel(type) + 1;
        float time = (type == ResearchType.Attack ? attackResearchTime : armorResearchTime)[nextLevel - 1];

        researchQueue.Add(new ResearchData(type, nextLevel, time));
    }

    // 매 프레임 대기열 맨 앞 항목의 남은 시간을 줄이고, 0 이하가 되면 연구를 완료 처리한다.
    // 대기열은 항상 맨 앞의 한 항목만 진행되는 순차(FIFO) 방식이다 (공격/방어를 같이 큐잉해도 하나씩 순서대로 진행됨).
    private void Research()
    {
        if (researchQueue.Count == 0)
            return;

        if (buildingController != null && !TerritoryManager.IsInsideAlliedTerritory(transform.position))
            return; // 영토 밖이면 타이머가 그 자리에서 멈춘다 (생산 큐와 동일한 규칙)

        ResearchData current = researchQueue[0];
        current.RemainTime -= Time.deltaTime;

        if (current.RemainTime > 0)
            return;

        ResearchType type = current.Type;
        researchQueue.RemoveAt(0);
        Complete(type);
    }

    private void Complete(ResearchType type)
    {
        if (type == ResearchType.Attack)
            attackLevel++;
        else
            armorLevel++;

        int bonus = type == ResearchType.Attack ? attackBonusPerLevel : armorBonusPerLevel;
        rtsController.AddGlobalBonus(type, bonus); // UpgradeManager를 직접 만지지 않고 RTSUnitController를 거쳐서 반영
    }

    // 대기열의 특정 인덱스 항목을 취소하고, 환불을 위해 그 ResearchType을 int로 반환한다 (유효하지 않으면 -1).
    // (레벨은 Complete()에서만 올라가므로, 큐에서 제거만 하는 시점엔 아직 레벨이 바뀌지 않아 GetCost()로 그대로 환불액을 되짚을 수 있음)
    public int Cancel(int index)
    {
        if (index < 0 || index >= researchQueue.Count)
            return -1;

        ResearchType type = researchQueue[index].Type;
        researchQueue.RemoveAt(index);
        return (int)type;
    }

    // 건물이 파괴될 때 호출: 대기열에 남아있던 항목 전체를 반환(제거)한다 - 환불 자체는 호출측(RTSUnitController)이 처리.
    public List<ResearchData> ClearQueue()
    {
        List<ResearchData> remaining = new List<ResearchData>(researchQueue);
        researchQueue.Clear();
        return remaining;
    }

    // 대기열 반환 (읽기 전용 - UI 표시용)
    public IReadOnlyList<ResearchData> GetResearchQueue() => researchQueue;

    /// <summary>
    /// 현재 연구 중인 항목의 진행률(0~1) 반환
    /// </summary>
    public float GetResearchProgress() => researchQueue.Count == 0 ? 0f : researchQueue[0].Progress;
}
