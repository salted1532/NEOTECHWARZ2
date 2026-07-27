# 0248 - 적 건물 지정 공격(우클릭/A 모드) + 마커 깜빡임

## 요청

적 건물도 우클릭이나 A공격 명령으로 지정 공격할 수 있게 해주고, 그렇게 됐을 때 마커가 깜빡이는 부분도
아군 건물/적 유닛과 똑같이 구현해달라는 요청. [[0244]]에서는 "건물은 공격을 안 하니 A모드 강제공격 대상
지정은 없음"으로 선택만 구현해뒀는데, 이번 요청은 그 반대(플레이어가 적 건물을 공격 대상으로 지정하는
것) - 이미 아군 건물(`BuildingController`)에 대해 존재하는 "아군 강제 공격" 메커니즘과 적 유닛
(`EnemyUnitController`)의 마커 깜빡임 패턴을 그대로 적용하면 되는 구조였음.

## 구현 방식: 새 상태머신 없이 기존 `AttackFriendlyTarget` 재사용

`UnitController.AttackFriendlyTarget(MonoBehaviour target)`는 이름은 "아군 강제 공격"이지만 실제로는
"고정된 대상 하나를 시야 이탈 개념 없이 파괴될 때까지 끝까지 쫓아가며 공격"하는 범용 메커니즘이라
`BuildingController`/`BaseStructure`(아군 건물) 양쪽에서 이미 재사용되고 있었음. 적 건물도 안 움직이는
대상이라는 점에서 완전히 동일한 요구사항이라, 새로 상태를 추가하지 않고 이 메서드를 그대로 재사용함.
대상 타입 판정(`IsAirborne`/`GetTargetSizeType`/`GetTargetArmorType`/`IsTargetAirborne`)도 전부
`UnitController`/`EnemyUnitController`/`BuildingController`가 아니면 안전한 기본값(Ground, Medium
크기, Light 장갑)으로 떨어지도록 이미 구현돼 있어서, `EnemyBuildingController`를 위한 별도 분기가 필요
없었음.

## 수정 내용

**`Assets/Scripts/System/RTSUnitController.cs`**
- `AttackEnemyBuildingSelectedUnits(EnemyBuildingController target)` 추가 - 선택된 유닛 전원에게
  `AttackFriendlyTarget(target)` 호출 (`AttackFriendlyBuildingSelectedUnits`와 동일한 패턴).

**`Assets/Scripts/Enemy/EnemyBuildingController.cs`**
- `flashInterval`/`flashCount`/`flashRoutine` 필드 + `FlashMarker()`/`FlashMarkerRoutine()` 추가
  (`EnemyUnitController`의 마커 깜빡임과 동일한 패턴). 깜빡임이 끝나면 `rtsController.selectedEnemyBuilding
  == this`([[0244]]에서 만든 단일 선택 필드) 조건으로 마커를 원래 선택 상태로 복원.
- `IEnumerator` 사용을 위해 `using System.Collections;` 추가.

**`Assets/Scripts/UserControl/UserControl.cs`**
- 좌클릭(`HandleLeftClick`) "2. 적 클릭" 블록의 적 건물 분기: 기존엔 선택만 했는데, `UsercurrentState ==
  OrderState.Attack`(A 모드)일 때 `AttackEnemyBuildingSelectedUnits` + `FlashMarker()` + 공격 포인터
  표시 분기를 추가(아군/적 유닛, 아군 건물과 동일한 패턴). 더 이상 사실과 다른 옛 주석("건물은 공격을
  안 하므로...")도 갱신함.
- 우클릭(`HandleRightClick`) "1. 적 우클릭" 블록: 기존엔 `EnemyUnitController`만 처리하고 적 건물은
  아예 다루지 않아 우클릭이 아무 반응도 안 했음. 적 유닛 분기 바로 다음에 `EnemyBuildingController` 분기를
  추가 - 안개로 가려지지 않았으면 `AttackEnemyBuildingSelectedUnits` + `FlashMarker()`, 가려졌으면 그
  지점으로 이동만(적 유닛 우클릭과 동일한 안개 처리 패턴).

## 변경 파일

- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Enemy/EnemyBuildingController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
