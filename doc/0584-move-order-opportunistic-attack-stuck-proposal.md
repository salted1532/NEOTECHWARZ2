# 0584. 아군 OC(및 적 AI)가 "이동" 명령 중 도중에 자동교전 후 영구히 멈추는 버그 - 원인 및 수정 제안

- 날짜: 2026-08-16

## 요청 내용

- "아군OC가 또 공격을 갔는데 중간에 전투하고 나서 멈춰버리네" → "일부 유닛만 멈춤"(전체 별동대가 아니라 몇몇만) → "방금 아군유닛이랑 같이 전투할때 중간에 멈추는 현상 일어났는데 인식했어?"

## 조사 내용

이전에 고친 버그(doc/0575, doc/0581 - `AttackMoveTo()`로 공격-이동 중 도달 불가 대상을 쫓다 포기할 때
멈추는 문제)는 `AllyController.cs`에 정상적으로 적용돼 있어 재현 대상이 아님을 코드로 재확인했다.

Unity Editor가 Play 모드였던 김에 리플렉션으로 씬의 모든 `AllyController`를 실시간으로 스캔해서 실제로
멈춘 유닛을 현장에서 잡았다 - `Railgunner (Ally)(Clone)` 3기가 다음과 같은, 지금까지의 가설로는 설명 안
되는 상태였다:

- `currentState == Move` (공격-이동이 아니라 **그냥 이동** 명령)
- `navMeshAgent.isStopped == true`
- `navMeshAgent.hasPath == true`, `remainingDistance ≈ 21` (아직 목적지에 안 도착 - 갈 길이 남아있는데 멈춰있음)
- `attackMoveDestination == null` ← 결정적 단서
- `attackRange.HasTargetInRange == false`, `targetsInRange.Count == 0` (지금은 근처에 아무 대상도 없음)

세 기가 전부 같은 `navMeshAgent.destination`(같은 이동 명령을 받은 그룹)을 가리키고 있어, 이동 도중
무언가와 교전한 뒤 다시는 못 움직이게 된 것으로 확인됨.

코드로 원인을 추적한 결과, `AttackMoveTo()`가 아니라 **`MoveTo()`(순수 이동 명령)**에 있던, 지금까지와는
다른 별도의 구멍을 찾음:

```csharp
public void MoveTo(Vector3 destination)
{
    arrived = false;
    attackMoveDestination = null;   // ← 순수 이동이라 "공격-이동 아님" 표시로 null 처리
    currentState = AllyState.Move;
    unitEffects?.StopAttackEffects();
    MoveAgentTo(destination);
}
```

그런데 자동교전 판정(`EnemyAttackRange.Update()`)은 `currentState`가 `Move`인지 `Idle`인지 가리지
않는다 - `IsAttack()`(`attackRange.HasTargetInAttackRange`)이 사거리 안 대상 유무만으로 결정되기
때문에, **순수 이동 중이어도** 사거리 안에 상대가 들어오면 그대로 `Attack()`이 호출된다:

```csharp
if (enemyUnit.IsAttack() || enemyUnit.IsIdle())   // IsAttack()은 state와 무관 - Move 중에도 true 가능
{
    if (sqrDistance <= UnitRange * UnitRange)
        enemyUnit.Attack(target.transform.position, target);
    ...
}
```

`Attack()`은 `navMeshAgent.isStopped = true`만 걸고 `currentState`는 건드리지 않는다:

```csharp
public void Attack(Vector3 end, GameObject target)
{
    if (!isAirUnit)
    {
        navMeshAgent.isStopped = true;
    }
    ...
}
```

전투가 끝나면(상대가 죽거나 멀어지면) 다시 움직여야 하는데, 그 "교전 후 재개" 로직은
`AttackMoveTick()` 하나뿐이고, 이 메서드는 맨 위에서 곧바로 막힌다:

```csharp
private void AttackMoveTick()
{
    if (attackMoveDestination == null)   // 순수 이동이라 애초에 null이었으므로 여기서 항상 return
        return;
    ...
}
```

즉 **순수 이동(`MoveTo`) 중에 우연히 사거리 안에 들어온 상대를 한 대 때리는 순간, 그 유닛은 이동을
재개할 방법이 영원히 사라진다** - `attackMoveDestination`이 애초에 null이라 "교전 끝나면 원래 목적지로
복귀" 로직 자체가 작동할 수 없기 때문. `AttackMoveTo()`(A-이동)만 이 복귀 로직을 갖고 있고, 순수
`MoveTo()`는 갖고 있지 않았던 것.

