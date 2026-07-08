# 0044. 생산 대기열 취소 환불 + 건설 취소/파괴 환불 ([[0043-resource-manager-wiring|0043]]과 함께 적용)

**날짜:** 2026-07-09

## 요청 내용
> 그럼 다음으로 진행할 내용인데 한번에 같이하자
> 대기열 환불(버튼클릭으로 대기열 유닛 취소시 해당하는 유닛의 가격만큼 환불 + 생산건물이 파괴되었을시 List clear하면서 해당하는 유닛들의 가격만큼 환불)
> 건설 취소 or 파괴(건물은 구조체(건물이 지어지는과정) -> 완성건물로 진행이 될건데 해당 구조체일때 환불버튼이 있고 그걸 누르거나 단축키를 누르면 취소되면서 건물가격만큼 환불) 이것까지 생각해서 만들어줘

정리하면 3가지:
1. 생산 대기열 슬롯을 클릭해서 취소하면, 그 유닛 가격만큼 환불.
2. 생산 건물이 파괴되면, 대기열에 남아있던 모든 유닛의 가격만큼 환불.
3. `BaseStructure`(건설 중) 선택 시 "취소(환불)" 버튼 + 단축키가 있어서, 누르면 건설이 취소되고 건물 가격 전액 환불. (요청 문구의 "취소 or 파괴"를 그대로 받아들여, 나중에 `BaseStructure`가 전투로 파괴되는 경로가 생기더라도 동일하게 환불되도록 `IDestructible`도 구현.)

이번 요청은 [[0043-resource-manager-wiring]](아직 미적용)에서 만들 `RTSUnitController.RefundBuilding`류 자원 지급 인프라를 그대로 이어서 쓰므로, **0043과 0044를 한 번에 같이 적용**함.

## 설계안

### 1. 대기열 취소 시 환불

**`UnitSpawner.cs`** — `Cancel()`이 취소된 유닛ID를 반환하고, 파괴 시 대기열 전체를 반환하는 `ClearQueue()` 추가:
```csharp
// 기존 코드
    public void Cancel(int index)
    {
        if (index < 0 || index >= productionQueue.Count)
            return;

        productionQueue.RemoveAt(index);
        PrintQueue();
    }
```
```csharp
// 변경 코드
    // 대기열의 특정 인덱스 항목을 취소(제거)하고, 환불에 쓸 수 있도록 그 유닛ID를 반환한다 (유효하지 않으면 -1).
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
```

**`BuildingController.cs`** — 취소된 유닛ID/대기열을 그대로 상위로 전달 + 파괴 시 환불 호출:
```csharp
// 기존 코드
    // 대기열의 특정 항목 생산을 취소한다 (UnitSpawner에 위임)
    public void CancelProduction(int index)
    {
        UnitSpawner.Cancel(index);
    }
```
```csharp
// 변경 코드
    // 대기열의 특정 항목 생산을 취소한다 (UnitSpawner에 위임) - 환불을 위해 취소된 유닛ID를 반환한다.
    public int CancelProduction(int index)
    {
        return UnitSpawner.Cancel(index);
    }

    // 파괴 시 대기열에 남아있던 항목들을 반환(제거)한다 - UnitSpawner가 없는 건물(생산 불가 건물)은 null.
    public IReadOnlyList<ProductionData> ClearProductionQueue()
    {
        return UnitSpawner != null ? UnitSpawner.ClearQueue() : null;
    }
```

`Die()`에서 파괴 시 대기열 환불 + (0043의) 인구수 반환을 함께 호출:
```csharp
// 기존 코드 (0043 적용 이후 기준)
    public void Die()
    {
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this);
        rtsController?.RemoveMaxPopulationForBuilding(buildingID);

        Destroy(gameObject);
    }
```
```csharp
// 변경 코드
    public void Die()
    {
        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this);
        rtsController?.RemoveMaxPopulationForBuilding(buildingID);

        Destroy(gameObject);
    }
```

