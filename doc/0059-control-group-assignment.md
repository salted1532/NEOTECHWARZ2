# 0059. 부대 지정(컨트롤 그룹)

**날짜:** 2026-07-12

## 요청 내용
> 부대지정 기능 추가. `Ctrl` + 숫자(1~0, 키보드 위쪽 숫자키)를 누르면 현재 선택된 유닛들 혹은 건물을 그 번호의 부대로 저장. 이후 숫자만 누르면 저장된 부대가 선택 리스트로 들어가서 선택된 유닛/건물로 표시됨.
>
> (추가 요청) `Shift` + 숫자(1~0)로도 지정 가능하게: 현재 선택된 유닛 중 그 숫자 부대에 아직 들어있지 않은 유닛만 그 부대에 추가(기존 멤버는 유지 - `Ctrl`처럼 덮어쓰지 않음). 이렇게 하면 유닛 하나가 여러 부대에 동시에 속할 수 있음.

## 조사 결과 (현재 코드 상태)
- 선택 상태는 `RTSUnitController`가 `selectedUnitList`(`List<UnitController>`)와 `selectedBuildingList`(`List<BuildingController>`)로 들고 있다. 부대 지정은 이 두 리스트의 "스냅샷"을 저장했다가 나중에 복원하는 기능이라 이 두 컬렉션만 다루면 된다(적/자원/건설중 건물은 "부대"라는 개념과 맞지 않아 제외).
- 유닛을 "덮어쓰지 않고 선택에 추가"하는 공개 메서드가 이미 있다: `DragSelectUnit(UnitController)` — 이미 선택돼 있지 않으면 내부적으로 `SelectUnit()`을 호출해 추가한다(드래그 박스 선택에 쓰던 것을 그대로 재사용 가능).
- 건물 쪽은 `SelectBuilding(BuildingController)`가 이미 `public`이라(`ShiftClickSelectBuilding`/`ClickSelectBuilding`이 그대로 호출) 별도 헬퍼 없이 바로 재사용 가능하다.
- `SelectUnit`/`SelectBuilding` 둘 다 `IsBuildMode()`면 무시하는 가드가 이미 있어서, 건설모드 중에는 부대 선택도 자연스럽게 막힌다(별도 처리 불필요).
- 유닛/건물이 죽으면 `RTSUnitController.UnitList`/`BuildingList`, `selectedUnitList`/`selectedBuildingList`에서는 각자 `Die()`에서 알아서 제거되지만, 부대 지정 저장소는 별도 컬렉션이라 그 시점에 자동으로 정리되지 않는다 → 부대를 다시 불러올 때(`SelectControlGroup`) Unity의 "가짜 null" 체크로 죽은 대상을 걸러내야 한다(`UnitList.RemoveAll(unit => unit == null)` 등 기존 패턴과 동일).
- 키보드 입력은 `UserControl.HandlekeyBoard()`에서 처리한다(현재는 랠리 모드 전환(Y), 건설모드 ESC만 있음 — 나머지 명령은 각 `ProductionSlot`이 자기 단축키를 스스로 감지). 부대 지정은 대응하는 UI 버튼이 없는 순수 키보드 전용 기능이라 `Rally`(Y)와 같은 방식으로 `UserControl`에 직접 추가한다.
- 숫자 키는 키보드 위쪽 숫자(`KeyCode.Alpha0`~`Alpha9`)를 써야 한다(요청에 "키보드 위에 숫자"라고 명시) — 넘패드(`Keypad0`~`Keypad9`)와는 다른 `KeyCode`이므로 명시적으로 `Alpha*`를 사용해야 한다.
- 기존 단축키들(M/A/S/P/H/R/B/W/I/F/D/C/L/T/Y 등) 중 숫자키를 쓰는 것은 없어서 충돌 없음.
- `Ctrl+숫자`(덮어쓰기 저장)와 `Shift+숫자`(겹치지 않는 대상만 추가)는 저장 로직이 다르다 - `Ctrl`은 `Clear()` 후 `AddRange`(완전 교체), `Shift`는 이미 들어있는 대상은 건드리지 않고 없는 대상만 `Add`(병합). 그래서 별도 메서드(`AssignControlGroup` / `AddSelectedToControlGroup`)로 나눈다.

## 설계안

### 1. `Assets/Scripts/System/RTSUnitController.cs`

