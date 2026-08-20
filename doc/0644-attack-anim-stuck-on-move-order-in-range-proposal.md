# 0644 - AttackRange 안에서 이동명령을 내려도 공격 애니메이션이 안 풀림 (원인 확인 + 제안)

## 요청
샤프슈터(및 사거리 기반 자동교전 유닛 전반)가 AttackRange 사거리 안에 적이 있는 상태에서 이동 명령(예: 후퇴)을 내려도 걷는 애니메이션으로 안 바뀌고 공격 모션(실제 발사는 안 해도 Fire 포즈)을 유지한 채 이동함. 확인 요청.

## 확인됨 - 원인
`UnitController.IsAttack()`(`Assets/Scripts/Unit/UnitController.cs:2136`):
```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack || (attackRange != null && attackRange.HasEnemyInRange);
```
`UnitAnimatorDriver.Update()`(`Assets/Scripts/Animation/UnitAnimatorDriver.cs:32`)가 매 프레임 이 값으로 Animator의 `Fire` bool을 그대로 세팅한다.

`HasEnemyInRange`(`AttackRange.cs:26`)는 순수하게 "감지 콜라이더(트리거) 안에 적이 있는가"만 보는 값이라 유닛의 현재 명령/상태와 무관하다. 감지 콜라이더 반경은 `UnitRange + 5`(마진, `AttackRange.EnsureDetectionRadius()`)로 실제 사거리보다 더 넓다.

`MoveTo()`(`UnitController.cs:585`)는 `UnitcurrentState`를 `Move`로 바꾸지만 `attackRange`의 감지 목록은 건드리지 않는다. 그래서 이동명령을 내린 직후에도 적이 감지 반경(사거리+5) 안에 남아있는 동안은 `HasEnemyInRange`가 계속 true → `IsAttack()`도 계속 true → `Fire` 애니메이션이 안 풀리고, 실제로는 적이 감지 반경을 완전히 벗어나야만(사거리보다 한참 더 물러나야) 비로소 걷는 애니메이션으로 바뀐다. 보고된 증상과 정확히 일치.

`IsAttack()`에 `HasEnemyInRange`를 OR로 넣어둔 이유는 주석(2133~2135줄)대로 패시브 자동교전(Idle 상태 유지, `AttackMoveTo`/`FollowUnit`/`FollowBuilding` 등)에서 실제 사격 중임을 상태값만으로는 못 잡기 때문 - 이 경우 `UnitcurrentState`가 계속 `Idle`로 유지되므로 `HasEnemyInRange`를 같이 봐야 한다. 문제는 이 OR 조건이 `Move`/`Attack` 상태에서도 무조건 적용된다는 것 - `Move` 상태(명시적 이동/후퇴 명령)에서도 적이 감지 범위에 남아있으면 그대로 새어 들어간다.

## 제안 설계
`IsAttack()`을 `HasEnemyInRange` 체크가 `Idle` 상태에서만 적용되도록 좁힌다:
```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack || (UnitcurrentState == UnitState.Idle && attackRange != null && attackRange.HasEnemyInRange);
```
- `Attack` 상태: 그대로 항상 true (명시적 공격 명령/실제 교전 중).
- `Idle` 상태: 기존과 동일하게 `HasEnemyInRange`로 패시브 자동교전을 잡아낸다 - 동작 변화 없음.
- `Move` 상태(이 문제의 핵심): 적이 감지 범위 안에 남아있어도 더 이상 true가 되지 않는다 - 이동 명령을 내리는 즉시 `Fire`가 꺼지고 `IsMoving`이 걷는 애니메이션을 보여준다.
- `UnitState`는 Idle/Move/Attack 3개뿐이라 다른 상태를 추가로 고려할 필요 없음.

`EnemyUnitController`/`AllyController`의 `IsAttack()`(`attackRange.HasTargetInAttackRange`만 보고 상태 구분이 아예 없음)은 같은 패턴이지만 이번 요청은 아군 유닛(샤프슈터 등, `UnitController`) 한정이라 범위 밖으로 둠 - 필요하면 후속으로 별도 확인.

## 범위 밖
- `EnemyUnitController`/`AllyController` 쪽 동일 패턴 수정 - 이번 보고는 아군 유닛 한정.
- `AttackRange.Update()`의 자동교전 로직 자체 변경 - 애니메이션 표시 조건만 고치는 것으로 충분.

## 구현 완료
`UnitController.cs:2136`의 `IsAttack()`을 제안대로 수정 - `HasEnemyInRange` 체크에 `UnitcurrentState == UnitState.Idle` 조건 추가. `AttackRange.Update()`의 자동교전 게이트는 `IsAttackOrderState()`(상태만 보는 별도 메서드, doc/0464에서 이미 분리됨)를 쓰므로 이번 수정과 무관 - 이동/교전 판정 로직은 그대로, 애니메이션 표시만 고쳐짐. 컴파일 성공(에러 0, 기존 경고 49개는 무관).

## 상태
완료. AttackRange 사거리(감지범위) 안에 적이 있어도 이동 명령을 내리면 즉시 Fire 애니메이션이 꺼지고 걷는 애니메이션으로 바뀐다. Idle 상태에서의 패시브 자동교전 애니메이션은 기존과 동일하게 동작.