**`RTSUnitController.cs`** — 취소 시 환불 로직 + 공용 `RefundUnit` 헬퍼:
```csharp
// 기존 코드
    //대기열 취소
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        selectedBuildingList[0].CancelProduction(index);
    }
```
```csharp
// 변경 코드
    //대기열 취소 (취소된 유닛 가격만큼 환불)
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        int canceledUnitID = selectedBuildingList[0].CancelProduction(index);
        RefundUnit(canceledUnitID);
    }

    // 생산 건물이 파괴됐을 때 대기열에 남아있던 유닛들을 전부 환불한다.
    public void RefundProductionQueue(IReadOnlyList<ProductionData> queue)
    {
        if (queue == null)
            return;

        foreach (ProductionData item in queue)
            RefundUnit(item.UnitID);
    }

    // 유닛 하나의 가격(광물/가스/인구수)만큼 환불한다. 생산 시 TryProduceUnit이 이미 소모해둔 것을 그대로 되돌리는 것.
    private void RefundUnit(int unitID)
    {
        if (unitID < 0)
            return;

        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return;

        resourceManager.AddOre(data.mineral);
        resourceManager.AddGas(data.gas);
        resourceManager.ReleasePopulation(data.population);
    }
```

### 2. `BaseStructure` 취소/파괴 시 환불

**`PlacementSystem.cs`** — 그리드 예약 해제 콜백을 `BaseStructure`에 넘겨줌(플레이어가 나중에 직접 취소할 때 쓰기 위함). `gridPos`를 `StartConstruction`까지 전달:
```csharp
// 기존 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));
```
```csharp
// 변경 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));
```
```csharp
// 기존 코드
    private void StartConstruction(BuildingData data, Vector3 groundPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        Vector3 structureSpawnPos = groundPos + Vector3.up * GetGroundOffsetY(baseStructurePrefab);

        GameObject obj = Instantiate(baseStructurePrefab, structureSpawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        structure.Initialize(data.ID, data.productionTime, groundPos);

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }
```
```csharp
// 변경 코드
    private void StartConstruction(BuildingData data, Vector3 groundPos, Vector3Int gridPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        Vector3 structureSpawnPos = groundPos + Vector3.up * GetGroundOffsetY(baseStructurePrefab);

        GameObject obj = Instantiate(baseStructurePrefab, structureSpawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        // 플레이어가 직접 건설을 취소할 때(CancelConstruction) 그리드 예약을 풀어줄 콜백도 함께 넘긴다.
        structure.Initialize(data.ID, data.productionTime, groundPos, () => CancelReservedConstruction(gridPos, null));

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }
```

**`BaseStructure.cs`** — 취소 콜백 저장 + `CancelConstruction()`/`IDestructible.Die()` 추가:
```csharp
// 기존 코드
public class BaseStructure : MonoBehaviour
{
    ...
    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    // 완공될 건물의 최대체력/아이콘을 프리팹에서 미리 읽어와 HealthManager와 Info_panel 표시에 반영한다.
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        ...
    }
```
```csharp
// 변경 코드
public class BaseStructure : MonoBehaviour, IDestructible
{
    ...
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)

    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    // 완공될 건물의 최대체력/아이콘을 프리팹에서 미리 읽어와 HealthManager와 Info_panel 표시에 반영한다.
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition, System.Action onCancelledByPlayer)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        this.onCancelledByPlayer = onCancelledByPlayer;
        ...
    }
```

**취소/파괴 처리 추가** (`CompleteConstruction()` 아래):
```csharp
    // 플레이어가 Info_panel의 취소 버튼/단축키로 건설을 직접 취소했을 때 호출된다.
    // 건물 가격 전액을 환불하고, 담당 일꾼을 해제하고, 그리드 예약을 풀어준 뒤 스스로 파괴된다.
    public void CancelConstruction()
    {
        rtsController?.RefundBuilding(buildingID);

        if (builder != null)
            builder.FinishConstruction();

        onCancelledByPlayer?.Invoke();

        rtsController?.ClearSelectedStructureIfMatches(this);

        Destroy(gameObject);
    }

    // HealthManager가 체력이 0 이하가 됐을 때 호출(IDestructible) - 취소와 동일하게 환불/정리한다.
    // (현재는 BaseStructure를 실제로 공격하는 경로가 없어 이론상의 대비이지만, "취소 or 파괴"를 모두 커버하기 위해 구현.)
    public void Die()
    {
        CancelConstruction();
    }
```

