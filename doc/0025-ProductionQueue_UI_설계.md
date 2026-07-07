# 0025 - SelectInfo 패널: 생산 대기열(Production Queue) & 상태정보 UI (설계 → 실제 구현)

> **번호에 대한 메모**: 이 문서는 원래 번호 없이 `doc/ProductionQueue_UI_설계.md`로 2026-07-04에
> 작성된 "코드 수정 전, 설계만 정리한 문서"다. 세션 로그 번호 규칙(0013)보다 먼저 작성되어 원래
> 몇 번째였는지 알 수 없어, 세션 로그를 전부 `doc/`로 옮기며 임의로 0025번을 부여했다. 이 설계를
> 실제로 구현한 정확한 세션은 번호 매김 시작(0001, 2026-07-06) 이전이라 `doc/` 안에 대응하는
> 세션 로그가 없다 — 그래서 아래는 "설계 당시 제안했던 코드" 대비 "지금 실제 코드"를 직접
> 비교하는 방식으로만 정리했다.
>
> 실제 구현은 아래에서 보듯 설계안과 세부 사항이 다른 부분이 있다(가장 큰 차이:
> `ProductionQueueSlot`을 새로 만들지 않고 기존 `ProductionSlot` + `UpdateQueue(queue, onCancel)` 콜백
> 방식을 그대로 재사용했다).

## 1. `UIController.cs` — 생산 대기열 UI

**기존 코드 (설계 당시 제안)**
```csharp
[Header("Production Queue")]
[SerializeField] private ProductionQueueSlot[] queueSlots; // productionQueue_Panel/QueueSlot0~4

public struct ProductionQueueData
{
    public Sprite Icon { get; }
    public float Progress01 { get; }   // 0~1, 진행률
    public Action OnCancel { get; }

    public ProductionQueueData(Sprite icon, float progress01, Action onCancel)
    {
        Icon = icon;
        Progress01 = progress01;
        OnCancel = onCancel;
    }
}

public void UpdateProductionQueue(ProductionQueueData[] queue) { /* ... */ }
```
(신규 스크립트 `ProductionQueueSlot.cs`를 따로 만들어 아이콘 + 진행률 Fill + 취소 버튼을 직접 갖게 하자는 제안이었음)

**변경 코드 (실제 구현)**
```csharp
[Header("Production Queue Panel (SelectInfo)")]
[SerializeField] private GameObject productionPanel;
[SerializeField] private ProductionSlot[] queueSlots;
[SerializeField] private UnitDataSO database;
[SerializeField] private Slider progressSlider;

private IReadOnlyList<ProductionData> currentQueue;
private bool isShowingProductionQueue;

// 생산 대기열 슬롯 UI 갱신: 큐에 있는 만큼 아이콘/취소 콜백을 채우고 나머지는 빈 슬롯으로 표시한다.
public void UpdateQueue(IReadOnlyList<ProductionData> queue, Action<int> onCancel)
{
    if (queue == null)
    {
        for (int i = 0; i < queueSlots.Length; i++)
            SetEmptyQueueSlot(i);

        return;
    }

    for (int i = 0; i < queueSlots.Length; i++)
    {
        if (i >= queue.Count)
        {
            SetEmptyQueueSlot(i);
            continue;
        }

        int queueIndex = i;
        int unitIndex = database.unitData.FindIndex(d => d.ID == queue[queueIndex].UnitID);

        if (unitIndex == -1)
        {
            queueSlots[i].Clear();
            continue;
        }

        UnitData data = database.unitData[unitIndex];

        queueSlots[i].SetData(
            new CommandButtonData(data.Icon, () => onCancel(queueIndex))
        );
    }
}

public void ShowProductionUI(IReadOnlyList<ProductionData> queue, Action<int> onCancel)
{
    if (productionPanel != null)
        productionPanel.SetActive(true);

    currentQueue = queue;
    isShowingProductionQueue = true;

    UpdateQueue(queue, onCancel);
}

public void HideProductionUI()
{
    if (productionPanel != null)
        productionPanel.SetActive(false);

    foreach (var slot in queueSlots)
        slot.Clear();

    progressSlider.gameObject.SetActive(false);

    currentQueue = null;
    isShowingProductionQueue = false;
}

// 진행 중인 첫 항목의 진행률(Progress)을 매 프레임 progressSlider에 반영
private void UpdateProductionProgress()
{
    if (!isShowingProductionQueue || currentQueue == null || currentQueue.Count == 0)
    {
        progressSlider.gameObject.SetActive(false);
        return;
    }

    progressSlider.gameObject.SetActive(true);
    progressSlider.value = currentQueue[0].Progress;
}
```

