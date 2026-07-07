# 0020 - TooltipUI 배경 크기를 내용에 맞게 축소 (Compact 모드)

**날짜:** 2026-07-07

## 요청 내용
[[0019]]에서 만든 공격력/방어력 호버 툴팁은 title 텍스트에 "Attack Damge : N" 한 줄만 채워 넣고 description은 비워두는 방식인데, 기존 Tooltip 배경 이미지(`ToolTip` GameObject의 `Image`, `TooltipUI.root`와 동일 오브젝트)는 항상 고정 크기(200x100)라 아래쪽에 불필요한 여백이 남음. 실제로 채워진 내용(제목 한 줄)만큼만 배경 크기를 줄여달라는 요청. "적용해보고 이상하면 되돌려달라고 하겠다"는 전제로 진행.

## 조사 내용
- `Assets/Scenes/SampleScene.unity`에서 `TooltipUI` 컴포넌트(및 `root`로 연결된 `ToolTip` GameObject)의 실제 배치를 확인:
  - `root`(배경 Image와 동일 GameObject) `sizeDelta = (200, 100)`, pivot/anchor 모두 (0.5, 0.5).
  - 자식들은 전부 `root` pivot 기준 절대 `anchoredPosition`으로 배치되어 있고 **Layout Group/ContentSizeFitter는 전혀 없음** (title `(15, 35)` size `(200,20)`, description `(15, 3)` size `(200,40)`, 비용 3줄이 `y=-30` 부근에 배치).
  - 즉 `root.sizeDelta`만 줄이면 자식들의 고정 좌표는 그대로라 제목이 새 배경 바깥으로 삐져나가는 문제가 생김 — 단순 크기 조절만으로는 안 됨.
- 씬에 Layout Group을 새로 붙이는 방식은 기존에 이미 잘 동작 중인 일반 명령/생산 버튼 툴팁(제목+설명[+비용])의 배치를 깨뜨릴 위험이 커서 배제. 대신 `TooltipUI.cs` 코드에서만 처리하는 방식을 선택 (되돌리기도 파일 하나만 되돌리면 되어 요청사항인 "이상하면 되돌려달라"에 부합).

## 변경 내용 (`Assets/Scripts/UI/Tooltip/TooltipUI.cs`)
- `Awake()`에서 `root.sizeDelta`와 `titleText`의 원래 `anchoredPosition`을 `defaultRootSize`/`defaultTitlePosition`으로 캐싱 (씬에 설정된 값을 그대로 기준으로 삼아, 하드코딩된 매직 넘버 없이 항상 현재 디자인 값을 기준으로 동작).
- `ShowInternal()`에서 `description`이 비어 있고 `hasCost`도 false인 경우("Attack Damge : N"/"Armor : N"처럼 제목 한 줄만 있는 경우)를 `isCompact`로 판단.
- `ApplyCompactLayout(bool isCompact)` 추가:
  - `isCompact`가 아니면(기존처럼 설명/비용이 있는 일반 툴팁) `root.sizeDelta`와 title 위치를 캐싱해둔 기본값으로 복원 — 기존 명령/생산 버튼 툴팁은 동작·모양 변화 없음.
  - `isCompact`면 제목을 세로 중앙(`y=0`)으로 옮기고, `root.sizeDelta.y`를 `title 높이 + compactVerticalPadding`(기본 10, 인스펙터에서 조절 가능)으로 축소. 가로 폭은 요청 범위가 세로쪽("밑에 부분")이라 기존 폭(200)을 유지.
- 매 `Show()` 호출마다 컴팩트/일반 여부를 다시 계산해 적용하므로, 같은 툴팁 인스턴스가 공격력/방어력 호버와 일반 명령 버튼 호버 사이를 오가도 항상 올바른 크기로 전환됨.

## 되돌리는 방법 (참고)
`Assets/Scripts/UI/Tooltip/TooltipUI.cs` 한 파일만 git으로 되돌리면 이번 변경 전 상태(고정 200x100 배경)로 복원됨.

## 변경된 파일
- `Assets/Scripts/UI/Tooltip/TooltipUI.cs`
