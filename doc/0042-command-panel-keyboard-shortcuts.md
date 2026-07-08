# 0042. 커맨드 패널 단축키 + 눌림 효과

**날짜:** 2026-07-09

## 요청 내용
> 이제 UserControl에서 단축키 설정을 해볼건데
> 단축키 설정
> 각 선택상황(유닛 선택, 일꾼선택, 건설모드 등)에서 각 버튼별 단축키 작동, UserControl에서 키보드를 통한
> 버튼과 같은 명령 수행 + 키보드 입력 시 버튼 눌러진 효과 넣기
> 유닛명령(공격 A, 이동 M, 정지 S, 순찰 P, 홀드 H, 자원리턴 R, 건설모드 B)
> 건물건설(mainbase C, 보급고 supplyDepot S, 병영 B, 공장 F, SpacePort S, Lab L)
> 유닛생산(메인기지 -> 일꾼 W, 병영 -> 어썰트투르퍼 A, 스카웃 드론 S 공장 -> 레인저IFV I, 펄스탱크 P, 공항 -> 파이어호크 F, 가디언 드론 D)
> 해당하는 모드 상태일때만 단축키가 활성화 되고 작동하고 아닐시 다른 명령에 대한 단축키는 작동하면 안돼

**SpacePort 단축키 충돌 확인**: 요청에 보급고(SupplyDepot)와 SpacePort 둘 다 "S"로 적혀있어 확인 요청 → **SpacePort는 P로 확정** (SupplyDepot은 S 그대로).

## 확정된 단축키 목록
- **유닛 명령** (Worker/AttackUnit 패널 공통): Attack=A, Move=M, Stop=S, Patrol=P, Hold=H / (Worker 전용) Return Cargo=R, Build=B
- **건물 건설** (BuildMode 패널): MainBase=C, SupplyDepot=S, Barracks=B, Factory=F, SpacePort=P, Lab=L
- **유닛 생산**: MainBase→Worker=W / Barracks→AssaultTrooper=A, ScoutDrone=S / Factory→RangerIFV=I, PulseTank=P / Airport→FireHawk=F, GuardianDrone=D

## 설계안: "버튼이 이미 알고 있는 명령을 키로 대신 누른다"

`UserControl.HandlekeyBoard()`에 명령마다 `if (Input.GetKeyDown(...)) rtsUnitController.SomeCommand();`를 일일이 나열하는 대신, **버튼(`ProductionSlot`) 자신이 자기 단축키를 알고 있다가, 그 키가 눌리면 스스로 "클릭됨" 이벤트를 발생시키는** 방식으로 구현. 이렇게 하면:
- 어떤 패널이 떠 있는지(=어떤 버튼들이 활성화돼 있는지)에 따라 자동으로 단축키가 활성/비활성됨 — `ProductionSlot`은 `Clear()`되면 `gameObject.SetActive(false)`가 되어 Unity가 아예 `Update()`를 호출하지 않으므로, "해당 모드가 아니면 그 단축키는 완전히 죽어있다"는 요구사항이 **자동으로** 성립함(상태별로 따로 if문을 겹겹이 쌓을 필요 없음).
- 버튼 클릭과 완전히 동일한 경로(`Button.onClick`)를 타므로, 커맨드 로직이 버튼과 따로 노는 일이 없음(중복 구현/불일치 위험 없음).
- "눌림 효과"도 실제 마우스 클릭이 발생시키는 것과 동일한 `PointerDown`/`PointerUp`/`PointerClick` 이벤트를 코드로 그대로 재현하는 것이라, 버튼의 기존 Transition(색상/스프라이트 전환) 설정을 그대로 재사용함 - 새로운 애니메이션을 따로 만들 필요 없음.

### 1. `UIController.cs` — `ButtonAction`/`CommandButtonData`에 단축키 필드 추가

