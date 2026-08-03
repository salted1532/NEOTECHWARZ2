# 0399 - 도착한 유닛이 다른 유닛에게 밀쳐지면 다시 목적지로 돌아가려 하는 이유

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 유닛이 목적지에 도착하고 나서 다른 유닛이 그 유닛을 밀쳤을때 왜 밀쳐진 유닛이 다시 목적지로
> 가려고하지?

## 조사 결과

`UnitController.Update()`의 지상 유닛 도착 판정(`UnitController.cs:422~438`):

```csharp
if (!isAirUnit)
{
    if (!arrived &&
        orderedTarget == null &&
        friendlyTarget == null &&
        followTarget == null &&
        !navMeshAgent.pathPending &&
        navMeshAgent.remainingDistance <= arriveDistance)
    {
        arrived = true;
        // ResetPath() 미호출 이유는 EnemyUnitController와 동일 (doc/0387) - AttackRange의
        // 순수 자동교전(ChaseTarget, 지정 명령 아님) 경로에서 매 프레임 재호출되는 동안 이
        // 도착 판정이 계속 ResetPath()를 부르면 doc/0386 목적지 캐시가 무효화된다.
        UnitcurrentState = UnitState.Idle;
        attackMoveDestination = null;
    }
}
```

도착 시 하는 일은 딱 두 가지뿐이다: `arrived = true` 플래그를 세우고, `UnitcurrentState`를
`Idle`로 바꾼다. **`navMeshAgent.isStopped`는 여기서 건드리지 않고, `navMeshAgent.ResetPath()`도
(의도적으로, doc/0387 참고) 호출하지 않는다.**

즉 도착 후에도 `NavMeshAgent` 컴포넌트 자체 입장에서는:
- `isStopped`는 여전히 `false`
- `destination`은 여전히 그 도착 지점을 가리키고 있음
- `hasPath`도 `true`로 유지됨

이 상태에서 다른 유닛이 (NavMeshAgent의 내장 장애물 회피/충돌 처리로) 이 유닛을 그 지점에서
살짝 밀어내면, `remainingDistance`가 다시 `stoppingDistance`보다 커진다. `arrived` 플래그는
우리 코드가 "도착 판정 블록에 다시 들어가지 않도록" 막는 용도일 뿐이고, NavMeshAgent 자신의
길찾기 동작을 멈추지는 않는다 - `isStopped == false`이고 `destination`이 여전히 유효한 한,
NavMeshAgent는 **우리 코드의 개입 없이 컴포넌트 스스로** 그 목적지까지의 경로를 다시 계산해서
걸어간다. 그래서 "밀쳐진 유닛이 다시 목적지로 걸어가는" 것처럼 보인다.

이건 버그라기보다 doc/0387에서 의도적으로 선택한 트레이드오프의 부작용에 가깝다:
- `ResetPath()`를 부르면 `hasPath`가 `false`가 되면서 doc/0386의 "목적지가 직전과 같으면
  재요청하지 않는다" 캐시가 깨져, 자동교전(`ChaseTarget`)이 매 프레임 재호출되는 동안 도착
  직후에도 계속 미세하게 재탐색하며 흔들리는 문제가 있었다.
- 그 문제를 막기 위해 `ResetPath()`를 빼면서, 대신 "도착 후에도 NavMeshAgent가 그 지점으로
  돌아가려는 힘을 계속 유지한다"는 지금의 부작용이 생김.

정리하면 원인은 **도착 처리가 `arrived` 플래그만 세우고 NavMeshAgent를 실제로 정지시키지
않기 때문**이다. (참고로 `BuildTick`(`UnitController.cs:1035~1036`)이나 다른 몇몇 도착
처리(`isStopped = true`를 부르는 지점들, 예: `UnitController.cs:914, 971, 1171, 1363` 등)는
이 패턴과 다르게 명시적으로 정지시킨다 - 일반 `MoveTo` 도착만 이 예외에 해당한다.)

## 코드 변경

도착 판정 블록에서 `arrived = true`와 함께 `navMeshAgent.isStopped = true`도 건다. 이 값은
파일 전체에서 이미 "정지/교전 상태"를 나타내는 데 쓰던 기존 관례와 같은 패턴이다(예:
`UnitController.Attack()`(1171번째 줄), `EnemyUnitController.Attack()`(430번째 줄)도 교전
시작 시 동일하게 `isStopped = true`를 건다). 다음 명령이 들어오면 `MoveAgentTo()`가 지상 유닛
이동 시작 시 항상 `isStopped = false`로 먼저 풀어주므로(671~676번째 줄), 다음 이동을 막지
않는다. `ResetPath()`는 doc/0387 그대로 호출하지 않는다 - `isStopped`만 바꾸는 것은 `hasPath`나
`destination`을 건드리지 않아 doc/0386 목적지 캐시에 영향이 없다.

