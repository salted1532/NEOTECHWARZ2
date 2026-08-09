# ControlGroupPanel

`Assets/Scripts/UI/ControlGroupPanel.cs`

## 개요

부대(컨트롤 그룹) 선택 버튼을 그룹이 생기고/전멸할 때마다 자동으로 생성·파괴하는 UI 패널. `buttonContainer`에 `HorizontalLayoutGroup`을 미리 배치해두면, "왼쪽부터 그룹번호 오름차순 배치"와 "하나 없어지면 나머지가 왼쪽으로 당겨지는" 동작을 좌표 계산 없이 sibling index만 다시 매겨서 레이아웃 그룹에 맡긴다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `buttonPrefab` | 컨트롤 그룹 버튼 프리팹 (Button + 자식 TextMeshProUGUI 필요) |
| `buttonContainer` | `HorizontalLayoutGroup`이 달린 부모 (Info_panel 위) |
| `rtsController` | 컨트롤 그룹 존재 여부/멤버 수 조회용 (`FindFirstObjectByType`로 캐싱) |
| `groupButtons[10]` | 그룹 번호별로 생성된 버튼 인스턴스 캐시 (없으면 null) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | `RTSUnitController` 참조 캐싱 |
| `Update()` | 좌클릭 중(다운~업)에는 버튼 생성/파괴/재배치를 미룬다 — 클릭 도중 다른 부대가 전멸/생성돼 버튼이 옆으로 밀리면, 마우스를 뗄 때 클릭 대상이 그 자리에 없어 선택이 씹히는 문제가 있었기 때문. 그 외에는 매 프레임 그룹 0~9의 멤버 유무를 확인해 버튼을 생성/파괴하고, 변경이 있으면 `ReorderButtons()` 호출 |
| `CreateButton(groupIndex)` (private) | 버튼 인스턴스화, 라벨 텍스트 설정, 클릭 시 `rtsController.SelectControlGroup(groupIndex)` 연결 |
| `ReorderButtons()` (private) | 그룹번호 오름차순으로 sibling index를 재부여 — `HorizontalLayoutGroup`이 그 순서대로 왼쪽부터 배치해준다 |
| `DisplayNumber(groupIndex)` (private, static) | 인덱스 0~8은 키보드 1~9 그대로, 인덱스 9는 키보드 0에 대응하므로 "0"으로 표시 |

## 연관 컴포넌트

- **RTSUnitController**: `PurgeAndCountControlGroup(i)`로 그룹 멤버 수를 조회하고, 버튼 클릭 시 `SelectControlGroup(i)` 호출
