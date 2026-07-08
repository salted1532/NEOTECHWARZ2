# ProductionSlot

`Assets/Scripts/UI/ProductionSlot.cs`

## 개요

커맨드 패널/생산 대기열의 버튼 슬롯 하나를 표현. 아이콘 표시, 클릭 콜백 연결, 비활성/초기화 처리, 호버 시 툴팁 표시, 그리고 **자기 키보드 단축키를 스스로 감지해 클릭을 재현**하는 것까지 담당하는 재사용 가능한 UI 슬롯 컴포넌트. `Clear()`되면 `gameObject` 자체가 비활성화돼 `Update()`가 호출되지 않으므로, "지금 이 버튼이 안 보이면 그 단축키도 자동으로 죽어있다"가 별도 상태 분기 없이 성립한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `button` | `Button` (SerializeField) | 슬롯의 클릭 버튼 |
| `iconImage` | `Image` (SerializeField) | 슬롯에 표시할 아이콘 |
| `callback` | `Action` | 클릭 시 실행할 콜백 |
| `data` | `UIController.CommandButtonData` | 현재 표시 중인 데이터(툴팁 등에 사용) |
| `hasData` | `bool` | 데이터가 채워져 있는지 |
| `shortcut` | `KeyCode` | 이 슬롯을 대신 "누르는" 키보드 단축키 (없으면 `KeyCode.None`) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 버튼/아이콘 참조가 비어있으면 자동 탐색, 버튼 클릭 리스너를 `OnClick`으로 연결 |
| `SetData(UIController.CommandButtonData data)` | 슬롯에 데이터를 표시한다 (아이콘/활성화 여부/콜백/단축키 설정 후 슬롯을 켠다) |
| `Clear()` | 슬롯을 초기화하고 비활성화한다 (아이콘 제거, 콜백/단축키 해제, 게임오브젝트 비활성화) |
| `OnClick()` (private) | 등록된 `callback`을 호출 |
| `Update()` (private) | 슬롯이 활성화(`hasData`)돼 있고 단축키가 있으며 버튼이 `interactable`일 때만, 해당 `KeyCode`가 `GetKeyDown`되면 `SimulateClickRoutine` 코루틴을 실행 |
| `SimulateClickRoutine()` (private, IEnumerator) | 실제 마우스 클릭과 동일한 이벤트를 코드로 재현: `ExecuteEvents.pointerDownHandler` → 0.08초 대기 → `pointerUpHandler` + `pointerClickHandler`. 버튼에 이미 설정된 눌림 색상/스프라이트 Transition이 그대로 재생되고, `pointerClickHandler`가 `Button.onClick`(=`callback`)을 호출한다 |
| `OnPointerEnter(eventData)` | 호버 시 `TooltipUI.Instance.Show(...)` 호출 (비용 유무에 따라 오버로드 분기) |
| `OnPointerExit(eventData)` | 호버 종료 시 `TooltipUI.Instance.Hide()` |

## 연관 컴포넌트

- **UIController**: `CommandButtonData`(단축키 포함)를 만들어 각 `ProductionSlot`(커맨드 패널) 및 `queueSlots`(생산 대기열)에 `SetData`/`Clear` 호출
- **TooltipUI**: 호버 시 툴팁 표시/숨김
- **UserControl**: 예전에는 여기서 처리하던 커맨드 단축키가 전부 이 컴포넌트로 이전됨
