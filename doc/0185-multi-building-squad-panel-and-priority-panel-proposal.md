# 0185 - 건물 다중 선택 Squad_panel + 유닛 우선순위 + 건물 티어 우선순위 - 수정 제안

## 요청

"건물도 여러 건물을 선택 시 squad_panel에서 보이도록 해줘 건물 + 유닛이면 유닛의 명령 패널이
보이고 건물 + 건물이면 mainbase -> tier1 -> tier2 ->tier3의 우선순위로 보이도록해줘 그니깐 메인기지랑
병영이 선택되면 메인기지 생산유닛이 보이도록" — 세 가지 요청:

1. 건물을 여러 개(Shift+클릭) 선택하면, 지금처럼 무조건 `selectedBuildingList[0]`의 Info_panel만
   보여주지 말고, 유닛 다중 선택과 동일하게 Squad_panel(아이콘 그리드)로 보여줄 것.
2. 건물 + 유닛이 함께 선택된 상태면 유닛의 명령 패널(이동/공격/정지 등)이 우선해서 보일 것.
3. 서로 다른 티어의 건물이 함께 선택된 경우, MainBase > Tier1 > Tier2 > Tier3 우선순위로 그 건물의
   생산 패널을 보여줄 것 (예: MainBase + 병영(Tier1) 선택 시 MainBase의 일꾼 생산 버튼이 보여야 함).

## 조사 내용

- `Assets/Scripts/System/RTSUnitController.cs`
  - 건물 선택은 `SelectBuilding()`/`ShiftClickSelectBuilding()`을 거치는데, `BuildingSelectState`는
    **가장 최근에 클릭/Shift클릭한 건물의 태그**로 매번 덮어써진다 - 우선순위 개념이 없다.
  - `UpdateUI()`의 `case SelectState.BuildingSelect:`는 항상 `selectedBuildingList[0]`만 보고
    Info_panel/생산 패널/리프트 버튼을 구성한다 - "건물은 항상 단일 선택 취급"이라는 주석이 그대로
    남아있다.
  - `GetProductionQueue()`/`GetProductionProgress()`/`CancelProduction(index)`도 전부
    `selectedBuildingList[0]` 고정 - 만약 대표 건물 개념을 도입하면 이 셋도 같은 대표 건물을 봐야
    버튼(생산)과 큐 표시(UI)가 서로 어긋나지 않는다.
  - `LiftSelectedBuilding()`/`BeginLandingSelectedBuilding()`/`IsSelectedBuildingLifted()`/
    `MoveSelectedLiftedBuilding()`도 전부 `selectedBuildingList[0]` 고정 - 마찬가지로 대표 건물 도입 시
    같이 바꿔야 "패널에 보이는 리프트 버튼"과 "실제 리프트되는 건물"이 어긋나지 않는다.
  - Shift+클릭은 유닛(`ShiftClickSelectUnit`)과 건물(`ShiftClickSelectBuilding`)이 서로 다른 리스트를
    독립적으로 관리하고, 어느 쪽도 상대방 리스트를 비우지 않는다. 즉 유닛을 Shift+클릭한 뒤 건물을
    Shift+클릭하면 `selectedUnitList`와 `selectedBuildingList`가 동시에 채워질 수 있다 - 이 상태에서
    `RTScurrentSate`는 마지막에 클릭한 쪽 기준으로 `UnitSelect` 또는 `BuildingSelect`가 된다(어느 쪽을
    먼저/나중에 클릭했는지에 따라 달라짐 - 요청 2가 말하는 "유닛 우선"이 아직 보장 안 됨).
- `Assets/Scripts/UI/UIController.cs`
  - `ShowSquadPanel(IReadOnlyList<UnitController>, Action<UnitController>)`이 이미 유닛 다중 선택용
    아이콘 그리드(페이지네이션 포함, 최대 60개/5페이지)로 구현돼 있다 - `squadPanel`/`squadSlots`/
    `squadPageButtons`를 그대로 재사용할 수 있다. 유닛 선택과 건물 선택은 항상 배타적으로 표시되므로
    (요청 2에 의해 유닛이 있으면 건물 패널 자체가 안 보임) 같은 UI 위젯을 건물용으로도 재사용해도
    화면상 충돌이 없다.
  - `SelectSquadPage(int page)`(페이지 버튼 클릭)이 `RefreshSquadSlots()`(유닛 전용)를 무조건
    호출하므로, 건물 모드일 때도 페이지 버튼을 쓰려면 현재 어느 모드인지 구분해서 알맞은 새로고침
    메서드를 호출하도록 분기가 필요하다.

