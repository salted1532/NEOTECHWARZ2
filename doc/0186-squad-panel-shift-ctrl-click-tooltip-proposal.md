# 0186 - Squad_panel 버튼 호버 툴팁 + Shift/Ctrl+Click 선택 조작 - 수정 제안

## 요청

"squad_panel에서 각 버튼을 호버 했을 시 ToolTip에 Click: Select Unit / Shift+Click: Deselect Unit /
Ctrl+Click: Select Unit Type 이런식으로 설명란에 작성되고 shift + Click 시 그 squad에서 선택된
리스트에서 해당 버튼유닛은 제거해주고 Ctrl+Click 시 해당하는 유닛과 같은 유닛만 선택 리스트에
남기도록 해줘" —

1. Squad_panel의 각 슬롯을 호버하면 툴팁에 "Click: Select Unit / Shift+Click: Deselect Unit /
   Ctrl+Click: Select Unit Type" 안내문이 보일 것.
2. Shift+Click: 그 슬롯의 유닛만 현재 선택 목록에서 제거(나머지는 그대로 선택 유지).
3. Ctrl+Click: 현재 선택 목록에서 그 유닛과 같은 종류만 남기고 나머지는 선택 해제.

[[multi-building-squad-panel-and-priority-panel-proposal]](0185)에서 Squad_panel을 건물 다중 선택에도
공유하도록 확장해뒀으므로, 이번 조작(호버 툴팁/Shift/Ctrl+Click)도 유닛뿐 아니라 건물 Squad_panel에
동일하게 적용해서 두 패널의 동작을 일관되게 맞춘다(건물 쪽 문구는 "Unit" 대신 "Building"으로).

## 조사 내용

- `Assets/Scripts/UI/ProductionSlot.cs`
  - `OnClick()`이 Shift/Ctrl 등 모디파이어 키를 전혀 보지 않고 항상 `callback?.Invoke()`만 호출한다.
  - `OnPointerEnter()`는 `data.Title`이 비어있으면 툴팁 자체를 안 띄운다 - 지금 Squad 슬롯은
    `CommandButtonData(icon, callback)` 축약 생성자를 쓰는데, 이 생성자가 내부적으로
    `ButtonAction.Simple(callback, string.Empty, string.Empty)`를 거치므로 Title이 항상 빈 문자열이라
    Squad_panel 슬롯엔 지금 툴팁이 전혀 안 뜬다.
- `Assets/Scripts/UI/UIController.cs`
  - `ButtonAction`/`CommandButtonData`는 클릭 콜백 하나(`Callback`)만 들고 있다 - Shift/Ctrl 전용
    콜백을 넣을 자리가 없다. 일반 커맨드 버튼(이동/공격/생산 등)은 지금처럼 클릭 하나만 있으면 되므로,
    새 필드는 전부 기본값 `null`로 두면 기존 버튼 동작에 영향이 없다.
  - `RefreshSquadSlots()`(유닛)/`RefreshSquadBuildingSlots()`(건물, 0185에서 추가)가 각각
    `squadOnSelectUnit`/`squadOnSelectBuilding` 콜백 하나만으로 슬롯을 채운다.
  - `ShowSquadPanel`/`ShowBuildingSquadPanel`도 콜백을 하나씩만 받는다 - Shift/Ctrl용 콜백을 추가로
    받아야 한다.
  - 유닛 이름 조회용 `UnitDataSO database` 필드가 이미 있으므로(생산 대기열 표시에 사용 중) 유닛
    툴팁 제목엔 실제 유닛 이름을 넣을 수 있다. 건물 이름 조회용 DB 참조는 `UIController`에 없으므로,
    새로 추가하는 대신 건물 툴팁 제목은 고정 문자열 "Building"을 쓴다(범위를 넘는 추가 배선을 피함).
- `Assets/Scripts/System/RTSUnitController.cs`
  - `DeselectUnit(unit)`/`Deselectbuilding(building)`이 **다른 유닛/건물이 더 남아있어도 무조건**
    `RTScurrentSate = SelectState.None`으로 되돌린다. `UpdateUI()`엔 `None`에 대응하는 case가 없어서
    `default:` 분기로 빠지고, 거기서 `HideSquadPanel()`/`ClearPanel()`이 호출돼 패널 전체가 사라진다.
  - 즉 이 상태에서 Squad_panel의 Shift+Click으로 "그 유닛만 제거"를 구현하려고 기존
    `DeselectUnit`을 그대로 재사용하면, 나머지 유닛들이 여전히 `selectedUnitList`에 남아있는데도
    패널이 통째로 사라져버리는 버그가 생긴다 - 이번 기능이 제대로 동작하려면 "리스트가 실제로
    비었을 때만 None으로 되돌리도록" 먼저 고쳐야 한다(건물도 동일).
  - `UnitController.GetUnitID()`/`BuildingController.GetBuildingID()`가 이미 있어서 "같은 종류" 비교에
    바로 쓸 수 있다.

