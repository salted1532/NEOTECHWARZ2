# 0236 - 가만히 있는 적이 전투 후 추적을 멈추던 문제 수정

## 요청

가만히 있는 적 유닛: 사거리(감지 범위) 안에 상대 유닛이 나타나면 실제 공격 사거리에 맞춰 공격하고, 상대가
`EnemyAttackRange`의 감지 콜라이더 밖으로 완전히 나가면 마지막으로 감지된 위치로 이동해서 계속 추적하도록
해달라는 요청.

## 원인

`EnemyUnitController.Attack()`이 매번 `currentState = EnemyState.Attack;`을 실행하고 있었는데, 이 상태를
다시 `Idle`로 되돌리는 코드가 (공격-이동 중이 아닌) 이 "가만히 있다가 자동 교전" 시나리오에는 전혀 없었음.
그래서 한 번이라도 공격하면 `currentState`가 `Attack`에 영구히 멈춰버림.

`EnemyAttackRange.Update()`의 추적 판단 로직은:

```csharp
if (enemyUnit.IsAttack() || enemyUnit.IsIdle())
{
    if (distance <= UnitRange)
        enemyUnit.Attack(...);
    else if (enemyUnit.IsIdle())      // ← Idle일 때만 추적
        enemyUnit.ChaseTarget(...);
}
```

사거리 밖으로 벗어났을 때 `ChaseTarget()`은 **`IsIdle()`이 true일 때만** 호출된다. 그런데 상태가 이미
`Attack`에 멈춰있으니 `IsIdle()`이 계속 false가 되어, 상대가 공격 사거리 밖으로 물러나도(또는 감지 범위
밖으로 완전히 나가도) 추적이 시작되지 않고 제자리에 멈춰 서 있기만 했음.

참고로 플레이어 쪽 `UnitController.Attack()`은 애초에 `UnitcurrentState`를 전혀 건드리지 않는다 - 가만히
서서 자동 교전하는 유닛은 계속 `Idle` 상태를 유지한 채로 싸우기 때문에, 이 문제 자체가 발생하지 않았음.
`EnemyUnitController`를 만들 때 불필요하게 `currentState = EnemyState.Attack;`을 추가했던 것이 원인.

## 수정 내용

`Assets/Scripts/Enemy/EnemyUnitController.cs`의 `Attack()`에서 `currentState = EnemyState.Attack;` 줄을
제거함 (`UnitController.Attack()`과 동일하게, 공격 중에도 상태를 건드리지 않음).

이제 정상 흐름:
1. 감지 콜라이더(트리거) 안에 상대가 들어오고 `Idle` 상태 + 사거리 밖 → `ChaseTarget()`으로 접근
2. 사거리 안으로 들어오면 → `Attack()` (상태는 계속 `Idle`로 유지됨)
3. 상대가 다시 사거리 밖(그러나 감지 범위 안)으로 물러나면 → 여전히 `Idle`이므로 `ChaseTarget()`이 다시
   호출되어 계속 쫓아감
4. 상대가 감지 콜라이더를 완전히 벗어나면 → 그 순간까지 매 프레임 갱신되던 마지막 목적지(NavMeshAgent에
   이미 설정된 마지막 위치)를 향해 별도 명령 없이도 계속 이동 → 도착하면 자동으로 정지하고 `Idle` 유지

## 변경 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