실제로 이 순수 `MoveTo()`가 불리는 곳은 `AllyAIDirector.AssembleAtRally()`(웨이브 집결 중 rallyPoint로
이동)과 `AllyAIDirector.SpawnUnit()`(막 생산된 유닛이 집결지로 자동 이동) - 둘 다 "다른 아군이 이미
싸우고 있는 전장 근처를 그냥 지나가는" 상황이 흔해서, 사용자가 표현한 "아군 유닛이랑 같이 전투할 때"와
정확히 일치한다.

`EnemyUnitController.cs`의 `MoveTo()`도 완전히 동일한 코드 복제본이라 같은 구멍이 그대로 있음(적 AI
웨이브/별동대가 이동 중(교전 아님) 상태로 지나가다 마주쳐도 동일하게 멈출 수 있음) - doc/0581과 동일한
논리로 양쪽 다 고치는 게 맞다고 판단.

`UnitController.cs`(플레이어 직접 조종 유닛)는 완전히 다른(훨씬 복잡한) 명령 구조라 이번에도
범위 밖(doc/0581과 동일 판단 - 필요 시 별도 확인).

## 수정 제안

`MoveTo()`도 `AttackMoveTo()`처럼 `attackMoveDestination`을 채워서, 이동 중 우연히 벌어진 교전이 끝나면
`AttackMoveTick()`이 원래 목적지로 복귀시킬 수 있게 한다. `currentState`는 그대로 `Move`로 두되,
`AttackMoveTick()`이 재개할 때 `Idle`로 바꾸는 기존 동작은 그대로 유지 - 순수 이동이든 공격-이동이든
"교전 끝났으면 목적지로 계속 간다"는 결과는 동일해야 하므로 문제 없음. `IsMove()`(`currentState ==
Move`) 값을 참조하는 다른 코드는 없음을 확인함(둘 다 grep으로 확인 - 자기 자신 선언 외 참조 없음).

### AllyController.cs / EnemyUnitController.cs 공통 - `MoveTo()`

기존 코드 (AllyController.cs):
```csharp
    public void MoveTo(Vector3 destination)
    {
        arrived = false;
        attackMoveDestination = null;
        currentState = AllyState.Move;

        unitEffects?.StopAttackEffects(); // 공격 중이었다면 이동 명령으로 전환되므로 재생 중인 공격 이펙트를 즉시 정지

        MoveAgentTo(destination);
    }
```

변경 코드:
```csharp
    public void MoveTo(Vector3 destination)
    {
        arrived = false;
        // attackMoveDestination을 null로 비우면 안 된다 - 순수 이동 중이어도 자동교전(EnemyAttackRange)은
        // currentState와 무관하게 사거리 안 상대를 그대로 공격하고 navMeshAgent.isStopped를 켜는데,
        // "교전 끝나면 여기로 복귀"(AttackMoveTick)가 이 값이 null이면 절대 작동하지 않아 이동이 영영
        // 재개되지 않았다(doc/0584) - AttackMoveTo()와 똑같이 목적지를 채워서 같은 복귀 경로를 태운다.
        attackMoveDestination = destination;
        currentState = AllyState.Move;

        unitEffects?.StopAttackEffects(); // 공격 중이었다면 이동 명령으로 전환되므로 재생 중인 공격 이펙트를 즉시 정지

        MoveAgentTo(destination);
    }
```

`EnemyUnitController.cs`의 `MoveTo()`도 (`AllyState`→`EnemyState`만 다름) 동일하게 수정.

## 예상 영향

- 순수 이동 명령(`MoveTo`) 중 우연히 자동교전이 벌어졌다가 끝나는 모든 경로에 적용 - 웨이브 집결
  이동, 생산 직후 집결지 이동 등. 교전 후 원래 목적지로 계속 이동을 재개하게 되어 사용자가 본 "일부
  유닛만 멈춤" 증상이 해결됨.
- `UnitController.cs`(플레이어 유닛)는 범위 밖 - 필요 시 별도 확인.

## 변경 예정 파일

- Assets/Scripts/FogOfWar/Ally/AllyController.cs
- Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs

## 확인 필요
이대로 구현해도 될까?