## 계획된 코드 변경

### 1. `Assets/Scripts/UI/UIController.cs`

건물용 Squad_panel 데이터/메서드를 유닛용과 나란히 추가한다.

Before:
```csharp
    private const int SquadUnitsPerPage = 12;

    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private int squadCurrentPage;
```

After:
```csharp
    private const int SquadUnitsPerPage = 12;

    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private int squadCurrentPage;

    // 건물 다중 선택용 (유닛과 같은 squadPanel/squadSlots를 공유 - 유닛/건물 선택은 항상 배타적으로 표시됨)
    private readonly List<BuildingController> squadBuildingsSnapshot = new List<BuildingController>();
    private Action<BuildingController> squadOnSelectBuilding;
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

        if (!SquadUnitsEqual(squadUnitsSnapshot, units))
        {
            squadUnitsSnapshot.Clear();
            squadUnitsSnapshot.AddRange(units);
            squadCurrentPage = 0;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadUnitsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);
        RefreshSquadSlots();
    }
```

After (`squadShowingBuildings` 플래그 추가 + 건물용 `ShowBuildingSquadPanel` 신규):
```csharp
    public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit)
    {
        HideInfoPanel();
        HideProductionUI();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectUnit = onSelectUnit;
        squadShowingBuildings = false;

        if (!SquadUnitsEqual(squadUnitsSnapshot, units))
        {
            squadUnitsSnapshot.Clear();
            squadUnitsSnapshot.AddRange(units);
            squadCurrentPage = 0;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadUnitsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);
        RefreshSquadSlots();
    }

    // 건물 다중 선택(Shift+클릭) 시 유닛과 같은 방식으로 Squad_panel에 건물 아이콘 그리드를 보여준다.
    // 생산 대기열/커맨드 패널(HideProductionUI 등)은 건드리지 않는다 - RTSUnitController.UpdateUI()가
    // "대표 건물" 기준으로 바로 이어서 갱신한다.
    public void ShowBuildingSquadPanel(IReadOnlyList<BuildingController> buildings, Action<BuildingController> onSelectBuilding)
    {
        HideInfoPanel();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectBuilding = onSelectBuilding;
        squadShowingBuildings = true;

        if (!SquadBuildingsEqual(squadBuildingsSnapshot, buildings))
        {
            squadBuildingsSnapshot.Clear();
            squadBuildingsSnapshot.AddRange(buildings);
            squadCurrentPage = 0;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadBuildingsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);
        RefreshSquadBuildingSlots();
    }
```

Before:
```csharp
    // 페이지 버튼(page1~5) 클릭 시 호출: 해당 페이지의 12마리로 슬롯을 다시 채운다.
    public void SelectSquadPage(int page)
    {
        if (squadPageButtons == null || page < 0 || page >= squadPageButtons.Length)
            return;

        if (!squadPageButtons[page].gameObject.activeSelf)
            return;

        squadCurrentPage = page;
        RefreshSquadSlots();
    }
```

After:
```csharp
    // 페이지 버튼(page1~5) 클릭 시 호출: 해당 페이지의 12개(유닛 또는 건물)로 슬롯을 다시 채운다.
    public void SelectSquadPage(int page)
    {
        if (squadPageButtons == null || page < 0 || page >= squadPageButtons.Length)
            return;

        if (!squadPageButtons[page].gameObject.activeSelf)
            return;

        squadCurrentPage = page;

        if (squadShowingBuildings)
            RefreshSquadBuildingSlots();
        else
            RefreshSquadSlots();
    }
```