**필드 추가** (`selectedBaseStructure` 아래):
```csharp
// 기존 코드
    public BaseStructure selectedBaseStructure; // 건설 중인 건물 기반도 항상 단일 선택

    // 맵에 존재하는 모든 유닛/건물/자원 노드
```
```csharp
// 변경 코드
    public BaseStructure selectedBaseStructure; // 건설 중인 건물 기반도 항상 단일 선택

    // ===== 부대 지정(컨트롤 그룹) - Ctrl+숫자(1~9,0)로 저장, 숫자만 누르면 해당 부대를 선택 =====
    // 인덱스는 눌린 숫자와 그대로 대응(1→[0], 2→[1], ..., 9→[8], 0→[9]) - UserControl에서 이미 이렇게 매핑해 넘겨준다.
    private readonly List<UnitController>[] controlGroupUnits = new List<UnitController>[10];
    private readonly List<BuildingController>[] controlGroupBuildings = new List<BuildingController>[10];

    // 맵에 존재하는 모든 유닛/건물/자원 노드
```

**`Awake()`에 초기화 추가**:
```csharp
// 기존 코드
    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        selectedBuildingList = new List<BuildingController>();
        selectedEnemyList = new List<EnemyController>();
        UnitList = new List<UnitController>();
        BuildingList = new List<BuildingController>();
        ResourceNodeList = new List<ResourceNode>();
    }
```
```csharp
// 변경 코드
    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        selectedBuildingList = new List<BuildingController>();
        selectedEnemyList = new List<EnemyController>();
        UnitList = new List<UnitController>();
        BuildingList = new List<BuildingController>();
        ResourceNodeList = new List<ResourceNode>();

        for (int i = 0; i < controlGroupUnits.Length; i++)
        {
            controlGroupUnits[i] = new List<UnitController>();
            controlGroupBuildings[i] = new List<BuildingController>();
        }
    }
```

**새 리전 추가** (`#region Unit선택 관련` 앞이나 뒤 아무 곳, 예: `DeselectAll()` 아래 `#region UserControl 상태 전환` 앞):
```csharp
    #region 부대 지정(컨트롤 그룹)

    // Ctrl+숫자: 현재 선택된 유닛/건물을 지정한 그룹 번호(0~9)에 저장한다(기존 저장 내용은 덮어씀).
    // 아무 것도 선택돼 있지 않으면 아무 것도 하지 않는다(실수로 빈 선택을 눌러 기존 그룹을 날리는 것 방지).
    public void AssignControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        if (selectedUnitList.Count == 0 && selectedBuildingList.Count == 0)
            return;

        controlGroupUnits[groupIndex].Clear();
        controlGroupUnits[groupIndex].AddRange(selectedUnitList);

        controlGroupBuildings[groupIndex].Clear();
        controlGroupBuildings[groupIndex].AddRange(selectedBuildingList);
    }

    // Shift+숫자: 현재 선택된 유닛/건물 중 그 그룹에 아직 없는 대상만 추가한다(기존 멤버는 그대로 유지).
    // Ctrl(AssignControlGroup)과 달리 완전 교체가 아니라 병합이라, 유닛 하나가 여러 그룹에 동시에 속할 수 있다.
    public void AddSelectedToControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        if (selectedUnitList.Count == 0 && selectedBuildingList.Count == 0)
            return;

        foreach (UnitController unit in selectedUnitList)
        {
            if (unit != null && !controlGroupUnits[groupIndex].Contains(unit))
                controlGroupUnits[groupIndex].Add(unit);
        }

        foreach (BuildingController building in selectedBuildingList)
        {
            if (building != null && !controlGroupBuildings[groupIndex].Contains(building))
                controlGroupBuildings[groupIndex].Add(building);
        }
    }

    // 숫자만 누르면: 저장된 그룹의 유닛/건물을 선택 상태로 되돌린다. 그 사이 죽거나 파괴된 대상은 자동으로 걸러진다.
    // 그룹이 비어있으면(저장한 적 없거나 전부 사라짐) 기존 선택을 그대로 둔다.
    public void SelectControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
        controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

        if (controlGroupUnits[groupIndex].Count == 0 && controlGroupBuildings[groupIndex].Count == 0)
            return;

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);
    }

    #endregion
```

### 2. `Assets/Scripts/UserControl/UserControl.cs`

**필드 추가** (`OrderState` enum 아래 또는 다른 필드들 근처):
```csharp
    // 부대 지정(컨트롤 그룹) 단축키 - 인덱스가 그대로 그룹 번호(0~9)가 되도록 키보드 위쪽 숫자(1~9,0) 순서로 배치.
    private static readonly KeyCode[] controlGroupKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
    };
```

