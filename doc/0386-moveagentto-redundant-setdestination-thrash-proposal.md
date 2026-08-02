# 0386 - 매 프레임 재요청으로 인한 MoveAgentTo 경로 재계산 반복(제자리 회전) 수정

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 지상에 있는 유닛이 언덕에 최대한 가까이 붙으면 언덕위에 대상에 사거리상 닿을 거리인데 현재는
> 루프에 빠져서 엄청 천천히 거의 움직이지 않을 정도로 회전하고 이동하려고하는데 이게 지금
> 절차적으로 작동하지 않는거 같아 언덕에 최대한 가까이 도달하기가 아니라 공격이 되는지 계속
> 루프에 빠진거처럼 보여

[[0375]]에서 만든 fallback 자체는 "한 번만 호출"되는 곳(`GoBuild`, `GatherTick` 반납 이동 등)에서는
잘 동작하지만, [[0384]]/[[0385]]로 손댄 공격 추적 로직들은 전부 **매 프레임** `MoveAgentTo()`를
다시 호출한다는 점을 감안하지 않아서, 도달 불가능한 대상에 대해선 매 프레임 경로 재계산만 반복하고
실제로는 거의 전진하지 못하는 문제가 새로 드러남.

## 조사 결과

- `AttackOrderTick()`(`UnitController.cs:1030`), `FriendlyAttackTick()`(`UnitController.cs:770`),
  `ChaseTarget()`이 매 프레임 부르는 `AttackRange.Update()`(`AttackRange.cs:92`) - 사거리 밖인 동안
  전부 매 프레임 `MoveAgentTo(대상 위치)`를 다시 호출한다. 대상이 거의 안 움직이는 정지 상태여도
  "거의 동일한 좌표"로 매 프레임 새로 호출된다.
- `MoveAgentTo()`(`UnitController.cs:612`)는 호출될 때마다 무조건 `navMeshAgent.SetDestination(destination)`
  을 새로 시도한다. 도달 불가능한 대상이면 이 호출은 항상 실패하므로, 매 프레임 다시
  `NavMesh.SamplePosition()` + 두 번째 `SetDestination()`까지 처음부터 다시 수행한다.
- 문제는 이게 그냥 "낭비"가 아니라 실제 이동을 방해한다는 것: NavMeshAgent는 `SetDestination()`이
  호출될 때마다 경로 계산을 다시 큐에 넣는다(`pathPending`이 다시 `true`가 됨). 매 프레임 이 요청이
  반복되면, 에이전트가 직전 프레임에 잡은 경로를 따라 가속을 붙이기도 전에 다음 프레임에서 또 새
  경로 요청이 들어와 방향/코너 계산이 계속 리셋된다 - 결과적으로 목적지 방향으로 계속 재조준(회전)만
  하고 실제 이동 속도는 거의 붙지 않는 것으로 보인다. [[0384]]에서 도입한 "더 이상 못 감" 취소 판정
  (`!navMeshAgent.pathPending && remainingDistance <= stoppingDistance`)도 이 상태에서는 `pathPending`이
  계속 다시 `true`로 리셋되니 좀처럼 조건을 만족하지 못해, 사용자 입장에서는 "가장 가까이 가지도
  못하고 취소되지도 않는 채로 제자리에서 버벅이는" 것처럼 보인다.
- 실제 목적지(대상 위치)가 이전 프레임과 사실상 같다면 재요청 자체가 불필요하다 - Unity
  NavMeshAgent는 이미 그 목적지로 가는 중이므로 그대로 두면 된다. "목적지가 유의미하게 바뀌었을
  때만 새로 경로를 잡는다"는 조건을 `MoveAgentTo()` 안에 캐싱해서 걸러내면, 매 프레임 호출 구조는
  그대로 유지하면서(추적 로직 자체는 안 건드림) 이 재계산 반복만 없앨 수 있다.

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs` - `MoveAgentTo()` (612~631번째 줄)

기존 코드:
```csharp
    private const float UnreachableDestinationSampleRadius = 20f;

    private bool MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            if (navMeshAgent.SetDestination(destination))
                return true;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas))
                return navMeshAgent.SetDestination(hit.position);

            return false;
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
            return true;
        }
    }
