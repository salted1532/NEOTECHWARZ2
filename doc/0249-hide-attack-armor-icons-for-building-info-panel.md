# 0249 - 건물 선택 시 공격력/방어력 아이콘 숨김 (아군/적 둘 다)

## 요청

건물 선택 시 정보(아이콘/이름/체력)는 잘 나오는데 공격력·방어력 아이콘도 같이 나온다는 문제 - 유저가
직접 가리는 용도의 "black" 이미지를 준비해뒀으니, 적 건물 선택 시엔 그걸 활성화해서 가리거나, 아니면
아이콘 자체를 끄는 방식으로 구성해달라는 요청 (적 유닛 선택 시엔 반대로 black을 꺼서 보이게).

## 원인: 건물용 오버로드가 "0으로 표시"만 하고 안 숨겼음

`UIController`에는 이미 공격력/방어력 아이콘 자체를 껐다 켰다 하는 `SetCombatStatsVisible(bool)`이
있었고, 자원(`ShowResourceInfoPanel`)과 건설 중인 구조체(`ShowBaseStructureInfoPanel`)는 이미
`SetCombatStatsVisible(false)`로 아이콘을 완전히 숨기고 있었음. 그런데 건물 선택용 3-인자
`ShowInfoPanel(icon, unitName, health)` 오버로드는 내부에서 `ShowInfoPanel(icon, unitName, health, 0,
0)`(5-인자 버전)을 호출할 뿐이었고, 5-인자 버전은 항상 `SetCombatStatsVisible(true)`를 호출해서
아이콘을 켜버렸음 - 그래서 공격력/방어력이 "0"인 채로 아이콘만 보이는 상태가 됐던 것.

이 3-인자 오버로드는 아군 건물(`BuildingController`, `RTSUnitController.cs:1415`)과 적 건물
(`EnemyBuildingController`, `RTSUnitController.cs:1532`) 딱 두 곳에서만 쓰이고 있어서(둘 다 "공격력/
방어력 개념이 없는 대상"), 이 오버로드 한 곳만 고치면 아군/적 건물 둘 다에 대칭으로 적용됨. 굳이 "black"
이미지로 가리는 방식보다, 아이콘 게임오브젝트 자체를 꺼버리는 기존 인프라(`SetCombatStatsVisible`)를
그대로 재사용하는 쪽이 더 깔끔해서 이 방식으로 구현함(호버 툴팁 트리거도 같이 꺼져서 부작용도 없음).

## 수정 내용

**`Assets/Scripts/UI/UIController.cs`**
- `ShowInfoPanel(Sprite icon, string unitName, HealthManager health)`: 기존 5-인자 위임 호출 뒤에
  `SetCombatStatsVisible(false)`를 추가해서 위임 호출 안에서 켜졌던 아이콘을 다시 끔.

## 참고

- 유저가 준비해둔 "black" 가리개 이미지는 이제 안 써도 됨(아이콘 자체가 비활성화되므로) - 필요 없으면
  그대로 두거나 나중에 정리해도 무방, 씬/프리팹은 손대지 않았음.
- 유닛 선택(`ShowInfoPanel`의 5-인자 버전, 아군 유닛/적 유닛 둘 다)은 이미 `SetCombatStatsVisible(true)`
  경로를 그대로 타므로 기존처럼 공격력/방어력 아이콘이 정상적으로 보임 - 손대지 않음.

## 변경 파일

- `Assets/Scripts/UI/UIController.cs`