## 계획된 코드 변경

### 1. `Assets/Scripts/UI/UIController.cs` - `ButtonAction`/`CommandButtonData`에 Shift/Ctrl 콜백 추가

Before:
```csharp
    public readonly struct ButtonAction
    {
        public Action Callback { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; } // 이 버튼을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)

        private ButtonAction(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            bool hasCost,
            KeyCode shortcut)
        {
            Callback = callback;
            Title = title;
            Description = description;
            Ore = ore;
            Gas = gas;
            Population = population;
            HasCost = hasCost;
            Shortcut = shortcut;
        }

        // 이동/공격/정지 등 비용이 없는 일반 명령 버튼용
        public static ButtonAction Simple(Action callback, string title, string description, KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, title, description, 0, 0, 0, false, shortcut);
        }

        // 유닛 생산/건물 건설처럼 광물/가스/인구 비용이 있는 버튼용
        public static ButtonAction WithCost(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, title, description, ore, gas, population, true, shortcut);
        }
    }
```

After:
```csharp
    public readonly struct ButtonAction
    {
        public Action Callback { get; }
        // Squad_panel 전용: Shift+Click(선택 목록에서 제거)/Ctrl+Click(같은 종류만 남기기) 콜백.
        // 일반 커맨드 버튼(WithCost/Simple)은 항상 null - ProductionSlot이 null이면 기본 Callback으로 처리한다.
        public Action ShiftClickCallback { get; }
        public Action CtrlClickCallback { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; } // 이 버튼을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)

        private ButtonAction(
            Action callback,
            Action shiftClickCallback,
            Action ctrlClickCallback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            bool hasCost,
            KeyCode shortcut)
        {
            Callback = callback;
            ShiftClickCallback = shiftClickCallback;
            CtrlClickCallback = ctrlClickCallback;
            Title = title;
            Description = description;
            Ore = ore;
            Gas = gas;
            Population = population;
            HasCost = hasCost;
            Shortcut = shortcut;
        }

        // 이동/공격/정지 등 비용이 없는 일반 명령 버튼용
        public static ButtonAction Simple(Action callback, string title, string description, KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, null, null, title, description, 0, 0, 0, false, shortcut);
        }

        // 유닛 생산/건물 건설처럼 광물/가스/인구 비용이 있는 버튼용
        public static ButtonAction WithCost(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, null, null, title, description, ore, gas, population, true, shortcut);
        }

        // Squad_panel 슬롯 전용: 기본 클릭 외에 Shift+Click/Ctrl+Click 동작을 추가로 지정한다.
        public static ButtonAction WithModifierClicks(
            Action callback,
            Action shiftClickCallback,
            Action ctrlClickCallback,
            string title,
            string description)
        {
            return new ButtonAction(callback, shiftClickCallback, ctrlClickCallback, title, description, 0, 0, 0, false, KeyCode.None);
        }
    }
```

Before:
```csharp
    public readonly struct CommandButtonData
    {
        public Sprite Icon { get; }
        public Action Callback { get; }
        public bool Interactable { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; }

        public CommandButtonData(
            Sprite icon,
            ButtonAction action,
            bool interactable = true)
        {
            Icon = icon;
            Callback = action.Callback;
            Interactable = interactable;
            Title = action.Title;
            Description = action.Description;
            Ore = action.Ore;
            Gas = action.Gas;
            Population = action.Population;
            HasCost = action.HasCost;
            Shortcut = action.Shortcut;
        }

        // 취소 버튼/빈 대기열 슬롯 등 툴팁이 필요 없는 버튼용
        public CommandButtonData(
            Sprite icon,
            Action callback,
            bool interactable = true)
            : this(icon, ButtonAction.Simple(callback, string.Empty, string.Empty), interactable)
        {
        }
    }
```