**차이점**
- 새 `ProductionQueueSlot` 스크립트를 만들지 않고, 기존 커맨드 버튼과 같은 `ProductionSlot` + `CommandButtonData(icon, callback)`를 그대로 재사용했다 (아이콘/취소 클릭만 있으면 충분했기 때문).
- 진행률은 슬롯 5개 각각의 Fill이 아니라, 대기열 맨 앞(0번, 현재 생산 중인 항목) 하나에 대해서만 공용 `progressSlider` 하나로 표시한다 — 설계안의 "슬롯마다 progress01" 개념 대신 "맨 앞 항목만 진행 중" 단순화.
- `ProductionQueueData` 구조체 대신 실제 `ProductionData`(UnitSpawner가 이미 들고 있는 큐 항목)를 그대로 UI까지 전달한다.

## 2. `UnitSpawner.cs` — 큐 조회 API

**기존 코드 (설계 당시 제안)**
```csharp
public IReadOnlyList<ProductionData> GetQueueSnapshot() => productionQueue;
```
(`ProductionData`에 `TotalTime`이 없어서 진행률 계산이 불가능하다는 문제도 함께 지적했었음)

**변경 코드 (실제 구현)**
```csharp
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

// 대기열 반환 (읽기 전용 - UI 표시용)
public IReadOnlyList<ProductionData> GetProductionQueue()
{
    return productionQueue;
}

// 현재 생산 중인 항목의 진행률(0~1) 반환
public float GetProductionProgress()
{
    if (productionQueue.Count == 0)
        return 0f;

    return productionQueue[0].Progress;
}
```

**차이점**
- 메서드 이름이 `GetQueueSnapshot()`이 아니라 `GetProductionQueue()`로 확정됨.
- 지적했던 `TotalTime` 문제는 실제로 `ProductionData`에 `TotalTime` 필드 + `Progress` 프로퍼티를 추가하는 방식으로 해결됨(설계안 그대로 반영).

## 3. `BuildingController.cs` — 위임 메서드

**기존 코드 (설계 당시 제안)**
```csharp
public IReadOnlyList<ProductionData> GetProductionQueue() => UnitSpawner.GetQueueSnapshot();
public void CancelProduction(int index) => UnitSpawner.Cancel(index);
```

**변경 코드 (실제 구현)**
```csharp
// 현재 생산 대기열 목록을 반환 (UI 표시용, UnitSpawner에 위임)
public IReadOnlyList<ProductionData> GetProductionQueue()
{
    return UnitSpawner.GetProductionQueue();
}

// 현재 생산 중인 항목의 진행률(0~1) 반환 (UnitSpawner에 위임)
public float GetProductionProgress()
{
    return UnitSpawner.GetProductionProgress();
}

// 대기열의 특정 항목 생산을 취소한다 (UnitSpawner에 위임)
public void CancelProduction(int index)
{
    UnitSpawner.Cancel(index);
}
```

**차이점**
- 설계안이 우려했던 "`UnitSpawner == null`일 때 널 참조" 방어 코드는 실제로는 추가되지 않았다 (현재 모든 생산 건물 프리팹에 `UnitSpawner`가 항상 자식으로 붙어있다는 전제로 동작 — 향후 생산 불가 건물이 생기면 방어 코드 필요).

## 4. `RTSUnitController.cs` — 건물 선택 시 호출

**기존 코드 (설계 당시 제안)**
```csharp
private void UpdateSelectionInfoPanel()
{
    // ...
    if (selectedBuildingList.Count == 1)
    {
        BuildingController building = selectedBuildingList[0];
        uIController.ShowInfoPanel(building.GetIcon(), building.GetComponent<HealthManager>());
        return;
    }
    // ...
}
```

**변경 코드 (실제 구현, `UpdateUI()`의 `SelectState.BuildingSelect` 분기 중 생산 건물 케이스)**
```csharp
case BuildingState.MainBaseSelect:
    uIController.ShowMainBasePanel(
        UnitButtonAction(() => SpawnUnit(UnitID.Worker), UnitID.Worker));
    uIController.ShowProductionUI(
        GetProductionQueue(),
        CancelProduction);
    break;
```

**차이점**
- 별도의 `UpdateSelectionInfoPanel()` 헬퍼로 분리하지 않고, 기존 `UpdateUI()`의 건물 상태별 `switch`문 안에 `ShowProductionUI(...)` 호출을 나란히 추가하는 방식으로 갔다 — "생산 건물 선택 시 Info_panel 위에 대기열도 같이 뜬다"는 요구사항은 설계 문서가 예상한 대로 같은 프레임에 두 호출이 나란히 실행되는 것만으로 자연히 만족된다.

## 5. 남은 항목 (설계 문서 대비 아직 그대로인 부분)
- 대기열 취소 시 자원 환불은 여전히 없음 (`UnitSpawner.Cancel`이 큐에서 제거만 함) — README 로드맵 참고.
