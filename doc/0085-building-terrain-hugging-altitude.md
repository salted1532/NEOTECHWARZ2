# 0085 — 이륙한 건물도 지형 추적 비행(언덕 감지) 적용

## 질문
"그럼 건물에도 이 매커니즘 추가해줘 건물도 지면을 감지해서 움직이도록"

## 내용

[[0084-air-unit-terrain-hugging-altitude]]에서 공중 유닛에 적용한 "매 프레임 발밑 지형을 다시 재서 그 고도를
따라가는" 방식을 `BuildingController`(이륙한 건물)에도 동일하게 적용. 건물의 `UpdateLiftedMovement()`는 이미
[[0078-building-lift-move-vertical-not-rising]]에서 X/Z와 Y를 독립적으로 보간하는 구조였어서, Y 쪽 목표 계산만
공중 유닛과 같은 방식으로 바꾸면 됐음.

## 수정

### `Assets/Scripts/Building/BuildingController.cs`
- 새 필드 `[SerializeField] private LayerMask groundLayer;` 추가 (공중 유닛의 `airGroundLayer`와 동일한 역할).
  비워두면(0) 기존처럼 목적지 고도로 곧장 직선 이동(하위 호환 유지).
- 새 헬퍼 `SampleGroundHeight(Vector3 xzPosition, float fallback)` 추가 - `UnitController`의 동명 메서드와 동일한
  구현(발밑 X/Z에서 아래로 레이캐스트).
- `UpdateLiftedMovement()`의 `isFlyingToDestination` 단계에서 Y 목표를
  `flightDestination.y`(목적지 고도, 미리 계산된 고정값) 대신
  `SampleGroundHeight(pos, ...) + liftHeight`(매 프레임 재계산되는 값)로 변경.
  나머지(수평 이동, 도착 판정, 상승/하강 단계)는 그대로 유지.

### 프리팹
`groundLayer`도 새 필드라 값을 채워야 실제로 동작함. 건물 프리팹 6개 전부(`canLift` 켜져 있는지와 무관하게 -
나중에 켤 수도 있으므로) `Ground` 레이어(비트값 128)로 채움:
- `MainBase.prefab`, `Tier1.prefab`, `Tier2.prefab`, `Tier3.prefab`(이상 `canLift: 1`)
- `SupplyDepot.prefab`, `Lab.prefab`(이상 현재 `canLift: 0`이지만 필드는 미리 채워둠)

## 확인 필요 사항
`canLift`가 켜진 건물(MainBase/Tier1/Tier2/Tier3)을 이륙시켜서 언덕 위에서 저지대로(또는 반대로) 이동시켰을 때,
[[0084-air-unit-terrain-hugging-altitude]]에서 확인한 것과 동일하게 언덕 능선을 실제로 벗어나는 시점에 맞춰
고도가 바뀌는지 확인 부탁.