**`HandlekeyBoard()`에서 호출 + 새 메서드 추가**:
```csharp
// 기존 코드
    private void HandlekeyBoard()
    {
        // 유닛 명령(Attack/Move/Stop/Patrol/Hold/Return/Build)과 건물 건설/유닛 생산 단축키는
        // 이제 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지해서 스스로 클릭되므로 여기서 따로 처리하지 않는다.
        // (Rally는 대응하는 버튼이 없는 순수 키보드 전용 모드 전환이라 그대로 남겨둠)
        if (rtsUnitController.IsUnitSelect())
        {
            //건물 랠리 설정
            if (Input.GetKeyDown(KeyCode.Y))
            {
                UsercurrentState = OrderState.Rally;
            }
        }

        if (rtsUnitController.IsBuildMode())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                rtsUnitController.ReturnState();
            }
        }
    }
```
```csharp
// 변경 코드
    private void HandlekeyBoard()
    {
        // 유닛 명령(Attack/Move/Stop/Patrol/Hold/Return/Build)과 건물 건설/유닛 생산 단축키는
        // 이제 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지해서 스스로 클릭되므로 여기서 따로 처리하지 않는다.
        // (Rally는 대응하는 버튼이 없는 순수 키보드 전용 모드 전환이라 그대로 남겨둠)
        if (rtsUnitController.IsUnitSelect())
        {
            //건물 랠리 설정
            if (Input.GetKeyDown(KeyCode.Y))
            {
                UsercurrentState = OrderState.Rally;
            }
        }

        if (rtsUnitController.IsBuildMode())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                rtsUnitController.ReturnState();
            }
        }

        HandleControlGroupInput();
    }

    // 부대 지정(컨트롤 그룹): Ctrl+숫자(1~9,0)는 덮어쓰기 저장, Shift+숫자는 겹치지 않는 대상만 추가(병합),
    // 숫자만 누르면 저장된 부대를 선택한다.
    private void HandleControlGroupInput()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        for (int i = 0; i < controlGroupKeys.Length; i++)
        {
            if (!Input.GetKeyDown(controlGroupKeys[i]))
                continue;

            if (ctrlHeld)
                rtsUnitController.AssignControlGroup(i);
            else if (shiftHeld)
                rtsUnitController.AddSelectedToControlGroup(i);
            else
                rtsUnitController.SelectControlGroup(i);

            break; // 한 프레임에 숫자 키 하나만 처리하면 충분
        }
    }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **숫자 매핑**: `1→그룹1, 2→그룹2, ..., 9→그룹9, 0→그룹10` (스타크래프트와 동일한 익숙한 순서). 내부 배열 인덱스는 `0~9`로, `Alpha1`이 인덱스 `0`, `Alpha0`이 인덱스 `9`입니다.
- **빈 선택으로 Ctrl+숫자 또는 Shift+숫자를 누른 경우**: 둘 다 아무 것도 하지 않습니다(기존에 저장된 그룹을 실수로 건드리지 않도록). 그룹을 진짜로 비우고 싶다면 별도 요청 주시면 처리하겠습니다.
- **한 유닛이 여러 부대에 동시 소속**: `Shift+숫자`로 의도적으로 지원합니다 - 저장소가 그룹별 독립된 리스트라 같은 유닛이 여러 그룹의 리스트에 동시에 들어가는 것 자체엔 아무 제약이 없습니다. 한 그룹을 불러와 이동시켜도 다른 그룹의 저장 내용에는 영향이 없습니다.
- **저장된 그룹의 대상이 죽거나 파괴된 경우**: 그룹을 불러올 때(숫자만 누를 때) 자동으로 걸러지고, 살아있는 대상만 선택됩니다. 전멸/전파괴된 그룹은 숫자를 눌러도 아무 반응이 없습니다(기존 선택 유지).
- **적/자원/건설중인 건물(BaseStructure)**: 부대 지정 대상에서 제외합니다(요청에 "유닛들 혹은 건물"이라고만 명시돼 있어 아군 유닛/완공 건물만 포함).
- **더블프레스로 카메라 이동(스타크래프트의 "같은 숫자 두 번 누르면 그 부대로 화면 이동")**: 요청에 없어서 이번엔 구현하지 않습니다.
- **건설모드 중**: `SelectUnit`/`SelectBuilding`에 이미 있는 `IsBuildMode()` 가드 덕분에 부대 선택도 자동으로 막힙니다(별도 처리 불필요).

## 변경 예정 파일
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