```

변경 코드:
```csharp
    private const float UnreachableDestinationSampleRadius = 20f;

    // AttackOrderTick/FriendlyAttackTick/ChaseTarget처럼 매 프레임 MoveAgentTo를 다시 호출하는 곳에서,
    // 목적지가 사실상 그대로인데도 매번 SetDestination(+실패 시 SamplePosition 재탐색)을 반복하면
    // NavMeshAgent가 경로를 다 계산하기도 전에 매 프레임 다시 리셋되어, 실제로는 목적지 방향으로
    // 계속 재조준(회전)만 하고 거의 전진하지 못하는 문제가 있었다(doc/0386). 직전과 사실상 같은
    // 목적지면 이미 잡혀있는 경로를 그대로 유지하고 재요청하지 않는다.
    private const float RedundantDestinationEpsilon = 0.5f;
    private Vector3? lastMoveAgentToDestination;

    private bool MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;

            if (navMeshAgent.hasPath &&
                lastMoveAgentToDestination.HasValue &&
                (lastMoveAgentToDestination.Value - destination).sqrMagnitude < RedundantDestinationEpsilon * RedundantDestinationEpsilon)
            {
                return true; // 목적지 변화 없음 - 진행 중인 경로 그대로 유지
            }

            if (navMeshAgent.SetDestination(destination))
            {
                lastMoveAgentToDestination = destination;
                return true;
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas) &&
                navMeshAgent.SetDestination(hit.position))
            {
                lastMoveAgentToDestination = destination;
                return true;
            }

            lastMoveAgentToDestination = null;
            return false;
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
            return true;
        }
    }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` - `MoveAgentTo()` (323~343번째 줄)

동일한 문제가 적 AI 쪽(`EnemyAttackRange.Update()` → `ChaseTarget()`, `AttackMoveTick()`)에도 그대로
있어서(0375처럼 두 파일을 항상 같이 손봐온 패턴) 같은 캐싱을 적용한다.

기존 코드:
```csharp
    private const float UnreachableDestinationSampleRadius = 20f;

    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            if (!navMeshAgent.SetDestination(destination) &&
                NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas))
            {
                navMeshAgent.SetDestination(hit.position);
            }
        }
        else
        {
            targetPosition = AirTargetPosition(destination);
            isMovingAirUnit = true;
        }
    }
```

변경 코드:
```csharp
    private const float UnreachableDestinationSampleRadius = 20f;

    // UnitController.MoveAgentTo와 동일한 캐싱(doc/0386) - 목적지가 직전과 사실상 같으면 재요청하지 않는다.
    private const float RedundantDestinationEpsilon = 0.5f;
    private Vector3? lastMoveAgentToDestination;

    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;

            if (navMeshAgent.hasPath &&
                lastMoveAgentToDestination.HasValue &&
                (lastMoveAgentToDestination.Value - destination).sqrMagnitude < RedundantDestinationEpsilon * RedundantDestinationEpsilon)
            {
                return;
            }

            if (navMeshAgent.SetDestination(destination))
            {
                lastMoveAgentToDestination = destination;
                return;
            }

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas) &&
                navMeshAgent.SetDestination(hit.position))
            {
                lastMoveAgentToDestination = destination;
                return;
            }

            lastMoveAgentToDestination = null;
        }
        else
        {
            targetPosition = AirTargetPosition(destination);
            isMovingAirUnit = true;
        }
    }
```

## 열린 질문

- 오차 허용치(`RedundantDestinationEpsilon` = 0.5m)는 임의로 잡은 값 - 실제 이동 중인 적/아군을
  추적할 때 목표 위치가 프레임마다 이 값보다 적게 움직이면 그 프레임엔 재요청을 건너뛰지만, 이미
  잡혀있는 경로가 있으므로 다음번에 문턱을 넘는 순간 정상적으로 재조준되어 눈에 띄는 추적 지연은
  없을 것으로 예상. 체감상 추적이 둔하다고 느껴지면 값을 낮추면 됨.
- `navMeshAgent.hasPath` 조건을 같이 걸어둬서, 도중에 경로가 리셋된 상태(`ResetPath()` 호출 등)라면
  목적지가 같아도 정상적으로 다시 `SetDestination`을 시도한다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
