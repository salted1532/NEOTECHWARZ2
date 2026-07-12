# 0083 — 공중 고도를 절대값 고정이 아니라 "목적지 지면 + 5"로 계산하도록 수정

## 질문
"아니야 지금 언덕을 오르락 내리락 해야하는데 그냥 지금 좌표5로 고정되어있어 y값 0인 지상에선 +5 = 5이고 y값이 -2인
언덕 아래에선 +5 해서 3의 y값을 가져야하는데 지금은 5로 고정되어있어 +5를 제한하라는거지 y값을 5로 고정하라는 말이
아니였어 이를 감안해서 코드 수정해줘"

## 문제

[[0079-air-altitude-stacking-bug]]에서 "다른 공중유닛을 따라가면 고도가 중첩되는" 버그를 고치면서,
`AirTargetPosition()`이 목적지의 Y를 무시하고 **항상 절대값 `airCruiseAltitude`(5)로 고정**하는 방식으로 만들었음
(`BuildingController`도 `cruiseAltitude`라는 이름으로 동일하게 절대 고정). 그런데 이 방식은 "중첩은 막았지만
지형 높이 차이를 완전히 무시"하는 부작용이 있었음 - 언덕 위(Y가 음수인 저지대 등)로 이동해도 무조건 월드 Y=5로만
날아가서, 사용자가 원하는 "그 지점 지면 + 5" 동작이 안 됐음.

## 올바른 규칙

사용자가 원한 건 절대 고정이 아니라 **"제한"** — 즉:
- 목적지가 **순수 지면 좌표**(땅 클릭, 지상 유닛 위치 등)면 → `그 지점의 Y + 5`로 날아가야 함 (지형 높이를 반영).
- 목적지가 **이미 공중에 뜬 대상의 좌표**(다른 공중유닛, 이륙한 건물, 자기 자신의 현재 위치)면 → 거기에 또 5를
  더하면 안 되고 그 값을 그대로 써야 함(0079에서 고친 중첩 방지는 유지).

즉 "더하기 자체를 없애는" 게 아니라 "이미 반영된 곳엔 안 더하고, 안 반영된 곳(지면)엔 그 지점 기준으로 더한다"는
조건부 로직이 필요했음.

## 수정

### `Assets/Scripts/Unit/UnitController.cs`
- `AirTargetPosition(Vector3 destination, bool destinationIsAirborne = false)`로 파라미터 추가.
  - `destinationIsAirborne == false`(기본): `new Vector3(destination.x, destination.y + airCruiseAltitude, destination.z)`
    — 목적지 지면 높이 기준으로 +5.
  - `destinationIsAirborne == true`: `destination`을 그대로 반환 — 이미 공중 고도가 반영된 좌표이므로 추가 안 함.
- `MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)`로 파라미터 추가, 그대로 `AirTargetPosition`에 전달.
- 새 `IsAirborne(MonoBehaviour target)` 헬퍼 추가 — `friendlyTarget`처럼 `UnitController`/`BuildingController` 둘 다
  될 수 있는 대상의 공중 여부를 타입 검사로 판별(`UnitController`면 `isAirUnit`, `BuildingController`면 `IsLifted()`).
- 호출부별로 상황에 맞게 `destinationIsAirborne` 값을 정확히 지정:
  - **지면 기준(false, 그대로 둠)**: 이동 클릭(`MoveTo`), 공격-이동(`AttackMoveTo`), 적 추격(`AttackUnitTarget`/`ChaseTarget`
    - `EnemyController`는 전부 지상이라 항상 지면), 순찰 목적지(`endPoint`), 스폰 시 최초 상승(`Awake`).
  - **이미 공중(true로 변경)**: 아군 강제공격(`AttackFriendlyTarget`/`FriendlyAttackTick` - `IsAirborne(target)`으로 판별),
    따라가기(`FollowUnit`/`FollowTick` - `target.isAirUnit`), 순찰 복귀 지점(`startPoint` - 순찰 시작 시 현재 위치를
    그대로 캡처한 값이라 이미 공중), 제자리 정지(`Attack`/`StopUnit`/`HoldUnit` - 현재 위치 그대로 유지).

### `Assets/Scripts/Building/BuildingController.cs`
- `cruiseAltitude`(이륙 시 확정해서 고정해두던 절대 고도) 필드를 제거.
- `BeginRelocationFlight()`/`MoveWhileLifted()`가 다시 목적지의 실제 지면 Y를 반영하도록 되돌림:
  `flightDestination = new Vector3(destination.x, destination.y + liftHeight, destination.z)`.
- 건물의 이동 목적지는 전부 지면 레이캐스트(`groundHit.point`) 또는 `PlacementSystem`이 계산한 지면 좌표에서만
  오고, 다른 유닛/건물의 "이미 공중에 뜬" 좌표를 목적지로 받는 경로가 없다는 걸 확인했으므로, 매번 다시
  더해도 [[0079-air-altitude-stacking-bug]]에서 고친 중첩 문제가 재발하지 않음.

## 확인 필요 사항
평지(Y=0)와 언덕/저지대(Y가 다른 곳) 양쪽에 공중유닛/이륙한 건물을 이동시켜서, 각각 "그 지점 지면 + 5" 고도로
날아가는지 확인. 그리고 다른 공중유닛을 따라가기/아군 강제공격했을 때 여전히 머리 위로 솟구치지 않는지
([[0079-air-altitude-stacking-bug]] 회귀 여부)도 같이 확인 부탁.