### `Assets/Scripts/Unit/UnitController.cs` (424~438번째 줄)

기존 코드:
```csharp
            if (!arrived &&
                orderedTarget == null &&
                friendlyTarget == null &&
                followTarget == null &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                // ResetPath() 미호출 이유는 EnemyUnitController와 동일 (doc/0387) - AttackRange의
                // 순수 자동교전(ChaseTarget, 지정 명령 아님) 경로에서 매 프레임 재호출되는 동안 이
                // 도착 판정이 계속 ResetPath()를 부르면 doc/0386 목적지 캐시가 무효화된다.
                UnitcurrentState = UnitState.Idle;
                attackMoveDestination = null;
            }
```

변경 코드:
```csharp
            if (!arrived &&
                orderedTarget == null &&
                friendlyTarget == null &&
                followTarget == null &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                // ResetPath() 미호출 이유는 EnemyUnitController와 동일 (doc/0387) - AttackRange의
                // 순수 자동교전(ChaseTarget, 지정 명령 아님) 경로에서 매 프레임 재호출되는 동안 이
                // 도착 판정이 계속 ResetPath()를 부르면 doc/0386 목적지 캐시가 무효화된다.
                // isStopped는 true로 건다 - 안 그러면 destination이 여전히 이 지점을 가리키는 채로
                // 남아서, 도착 후 다른 유닛에게 밀려나면 NavMeshAgent가 스스로 원래 자리로 되돌아가려
                // 한다(doc/0399). MoveAgentTo가 다음 명령 때 항상 isStopped = false로 풀어준다.
                navMeshAgent.isStopped = true;
                UnitcurrentState = UnitState.Idle;
                attackMoveDestination = null;
            }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (260~274번째 줄)

같은 원인이 적유닛(`EnemyUnitController`)에도 동일하게 있어(doc/0387이 두 파일을 항상 짝으로
같이 고쳐온 이유와 동일) 함께 수정.

기존 코드:
```csharp
        if (!isAirUnit)
        {
            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                // ResetPath()는 호출하지 않는다 - NavMeshAgent는 도착하면(또는 도달 불가능한 대상이라
                // 갈 수 있는 데까지만 간 채) 스스로 정지하므로 불필요하고, 오히려 hasPath를 false로
                // 만들어서 MoveAgentTo의 목적지 캐시(doc/0386)를 매 프레임 무효화시킨다 - 자동교전
                // (EnemyAttackRange → ChaseTarget)이 매 프레임 다시 호출되는 동안 도착 판정이 계속
                // ResetPath()를 부르면 이미 도착한 뒤에도 매 프레임 경로가 재계산되어 미세하게 계속
                // 흔들리는 문제가 있었다 (doc/0387).
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }
```

변경 코드:
```csharp
        if (!isAirUnit)
        {
            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                // ResetPath()는 호출하지 않는다 - NavMeshAgent는 도착하면(또는 도달 불가능한 대상이라
                // 갈 수 있는 데까지만 간 채) 스스로 정지하므로 불필요하고, 오히려 hasPath를 false로
                // 만들어서 MoveAgentTo의 목적지 캐시(doc/0386)를 매 프레임 무효화시킨다 - 자동교전
                // (EnemyAttackRange → ChaseTarget)이 매 프레임 다시 호출되는 동안 도착 판정이 계속
                // ResetPath()를 부르면 이미 도착한 뒤에도 매 프레임 경로가 재계산되어 미세하게 계속
                // 흔들리는 문제가 있었다 (doc/0387).
                // isStopped는 true로 건다 - 안 그러면 destination이 여전히 이 지점을 가리키는 채로
                // 남아서, 도착 후 다른 유닛에게 밀려나면 NavMeshAgent가 스스로 원래 자리로 되돌아가려
                // 한다(doc/0399). MoveAgentTo가 다음 명령 때 항상 isStopped = false로 풀어준다.
                navMeshAgent.isStopped = true;
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }
```

## 요약/남은 작업

- 원인: 일반 이동 도착 처리가 `NavMeshAgent`를 실제로 멈추지 않고 `arrived` 플래그만 세워서,
  도착 후 밀쳐지면 `destination`이 여전히 유효한 NavMeshAgent가 스스로 원래 목적지로 재추적함.
- 수정: 도착 시 `isStopped = true`를 걸어 NavMeshAgent의 자체 추적을 멈춘다(이 파일에서 교전
  시작 시 이미 쓰던 것과 같은 패턴). 다음 명령이 오면 `MoveAgentTo`가 항상 다시 풀어준다.
  `ResetPath()`는 그대로 호출하지 않아 doc/0387/0386이 막았던 자동교전 재탐색 흔들림 문제는
  재발하지 않는다.
- 플레이어 유닛(`UnitController.cs`)과 적유닛(`EnemyUnitController.cs`) 양쪽 다 수정함.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs` (422~440번째 줄)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (260~277번째 줄)