Before:
```csharp
    private static bool SquadUnitsEqual(List<UnitController> current, IReadOnlyList<UnitController> incoming)
    {
        if (current.Count != incoming.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != incoming[i])
                return false;
        }

        return true;
    }

    public void HideSquadPanel()
    {
        if (squadPanel != null)
            squadPanel.SetActive(false);

        for (int i = 0; i < squadSlots.Length; i++)
            squadSlots[i]?.Clear();

        squadUnitsSnapshot.Clear();
        squadCurrentPage = 0;
    }
```

After (`RefreshSquadBuildingSlots`/`SquadBuildingsEqual` 추가, `HideSquadPanel`에 건물 스냅샷/플래그
정리 추가):
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

    private static bool SquadUnitsEqual(List<UnitController> current, IReadOnlyList<UnitController> incoming)
    {
        if (current.Count != incoming.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != incoming[i])
                return false;
        }

        return true;
    }

    private static bool SquadBuildingsEqual(List<BuildingController> current, IReadOnlyList<BuildingController> incoming)
    {
        if (current.Count != incoming.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != incoming[i])
                return false;
        }

        return true;
    }

    public void HideSquadPanel()
    {
        if (squadPanel != null)
            squadPanel.SetActive(false);

        for (int i = 0; i < squadSlots.Length; i++)
            squadSlots[i]?.Clear();

        squadUnitsSnapshot.Clear();
        squadBuildingsSnapshot.Clear();
        squadCurrentPage = 0;
        squadShowingBuildings = false;
    }
```

### 2. `Assets/Scripts/System/RTSUnitController.cs`

#### 2-1. "대표 건물" 우선순위 헬퍼 추가 (여러 건물이 섞여 선택됐을 때 패널/생산/리프트가 전부 이 건물
하나를 기준으로 통일되도록). `GetProducerTagForUnit` 근처(생산 관련 region)에 추가:

```csharp
    // 여러 건물이 섞여 선택됐을 때 패널/생산 대기열/리프트 버튼에 쓸 "대표 건물"을 우선순위
    // (MainBase > Tier1 > Tier2 > Tier3 > SupplyDepot > Lab)로 고른다. 우선순위 태그의 건물이 하나도
    // 없으면(이론상 발생 안 함) 선택된 첫 번째 건물을 그대로 쓴다.
    private static readonly string[] BuildingPriorityTags =
        { "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab" };

    private BuildingController GetRepresentativeBuilding()
    {
        if (selectedBuildingList.Count == 0)
            return null;

        foreach (string tag in BuildingPriorityTags)
        {
            BuildingController match = selectedBuildingList.Find(b => b != null && b.CompareTag(tag));
            if (match != null)
                return match;
        }

        return selectedBuildingList[0];
    }

    // 건물 태그 → 커맨드 패널 상태 매핑 (SelectBuilding()의 태그 스위치와 동일한 짝짓기).
    private static BuildingState TagToBuildingState(string tag)
    {
        switch (tag)
        {
            case "MainBase": return BuildingState.MainBaseSelect;
            case "Tier1": return BuildingState.Tier1Select;
            case "Tier2": return BuildingState.Tier2Select;
            case "Tier3": return BuildingState.Tier3Select;
            case "SupplyDepot": return BuildingState.SupplyDepot;
            case "Lab": return BuildingState.Lab;
            default: return BuildingState.None;
        }
    }
```

#### 2-2. 리프트/이동 메서드가 대표 건물을 쓰도록 변경 (패널에 보이는 리프트 버튼과 실제로 리프트되는
건물이 어긋나지 않도록)

Before:
```csharp
    // "리프트" 버튼: 선택된 건물(단일 취급)을 공중으로 띄운다.
    public void LiftSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].LiftOff();
    }

    // "착륙" 버튼: 선택된 건물(단일 취급)의 착륙 위치 선택 모드로 진입한다.
    public void BeginLandingSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].BeginLanding();
    }

    // 선택된 건물(단일 취급)이 현재 공중에 떠 있는지 (UserControl 우클릭 분기용)
    public bool IsSelectedBuildingLifted()
    {
        return selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();
    }

    // 공중에 뜬 건물을 공중유닛처럼 우클릭/Move버튼 지점으로 수평 이동시킨다 (착륙하지 않고 계속 공중에 떠 있음).
    public void MoveSelectedLiftedBuilding(Vector3 destination)
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].MoveWhileLifted(destination);
    }