After:
```csharp
    public readonly struct CommandButtonData
    {
        public Sprite Icon { get; }
        public Action Callback { get; }
        public Action ShiftClickCallback { get; }
        public Action CtrlClickCallback { get; }
        public bool Interactable { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; }

        public CommandButtonData(
            Sprite icon,
            ButtonAction action,
            bool interactable = true)
        {
            Icon = icon;
            Callback = action.Callback;
            ShiftClickCallback = action.ShiftClickCallback;
            CtrlClickCallback = action.CtrlClickCallback;
            Interactable = interactable;
            Title = action.Title;
            Description = action.Description;
            Ore = action.Ore;
            Gas = action.Gas;
            Population = action.Population;
            HasCost = action.HasCost;
            Shortcut = action.Shortcut;
        }

        // 취소 버튼/빈 대기열 슬롯 등 툴팁이 필요 없는 버튼용
        public CommandButtonData(
            Sprite icon,
            Action callback,
            bool interactable = true)
            : this(icon, ButtonAction.Simple(callback, string.Empty, string.Empty), interactable)
        {
        }
    }
```

### 2. `Assets/Scripts/UI/ProductionSlot.cs` - 클릭 시 Shift/Ctrl 확인

Before:
```csharp
    private void OnClick()
    {
        callback?.Invoke();
    }
```

After:
```csharp
    private void OnClick()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (ctrlHeld && data.CtrlClickCallback != null)
        {
            data.CtrlClickCallback.Invoke();
            return;
        }

        if (shiftHeld && data.ShiftClickCallback != null)
        {
            data.ShiftClickCallback.Invoke();
            return;
        }

        callback?.Invoke();
    }
```

(일반 커맨드 버튼/생산 대기열 슬롯은 `ShiftClickCallback`/`CtrlClickCallback`이 항상 null이므로 이
분기를 타지 않고 그대로 기본 `callback`이 실행된다 - 기존 동작 변화 없음.)

### 3. `Assets/Scripts/UI/UIController.cs` - Squad_panel에 Shift/Ctrl 콜백 배선 + 툴팁 텍스트

Before:
```csharp
    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private int squadCurrentPage;

    // 건물 다중 선택용 (유닛과 같은 squadPanel/squadSlots를 공유 - 유닛/건물 선택은 항상 배타적으로 표시됨)
    private readonly List<BuildingController> squadBuildingsSnapshot = new List<BuildingController>();
    private Action<BuildingController> squadOnSelectBuilding;
    private bool squadShowingBuildings;
```

After:
```csharp
    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private Action<UnitController> squadOnShiftClickUnit;
    private Action<UnitController> squadOnCtrlClickUnit;
    private int squadCurrentPage;

    // 건물 다중 선택용 (유닛과 같은 squadPanel/squadSlots를 공유 - 유닛/건물 선택은 항상 배타적으로 표시됨)
    private readonly List<BuildingController> squadBuildingsSnapshot = new List<BuildingController>();
    private Action<BuildingController> squadOnSelectBuilding;
    private Action<BuildingController> squadOnShiftClickBuilding;
    private Action<BuildingController> squadOnCtrlClickBuilding;
    private bool squadShowingBuildings;
```

Before:
```csharp
    public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit)
    {
        HideInfoPanel();
        HideProductionUI();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectUnit = onSelectUnit;
        squadShowingBuildings = false;
        ...
```

After (매개변수만 확장, 나머지 본문 동일):
```csharp
    public void ShowSquadPanel(
        IReadOnlyList<UnitController> units,
        Action<UnitController> onSelectUnit,
        Action<UnitController> onShiftClickUnit,
        Action<UnitController> onCtrlClickUnit)
    {
        HideInfoPanel();
        HideProductionUI();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectUnit = onSelectUnit;
        squadOnShiftClickUnit = onShiftClickUnit;
        squadOnCtrlClickUnit = onCtrlClickUnit;
        squadShowingBuildings = false;
        ...
```

Before:
```csharp
    public void ShowBuildingSquadPanel(IReadOnlyList<BuildingController> buildings, Action<BuildingController> onSelectBuilding)
    {
        HideInfoPanel();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectBuilding = onSelectBuilding;
        squadShowingBuildings = true;
        ...
```

After:
```csharp
    public void ShowBuildingSquadPanel(
        IReadOnlyList<BuildingController> buildings,
        Action<BuildingController> onSelectBuilding,
        Action<BuildingController> onShiftClickBuilding,
        Action<BuildingController> onCtrlClickBuilding)
    {
        HideInfoPanel();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectBuilding = onSelectBuilding;
        squadOnShiftClickBuilding = onShiftClickBuilding;
        squadOnCtrlClickBuilding = onCtrlClickBuilding;
        squadShowingBuildings = true;
        ...
```

