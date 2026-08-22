# 0665 - 메딕 "우클릭 대상 지정(따라가기)" 중 힐 범위 안에서도 계속 따라가지는 버그 - 수정 제안

## 요청
> 메딕이 우클릭으로 힐하는 유닛 지정하는거에서 지정은 되는거 같은데 메딕이 힐 범위 안에있는데도
> 해당 유닛을 따라가기도 같이 되는거 같아. 힐 범위 안에있으면 정지해서 힐하고 그러고 나서
> 따라가기로 넘어가고, 범위 밖이면 그 유닛한테 가다가 범위 안에 들어가면 정지하고 이런식으로
> 작동하면 될거같아.

## 원인 (버그)
메딕이 아군 유닛을 우클릭하면 일반 "따라가기" 명령(`UnitController.FollowUnit`, doc/0035 - 힐
전용이 아니라 모든 유닛이 공용으로 쓰는 명령)이 걸린다. `FollowUnit`은 `UnitcurrentState = Idle`을
유지한 채로 `hasFollowOrder = true`만 세팅하는데, 이 Idle 상태 덕분에 `HealRange.Update()`(매 프레임,
`unitController.IsIdle()`일 때만 동작)가 그대로 같이 동작해서 사거리 안에 들어오면 `BeginHeal()`을
호출해 정지시킨다 - 여기까지는 의도대로.

문제는 그 다음 매 프레임 같이 도는 `FollowTick()`이다. `FollowTick()`은 "따라가기 정지 거리"
(`stopDistance` = 두 유닛 반경 합 + `followStopMargin`, 몸이 거의 맞닿는 수준의 짧은 거리)만
보고, 그보다 멀면 무조건 `UpdateUnreachableChase()` → `MoveAgentTo()`를 호출해 이동을 재개시킨다.
힐 사거리(`HealRange.UnitRange`)는 이 따라가기 정지 거리보다 훨씬 커서, "힐 사거리 안 + 따라가기
정지 거리 밖"인 구간에서 `BeginHeal()`이 막 세운 `isStopped = true`를 같은/다음 프레임에
`FollowTick()`이 다시 `false`로 덮어써서 정지 못 하고 계속 따라가려는 상태가 된다.

같은 원인의 버그가 doc/0662("땅공격 중 힐" 케이스)에서 이미 한 번 고쳐졌다 - 그때는
`AttackOrderTick()`에 `isHealing` 가드가 없어서 생긴 문제였고, `FollowTick()`에는 그 가드가 애초에
빠져 있다(같은 실수가 다른 진입점에 남아있던 것).

## 수정 제안
`FollowTick()`(`Assets/Scripts/Unit/UnitController.cs`, 약 1004번째 줄)에 doc/0662와 동일한 패턴으로
`isHealing` 가드를 추가:

```csharp
private void FollowTick()
{
    if (!hasFollowOrder)
        return;

    if (followTarget == null)
    {
        ...
        return;
    }

    if (attackRange != null && attackRange.HasEnemyInRange)
        return; // 교전 중이면 그대로 둔다 (AttackRange가 정지시킨 상태 유지)

    if (isHealing)
        return; // 치유 중이면 그대로 둔다 - 치유가 끝나면 isStopped가 남아있어
                 // 다음 FollowTick에서 자동으로 따라가기를 재개한다 (doc/0662와 동일 패턴)

    float stopDistance;
    ...
```

`StopHeal()`은 `navMeshAgent.isStopped`를 직접 건드리지 않고 `true`인 채로 남겨두므로, 치유가
끝난 다음 프레임 `FollowTick()`이 가드를 통과해 기존 로직(정지 거리 안이면 정지 유지, 밖이면
`MoveAgentTo`로 재개)을 그대로 타면서 자동으로 "따라가기"로 복귀한다 - 코드 추가 없이 기존 분기가
그대로 처리해준다.

## 예상 결과
- 힐 범위 밖: 지정한 유닛을 향해 이동(기존과 동일).
- 힐 범위 안: `HealRange`가 정지시키고 치유, `FollowTick`이 더 이상 덮어쓰지 않음(수정 대상).
- 대상이 다 낫거나(만피)/죽거나/범위를 벗어나서 치유가 끝나면: 다음 프레임부터 다시 따라가기 재개.

## 변경 범위
`UnitController.FollowTick()`에 3줄(가드 1개) 추가. 다른 로직 변경 없음.

## 추가 확인 (사용자 요청: "체력이 다 차있으면 따라가기, 아니면 힐 - 일꾼 건물 수리같이")
일꾼의 건물 우클릭(`MoveToBuilding`)은 클릭 시점에 `IsDamaged(building)`을 직접 확인해서
`Repair(building)` / `FollowBuilding(building)`으로 분기한다. 메딕도 겉보기엔 같은 동작이
필요해 보이지만, 실제로는 우클릭 디스패치 쪽에 분기를 추가할 필요가 없다 - 이미 있는
`HealRange`가 이 판정을 프레임마다 자동으로 하고 있기 때문이다.

- 아군 우클릭은 (일꾼/메딕 구분 없이) 항상 `FollowUnit(target)`만 호출한다
  (`UserControl.cs:601`) - `UnitcurrentState = Idle`을 유지한 채 대상 쪽으로 이동.
- `HealRange.Update()`는 메딕이 Idle인 동안 매 프레임 독립적으로 돌면서
  `GetClosestDamagedAlly()`로 대상을 고른다 - 이때 **만피인 유닛은 애초에 후보에서
  제외**된다(`health.GetHealth() >= health.GetMaxHealth()`).
  - 클릭한 유닛이 만피 → `HealRange`가 절대 개입하지 않음 → `FollowUnit`만 남아 그냥
    따라가기. (분기 없이 이미 "따라가기"로 귀결)
  - 클릭한 유닛이 다침 → 사거리 밖이면 `ChaseTarget`으로 접근, 사거리 안이면
    `BeginHeal`로 정지 + 치유. (분기 없이 이미 "힐"로 귀결)

즉 "체력 다 참 → 따라가기 / 안 참 → 힐"은 디스패치 시점에 새로 만들 필요 없이 위 doc/0665
수정 하나로 이미 만족된다. 일꾼 수리처럼 **클릭한 그 유닛만 콕 집어 고정 치유**하는 방식은
아니고(수리와 달리 `HealRange`는 의도적으로 "고정 타겟 없이 매 프레임 사거리 안 최근접 다친
아군 재탐색" 방식 - doc/0661 주석 참고), 보통 메딕 하나에 지정 대상 하나인 상황에선 결과가
동일하다. 이 차이(다른 다친 아군이 더 가까이 있으면 그쪽을 먼저 치유할 수 있음)까지 없애고
"클릭한 유닛 고정"으로 바꾸고 싶으면 별도 설계가 필요 - 우선은 기존 자동교전 설계를 그대로
따르는 쪽으로 제안.