```csharp
// 기존 코드
    public readonly struct ButtonAction
    {
        public Action Callback { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }

        private ButtonAction(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            bool hasCost)
        {
            Callback = callback;
            Title = title;
            Description = description;
            Ore = ore;
            Gas = gas;
            Population = population;
            HasCost = hasCost;
        }

        // 이동/공격/정지 등 비용이 없는 일반 명령 버튼용
        public static ButtonAction Simple(Action callback, string title, string description)
        {
            return new ButtonAction(callback, title, description, 0, 0, 0, false);
        }

        // 유닛 생산/건물 건설처럼 광물/가스/인구 비용이 있는 버튼용
        public static ButtonAction WithCost(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population)
        {
            return new ButtonAction(callback, title, description, ore, gas, population, true);
        }
    }
```
```csharp
// 변경 코드
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

`CommandButtonData`에도 동일하게 `Shortcut` 추가:
```csharp
// 기존 코드
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
        }
```
```csharp
// 변경 코드
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
```
(`Shortcut` 프로퍼티 선언도 `HasCost` 옆에 추가. 취소 버튼/빈 슬롯 등에 쓰는 `CommandButtonData(icon, callback, interactable)` 생성자는 내부적으로 `ButtonAction.Simple(callback, "", "")`을 거치므로 자동으로 `Shortcut = KeyCode.None`— 기존 동작 그대로 회귀 없음.)

### 2. `ProductionSlot.cs` — 자기 단축키를 감지해서 스스로 "클릭"한다

```csharp
// 기존 코드
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProductionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    private RectTransform rectTransform;
    private Action callback;
    private UIController.CommandButtonData data;
    private bool hasData;
```
```csharp
// 변경 코드
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProductionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    private RectTransform rectTransform;
    private Action callback;
    private UIController.CommandButtonData data;
    private bool hasData;
    private KeyCode shortcut = KeyCode.None;
```

`SetData`/`Clear`에 shortcut 기록/초기화 추가:
```csharp
// 기존 코드 (SetData 안)
        this.data = data;
        hasData = true;
        callback = data.Callback;
```
```csharp
// 변경 코드
        this.data = data;
        hasData = true;
        callback = data.Callback;
        shortcut = data.Shortcut;
```
```csharp
// 기존 코드 (Clear 안)
        callback = null;
        hasData = false;
```
```csharp
// 변경 코드
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;
```

**매 프레임 자기 단축키 확인 + 눌림 효과 재생 추가**:
```csharp
    // 이 슬롯이 활성화(SetData)돼 있는 동안에만 실행됨 - Clear()되면 gameObject 자체가 비활성화라
    // Update()가 아예 호출되지 않으므로, "지금 이 버튼이 안 보이면 단축키도 죽어있다"가 자동으로 성립한다.
    private void Update()
    {
        if (!hasData || shortcut == KeyCode.None || button == null || !button.interactable)
            return;

        if (Input.GetKeyDown(shortcut))
            StartCoroutine(SimulateClickRoutine());
    }

    // 실제 마우스 클릭과 동일한 PointerDown → (짧은 대기) → PointerUp/PointerClick 이벤트를 그대로 재현한다.
    // 버튼에 이미 설정된 눌림 색상/스프라이트 Transition이 그대로 재생되고, PointerClick이 onClick을 호출한다.
    private IEnumerator SimulateClickRoutine()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);

        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSeconds(0.08f);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerClickHandler);
    }
```

### 3. `RTSUnitController.cs` — 각 버튼 생성 부분에 단축키 지정

**`UnitButtonAction`/`BuildingButtonAction` 헬퍼에 단축키 인자 추가**:
```csharp
// 기존 코드
    private ButtonAction UnitButtonAction(Action callback, int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Train {data.unitName}."
            : data.description;

        return ButtonAction.WithCost(callback, data.unitName, description, data.mineral, data.gas, data.population);
    }

    private ButtonAction BuildingButtonAction(Action callback, int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Construct {data.Name}."
            : data.description;

        return ButtonAction.WithCost(callback, data.Name, description, data.mineral, data.gas, data.population);
    }
```
```csharp
// 변경 코드
    private ButtonAction UnitButtonAction(Action callback, int unitID, KeyCode shortcut = KeyCode.None)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Train {data.unitName}."
            : data.description;

        return ButtonAction.WithCost(callback, data.unitName, description, data.mineral, data.gas, data.population, shortcut);
    }

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

