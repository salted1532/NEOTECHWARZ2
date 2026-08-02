# 0387 - 도착 판정의 ResetPath()가 0386 목적지 캐시를 매 프레임 무효화하는 문제 수정

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 현재 로직상 적유닛이 아군유닛을 발견한 경우 그 위치로 가려고 하잖아(추적) 근데 그 위치가
> 도달할수 없는 언덕 위라면 최대한 그 위치랑 가까운 위치까지 도달하도록 수정해줘

`EnemyUnitController.ChaseTarget()`(`EnemyUnitController.cs:316`)은 이미 [[0375]]/[[0386]]으로 고친
`MoveAgentTo()`를 쓰고 있어서 요청하신 "가장 가까운 위치까지 이동"은 골격상 이미 되어 있어야 하는데,
찾아보니 별개의 경로로 [[0386]]에서 만든 캐시를 매 프레임 스스로 무효화시키는 문제가 남아있었음.

## 조사 결과

- `EnemyAttackRange.Update()`(`EnemyAttackRange.cs:109~119`)는 대상이 사거리 밖이고 유닛이 Idle이면
  **매 프레임** `ChaseTarget()`을 다시 호출한다. `ChaseTarget()`(`EnemyUnitController.cs:316~321`)은
  호출될 때마다 `arrived = false`로 리셋한 뒤 `MoveAgentTo()`를 부른다.
- 한편 `EnemyUnitController.Update()`의 지상 유닛 "도착 판정"(`EnemyUnitController.cs:260~269`)은
  `!arrived && !pathPending && remainingDistance <= arriveDistance`가 참이면(=유닛이 갈 수 있는
  데까지 가서 멈춘 상태) `arrived = true`로 바꾸면서 `navMeshAgent.ResetPath()`를 호출한다. 이 조건에는
  플레이어 쪽(`UnitController.cs:424~429`)에 있는 "지정 대상 추격 중이면 건너뛴다" 같은 예외가 없다
  (애초에 `EnemyUnitController`엔 `orderedTarget` 같은 지정 대상 필드 자체가 없음).
- 두 로직이 같은 프레임(또는 인접 프레임)에 번갈아 실행되면서 매 프레임 다음이 반복된다:
  1. `EnemyAttackRange.Update()` → 아직 사거리 밖 → `ChaseTarget()` → `arrived = false`,
     `MoveAgentTo(대상 위치)`.
  2. 유닛이 이미 가장 가까운 지점에 도착해 있으므로(`remainingDistance`가 거의 0) `EnemyUnitController.Update()`
     의 도착 판정이 바로 참이 되어 `arrived = true` + **`navMeshAgent.ResetPath()`** 실행.
  3. `ResetPath()`로 `navMeshAgent.hasPath`가 `false`가 되면서, [[0386]]에서 만든 "목적지가 직전과
     같으면 재요청하지 않는다" 캐시의 전제(`navMeshAgent.hasPath`)가 깨진다.
  4. 다음 프레임에 `ChaseTarget()`이 다시 호출되면 캐시 조건을 통과하지 못해 `SetDestination()` →
     실패 → `NavMesh.SamplePosition()` → `SetDestination(hit.position)`을 처음부터 다시 수행한다.
  5. 이 재요청으로 다시 짧은 경로가 잡히고 곧바로 도착 판정을 통과 → 2번으로 복귀 → 무한 반복.
- 즉 [[0386]]이 막으려던 "매 프레임 경로 재계산" 자체가, 목적지에 도착한 **이후**에도 이 도착 판정의
  `ResetPath()` 때문에 다시 재현된다. 실제 이동 거리는 이미 0에 가까우므로 겉보기엔 "가장 가까운
  지점 근처에서 미세하게 계속 흔들리는" 정도로 보일 수 있음 - 완전히 멈춘 것도 아니고 매끄럽게
  대기하는 것도 아닌 애매한 상태.
- `ResetPath()`는 여기서 사실 불필요하다: NavMeshAgent는 목적지(또는 도달 가능한 partial path의 끝)에
  도착하면 `remainingDistance`가 0에 가까워지면서 자연스럽게 `velocity`가 0으로 수렴해 스스로 멈춘다
  (`IsCurrentlyMoving()`도 `hasPath`가 아니라 `isStopped`/`velocity`만 본다 - `UnitController.cs:1847`,
  `EnemyUnitController.cs:605` 확인 완료). `ResetPath()`를 빼도 정지 동작 자체는 그대로고, 대신
  `hasPath`가 계속 `true`로 유지되어 [[0386]] 캐시가 의도대로 계속 재요청을 걸러준다.
- 같은 구조(자동교전 경로가 매 프레임 `ChaseTarget()`을 부르고, 도착 판정이 `ResetPath()`를 부르는
  패턴)가 플레이어 쪽 `UnitController.cs:424~435`에도 똑같이 있다 - 단, 플레이어는 `orderedTarget`
  등을 통한 "명시 지정 공격" 경로는 이미 예외 처리돼 있으므로 그 경로는 영향 없지만, `AttackRange.Update()`
  의 순수 자동교전(`ChaseTarget()`, `orderedTarget`을 세팅하지 않음) 경로는 동일하게 영향받는다.
  두 파일 다 같이 고쳐야 함(0375/0386과 동일한 이유로 항상 같이 유지해온 짝).

## 코드 변경 (제안)

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (260~269번째 줄)

기존 코드:
```csharp
        if (!isAirUnit)
        {
            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
            {
                arrived = true;
                navMeshAgent.ResetPath();
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
                currentState = EnemyState.Idle;
                attackMoveDestination = null;
            }
        }
```

### `Assets/Scripts/Unit/UnitController.cs` (424~435번째 줄)

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
                navMeshAgent.ResetPath();
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
                UnitcurrentState = UnitState.Idle;
                attackMoveDestination = null;
            }
```

## 열린 질문

- `ResetPath()`를 빼면 `hasPath`가 계속 `true`로 남는다 - 이후 완전히 새로운(먼) 목적지로 명령이
  오면 어차피 `MoveAgentTo()`가 새 `SetDestination()`으로 덮어쓰므로 문제없음. `navMeshAgent.destination`
  값 자체를 조회하는 다른 코드가 있는지도 확인했으나(grep) 없음 - 영향 없음.

## 영향받는 파일 (예정)

- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/Unit/UnitController.cs`