```

After:
```csharp
    // "리프트" 버튼: 선택된 건물들 중 대표 건물을 공중으로 띄운다.
    public void LiftSelectedBuilding()
    {
        GetRepresentativeBuilding()?.LiftOff();
    }

    // "착륙" 버튼: 선택된 건물들 중 대표 건물의 착륙 위치 선택 모드로 진입한다.
    public void BeginLandingSelectedBuilding()
    {
        GetRepresentativeBuilding()?.BeginLanding();
    }

    // 선택된 건물들 중 대표 건물이 현재 공중에 떠 있는지 (UserControl 우클릭 분기용)
    public bool IsSelectedBuildingLifted()
    {
        BuildingController building = GetRepresentativeBuilding();
        return building != null && building.IsLifted();
    }

    // 공중에 뜬 대표 건물을 공중유닛처럼 우클릭/Move버튼 지점으로 수평 이동시킨다 (착륙하지 않고 계속 공중에 떠 있음).
    public void MoveSelectedLiftedBuilding(Vector3 destination)
    {
        GetRepresentativeBuilding()?.MoveWhileLifted(destination);
    }
```

#### 2-3. 대기열 조회/취소도 대표 건물 기준으로 변경 (생산 버튼이 대표 건물 기준으로 보이므로, 큐
표시/취소도 같은 건물을 봐야 서로 어긋나지 않음)

Before:
```csharp
    //건물의 대기열 정보 반환용
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        if (selectedBuildingList.Count == 0)
            return null;

        return selectedBuildingList[0].GetProductionQueue();
    }

    //생산 진행 시간 반환
    public float GetProductionProgress()
    {
        if (selectedBuildingList.Count == 0)
            return 0f;

        return selectedBuildingList[0].GetProductionProgress();
    }

    //대기열 취소 (취소된 유닛 가격만큼 환불)
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        int canceledUnitID = selectedBuildingList[0].CancelProduction(index);
        RefundUnit(canceledUnitID);
    }
```

After:
```csharp
    //건물의 대기열 정보 반환용 (대표 건물 기준)
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        return GetRepresentativeBuilding()?.GetProductionQueue();
    }

    //생산 진행 시간 반환 (대표 건물 기준)
    public float GetProductionProgress()
    {
        BuildingController building = GetRepresentativeBuilding();
        return building != null ? building.GetProductionProgress() : 0f;
    }

    //대기열 취소 (취소된 유닛 가격만큼 환불, 대표 건물 기준)
    public void CancelProduction(int index)
    {
        BuildingController building = GetRepresentativeBuilding();
        if (building == null)
            return;

        int canceledUnitID = building.CancelProduction(index);
        RefundUnit(canceledUnitID);
    }