Before:
```csharp
    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 채운다.
    private void RefreshSquadSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int unitIndex = startIndex + i;

            if (unitIndex < squadUnitsSnapshot.Count)
            {
                UnitController unit = squadUnitsSnapshot[unitIndex];
                squadSlots[i].SetData(new CommandButtonData(unit.GetIcon(), () => squadOnSelectUnit(unit)));
            }
            else
            {
                squadSlots[i].Clear();
            }
        }
    }
```

After:
```csharp
    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 채운다.
    private void RefreshSquadSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int unitIndex = startIndex + i;

            if (unitIndex < squadUnitsSnapshot.Count)
            {
                UnitController unit = squadUnitsSnapshot[unitIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    unit.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectUnit(unit),
                        () => squadOnShiftClickUnit(unit),
                        () => squadOnCtrlClickUnit(unit),
                        GetUnitDisplayName(unit),
                        "Click: Select Unit\nShift+Click: Deselect Unit\nCtrl+Click: Select Unit Type")));
            }
            else
            {
                squadSlots[i].Clear();
            }
        }
    }

    // Squad_panel 툴팁 제목용 유닛 이름 조회 (database에 없으면 "Unit"로 대체 - Title이 비어있으면
    // ProductionSlot이 툴팁 자체를 안 띄우므로 항상 비어있지 않은 값을 돌려줘야 한다).
    private string GetUnitDisplayName(UnitController unit)
    {
        if (database != null)
        {
            UnitData data = database.unitData.Find(d => d.ID == unit.GetUnitID());
            if (data != null && !string.IsNullOrEmpty(data.unitName))
                return data.unitName.Trim();
        }

        return "Unit";
    }
```

Before:
```csharp
    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 건물로 채운다 (RefreshSquadSlots의 건물 버전).
    private void RefreshSquadBuildingSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int buildingIndex = startIndex + i;

            if (buildingIndex < squadBuildingsSnapshot.Count)
            {
                BuildingController building = squadBuildingsSnapshot[buildingIndex];
                squadSlots[i].SetData(new CommandButtonData(building.GetIcon(), () => squadOnSelectBuilding(building)));
            }
            else
            {
                squadSlots[i].Clear();
            }
        }
    }
```

After:
```csharp
    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 건물로 채운다 (RefreshSquadSlots의 건물 버전).
    private void RefreshSquadBuildingSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int buildingIndex = startIndex + i;

            if (buildingIndex < squadBuildingsSnapshot.Count)
            {
                BuildingController building = squadBuildingsSnapshot[buildingIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    building.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectBuilding(building),
                        () => squadOnShiftClickBuilding(building),
                        () => squadOnCtrlClickBuilding(building),
                        "Building",
                        "Click: Select Building\nShift+Click: Deselect Building\nCtrl+Click: Select Building Type")));
            }
            else
            {
                squadSlots[i].Clear();
            }
        }
    }
```

### 4. `Assets/Scripts/System/RTSUnitController.cs` - Shift/Ctrl 대상 메서드 추가 + `None` 되돌림 버그 수정

`DeselectUnit`/`Deselectbuilding`이 목록이 실제로 비었을 때만 `RTScurrentSate`를 `None`으로 되돌리도록
먼저 고친다(안 고치면 Squad_panel Shift+Click으로 하나만 제거해도 패널 전체가 사라짐).

Before:
```csharp
    private void DeselectUnit(UnitController unit)
    {
        RTScurrentSate = SelectState.None;
        unit.DeselectUnit();
        selectedUnitList.Remove(unit);
    }
```

After:
```csharp
    private void DeselectUnit(UnitController unit)
    {
        unit.DeselectUnit();
        selectedUnitList.Remove(unit);

        if (selectedUnitList.Count == 0)
            RTScurrentSate = SelectState.None;
    }

    // Squad_panel Shift+Click: 그 유닛만 선택 목록에서 제거한다 (나머지는 그대로 선택 유지).
    public void RemoveUnitFromSelection(UnitController unit)
    {
        if (unit == null || !selectedUnitList.Contains(unit))
            return;

        DeselectUnit(unit);
    }

    // Squad_panel Ctrl+Click: 현재 선택 목록에서 그 유닛과 같은 종류(unitID)만 남기고 나머지는 선택 해제한다.
    public void KeepOnlySameUnitTypeInSelection(UnitController unit)
    {
        if (unit == null)
            return;

        int unitID = unit.GetUnitID();

        // 뒤에서부터 순회 - 앞에서부터 Remove하면 인덱스가 밀려서 일부를 건너뛸 수 있다.
        for (int i = selectedUnitList.Count - 1; i >= 0; i--)
        {
            UnitController other = selectedUnitList[i];
            if (other != null && other.GetUnitID() != unitID)
                DeselectUnit(other);
        }
    }
```

