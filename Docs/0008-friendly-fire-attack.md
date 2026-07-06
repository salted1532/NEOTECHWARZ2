# 0008. 아군 강제 공격(Friendly Fire) 구현

## 날짜
2026-07-07

## 요청
이번엔 아군 사격(공격)을 구현할건데 A로 공격모드로 들어가고 아군을 좌클릭하면 아군을 강제공격하는 거야
적유닛 선택공격은 시야에서 사라지면 공격명령으로 그 위치를 가지만 이번엔 아군유닛이기 때문에 끝까지 그 유닛을 따라가서 공격하도록 만들어줘

## 답변 / 변경사항
**`UnitController.cs`**
- `friendlyTarget`(강제 공격 대상), `hasFriendlyOrder`(대상 사망 시 정지 상태 정리를 위한 플래그) 필드 추가.
- `AttackFriendlyTarget(UnitController target)` — A 모드에서 아군 좌클릭 시 호출. `Attack` 상태로 전환하고 대상에게 이동.
- `FriendlyAttackTick()` (매 프레임): `AttackRange`는 "Enemy" 태그만 감지하므로 아군 전투는 직접 처리 — 사거리 안이면 `Attack()` 호출(데미지), 밖이면 **거리 제한 없이 끝까지 추격** (적 추격의 `chaseLoseSightRange`/시야 이탈 개념 없음). 대상이 죽어 파괴되면 그 프레임에 정지 상태를 풀고 `Idle`로 복귀 (`hasFriendlyOrder` 플래그로 "막 끝난" 전이 시점 판별 — Unity의 fake-null 때문에 플래그 없이는 판별 불가).
- `CancelAttackOrder()`에 `friendlyTarget`/`hasFriendlyOrder` 초기화 추가. `AttackUnitTarget`/`AttackMoveTo`도 `friendlyTarget`을 초기화해 서로 배타적으로 만듦.
- 도착/정지 가드 조건에 `friendlyTarget == null` 추가.
- `FlashMarker()` 추가 — 공격 대상으로 지정된 아군의 마커가 0.3초 간격 3번 깜빡인 뒤 실제 선택 상태로 복원.

**`RTSUnitController.cs`**
- `AttackFriendlySelectedUnits(UnitController target)` 추가 — 대상 자신이 선택 목록에 있어도 자기 자신은 공격하지 않게 스킵.

**`UserControl.cs`**
- 좌클릭 유닛 처리에서 `UsercurrentState == OrderState.Attack`(A 모드)이면 선택 대신 `AttackFriendlySelectedUnits` + `FlashMarker` 호출.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
