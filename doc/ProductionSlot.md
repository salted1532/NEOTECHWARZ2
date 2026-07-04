# ProductionSlot

`Assets/Scripts/UI/ProductionSlot.cs`

## 개요

커맨드 패널/생산 대기열의 버튼 슬롯 하나를 표현. 아이콘 표시, 클릭 콜백 연결, 비활성/초기화 처리를 담당하는 재사용 가능한 UI 슬롯 컴포넌트.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `button` | `Button` (SerializeField) | 슬롯의 클릭 버튼 |
| `iconImage` | `Image` (SerializeField) | 슬롯에 표시할 아이콘 |
| `callback` | `Action` | 클릭 시 실행할 콜백 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 버튼/아이콘 참조가 비어있으면 자동 탐색, 버튼 클릭 리스너를 `OnClick`으로 연결 |
| `SetData(UIController.CommandButtonData data)` | 슬롯에 데이터를 표시한다 (아이콘/활성화 여부/콜백 설정 후 슬롯을 켠다) |
| `Clear()` | 슬롯을 초기화하고 비활성화한다 (아이콘 제거, 콜백 해제, 게임오브젝트 비활성화) |
| `OnClick()` (private) | 등록된 `callback`을 호출 |

## 연관 컴포넌트

- **UIController**: `CommandButtonData`를 만들어 각 `ProductionSlot`(커맨드 패널) 및 `queueSlots`(생산 대기열)에 `SetData`/`Clear` 호출
