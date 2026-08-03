# 0403 - 도달 불가 추격 중 대상이 계속 움직이면 재탐색이 반복되어 멈칫거리는 문제 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 0개.

## 요청 내용

> 순찰이랑 따라갈수 있는 위치에 유닛에 경우는 괜찮은데 또 도달할수 없는 위치나 올라갈수 없는
> 언덕일 경우 그 대상이 가만히 있으면 가까운 위치까지 정상적으로 이동하는데 또 그 대상이 움직이면
> 계속 재탐색 하느라 유닛이 거의 멈춰버리는 수준으로 움직이네 만약 탐색을 했는데 도착하지 못하는
> 위치일 경우는 재탐색 하지 않고 가장 가까운 위치로 이동하는데 해당 위치를 갈수 있는지 탐색만
> 따로 할수 있나? 그래서 그거에 대해서 판단하도록 하고싶은데 도달할수 없으면 재탐색 안하고 일단
> 마지막 경로로 이동하면서 따로 그 유닛에 대한 탐색은 하는데 경로 재탐색은 안하도록 그러고 만약
> 따라가는 유닛이 갈수 있는 위치로 이동했을 경우 그때는 재탐색을 하도록 이런식에 메커니즘으로
> 할수 있나?

## 조사 결과 - 현재 "도달 불가 추격" 판정 구조 ([[0397]]에서 확정된 로직)

플레이어 쪽 `UnitController.UpdateUnreachableChase()`(`UnitController.cs:638~673`, `FriendlyAttackTick`/
`AttackOrderTick`이 호출)와 적 쪽 `EnemyUnitController.ChaseTarget()`(`EnemyUnitController.cs:328~358`)는
완전히 같은 구조다:

```csharp
// 도착(또는 더 갈 수 없어 멈춤) - 그 사이 대상이 움직였는지 확인
bool targetMoved = !lastMoveAgentToDestination.HasValue ||
    (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

if (targetMoved)
{
    MoveAgentTo(targetPos, false); // 새 위치로 재탐색하고 계속 추격
    return false;
}

// 도착했고 대상도 그 사이 안 움직였다 - 진짜 도달 불가로 최종 판정
return true;
```

이 유닛이 (언덕 위 등) 갈 수 있는 데까지 가서 멈춘 상태(`remainingDistance <= stoppingDistance`)에서,
**대상이 조금이라도 움직이면(0.5m 초과) 무조건 `MoveAgentTo()`로 재탐색한다.** 이 재탐색이 무거운
이유는 `MoveAgentTo()`(`UnitController.cs:675~710`) 안에서:

1. `navMeshAgent.SetDestination()` 호출 - 유닛이 여전히 도달 불가능한 지점이므로 이 자체가 매번
   PathPartial/실패로 다시 계산됨.
2. 실패 시 `NavMesh.SamplePosition()`으로 반경 20m를 다시 훑어 대체 지점을 찾고 다시
   `SetDestination()` - 이게 훨씬 무겁다.

를 매 프레임(또는 대상이 계속 살짝씩 움직이는 한 거의 매 프레임)에 걸쳐 반복하기 때문이다.
`NavMeshAgent`가 새 경로를 다 계산하기도 전에 다음 프레임에 또 리셋되니, 실제로는 거의
전진하지 못하고 제자리에서 계속 재조준만 하는 것처럼 보인다 - 정확히 [[0386]]/[[0391]]이 "아직
이동 중일 때" 막았던 것과 같은 현상이, "도달 불가 판정 이후 대상이 계속 움직이는 경우"에는 막혀
있지 않다.

즉 지금 코드는 "대상이 움직였다 = 다시 갈 수 있을지도 모른다"고 **가정**하고 무조건 무거운
재탐색을 실행하는데, 실제로는 그 새 위치도 여전히 도달 불가능한 경우가 대부분이라 매번 헛수고를
반복하며 멈칫거림만 만든다.

