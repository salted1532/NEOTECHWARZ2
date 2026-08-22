# 0662 - 메딕 드론 "땅공격(A+땅클릭)" 중 치유 후 목적지 재이동 - 버그 수정

## 요청
> 메딕이 힐을 하고 있는 중에는 공격유닛의 공격과 같이 멈춰서 힐을 하고, 땅공격이였을 경우 해당
> 위치로 가도록 하는식으로 - 힐중일땐 정지하고 힐 했으면 좋겠어.

## 원인 (버그였음)
"땅공격"은 `UnitController.AttackMoveTo(Vector3 destination)`(A모드에서 땅 클릭) - `attackMoveDestination`을
저장해두고 `UnitcurrentState = Idle`로 이동을 시작한다. 매 프레임 `AttackOrderTick()`이 "교전 중이면
가만히 두고, 교전이 끝나면(`attackRange.HasEnemyInRange == false`) 다시 `attackMoveDestination`으로
이동을 재개"한다 - 전투 유닛은 이 로직 하나로 "가다가 사거리에 적 들어오면 멈춰서 싸우고, 끝나면
계속 감"이 전부 처리된다.

문제는 이 재개 판정이 `attackRange.HasEnemyInRange`만 본다는 것 - 메딕 드론은 `AttackRange`가 아예
없고(`HealRange`만 있음, doc/0661) `attackRange` 필드가 항상 `null`이라 `attackRange != null && ...`가
항상 `false`가 되어 **이 가드가 전혀 작동하지 않았다**. 그 결과:
- `HealRange.Update()`가 사거리 안 다친 아군을 찾아 `BeginHeal()`을 호출 → `navMeshAgent.isStopped = true`로 정지.
- 바로 같은/다음 프레임에 `AttackOrderTick()`이 "교전 중 아님"으로 오판(가드가 항상 통과) → 즉시
  `MoveAgentTo(attackMoveDestination)`를 다시 호출해서 이동을 재개(`isStopped = false`).
- 두 로직이 매 프레임 서로를 덮어써서 제자리에서 멈추지도 못하고 미세하게 떨거나 치유가 제대로 안
  걸리는 상태가 됐을 것(멈춰서 힐하는 게 아니라 계속 이동 시도).

## 수정
`AttackOrderTick()`(`UnitController.cs`)의 재개 가드에 `isHealing` 조건 추가:
```csharp
if (attackRange != null && attackRange.HasEnemyInRange)
    return; // 아직 교전 중이면 그대로 둔다

if (isHealing)
    return; // 치유 중이면 그대로 둔다 - 치유가 끝나면 isStopped가 남아있어 아래에서 자동으로 재개된다
```
`isHealing`은 `BeginHeal()`에서 true, `StopHeal()`(대상이 다 나았거나/죽었거나/사거리를 벗어났을 때
`HealRange`/`HealTick()`이 호출)에서 false로 돌아간다 - `StopHeal()`은 이동을 직접 재개시키지 않고
`navMeshAgent.isStopped = true`인 채로 남겨두는데, 그다음 프레임 `AttackOrderTick()`이 (이제
`isHealing == false`라) 가드를 통과해서 `attackMoveDestination`으로 이동을 자동 재개한다 - 기존
전투 유닛의 "교전 종료 → 원래 목적지로 이동 재개"와 완전히 같은 경로를 그대로 재사용.

## 결과
- 메딕이 "땅공격"(A + 땅클릭) 이동 중 사거리 안에 다친 아군이 들어오면 멈춰서 치유하고, 치유가
  끝나면(대상이 풀피가 되거나 죽거나 멀어지면) 자동으로 원래 목적지로 이동을 재개한다.
- 순수 이동 명령(공격-이동이 아닌 일반 우클릭 이동) 중에는 기존과 동일하게 자동 개입하지 않는다
  (전투 유닛이 일반 이동 중엔 자동교전하지 않는 것과 동일한 관례 - `HealRange.Update()`도
  `unitController.IsIdle()`일 때만 동작).
- 컴파일 에러 0.