```

(`CancelLastQueuedProduction()`은 내부에서 `GetProductionQueue()`/`CancelProduction()`을 그대로
호출하므로 변경 없이 자동으로 대표 건물 기준이 됨.)

#### 2-4. `UpdateUI()`: 유닛+건물 혼합 시 유닛 우선 + 건물 다중 선택 Squad_panel + 대표 건물 기준 패널

Before:
```csharp
    private void UpdateUI()
    {
        //UIController에서 현재 상황에 맞게 UI창 상태 변경
        switch (RTScurrentSate)
        {
```

After:
```csharp
    private void UpdateUI()
    {
        // 건물과 유닛이 함께 선택된 경우 유닛 명령 패널을 우선한다 (Shift+클릭으로 유닛/건물을 섞어
        // 선택해도 selectedUnitList가 하나라도 있으면 항상 유닛 패널이 보임).
        SelectState uiState = RTScurrentSate;

        if ((uiState == SelectState.UnitSelect || uiState == SelectState.BuildingSelect)
            && selectedUnitList.Count > 0)
        {
            uiState = SelectState.UnitSelect;
        }

        //UIController에서 현재 상황에 맞게 UI창 상태 변경
        switch (uiState)
        {
```

Before (`case SelectState.BuildingSelect:` 전체):
```csharp
            case SelectState.BuildingSelect:

                // 건물은 항상 단일 선택 취급 (Squad_panel은 유닛 다중 선택 전용) -> Info_panel 표시
                if (selectedBuildingList.Count > 0)
                {
                    BuildingController building = selectedBuildingList[0];
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                // 공중에 뜬 건물은 생산/연구 등 모든 커맨드를 막고 아래에서 Land/Move 버튼만 노출한다.
                bool selectedBuildingLifted = selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();

                if (selectedBuildingLifted)
                {
                    // ClearPanel()이 아니라 리프트/이동 슬롯을 보호하는 전용 메서드를 쓴다 - 매 프레임 호출되므로
                    // ClearPanel()로 그 두 슬롯까지 매번 껐다 켰다 하면 실행 중이던 클릭 코루틴/단축키가 끊긴다.
                    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: true);
                    uIController.HideProductionUI();
                }
                else
                {
                    switch (BuildingSelectState)
                    {
                        case BuildingState.MainBaseSelect:
                            uIController.ShowMainBasePanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier1Select:
                            uIController.ShowBarracksPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier2Select:
                            uIController.ShowFactoryPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier3Select:
                            uIController.ShowAirportPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.SupplyDepot:
                        case BuildingState.Lab:
                        case BuildingState.None:
                            uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
                            uIController.HideProductionUI();
                            break;
                    }
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 고정 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (selectedBuildingList.Count > 0 && selectedBuildingList[0].CanLift())
                {
                    BuildingController building = selectedBuildingList[0];

                    uIController.ShowBuildingLiftCommand(
                        building.IsLifted(),
                        building.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>L</color>]", KeyCode.L)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>L</color>]", KeyCode.L));

                    // 공중에 뜬 상태에서만 고정 슬롯(0번)에 "이동" 버튼을 추가로 노출한다.
                    if (building.IsLifted())
                    {
                        uIController.ShowBuildingMoveCommand(
                            ButtonAction.Simple(EnterBuildingMoveMode, "Move", "Move to a location while airborne. \nshortcut key [<color=yellow>M</color>]", KeyCode.M));
                    }
                }
                else if (selectedBuildingList.Count > 0)
                {
                    // 리프트 불가능한 건물(CanLift() == false)이면, 이전에 선택했던 다른 건물의 리프트/이동 버튼이
                    // 잔상으로 남지 않도록 정리한다.
                    uIController.ClearBuildingLiftSlots();
                }
                break;
```

After:
```csharp
            case SelectState.BuildingSelect:

                BuildingController representativeBuilding = GetRepresentativeBuilding();

                // 건물을 여러 개 선택했으면 Squad_panel(아이콘 그리드), 한 개면 Info_panel을 보여준다 (유닛과 동일한 패턴).
                if (selectedBuildingList.Count > 1)
                {
                    uIController.ShowBuildingSquadPanel(selectedBuildingList, ClickSelectBuilding);
                }
                else if (selectedBuildingList.Count == 1)
                {
                    BuildingController building = selectedBuildingList[0];
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                // 공중에 뜬 건물은 생산/연구 등 모든 커맨드를 막고 아래에서 Land/Move 버튼만 노출한다.
                // (여러 건물이 섞여 선택돼도 항상 대표 건물 하나를 기준으로 판단해 패널을 통일한다)
                bool selectedBuildingLifted = representativeBuilding != null && representativeBuilding.IsLifted();

                if (selectedBuildingLifted)
                {
                    // ClearPanel()이 아니라 리프트/이동 슬롯을 보호하는 전용 메서드를 쓴다 - 매 프레임 호출되므로
                    // ClearPanel()로 그 두 슬롯까지 매번 껐다 켰다 하면 실행 중이던 클릭 코루틴/단축키가 끊긴다.
                    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: true);
                    uIController.HideProductionUI();
                }
                else
                {
                    // 우선순위(MainBase > Tier1 > Tier2 > Tier3 > SupplyDepot > Lab)에 따라 뽑힌 대표 건물의
                    // 태그로 어느 생산 패널을 보여줄지 정한다. 예: MainBase + Tier1(병영) 선택 시 MainBase가
                    // 우선이므로 일꾼 생산 버튼이 보인다.
                    switch (representativeBuilding != null ? TagToBuildingState(representativeBuilding.tag) : BuildingState.None)
                    {
                        case BuildingState.MainBaseSelect:
                            uIController.ShowMainBasePanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier1Select:
                            uIController.ShowBarracksPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier2Select:
                            uIController.ShowFactoryPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier3Select:
                            uIController.ShowAirportPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.SupplyDepot:
                        case BuildingState.Lab:
                        case BuildingState.None:
                            uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
                            uIController.HideProductionUI();
                            break;
                    }
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 고정 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (representativeBuilding != null && representativeBuilding.CanLift())
                {
                    uIController.ShowBuildingLiftCommand(
                        representativeBuilding.IsLifted(),
                        representativeBuilding.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>L</color>]", KeyCode.L)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>L</color>]", KeyCode.L));

                    // 공중에 뜬 상태에서만 고정 슬롯(0번)에 "이동" 버튼을 추가로 노출한다.
                    if (representativeBuilding.IsLifted())
                    {
                        uIController.ShowBuildingMoveCommand(
                            ButtonAction.Simple(EnterBuildingMoveMode, "Move", "Move to a location while airborne. \nshortcut key [<color=yellow>M</color>]", KeyCode.M));
                    }
                }
                else if (selectedBuildingList.Count > 0)
                {
                    // 리프트 불가능한 건물(CanLift() == false)이면, 이전에 선택했던 다른 건물의 리프트/이동 버튼이
                    // 잔상으로 남지 않도록 정리한다.
                    uIController.ClearBuildingLiftSlots();
                }
                break;
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/UI/UIController.cs` (건물용 Squad_panel 데이터/메서드 추가,
  `SelectSquadPage`가 현재 모드에 맞는 새로고침 메서드를 호출하도록 분기, `HideSquadPanel` 정리 대상
  확장), `Assets/Scripts/System/RTSUnitController.cs` (`GetRepresentativeBuilding`/
  `TagToBuildingState` 헬퍼 추가, 리프트/생산 대기열 메서드들이 `selectedBuildingList[0]` 대신 대표
  건물을 쓰도록 변경, `UpdateUI()`가 유닛 우선 + 대표 건물 기준으로 동작하도록 변경).
- 동작 변화:
  - 건물을 2개 이상 Shift+클릭으로 선택하면 Info_panel 대신 Squad_panel(아이콘 그리드)이 보이고,
    그리드에서 건물 아이콘을 클릭하면 그 건물 하나로 선택이 좁혀진다(유닛과 동일).
  - 건물과 유닛을 함께 선택한 상태에서는 항상 유닛의 명령 패널(이동/공격/정지 등, 또는 유닛 다중
    선택이면 Squad_panel)이 보인다. 건물은 계속 선택된 상태로 남지만(맵 상 마커 등) 커맨드 패널에는
    드러나지 않는다.
  - 서로 다른 티어의 건물을 함께 선택하면 MainBase > Tier1 > Tier2 > Tier3 > SupplyDepot > Lab
    우선순위로 뽑힌 대표 건물의 생산 패널이 보인다. 생산 대기열 표시/취소(ESC 포함, [[esc-cancel-production-queue-from-back]] 0184)와 리프트/착륙/이동 버튼도 전부 같은 대표 건물 기준으로
    동작해서 "보이는 패널"과 "실제로 명령이 가는 건물"이 항상 일치한다.
  - 같은 티어 건물끼리만 섞여 있을 때(예: 병영 2개)는 대표 건물이 그중 하나로 뽑히고, 생산 버튼을
    누르면 [[multi-building-production-resource-dupe-bugfix-proposal]](0183)에서 이미 고친 대로
    태그가 일치하는 모든 선택 건물(즉 이 경우 병영 2개 다)에 각각 비용을 내고 큐잉된다 - 이번 변경은
    "어떤 패널을 보여줄지"만 바꾸고, 실제 생산 큐잉 대상 건물 목록(0183의 로직)은 그대로 유지한다.
  - 건물 1개만 선택했을 때의 동작은 기존과 동일.

## 확인 필요

이대로 진행해도 될까요?
