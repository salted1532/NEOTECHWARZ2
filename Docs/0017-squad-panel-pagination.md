# 0017 - Squad Panel 페이지네이션 (최대 60마리)

**날짜:** 2026-07-07

## 요청 내용
여러 유닛 선택 시 보여지는 Squad Panel이 기존에는 0~11번(12마리) 슬롯만 표시했음. Squad Panel에 page1~page5 버튼을 UI 상 추가해뒀으니, 선택된 유닛을 12마리씩 페이지로 나눠 각 페이지 버튼을 누르면 해당 12마리를 슬롯에 보여주도록 요청. 예: 36마리 선택 시 1~3페이지 버튼만 활성화. 최대 60마리(12 x 5페이지)까지 지원.

## 조사 내용
- `Assets/Scripts/UI/UIController.cs`의 `ShowSquadPanel`/`HideSquadPanel`이 Squad Panel을 담당하며, `squadSlots`(12개 `ProductionSlot`) 배열만 있고 페이지 개념이 없었음.
- `Assets/Scripts/System/RTSUnitController.cs`의 `UpdateUI()`가 매 프레임(`Update()`에서 호출) `uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit)`을 호출 — 즉 페이지 상태를 매 프레임 리셋하면 안 되고, "선택 내용이 실제로 바뀐 경우"에만 페이지를 0으로 되돌려야 함.
- `selectedUnitList`는 재할당 없이 계속 재사용되는 동일 `List<UnitController>` 참조라 참조 비교로는 선택 변경을 감지할 수 없어, 내용(snapshot) 비교로 구현.
- 씬(`SampleScene.unity`)에서 실제 page1~5 버튼 오브젝트는 아직 이름으로 확인되지 않음 — 사용자가 에디터에서 추가만 하고 저장/커밋 전이거나 아직 스크립트에 연결 전인 상태로 추정. 코드는 `squadPageButtons` 배열(5개, 인스펙터에서 버튼 드래그)만 있으면 동작하도록 구현.
- 선택 인원수 자체에는 기존에 12마리 제한이 없음(드래그 선택 등에서 상한 없음) — 표시 로직만 손보면 됨.

## 변경 내용 (`Assets/Scripts/UI/UIController.cs`)
- `squadPageButtons` (`Button[]`, 5개), `SquadUnitsPerPage`(12) 상수, `squadUnitsSnapshot`/`squadOnSelectUnit`/`squadCurrentPage` 상태 필드 추가.
- `Start()`에서 `SetupSquadPageButtons()` 호출 — 각 버튼 onClick에 `SelectSquadPage(i)`를 코드로 연결(인스펙터에서 OnClick 수동 연결 불필요, `squadSlots`와 동일한 패턴).
- `ShowSquadPanel`: 들어온 유닛 리스트가 이전과 다르면(`SquadUnitsEqual`) `squadUnitsSnapshot` 갱신 + 페이지를 0으로 리셋, 같으면 유지. 전체 페이지 수를 계산해 `squadCurrentPage`를 클램프하고, `UpdateSquadPageButtons`(버튼 활성화)와 `RefreshSquadSlots`(현재 페이지 12마리 렌더링) 호출.
- `SelectSquadPage(page)`: page1~5 버튼 클릭 시 호출되는 public 메서드. 비활성 버튼(유닛이 없는 페이지)은 무시.
- `RefreshSquadSlots()`: `squadCurrentPage * 12`부터 12개씩 `squadSlots`에 채움, 남는 슬롯은 `Clear()`.
- `UpdateSquadPageButtons(pageCount)`: 유닛 수 기준 필요한 페이지까지만 `Button.interactable = true` (예: 36마리 → 1~3페이지만 활성, 4~5페이지는 비활성).
- `HideSquadPanel()`에 `squadUnitsSnapshot.Clear()` / `squadCurrentPage = 0` 추가 — 다음 선택 시 항상 1페이지부터 시작.

## 남은 작업 (에디터에서 사용자가 해야 할 부분)
- Unity 에디터에서 `UIController` 컴포넌트의 `Squad Page Buttons` 배열(크기 5)에 `page1~page5` 버튼을 순서대로 드래그해서 연결해야 실제로 동작함 (스크립트에서 onClick을 자동으로 연결하므로 버튼 오브젝트만 슬롯에 꽂으면 됨).

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
