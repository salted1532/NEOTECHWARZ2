# 0189 - 건물 테크 트리(선행 건물 조건) - 수정 제안

## 요청

"건물의 테크 트리를 만들고 싶어 병영 tier1을 지어야 공장 tier2를 지을수 있고 공장을 지어야 공항
tier3를 지을수 있는 그런 건물 설치 조건을 만들어줘" — 병영(Barracks) → 공장(Factory) → 공항
(Airport) 순서로, 앞 단계 건물이 완공되어 있어야 다음 단계 건물을 지을 수 있게 하는 선행 조건을
추가해달라는 요청.

## 조사 내용

- `Assets/Scripts/ScriptableObject/BuildingDataSO.cs`
  - 건물 스펙(`BuildingData`)에 비용/크기/생산시간은 있지만 "선행 건물" 개념은 없다.
- `Assets/Scripts/System/RTSUnitController.cs`
  - `BuildingID` 상수: `CommandCenter=1, SupplyDepot=2, Barracks=3, Factory=4, Airport=5, Lab=6`.
  - `TryConstructBuilding(int buildingID)`(1002번 줄)이 배치 확정 직전에 호출되어 현재는
    자원(광물/가스)만 확인하고 차감한다 - 실제로 건설을 막는 최종 관문이라, 여기서 선행 조건도 함께
    막아야 단축키나 버튼 우회 없이 확실히 차단된다.
  - `BuildingButtonAction(...)`(1030번 줄)이 건설 버튼의 제목/설명/비용 툴팁을 만든다.
  - `UpdateUI()`의 `case SelectState.BuildMode:`(1287번 줄)가 `uIController.ShowBuildPanel(...)`을
    호출해 6개 건물 버튼 + 취소 버튼을 구성한다. 지금은 전부 무조건 `interactable = true`로 보인다.
  - `BuildingController.Start()`(84번 줄)가 `rtsController.BuildingList.Add(this)`를 실행하는데, 이
    스크립트는 "완공된 건물" 프리팹에만 붙어 있다(건설 중 표시되는 `BaseStructure`는 별도 스크립트).
    즉 `BuildingList`에 어떤 `buildingID`가 있는지 확인하면 "그 건물이 최소 1개 완공되어 있는지"를
    정확히 알 수 있다 (짓는 중인 건물은 카운트되지 않음).
- `Assets/Scripts/UI/UIController.cs`
  - `CommandButtonData`(108번 줄)에 이미 `Interactable` 필드가 있고, `ProductionSlot.SetData()`가
    `button.interactable = data.Interactable && data.Callback != null`로 그대로 반영한다 - 버튼이
    비활성화되면 마우스 클릭도, 단축키 시뮬레이션 클릭(`ProductionSlot.Update`가 `!button.interactable`
    이면 단축키를 무시)도 자동으로 막힌다. 즉 "잠금 표시"를 위한 배관은 이미 있고 지금은 아무도 안 쓰고
    있을 뿐이다.
  - `ShowBuildPanel(...)`(910번 줄)은 현재 `ButtonAction` 6개 + 취소 1개만 받아서 전부
    `new CommandButtonData(icon, action)`(2-인자 생성자, `interactable` 기본값 `true`)로 만든다.
- `Assets/Scripts/Scripts/ScriptableObject/New Building Data SO.asset` (실제 경로:
  `Assets/Scripts/ScriptableObject/New Building Data SO.asset`)
  - YAML로 직접 수정 가능한 에셋. Barracks=ID 3, Factory=ID 4, Spaceport(코드상 이름은 Airport)=ID 5.

## 계획된 코드 변경

### 1. `Assets/Scripts/ScriptableObject/BuildingDataSO.cs`

`BuildingData`에 선행 건물 ID 필드 추가 (0 = 조건 없음).

Before:
```csharp
    [field: SerializeField]
    public int productionTime { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
}
```

After:
```csharp
    [field: SerializeField]
    public int productionTime { get; private set; }
    // 이 건물을 짓기 전에 미리 완공되어 있어야 하는 건물의 ID (RTSUnitController.BuildingID 상수, 0이면 조건 없음)
    [field: SerializeField]
    public int requiredBuildingID { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
}
```

### 2. `Assets/Scripts/ScriptableObject/New Building Data SO.asset`

Factory(ID 4)는 Barracks(ID 3)를, Spaceport/Airport(ID 5)는 Factory(ID 4)를 선행 건물로 지정한다.
(YAML에 새 필드를 직접 추가 - Unity가 다음에 이 에셋을 열면 인스펙터에도 그대로 값이 채워져 보인다.)