Before:
```csharp
    private void Deselectbuilding(BuildingController building)
    {
        RTScurrentSate = SelectState.None;
        building.DeselecBuilding();
        selectedBuildingList.Remove(building);
    }
```

After:
```csharp
    private void Deselectbuilding(BuildingController building)
    {
        building.DeselecBuilding();
        selectedBuildingList.Remove(building);

        if (selectedBuildingList.Count == 0)
            RTScurrentSate = SelectState.None;
    }

    // Squad_panel Shift+Click: 그 건물만 선택 목록에서 제거한다 (나머지는 그대로 선택 유지).
    public void RemoveBuildingFromSelection(BuildingController building)
    {
        if (building == null || !selectedBuildingList.Contains(building))
            return;

        Deselectbuilding(building);
    }

    // Squad_panel Ctrl+Click: 현재 선택 목록에서 그 건물과 같은 종류(buildingID)만 남기고 나머지는 선택 해제한다.
    public void KeepOnlySameBuildingTypeInSelection(BuildingController building)
    {
        if (building == null)
            return;

        int buildingID = building.GetBuildingID();

        for (int i = selectedBuildingList.Count - 1; i >= 0; i--)
        {
            BuildingController other = selectedBuildingList[i];
            if (other != null && other.GetBuildingID() != buildingID)
                Deselectbuilding(other);
        }
    }
```

`UpdateUI()`의 두 호출부도 새 콜백을 같이 넘기도록 바꾼다.

Before:
```csharp
                if (selectedUnitList.Count > 1)
                {
                    uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit);
                }
```

After:
```csharp
                if (selectedUnitList.Count > 1)
                {
                    uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit, RemoveUnitFromSelection, KeepOnlySameUnitTypeInSelection);
                }
```

Before:
```csharp
                if (selectedBuildingList.Count > 1)
                {
                    uIController.ShowBuildingSquadPanel(selectedBuildingList, ClickSelectBuilding);
                }
```

After:
```csharp
                if (selectedBuildingList.Count > 1)
                {
                    uIController.ShowBuildingSquadPanel(selectedBuildingList, ClickSelectBuilding, RemoveBuildingFromSelection, KeepOnlySameBuildingTypeInSelection);
                }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/UI/UIController.cs`(`ButtonAction`/`CommandButtonData`에 Shift/Ctrl 콜백
  필드 추가, Squad_panel 관련 메서드들이 이 콜백과 툴팁 문구를 배선), `Assets/Scripts/UI/ProductionSlot.cs`
  (`OnClick()`이 모디파이어 키를 확인), `Assets/Scripts/System/RTSUnitController.cs`
  (`DeselectUnit`/`Deselectbuilding` None 복귀 버그 수정 + Shift/Ctrl 대상 메서드 4개 추가 + 두
  `ShowSquadPanel`/`ShowBuildingSquadPanel` 호출부 갱신).
- 동작 변화:
  - Squad_panel 슬롯에 마우스를 올리면 유닛은 이름 + "Click/Shift+Click/Ctrl+Click" 안내, 건물은
    "Building" + 같은 안내가 툴팁으로 뜬다(지금은 툴팁이 아예 안 뜨던 상태).
  - Shift+Click: 그 유닛/건물만 선택에서 제거, 나머지는 그대로 유지(제거 후 1개만 남으면 자동으로
    Info_panel로, 0개면 패널이 닫힘 - 기존 프레임 갱신 로직 그대로 재사용).
  - Ctrl+Click: 그 유닛/건물과 같은 종류만 남기고 나머지는 선택 해제.
  - 일반 커맨드 버튼(이동/공격/생산 등)과 생산 대기열 슬롯은 Shift/Ctrl 콜백이 항상 null이라 동작
    변화 없음.
  - 부수 효과: 월드에서 Shift+클릭으로 유닛/건물 하나를 선택 해제할 때, 다른 유닛/건물이 더 선택돼
    있어도 패널 전체가 사라지던 기존 버그가 함께 고쳐진다(이번 기능이 정상 동작하려면 필수적인
    선행 수정).

## 확인 필요

이대로 진행해도 될까요?