**Worker 패널**:
```csharp
// 기존 코드
                        uIController.ShowWorkerPanel(
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]"),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]"),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]"),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]"),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]"),
                            ButtonAction.Simple(EnterReturnMode, "Return Cargo", "Return gathered resources to base. \nshortcut key [<color=yellow>R</color>]"),
                            ButtonAction.Simple(BuildModeOn, "Build", "Enter build mode. \nshortcut key [<color=yellow>B</color>]"));
```
```csharp
// 변경 코드
                        uIController.ShowWorkerPanel(
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]", KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]", KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]", KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]", KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]", KeyCode.H),
                            ButtonAction.Simple(EnterReturnMode, "Return Cargo", "Return gathered resources to base. \nshortcut key [<color=yellow>R</color>]", KeyCode.R),
                            ButtonAction.Simple(BuildModeOn, "Build", "Enter build mode. \nshortcut key [<color=yellow>B</color>]", KeyCode.B));
```

**AttackUnit 패널** (Return/Build 버튼 자체가 없어서 R/B는 여기선 아무 의미 없음 - 자동으로 비활성):
```csharp
// 기존 코드
                        uIController.ShowAttackUnitPanel(
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]"),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]"),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]"),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]"),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]"));
```
```csharp
// 변경 코드
                        uIController.ShowAttackUnitPanel(
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]", KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]", KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]", KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]", KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]", KeyCode.H));
```

**BuildMode 패널** (Cancel 버튼은 단축키 요청에 없어서 그대로 둠):
```csharp
// 기존 코드
                uIController.ShowBuildPanel(
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.CommandCenter), BuildingID.CommandCenter),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.SupplyDepot), BuildingID.SupplyDepot),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Barracks), BuildingID.Barracks),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Factory), BuildingID.Factory),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab),
                    ButtonAction.Simple(
                        () =>
                        {
                            PlacementSystem.StopPlacement();
                            ReturnState();
                        },
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>C</color>]"));
```
```csharp
// 변경 코드
                uIController.ShowBuildPanel(
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.CommandCenter), BuildingID.CommandCenter, KeyCode.C),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.SupplyDepot), BuildingID.SupplyDepot, KeyCode.S),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Barracks), BuildingID.Barracks, KeyCode.B),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Factory), BuildingID.Factory, KeyCode.F),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport, KeyCode.P),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab, KeyCode.L),
                    ButtonAction.Simple(
                        () =>
                        {
                            PlacementSystem.StopPlacement();
                            ReturnState();
                        },
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>C</color>]")); // 기존 텍스트에 C라고 써있었지만 실제 단축키 배정은 없었음 - 이번 요청 범위 밖이라 그대로 둠
```
(⚠️ 기존 Cancel 버튼 툴팁 텍스트에 이미 "C"라고 적혀 있었는데, 정작 `MainBase` 단축키도 이번에 C로 배정됨 - 원래 Cancel 버튼엔 실제 단축키가 연결된 적이 없어서(툴팁 텍스트일 뿐) 지금 당장 충돌은 아니지만, 사용자가 인지하고 있어야 할 부분이라 표시해둠. 이번 요청 목록엔 Cancel 단축키가 없어서 그대로 미배정 상태로 둠.)

