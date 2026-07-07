# 0022 - 자원(Ore/Gas) 선택 시 AttackDamageImage/ArmorImage 숨김

**날짜:** 2026-07-08

## 요청 내용
Ore/Gas 같은 자원 노드를 선택했을 때는 Info_panel의 `AttackDamageImage`/`ArmorImage`가 아예 보이지 않도록 해달라는 요청.

## 조사 내용
- Info_panel 진입점은 세 가지: `ShowInfoPanel(icon, name, health)`(건물/자원 겸용 3인자, 내부적으로 5인자에 0/0 위임), `ShowInfoPanel(icon, name, health, attackDamage, armor)`(유닛/적 전용 5인자, [[0019]]/[[0021]]), `ShowResourceInfoPanel(icon, name, remainingAmount)`(자원 전용, 체력 대신 채취량 표시).
- 지금까지는 `attackDamageImage`/`armorImage`가 항상 활성 상태로 남아있어서, 자원을 선택해도 이전에 표시되던 유닛의 공격력/방어력 아이콘이 그대로 보이는(또는 0으로 보이는) 문제가 있었음.

## 변경 내용 (`Assets/Scripts/UI/UIController.cs`)
- `SetCombatStatsVisible(bool visible)` private 헬퍼 추가: `attackDamageImage`/`armorImage`의 GameObject를 통째로 `SetActive`.
- `ShowInfoPanel(icon, name, health, attackDamage, armor)`(유닛/적 선택 경로)에서 `SetCombatStatsVisible(true)` 호출 — 아이콘이 보이고 호버 툴팁도 정상 동작.
- `ShowResourceInfoPanel(icon, name, remainingAmount)`(Ore/Gas 선택 경로)에서 `SetCombatStatsVisible(false)` 호출 — 두 아이콘이 완전히 비활성화되어 안 보임.
- 이미지가 `SetActive(false)`여도 이미 붙어있는 `EventTrigger`(호버 툴팁, [[0019]])는 비활성 상태에선 어차피 이벤트를 받지 않으므로 별도 처리 불필요.
- 건물 선택(3인자 오버로드, 공격력/방어력 0으로 위임)은 이번 요청 범위가 아니라 그대로 둠 — 여전히 아이콘이 보이고 0으로 표시됨.

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
