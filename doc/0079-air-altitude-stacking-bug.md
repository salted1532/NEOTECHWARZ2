# 0079 — 공중유닛이 다른 공중유닛/건물을 따라갈 때 고도가 중첩되던 버그

## 질문
"버그를 하나 발견했는데 공중유닛이 공중유닛을 우클릭 해서 유닛 따라가기를 하면 공중유닛의 좌표 y값 +5가 된 좌표에
+5를 해서 그 공중유닛의 머리위를 올라가버리네 공중유닛, 건물들의 공중제한 값 5로 해서 제한을 뒀을면 좋겠네"

## 원인

`Assets/Scripts/Unit/UnitController.cs`의 공중 유닛 이동 목표 계산이 전부
`목적지 + Vector3.up * 5f` 형태로 "무조건 더하기" 방식이었음. 우클릭으로 다른 유닛을 따라갈 때(`FollowTick()` →
`MoveAgentTo(followTarget.transform.position)`)는 `followTarget`이 지상 좌표가 아니라 **이미 고도 5에 떠 있는 다른
공중유닛의 좌표**라서, 여기에 다시 +5를 더하면 목표 고도가 10이 되어 그 유닛 머리 위로 솟구쳐 올라가 버림. 같은
패턴(`MoveAgentTo` 경유)을 쓰는 지정 공격 추격(`AttackOrderTick`)·아군 강제공격(`FriendlyAttackTick`)도 대상이
공중유닛/이륙한 건물이면 동일하게 겹칠 수 있는 구조였음.

`Assets/Scripts/Building/BuildingController.cs`는 현재 "다른 유닛을 따라가는" 기능이 없어서 정확히 같은 버그는
없었지만, 자유이동(`MoveWhileLifted`)/착륙 비행(`BeginRelocationFlight`)도 똑같이 "목적지 좌표 + liftHeight" 방식이라
구조적으로 같은 종류의 위험을 안고 있었음(요청대로 함께 정리).

## 수정

**"더하기"가 아니라 "고정값으로 고정"** 방식으로 바꿔서, 목적지 좌표의 Y가 무엇이든(지상이든 이미 공중이든) 항상
동일한 순항 고도로 수렴하도록 함.

### `Assets/Scripts/Unit/UnitController.cs`
- `[SerializeField] private float airCruiseAltitude = 5f;` 필드 추가 (인스펙터에서 조절 가능한 "공중 제한 고도").
- `AirTargetPosition(Vector3 destination)` 헬퍼 추가: `new Vector3(destination.x, airCruiseAltitude, destination.z)` —
  X/Z만 목적지를 따라가고 Y는 항상 `airCruiseAltitude`로 고정.
- `목적지 + Vector3.up * 5f` 패턴이 있던 9곳(이동/추적/공격/정지/순찰 시작·끝/Hold 등) 전부를
  `AirTargetPosition(...)` 호출로 교체. `MoveAgentTo()`를 경유하는 이동(따라가기 포함)은 물론, 개별적으로 흩어져
  있던 나머지 지점도 전부 이 헬퍼로 통일해서 앞으로 새 코드가 추가돼도 같은 실수가 반복되지 않게 함.
- 순찰 복귀(`PatrolTick`에서 시작점으로 되돌아가는 분기)는 원래 +5를 아예 안 붙이던 지점이었는데(우연히 버그가 안
  드러났던 케이스), 마찬가지로 `AirTargetPosition(startPoint)`로 통일해서 일관성 확보.

### `Assets/Scripts/Building/BuildingController.cs`
- `cruiseAltitude` 필드 추가: `LiftOff()`에서 `verticalTarget`(자기 지면 위치 기준 + liftHeight)을 계산한 직후
  그 Y값을 저장해둠.
- `MoveWhileLifted()`/`BeginRelocationFlight()`가 더 이상 `destination.y + liftHeight`를 다시 계산하지 않고,
  항상 저장해둔 `cruiseAltitude`로 고정된 Y를 씀. (건물별로 지면 피벗 오프셋이 달라서 유닛처럼 절대값 하나로
  고정하면 안 되므로, 유닛 쪽과 달리 "이번 이륙에서 실제로 도달한 고도"를 기억했다가 그 값으로 고정하는 방식을 씀.)

## 확인 필요 사항
1. 공중유닛 A, B를 각각 배치하고 A를 우클릭 → B를 따라가기 지정했을 때, A가 B의 머리 위로 솟구치지 않고 같은
   고도(5)에서 나란히 따라가는지 확인.
2. 건물을 이륙시킨 뒤 우클릭 이동/착륙을 여러 번 반복해도 고도가 계속 5로 유지되는지(누적되지 않는지) 확인.
3. 지정 추격/아군 강제공격 대상이 공중유닛일 때도 같은 고도로 접근하는지 확인.