**생산 패널 4종**:
```csharp
// 기존 코드
                    case BuildingState.MainBaseSelect:
                        uIController.ShowMainBasePanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Worker), UnitID.Worker));
                        ...

                    case BuildingState.Tier1Select:
                        uIController.ShowBarracksPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Marine), UnitID.Marine),
                            UnitButtonAction(() => SpawnUnit(UnitID.Vulture), UnitID.Vulture));
                        ...

                    case BuildingState.Tier2Select:
                        uIController.ShowFactoryPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Goliath), UnitID.Goliath),
                            UnitButtonAction(() => SpawnUnit(UnitID.Tank), UnitID.Tank));
                        ...

                    case BuildingState.Tier3Select:
                        uIController.ShowAirportPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Wraith), UnitID.Wraith),
                            UnitButtonAction(() => SpawnUnit(UnitID.Guardian), UnitID.Guardian));
                        ...
```
```csharp
// 변경 코드
                    case BuildingState.MainBaseSelect:
                        uIController.ShowMainBasePanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
                        ...

                    case BuildingState.Tier1Select:
                        uIController.ShowBarracksPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                            UnitButtonAction(() => SpawnUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));
                        ...

                    case BuildingState.Tier2Select:
                        uIController.ShowFactoryPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                            UnitButtonAction(() => SpawnUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));
                        ...

                    case BuildingState.Tier3Select:
                        uIController.ShowAirportPanel(
                            UnitButtonAction(() => SpawnUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                            UnitButtonAction(() => SpawnUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));
                        ...
```
(요청의 "어썰트투르퍼/스카웃 드론/레인저IFV/펄스탱크/파이어호크/가디언 드론"은 실제 유닛 이름(`UnitData.unitName`)이 그렇게 바뀌어있는 것으로 보고, 코드상 ID(Marine/Vulture/Goliath/Tank/Wraith/Guardian)는 그대로 두고 버튼 순서(=요청한 순서)만 기준으로 매칭함.)

### 4. `UserControl.cs` — 이제 버튼이 알아서 처리하므로 중복 하드코딩 제거

```csharp
// 기존 코드
    private void HandlekeyBoard()
    {

        if(rtsUnitController.IsUnitSelect())
        {
            // 공격모드 변화
            if (Input.GetKeyDown(KeyCode.A))
            {
                UsercurrentState = OrderState.Attack;
            }
            // 유닛 정지 명령
            if (Input.GetKeyDown(KeyCode.S))
            {
                rtsUnitController.StopSelectedUnits();
            }
            // 유닛 홀드 명령
            if (Input.GetKeyDown(KeyCode.H))
            {
                rtsUnitController.HoldSelectedUnits();
            }
            // 순찰모드 변화
            if (Input.GetKeyDown(KeyCode.P))
            {
                UsercurrentState = OrderState.Patrol;
            }
            // 이동모드 변화
            if (Input.GetKeyDown(KeyCode.M))
            {
                UsercurrentState = OrderState.Move;
            }
            //건설모드 전환
            if (Input.GetKeyDown(KeyCode.V))
            {
                //건설 모드 전환
            }
            //건물 랠리 설정
            if (Input.GetKeyDown(KeyCode.Y))
            {
                UsercurrentState = OrderState.Rally;
            }
        }

        if(rtsUnitController.IsBuildMode())
        {
            //건물 랠리 설정
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
    }
```

## 추가 반영 (같은 세션 후속 요청)
- BuildMode 패널의 "Cancel"(건설모드 취소) 버튼 단축키를 **T**로 지정(요청). 기존 툴팁 텍스트에 남아있던 잘못된 "C" 표기(MainBase와 겹치는, 실제로 연결된 적 없던 문구)도 "T"로 함께 수정.

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **SpacePort 단축키**: P로 확정(질문에서 답변받음).
- **Rally(Y)/ESC**: 이번 요청 목록에 없고 대응하는 버튼도 없어서 기존 하드코딩 방식 그대로 유지.
- **Cancel 버튼**: 요청 목록에 단축키가 없어서 이번엔 배정하지 않음(툴팁엔 예전부터 "C"라고 적혀 있었지만 실제로 연결된 적은 없었음 - 그대로 둠).
- **버튼이 비활성(`interactable=false`, 예: 자원 부족)일 때**: 실제 클릭과 동일하게 단축키도 무시됨(Unity Button이 원래 그렇게 동작).
- **다른 상태에서 같은 키 재사용**: 유닛 선택 상태의 A(공격)와 병영 생산 상태의 A(어썰트투르퍼)처럼, 서로 다른 상태에서 같은 키를 다른 명령에 써도 문제없음 — 두 상태가 동시에 활성화되는 일이 없고, 버튼이 안 보이면 단축키도 자동으로 죽어있기 때문.

## 변경 예정 파일
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UI/ProductionSlot.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