Before (Factory 항목):
```yaml
  - <Name>k__BackingField: 'Factory '
    ...
    <productionTime>k__BackingField: 37
    <Prefab>k__BackingField: {fileID: 7636683964052424660, guid: a1afb913cb6c8e54388e6960d529804f, type: 3}
```

After:
```yaml
  - <Name>k__BackingField: 'Factory '
    ...
    <productionTime>k__BackingField: 37
    <requiredBuildingID>k__BackingField: 3
    <Prefab>k__BackingField: {fileID: 7636683964052424660, guid: a1afb913cb6c8e54388e6960d529804f, type: 3}
```

Before (Spaceport 항목):
```yaml
  - <Name>k__BackingField: 'Spaceport '
    ...
    <productionTime>k__BackingField: 37
    <Prefab>k__BackingField: {fileID: 7636683964052424660, guid: 8cf6ce7512319444aa0998b140fc187d, type: 3}
```

After:
```yaml
  - <Name>k__BackingField: 'Spaceport '
    ...
    <productionTime>k__BackingField: 37
    <requiredBuildingID>k__BackingField: 4
    <Prefab>k__BackingField: {fileID: 7636683964052424660, guid: 8cf6ce7512319444aa0998b140fc187d, type: 3}
```

나머지 건물(CommandCenter/SupplyDepot/Barracks/Lab)은 `requiredBuildingID`를 아예 안 넣으면 Unity가
기본값 0(조건 없음)으로 채운다.

### 3. `Assets/Scripts/System/RTSUnitController.cs`

#### 3-1. 선행 조건 확인 헬퍼 추가 (`TryConstructBuilding` 근처, 생산 관련 region)

```csharp
    // buildingID에 해당하는 건물이 최소 1개 완공되어 있는지 (건설 중인 BaseStructure는 포함 안 됨)
    public bool HasCompletedBuilding(int buildingID) =>
        BuildingList.Exists(b => b != null && b.GetBuildingID() == buildingID);

    // buildingID 건설에 필요한 선행 건물 조건을 만족하는지 (선행 건물이 없으면 항상 true)
    public bool IsBuildingPrerequisiteMet(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null || data.requiredBuildingID == 0)
            return true;

        return HasCompletedBuilding(data.requiredBuildingID);
    }
```

#### 3-2. `TryConstructBuilding`: 실제 배치를 막는 최종 관문에 선행 조건 검사 추가

Before:
```csharp
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        return resourceManager.TrySpend(data.mineral, data.gas);
    }
```

After:
```csharp
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        if (!IsBuildingPrerequisiteMet(buildingID))
            return false;

        return resourceManager.TrySpend(data.mineral, data.gas);
    }
```

#### 3-3. `BuildingButtonAction`: 잠겨 있으면 툴팁에 선행 건물 안내 추가

Before:
```csharp
    private ButtonAction BuildingButtonAction(Action callback, int buildingID, KeyCode shortcut = KeyCode.None)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Construct {data.Name}."
            : data.description;

        return ButtonAction.WithCost(callback, data.Name, description, data.mineral, data.gas, data.population, shortcut);
    }
```

After:
```csharp
    private ButtonAction BuildingButtonAction(Action callback, int buildingID, KeyCode shortcut = KeyCode.None)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Construct {data.Name}."
            : data.description;

        if (data.requiredBuildingID != 0 && !HasCompletedBuilding(data.requiredBuildingID))
        {
            string requiredName = GetBuildingName(data.requiredBuildingID);
            description += $"\n<color=red>Requires {requiredName}</color>";
        }

        return ButtonAction.WithCost(callback, data.Name, description, data.mineral, data.gas, data.population, shortcut);
    }
```

#### 3-4. `UpdateUI()`의 `case SelectState.BuildMode:` - Factory/Airport 버튼에 잠금 상태 전달

Before:
```csharp
            case SelectState.BuildMode:
                uIController.ShowBuildPanel(
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.CommandCenter), BuildingID.CommandCenter, KeyCode.C),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.SupplyDepot), BuildingID.SupplyDepot, KeyCode.S),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Barracks), BuildingID.Barracks, KeyCode.B),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Factory), BuildingID.Factory, KeyCode.F),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport, KeyCode.P),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab, KeyCode.L),
                    ButtonAction.Simple(
                        CancelBuildMode,
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>T</color>]",
                        KeyCode.T));
```