## 조사 결과 - 재탐색 없이 "갈 수 있는지"만 따로 확인하는 방법

Unity `NavMesh.CalculatePath(start, target, areaMask, path)`는 `NavMeshAgent.SetDestination()`과
달리 **에이전트의 현재 경로/이동 상태를 전혀 건드리지 않는 순수 계산**이다 - 결과를 별도
`NavMeshPath` 객체에 담아 반환하고, 유닛은 그동안 원래 경로를 따라 그대로 계속 움직인다. 결과
`path.status`가:
- `NavMeshPathStatus.PathComplete` → 그 지점까지 완전히 도달 가능
- `PathPartial`/`PathInvalid` → 도달 불가 (부분 경로만 있거나 아예 경로 없음)

사용자가 요청한 "탐색만 따로 하는" 기능이 정확히 이것이다. 이걸 이용해 "대상이 움직였을 때 실제로
재탐색(`MoveAgentTo`)할지"를 판단하는 게이트로 쓸 수 있다.

## 메커니즘 정교화 (사용자 피드백)

1차 제안(위 - "도달 불가 상태 내내 아무것도 안 하고 지켜만 봄")에 대해 사용자가 아래처럼 구체화를
요청함:

> 도달 가능할 때만 실제 재탐색(MoveAgentTo)을 실행하고, 여전히 도달 불가면 도달 불가능으로 처음
> 되었을때 그때 딱 그 마지막 위치를 기준으로 가까운 위치를 계산하고 그 위치로 이동시키고 그러면서
> 유닛 위치 탐색은 계속하는데 그쪽으로 이동은 안하고 만약 유닛 위치가 계속 변경되는게 도달할수
> 있는 위치로 바뀐다 그러면 그때 경로 재탐색을 돌리고 다시 도달할수 없는 위치로 가면 그때는 또
> 마지막 위치(가장 가까운 위치)로 이동만 시키는식으로 하면 될거 같아

정리하면 3가지 상태 전환에 대한 반응이 달라야 한다:
- **도달 가능 위치로 이동** → 실제 재탐색(`MoveAgentTo`) 실행.
- **도달 불가 상태로 막 전환된 순간** → 그 지점 기준 "가장 가까운 위치"로 1회 이동.
- **이미 도달 불가 상태를 유지 중(대상이 계속 다른 도달 불가 지점으로만 움직임)** → 이동 없이
  위치 확인만 계속.

## 제안하는 수정

"가장 가까운 위치로 이동"은 별도 계산이 필요 없다 - `MoveAgentTo()`가 이미 그 역할을 한다:
`SetDestination()`이 실패하면 내부에서 `NavMesh.SamplePosition()`으로 반경 20m의 가장 가까운
지점을 찾아 대신 이동시키고(694~699번째 줄), 경사로로 연결됐지만 끝까지는 못 가는 경우는
`NavMeshAgent`가 알아서 갈 수 있는 데까지만(Partial Path) 이동한다. 즉 `MoveAgentTo(targetPos)`를
"도달 불가 상태로 막 전환된 그 순간"에 **딱 한 번만** 부르면 요청한 동작 그대로 된다.

