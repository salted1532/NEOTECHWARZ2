# 0014. 아군 강제 공격에 건물 포함

## 날짜
2026-07-07

## 요청
이제 아군 강제공격을 할떄 아군 건물도 강제공격할수 있도록 해줘

## 원인 조사 중 추가로 발견한 버그
`BuildingController`도 `UnitController`(0009)와 동일하게 `IDestructible`을 구현하지 않고 있었음 — 건물이 지금까지는 전투로 파괴될 일이 거의 없어서 드러나지 않았지만, 이번에 건물을 공격 대상으로 삼을 수 있게 되면서 그대로 두면 0009와 같은 `MissingReferenceException`이 재발할 것이 확실해 함께 수정함.

## 답변 / 변경사항
**`UnitController.cs`**
- `friendlyTarget` 필드 타입을 `UnitController` → `MonoBehaviour`로 일반화 (유닛/건물 둘 다 `.transform`/`.gameObject`만 있으면 되므로).
- `AttackFriendlyTarget(UnitController target)` → `AttackFriendlyTarget(MonoBehaviour target)`로 시그니처 변경 (`UnitController`/`BuildingController` 모두 암시적으로 전달 가능). `FriendlyAttackTick()` 로직은 변경 없음(그대로 `.transform`/`.gameObject` 사용).

**`RTSUnitController.cs`**
- `AttackFriendlyBuildingSelectedUnits(BuildingController target)` 추가 — 선택된 유닛들이 지정 건물을 강제 공격하도록 지시.

**`BuildingController.cs`**
- `IDestructible` 구현 추가 (`class BuildingController : MonoBehaviour, IDestructible`) — 0009와 동일한 원인의 잠재 버그 수정. 건물이 파괴되면 `HealthManager.Die()`가 이제 정상적으로 `BuildingController.Die()`를 호출해 `BuildingList`/`selectedBuildingList`에서 제거됨.
- `RTSUnitController` 참조를 `rtsController` 필드로 캐싱(기존엔 `Start()`/`Die()`에서 각각 `FindFirstObjectByType` 호출).
- `FlashMarker()` 추가 — Enemy/ResourceNode/UnitController와 동일한 패턴으로 `buildingMarker`가 0.3초 간격 3번 깜빡인 뒤 선택 상태로 복원.

**`UserControl.cs`**
- 좌클릭 건물 처리에서 `UsercurrentState == OrderState.Attack`(A 모드)이면 선택 대신 `AttackFriendlyBuildingSelectedUnits` + `FlashMarker`를 호출하도록 분기 추가. 아니면 기존처럼 선택.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
