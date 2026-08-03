# 0402 - 순찰(P) 명령이 첫 구간 도착 후 멈추는 문제 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 0개.

## 요청 내용

> 0399에 대한 요청에서 부작용이 발생했는데 P 순찰 명령을 내리면 그 위치로 이동하고 그대로
> 멈춰버리네 현재 변경사항과 순찰 로직을 확인해줘

## 조사 결과 - 원인은 [[0399]]와 순찰 로직의 상호작용

[[0399]]에서 `UnitController.Update()`의 일반 이동 도착 판정 블록(`UnitController.cs:424~441`)에
`navMeshAgent.isStopped = true`를 추가했다:

```csharp
if (!arrived &&
    orderedTarget == null &&
    friendlyTarget == null &&
    followTarget == null &&
    !navMeshAgent.pathPending &&
    navMeshAgent.remainingDistance <= arriveDistance)
{
    arrived = true;
    navMeshAgent.isStopped = true;   // 0399에서 추가
    UnitcurrentState = UnitState.Idle;
    attackMoveDestination = null;
}
```

이 조건문은 `orderedTarget`/`friendlyTarget`/`followTarget`이 모두 `null`이면 통과하는데,
`PatrolUnit()`(`UnitController.cs:1375~1402`)은 이 세 필드를 전혀 건드리지 않는다. 즉 **순찰
중에도 이 블록이 그대로 실행된다.**

`Update()` 안에서 이 블록(424번째 줄)이 `PatrolTick()`(1404번째 줄) 호출보다 먼저 실행되는 순서도
문제를 만든다:

1. 유닛이 순찰 첫 구간(`endPoint`)에 도착 → 424번째 줄 블록이 먼저 실행되어 `arrived = true`,
   **`navMeshAgent.isStopped = true`**로 정지시킴.
2. 곧바로 `PatrolTick()`이 실행되어 도착을 감지하고 `arrived = false`로 되돌린 뒤, 반대 구간으로
   `navMeshAgent.SetDestination(startPoint)`를 호출(1432번째 줄) - **하지만 `isStopped`는 그대로
   `true`로 남겨둔 채라서 새 목적지를 줘도 NavMeshAgent가 움직이지 않는다.**

`PatrolUnit()`이 순찰을 처음 시작할 때는 1394번째 줄에서 `isStopped = false`를 직접 풀어주지만,
그 이후 매 구간 전환을 담당하는 `PatrolTick()`(1432, 1441번째 줄)은 `SetDestination`만 부르고
`isStopped`는 건드리지 않는다 - [[0399]] 이전에는 `isStopped`가 애초에 도착으로 인해 `true`가 될
일이 없었으니 문제가 없었지만, [[0399]]로 인해 그 전제가 깨졌다.

정리: 첫 구간 끝에서 유닛이 멈춰 서 있는 이유는 NavMeshAgent가 `isStopped == true` 상태로 굳어서,
그 뒤로 아무리 `SetDestination`을 불러도 실제로는 이동을 시작하지 못하기 때문이다.

## 제안하는 수정

`MoveAgentTo()`(675~679번째 줄)가 이동을 시작할 때 항상 `isStopped = false`부터 푸는 것과 같은
패턴으로, `PatrolTick()`이 다음 구간으로 목적지를 바꿀 때도 `isStopped = false`를 먼저 풀어준다.
순찰은 `MoveAgentTo()`를 거치지 않고 `navMeshAgent.SetDestination()`을 직접 부르므로, 그 직전에
직접 풀어줘야 한다.

### `Assets/Scripts/Unit/UnitController.cs` - `PatrolTick()` (1404~1444번째 줄 부근)

```csharp
        arrived = false; // 🔥 다음 이동 준비

        if (goingToEnd)
        {
            goingToEnd = false;

            if (!isAirUnit)
            {
                navMeshAgent.isStopped = false; // 0399로 도착 시 걸린 정지를 다음 구간 이동 전에 풀어준다
                navMeshAgent.SetDestination(startPoint);
            }
            else
                targetPosition = AirTargetPosition(startPoint, true);
        }
        else
        {
            goingToEnd = true;

            if (!isAirUnit)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(endPoint);
            }
            else
                targetPosition = AirTargetPosition(endPoint);
        }
```

이렇게 하면:
- [[0399]]가 고친 "도착 후 밀쳐지면 원위치로 되돌아가는" 동작은 그대로 유지된다(순찰이 아닌 일반
  이동 도착 시에는 여전히 `isStopped = true`가 걸림).
- 순찰 중 각 구간에 도착했을 때만 `PatrolTick()`이 즉시 다음 구간으로 `isStopped`를 풀고 이동을
  재개하므로, 첫 구간에서 멈춰버리는 문제가 없어진다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`PatrolTick()`, 1404~1444번째 줄 부근)

## 요약

- 원인: [[0399]]가 일반 이동 도착 시 `navMeshAgent.isStopped = true`를 걸도록 했는데, 순찰은
  `orderedTarget`/`friendlyTarget`/`followTarget`을 쓰지 않아 이 도착 블록이 순찰 중에도 그대로
  실행됨. 첫 구간 도착 시 `isStopped = true`로 굳고, 뒤이은 `PatrolTick()`의 `SetDestination`은
  `isStopped`를 풀지 않아 실제로는 움직이지 못함.
- 수정: `PatrolTick()`이 다음 구간으로 전환할 때(1432/1441번째 줄 부근) `SetDestination` 직전에
  `navMeshAgent.isStopped = false`를 건다. `MoveAgentTo()`가 이동 시작 시 항상 하는 것과 동일한
  패턴.
- [[0399]]가 고친 "도착 후 밀쳐지면 원위치로 되돌아가는" 동작에는 영향 없음(순찰이 아닌 일반 이동
  도착 시에는 여전히 `isStopped = true`가 걸림).
- 컴파일 확인 완료(에러 0, 경고 0).