"막 전환된 순간"인지는 새 상태 변수 없이 기존 `lastMoveAgentToDestination`(직전에 실제로
이동시켰던 목적지)으로 판단할 수 있다: 그 직전 목적지가 지금도 도달 가능하면("도달 가능 →
방금 도달 불가로 바뀜" 전환), 도달 불가면("이미 도달 불가 유지 중") 이렇게 구분된다. 새 필드를
추가하지 않으므로 "다른 대상으로 전환됐을 때 상태 초기화를 깜빡한다" 같은 버그도 원천적으로
없다 - 대상이 바뀌면 그 대상을 처음 지정하는 시점(`AttackUnitTarget`/`FollowUnit` 등)에서 이미
`MoveAgentTo()`가 호출되어 `lastMoveAgentToDestination`이 새 대상 기준으로 갱신되기 때문이다.

### `Assets/Scripts/Unit/UnitController.cs`

새 헬퍼 추가 (`MoveAgentTo` 근처):

```csharp
private readonly NavMeshPath reachabilityProbePath = new NavMeshPath();

// MoveAgentTo와 달리 에이전트의 실제 경로/이동 상태를 전혀 건드리지 않는 순수 조회 - 그 지점이
// 지금 이 유닛 기준으로 완전히 도달 가능한지만 확인한다 (doc/0403).
private bool IsPositionReachable(Vector3 pos)
{
    return NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, reachabilityProbePath) &&
        reachabilityProbePath.status == NavMeshPathStatus.PathComplete;
}
```

`UpdateUnreachableChase()`(638~673번째 줄) 수정:

```csharp
        // 도착(또는 더 갈 수 없어 멈춤) - 그 사이 대상이 움직였는지 확인
        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            bool reachableNow = IsPositionReachable(targetPos);
            // 직전에 실제로 이동시켰던 목적지가 지금도 도달 가능한지 - false면 "이미 도달 불가 상태를
            // 유지 중"이라는 뜻이고, true면 "방금 막 도달 불가로 전환됐다"는 뜻이다 (doc/0403).
            bool wasReachable = !lastMoveAgentToDestination.HasValue || IsPositionReachable(lastMoveAgentToDestination.Value);

            if (reachableNow || wasReachable)
            {
                // 도달 가능해졌으면 실제 재탐색, 방금 막 도달 불가로 전환됐으면 가장 가까운 위치로
                // 1회 이동(MoveAgentTo 내부의 SamplePosition/Partial Path 폴백이 처리) - 둘 다 여기서
                // 처리된다. 이미 도달 불가 상태가 계속되는 중이면(둘 다 false) 아래로 내려가 재탐색
                // 없이 반환한다.
                MoveAgentTo(targetPos, false);
            }

            return false;
        }

        // 도착했고 대상도 그 사이 안 움직였다 - 진짜 도달 불가로 최종 판정
        return true;
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 구조이므로 동일하게 `IsPositionReachable()` 헬퍼를 추가하고 `ChaseTarget()`(328~358번째 줄)의
`targetMoved` 분기에 동일한 게이트를 적용한다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()`, 638~673번째 줄 부근 + 헬퍼 추가)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`, 328~358번째 줄 부근 + 헬퍼 추가)

## 요약

- 원인: 도달 불가 지점에 멈춘 뒤 대상이 조금이라도 움직이면(0.5m 초과) 무조건 무거운 재탐색
  (`SetDestination` + 실패 시 `NavMesh.SamplePosition`)을 실행해서, 대상이 계속 움직이는 한 매
  프레임 헛수고를 반복하며 멈칫거렸다.
- 수정: `NavMesh.CalculatePath`(에이전트 상태를 건드리지 않는 순수 조회)로 만든
  `IsPositionReachable()` 헬퍼를 이용해 두 상태만 구분한다 - 지금 위치가 도달 가능하거나, 직전
  목적지가 도달 가능했다가 방금 막 도달 불가로 전환됐으면(둘 중 하나라도 참) `MoveAgentTo()`를
  호출하고, 이미 도달 불가 상태가 계속되는 중이면(둘 다 거짓) 재탐색 없이 반환한다.
  `MoveAgentTo()` 자체가 이미 "갈 수 있는 가장 가까운 위치로 이동"(SamplePosition 폴백/Partial
  Path 자동 정지)을 처리하므로 별도 계산은 필요 없었다.
- 새 상태 변수 없이 기존 `lastMoveAgentToDestination`으로 전환 여부를 판단해서, 대상이 바뀌는
  경우 초기화를 깜빡할 여지가 없다.
- 플레이어 유닛(`UnitController.UpdateUnreachableChase`)과 적유닛(`EnemyUnitController.ChaseTarget`)
  양쪽 다 동일하게 적용.
- 컴파일 확인 완료(에러 0, 경고 0).
