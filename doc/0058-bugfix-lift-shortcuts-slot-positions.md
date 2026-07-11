# 0058. 버그수정: 리프트/이동 단축키 미작동 + 슬롯 위치 고정(0/8) + 상승고도 5

**날짜:** 2026-07-12

## 요청 내용
> 테스트해보니 M(이동)/L(착륙·이륙) 단축키가 작동하지 않고, 버튼도 눌린 채로 어두운 상태에 멈춰있다. 상승 높이는 y값 5 정도로 낮춰달라. 그리고 이동 버튼은 slot0번, 착륙/이륙 버튼은 slot8번에 고정으로 넣어달라.

## 원인 분석
`RTSUnitController.UpdateUI()`는 매 프레임(`Update()`) 호출된다. [[0057-lift-freeflight-land-lock-shortcut|0057]]에서 추가한 "공중 상태면 생산 패널을 감추고 Land/Move 버튼만 노출" 로직이 매 프레임 `uIController.ClearPanel()`을 호출했는데, `ClearPanel()`은 `slots[]` 배열 전체를 `Clear()`한다. `ProductionSlot.Clear()`는 `gameObject.SetActive(false)`를 호출하고, 바로 다음 줄에서(같은 프레임 안에서) `ShowBuildingLiftCommand`/`ShowBuildingMoveCommand`가 그 슬롯에 `SetData()`(→ `SetActive(true)`)를 다시 호출해 되살린다.

문제는 **Unity에서 `GameObject.SetActive(false)`가 호출되는 순간, 그 오브젝트에서 실행 중이던 코루틴은 즉시 강제 종료된다**는 점이다(같은 프레임 안에서 곧바로 `true`로 되돌려도 코루틴은 재시작되지 않는다). `ProductionSlot.Update()`는 키보드 단축키(M/L)를 감지하면 `SimulateClickRoutine()` 코루틴을 시작해 `PointerDown → 0.08초 대기 → PointerUp/PointerClick` 순서로 실제 클릭을 재현하는데, 실제 클릭 콜백(`callback.Invoke()`)은 이 코루틴의 **맨 마지막**에서 실행된다. 그런데 0.08초(약 5프레임) 동안 매 프레임 `ClearPanel()`이 그 슬롯을 `SetActive(false)`시켜버리므로:
- 코루틴이 `PointerDown`(버튼이 눌린 색으로 바뀜)까지만 실행되고 그 직후 강제 종료됨 → **버튼이 눌린 채(어두운 색)로 영원히 멈춤** (되살아난 뒤에도 `PointerUp`이 실행된 적이 없으니 눌린 색이 그대로 남음).
- `PointerClick`(=`callback.Invoke()`, 즉 실제 `LiftSelectedBuilding`/`BeginLandingSelectedBuilding`/`EnterBuildingMoveMode` 호출)까지 도달하지 못함 → **단축키가 동작하지 않는 것처럼 보임**.

실제 마우스 클릭은 Unity `EventSystem`이 같은 프레임 안에서 즉시 Down→Up→Click을 처리하는 경우가 많아 상대적으로 덜 티가 나지만, 동일한 근본 문제(슬롯이 매 프레임 꺼졌다 켜졌다 함) 때문에 클릭 타이밍이 애매하게 겹치면 마찬가지로 눌린 채로 멈출 수 있다.

## 수정 내용

### `Assets/Scripts/UI/UIController.cs`
- 리프트(`slot8`)/이동(`slot0`) 슬롯을 고정 인덱스 상수(`BuildingLiftSlotIndex = 8`, `BuildingMoveSlotIndex = 0`)로 명시하고, 건물 선택 컨텍스트 안에서는 이 두 슬롯이 **`Clear()`(=`SetActive(false)`)를 절대 타지 않도록** 했다.
  - `SetCommands`에 "이 인덱스들은 비워도 되는 목록에서 제외" 오버로드(`SetCommands(CommandButtonData[], HashSet<int> protectedSlotIndices)`)를 추가.
  - `ShowMainBasePanel`/`ShowBarracksPanel`/`ShowFactoryPanel`/`ShowAirportPanel`이 이 오버로드로 `{8}`(리프트 슬롯)을 보호하도록 변경 — 생산 버튼이 몇 개든 관계없이 뒤이어 `ShowBuildingLiftCommand`가 채우는 슬롯8을 건드리지 않는다.
  - 새 `ClearBuildingPanelExceptLiftSlots(bool protectMoveSlot)`: 생산 패널이 없는 경우(SupplyDepot/Lab/None, 또는 공중 상태)에 `ClearPanel()` 대신 사용. 슬롯8은 항상 보호하고, 공중 상태(`protectMoveSlot: true`)일 때만 슬롯0도 함께 보호한다(착륙 직후엔 `false`로 호출되어 이동 버튼 잔상이 자연스럽게 지워짐).
  - 새 `ClearBuildingLiftSlots()`: 리프트 불가능한 건물(`CanLift() == false`)을 선택했을 때 이전 건물의 버튼 잔상이 남지 않도록 두 슬롯을 정리.
  - `ShowBuildingLiftCommand`/`ShowBuildingMoveCommand`가 이제 `slots.Length - 1`/`slots.Length - 2`(상대 위치) 대신 고정 인덱스 `BuildingLiftSlotIndex`(8)/`BuildingMoveSlotIndex`(0)를 사용하도록 변경.

### `Assets/Scripts/System/RTSUnitController.cs`
- `UpdateUI()`의 `SelectState.BuildingSelect` 케이스에서 `uIController.ClearPanel()` 호출 두 곳(공중 상태 분기, `SupplyDepot`/`Lab`/`None` 분기)을 모두 `uIController.ClearBuildingPanelExceptLiftSlots(...)`로 교체.
- 리프트 불가능한 건물을 선택한 경우를 위한 `else if` 분기를 추가해 `uIController.ClearBuildingLiftSlots()` 호출(잔상 방지).

### `Assets/Scripts/Building/BuildingController.cs`
- `liftHeight` 기본값을 `8f` → `5f`로 변경.
  - ⚠️ **주의**: 이미 씬/프리팹에 배치된 `BuildingController`는 `liftHeight` 값이 이미 `8`로 직렬화되어 저장돼 있어서, 코드의 기본값만 바꿔서는 기존 오브젝트에 적용되지 않습니다. 유니티 에디터에서 해당 프리팹(또는 씬의 인스턴스)의 `Building Controller` 컴포넌트에서 `Lift Height` 값을 직접 `5`로 바꿔주셔야 실제로 반영됩니다.

## 이번 수정에서 결정한 세부 동작
- **슬롯 인덱스가 배열 길이를 벗어나는 경우**: `ShowBuildingLiftCommand`/`ShowBuildingMoveCommand`는 조용히 아무 것도 하지 않고 반환합니다(기존과 동일한 방어 패턴).
- **`slot0`이 생산 버튼과 겹치는 경우(예: MainBase의 일꾼 생산 버튼)**: 문제 없습니다 — 이동 버튼은 공중 상태에서만 표시되고, 그때는 생산 패널 자체가 그려지지 않으므로 동시에 같은 슬롯을 두고 경합하지 않습니다.

## 변경 예정 파일
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Building/BuildingController.cs`

## 상태
**적용 완료.**