### 3. `RTSUnitController.cs` — 건물 가격 환불 + 취소 버튼 진입점

```csharp
    // BaseStructure 건설을 취소했을 때 건물 가격(광물/가스) 전액을 환불한다. (건설 중엔 인구수를 소모하지 않으므로 인구수 환불은 없음)
    public void RefundBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return;

        resourceManager.AddOre(data.mineral);
        resourceManager.AddGas(data.gas);
    }

    // Info_panel의 "취소" 버튼/단축키(T)에서 호출.
    public void CancelSelectedBaseStructure()
    {
        selectedBaseStructure?.CancelConstruction();
    }
```

**`UpdateUI()`의 `BaseStructureSelect` 케이스에 커맨드 패널(취소 버튼) 추가**:
```csharp
// 기존 코드
            case SelectState.BaseStructureSelect:

                if (selectedBaseStructure != null)
                {
                    uIController.ShowBaseStructureInfoPanel(
                        selectedBaseStructure.GetIcon(),
                        GetBuildingName(selectedBaseStructure.GetBuildingID()),
                        selectedBaseStructure.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;
```
```csharp
// 변경 코드
            case SelectState.BaseStructureSelect:

                if (selectedBaseStructure != null)
                {
                    uIController.ShowBaseStructureInfoPanel(
                        selectedBaseStructure.GetIcon(),
                        GetBuildingName(selectedBaseStructure.GetBuildingID()),
                        selectedBaseStructure.GetComponent<HealthManager>());

                    uIController.ShowBaseStructureCommandPanel(
                        ButtonAction.Simple(
                            CancelSelectedBaseStructure,
                            "Cancel",
                            "Cancel construction and refund resources. \nshortcut key [<color=yellow>T</color>]",
                            KeyCode.T));
                }
                else
                {
                    uIController.HideInfoPanel();
                    uIController.ClearPanel();
                }

                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;
```

### 4. `UIController.cs` — BaseStructure 전용 커맨드 패널(취소 버튼 1개) 표시 메서드

```csharp
    public enum UISelectionState
    {
        None,
        Worker,
        CombatUnit,
        BuildMode,
        Tier1Building,
        Tier2Building,
        Tier3Building,
        MainBase,
        BaseStructureSelect // 추가
    }
```
```csharp
    // BaseStructure(건설 중) 선택 시 커맨드 패널: 취소(환불) 버튼 하나만 표시. 기존 취소 아이콘(cancelIcon)을 재사용.
    public void ShowBaseStructureCommandPanel(ButtonAction onCancelConstruction)
    {
        CurrentState = UISelectionState.BaseStructureSelect;

        SetCommands(new CommandButtonData(cancelIcon, onCancelConstruction));
    }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **건설 취소 단축키**: 아직 명확히 지정 안 하셔서 일단 **T**로 제안(BuildMode의 "취소해서 나가기"와 같은 키, 서로 다른 상태라 겹쳐도 문제없음) — 다른 키를 원하시면 바로 바꿀게요.
- **환불액**: 유닛/건물 모두 항상 정가 전액 환불(진행률에 따른 부분 환불 없음) — 요청 문구("가격만큼")를 그대로 반영.
- **건설 취소 시 인구수**: 건설 중엔애초에 인구수를 소모하지 않으므로([[0043-resource-manager-wiring|0043]] 설계) 인구수 환불 대상이 아님 - 광물/가스만 환불.
- **BaseStructure가 전투로 파괴되는 경우**: 현재는 실제로 그런 경로가 없지만(자동 타게팅 대상 아님), 요청의 "취소 or 파괴"를 문자 그대로 커버하기 위해 `IDestructible.Die()`를 구현해서 취소와 동일하게 환불/정리되도록 함.
- **생산 대기열 다중 건물 선택**: 기존처럼 `selectedBuildingList[0]` 기준으로만 취소/환불(이번 요청 범위 밖이라 새로 설계 안 함).

## 변경 예정 파일 (0043 포함)
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs`
- `Assets/Scripts/UI/UIController.cs`

## 상태
**적용 완료** — [[0043-resource-manager-wiring|0043]]과 함께 실제 코드에 반영함 (건설 취소 단축키는 제안한 T로 그대로 적용).
