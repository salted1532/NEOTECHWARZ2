# 0244 - 적 건물 선택 기능 (아이콘/체력/이름만 표시)

## 요청

적 건물([[0243]]의 껍데기)을 선택하면 아이콘/체력/이름만 Info_panel에 보이면 됨. 건물은 공격을 하지
않으므로 공격력/방어력은 표시할 필요 없음.

## 구현

기존 "적 유닛 선택"(`EnemyUnitController`/`RTSUnitController.selectedEnemyList`)과 "아군 건물 선택"
패턴을 그대로 따라감 - 다만 건물이라 항상 단일 선택(리스트가 아니라 필드 하나)으로 처리.

**`Assets/Scripts/Enemy/EnemyBuildingController.cs`**
- `buildingMarker`(선택 표시), `SelectEnemyBuilding()`/`DeselectEnemyBuilding()` 추가
  (`EnemyUnitController.enemyMarker`와 동일한 패턴)
- `rtsController` 참조 추가, `Die()`에서 `ClearSelectedEnemyBuildingIfMatches()` 호출 - 선택된 채로
  파괴돼도 UI가 유령 참조를 들고 있지 않게 함

**`Assets/Scripts/System/RTSUnitController.cs`**
- `SelectState`에 `EnemyBuildingSelect` 추가
- `selectedEnemyBuilding` 필드 추가 (`selectedBaseStructure`/`selectedResourceNode`와 동일하게 단일 선택)
- `ClickSelectEnemyBuilding()`/`SelectEnemyBuilding()`/`ClearSelectedEnemyBuildingIfMatches()` 추가
- `DeselectAll()`에 정리 로직 추가
- `UpdateUI()`에 `EnemyBuildingSelect` 케이스 추가 - **3-인자 `ShowInfoPanel(icon, name, health)`**
  오버로드 사용 (아군 건물 Info_panel과 동일한 호출) → 공격력/방어력 없이 아이콘/이름/체력만 표시

**`Assets/Scripts/UserControl/UserControl.cs`**
- 좌클릭 처리 중 "2. 적 클릭" 블록 안에, `EnemyUnitController`가 없을 때(즉 유닛이 아니라 건물일 때)
  `EnemyBuildingController`를 찾아 선택만 처리하는 분기 추가. A모드 강제공격 처리는 없음(건물은 공격
  대상 지정이 필요 없음 - 자동 감지/공격은 기존 `AttackRange`가 이미 담당).

`Enemy` 레이어/태그를 쓰기로 확정했으므로([[0243]] 관련 대화) 별도 레이어 라우팅 코드 없이
`layerEnemy` 레이캐스트 경로 그대로 재사용.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyBuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
