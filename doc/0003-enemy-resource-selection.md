# 0003. 적/자원 선택 시스템 구현

## 날짜
2026-07-06

## 요청
지금 구현하는게 적선택, ore(자원)선택을 구현하고 싶은데 지금 좌클릭시 적, 자원 선택되고 선택되면 Marker오브젝트 온오프 되도록 하고 해당하는 정보가 Info_panel에 적유닛 아이콘, 체력, 이름 같은 정보가 지금 아군유닛 표시되는거처럼 나오고 자원은 아이콘 이미지랑 자원 이름(Ore, Gas), 자원량 등을 체력 부분에 표시하도록 했으면 좋겠어

## 답변 / 변경사항
기존 `UnitController`/`BuildingController`의 마커·Info_panel 패턴을 그대로 따라 구현.

- **`EnemyController.cs`**: `enemyMarker`, `icon`, `enemyName` 필드 추가. `SelectEnemy()`/`DeselectEnemy()`로 마커 온오프. `IDestructible.Die()` 구현으로 사망 시 선택 목록 정리.
- **`ResourceNode.cs`**: `resourceMarker`, `icon` 필드 및 `RemainingAmount` 프로퍼티 추가. `SelectResource()`/`DeselectResource()` 추가. 채취 중 고갈되어 파괴될 때 선택 상태가 유령 참조로 남지 않도록 정리 로직 추가.
- **`RTSUnitController.cs`**: `selectedEnemyList`, `selectedResourceNode` 필드 추가. `ClickSelectEnemy`/`ClickSelectResource` 진입점 추가(둘 다 항상 단일 선택). `DeselectAll()`이 적/자원 선택도 함께 해제하도록 확장. `UpdateUI()`에 `EnemySelect`/`OreSelect` 케이스 추가 — 적은 기존 `ShowInfoPanel`(아이콘/이름/체력) 재사용, 자원은 새 `ShowResourceInfoPanel`(아이콘/"Ore" or "Gas"/남은 채취량) 사용. 커맨드 버튼 패널은 둘 다 없음.
- **`UIController.cs`**: `ShowResourceInfoPanel(icon, resourceName, remainingAmount)` 추가 — 체력 텍스트 자리에 남은 자원량 표시, HealthManager 구독 해제.
- **`UserControl.cs`**: 비어있던 "적 클릭"/"광물 클릭"/"가스 클릭" 블록을 채워 각각 `ClickSelectEnemy`/`ClickSelectResource` 호출.

## 변경 파일
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Resource/ResourceNode.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 비고
인스펙터에서 각 적/자원 프리팹에 `enemyMarker`/`resourceMarker`(테두리 표시용 오브젝트)와 `icon`을 연결해야 실제로 마커/아이콘이 표시됨.