After:
```csharp
            case SelectState.BuildMode:
                uIController.ShowBuildPanel(
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.CommandCenter), BuildingID.CommandCenter, KeyCode.C),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.SupplyDepot), BuildingID.SupplyDepot, KeyCode.S),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Barracks), BuildingID.Barracks, KeyCode.B),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Factory), BuildingID.Factory, KeyCode.F),
                    IsBuildingPrerequisiteMet(BuildingID.Factory),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport, KeyCode.P),
                    IsBuildingPrerequisiteMet(BuildingID.Airport),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab, KeyCode.L),
                    ButtonAction.Simple(
                        CancelBuildMode,
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>T</color>]",
                        KeyCode.T));
```

### 4. `Assets/Scripts/UI/UIController.cs`

`ShowBuildPanel`이 Factory/Airport 버튼의 잠금 여부(`bool`)를 추가로 받아 `CommandButtonData`의
`interactable`에 반영한다.

Before:
```csharp
    public void ShowBuildPanel(
    ButtonAction onCommandCenter,
    ButtonAction onSupplyDepot,
    ButtonAction onBarracks,
    ButtonAction onFactory,
    ButtonAction onAirport,
    ButtonAction onLab,
    ButtonAction onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(

            new CommandButtonData(commandCenterIcon, onCommandCenter),
            new CommandButtonData(supplyDepotIcon, onSupplyDepot),
            new CommandButtonData(barracksIcon, onBarracks),
            new CommandButtonData(factoryIcon, onFactory),
            new CommandButtonData(airportIcon, onAirport),
            new CommandButtonData(labIcon, onLab),
            new CommandButtonData(cancelIcon, onCancel)
        );
    }
```

After:
```csharp
    public void ShowBuildPanel(
    ButtonAction onCommandCenter,
    ButtonAction onSupplyDepot,
    ButtonAction onBarracks,
    ButtonAction onFactory,
    bool factoryUnlocked,
    ButtonAction onAirport,
    bool airportUnlocked,
    ButtonAction onLab,
    ButtonAction onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(

            new CommandButtonData(commandCenterIcon, onCommandCenter),
            new CommandButtonData(supplyDepotIcon, onSupplyDepot),
            new CommandButtonData(barracksIcon, onBarracks),
            new CommandButtonData(factoryIcon, onFactory, factoryUnlocked),
            new CommandButtonData(airportIcon, onAirport, airportUnlocked),
            new CommandButtonData(labIcon, onLab),
            new CommandButtonData(cancelIcon, onCancel)
        );
    }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/ScriptableObject/BuildingDataSO.cs` (`requiredBuildingID` 필드 추가),
  `Assets/Scripts/ScriptableObject/New Building Data SO.asset` (Factory→Barracks, Airport→Factory
  선행 조건 값 채움), `Assets/Scripts/System/RTSUnitController.cs` (`HasCompletedBuilding`/
  `IsBuildingPrerequisiteMet` 헬퍼 추가, `TryConstructBuilding`에 선행 조건 검사 추가,
  `BuildingButtonAction`에 잠김 안내 문구 추가, `BuildMode` 패널 호출에 잠금 플래그 전달),
  `Assets/Scripts/UI/UIController.cs` (`ShowBuildPanel`이 Factory/Airport 잠금 플래그를 받아 버튼
  `interactable`에 반영).
- 동작 변화:
  - 병영(Barracks)이 최소 1개 완공되어 있지 않으면 건설 패널의 공장(Factory) 버튼이 회색으로
    비활성화되고, 마우스 클릭/단축키(F) 모두 먹지 않는다. 툴팁에는 "Requires Barracks"가 빨간
    글씨로 덧붙는다.
  - 공장(Factory)이 최소 1개 완공되어 있지 않으면 공항(Airport) 버튼도 같은 방식으로 잠긴다
    ("Requires Factory").
  - 완공 여부는 `BuildingList`(완공된 건물에만 붙는 `BuildingController`가 자기 자신을 등록하는
    리스트) 기준이라, 건설 중인(아직 완성 안 된) 병영/공장은 조건을 만족시키지 못한다. 다 지은
    선행 건물이 나중에 파괴되면 `BuildingList`에서 빠지므로, 이미 지어둔 다음 단계 건물은 그대로
    남지만 아직 안 지은 건물은 다시 잠긴다(선행 건물을 다시 지어야 함).
  - 건설 버튼이 비활성화돼도 `TryConstructBuilding`이 최종 관문에서 한 번 더 막기 때문에, UI를
    우회하는 경로(예: 버그로 잠긴 버튼이 눌리는 경우)가 있어도 실제 배치까지 이어지지 않는다.
  - CommandCenter/SupplyDepot/Barracks/Lab은 선행 조건이 없어 지금과 동일하게 항상 지을 수 있다.

## 확인 필요

이대로 진행해도 될까요?
