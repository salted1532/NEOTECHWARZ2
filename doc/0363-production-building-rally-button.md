# 0363 — 생산 건물 랠리 버튼(슬롯 6) + 선택 시 랠리 포인트 마커

**날짜:** 2026-08-02

## 요청

"생산 건물 랠리 버튼 만들려고해 6번 슬롯에다가 랠리 버튼을 추가해줘 이걸 누르면 M 이동명령처럼
usercontrol에서 위치 지정할수 있게 해주고 클릭시 거기로 위치가 지정되는거야(건물에서 우클릭과 같음).
그리고 건물을 선택하면 자신의 랠리 포인트 위치에 포인터가 보이도록 해줘 마커는 3초있다가 사라지는건
그대로 유지 되었으면 좋겠어 랠리 단축키는 Y야"

## 조사 — 이미 대부분 구현돼 있었음

랠리 기능 자체(위치 지정 대기 모드, 우클릭으로 확정, Y 단축키)는 이미 코드에 있었고, **버튼만 없었다**:

- `UserControl.cs`: `OrderState.Rally` 상태, 좌클릭 확정 시
  `rtsUnitController.SetRallySelectBuilding(groundPoint)` 호출 + `ShowMovePointer(groundPoint)`로
  기존 이동 포인터 표시(이미 "건물 우클릭과 동일" 동작 - `IssueRightClickMoveAt()`가 이걸 그대로 씀).
- `RTSUnitController.cs`: `EnterRallyMode()` → `userControl.SetOrderState("Rally")`.
- `BuildingController.cs`: `RallyPosition`/`SetRallyPosition()`/`GetRallyPos()`.
- **버그 발견**: `UserControl.HandlekeyBoard()`의 Y 단축키가 `if (rtsUnitController.IsUnitSelect())`로
  감싸져 있었음 — 랠리는 건물 전용 기능인데 "유닛 선택 중"일 때만 반응하는 조건이라, 실제로 건물을
  선택한 상태에서는 Y를 눌러도 아무 일도 안 일어나는 죽은 코드였음(주석엔 "건물 랠리 설정"이라고
  써있어서 의도와 조건이 안 맞음 - 복붙 실수로 보임).

## 적용

**`Assets/Scripts/UI/UIController.cs`**

- `BuildingRallySlotIndex = 6` 상수 + `LiftAndRallySlotsProtected`(Lift+Rally 슬롯 보호 집합) 추가.
  슬롯 6은 tier당 최대 유닛 수(NTA 데이터 기준 최대 3개, `<tier>k__BackingField` 값으로 확인)로는
  절대 안 채워지는 여유 슬롯이라 실제 생산 버튼과 안 겹침.
- `rallyIcon` Sprite 필드 추가(인스펙터 연결 필요 - 아래 "남은 작업" 참고).
- `ShowUnitProductionPanel()`의 보호 슬롯을 `LiftSlotOnlyProtected` → `LiftAndRallySlotsProtected`로
  변경 - 매 프레임 갱신되는 생산 패널이 슬롯 6을 지워버리지 않도록.
- `ShowBuildingRallyCommand(ButtonAction onRally)` 신규 - `ShowBuildingMoveCommand`와 동일한 패턴으로
  슬롯 6에 랠리 버튼 데이터를 채움.

**`Assets/Scripts/System/RTSUnitController.cs`**

- `RallyButtonAction()` 신규 - `ButtonAction.Simple(EnterRallyMode, "Rally", "...", KeyCode.Y)`.
  `EnterRallyMode()`는 이미 있던 메서드 그대로 재사용(신규 코드 없음, 버튼 콜백으로 연결만 함).
- `UpdateUI()`의 생산 패널 switch문에서 MainBase/Tier1/Tier2/Tier3 네 케이스 각각에
  `uIController.ShowBuildingRallyCommand(RallyButtonAction());` 추가 (Lift/Move 버튼과 동일하게
  `ShowProductionUI()` 바로 뒤).
- `SelectBuilding()`: 생산 건물(`IsProductionBuildingState()` - MainBase/Tier1/Tier2/Tier3)을
  선택하는 순간 `userControl.ShowMovePointerAt(building.GetRallyPos())` 호출 - 기존 3초 자동 소멸
  이동 포인터를 그대로 재사용해서 랠리 포인트 위치에 표시.

**`Assets/Scripts/UserControl/UserControl.cs`**

- `ShowMovePointerAt(Vector3)` 공개 진입점 추가 - 기존 private `ShowMovePointer()`를 외부
  (RTSUnitController)에서 부를 수 있게 얇게 감싼 것뿐, 동작(3초 자동 사라짐 포함)은 완전히 동일.
- `HandlekeyBoard()`에서 버그 있던 Y 키 수동 처리 블록 삭제 - 이제 랠리 버튼(`ProductionSlot`)이
  자기 단축키(Y)를 스스로 감지해서 클릭을 시뮬레이션하므로(기존 Move/Attack/Build 버튼들과 동일한
  기존 관례) 더 이상 필요 없음.

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

## 남은 작업 (사용자가 직접)

- `UIController` 인스펙터에 신규 `rallyIcon` 스프라이트 필드가 비어있음 — 랠리 버튼 아이콘 이미지를
  직접 연결해야 버튼에 그림이 보임(연결 전엔 빈 아이콘으로 표시됨, 클릭/단축키 자체는 정상 동작).

## 확인 필요 사항

- 이번엔 사전 제안 없이 바로 구현했습니다 - 랠리 기능 자체(모드 전환/우클릭 확정/Y 단축키)가 이미 다
  구현돼 있어서 "버튼 하나 추가해 기존 진입점에 연결"이 명확했기 때문입니다. 혹시 원하신 동작과
  다른 부분(예: 마커를 "선택할 때마다" 대신 "랠리를 새로 지정할 때만" 보여주고 싶다거나)이 있으면
  말씀해주세요.
