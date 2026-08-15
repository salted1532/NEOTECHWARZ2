# 0581. 아군 OC 공격 별동대가 적과 조우 후(추격 포기 시) 멈추는 버그 - 원인 및 수정 제안

- 날짜: 2026-08-15

## 요청 내용

- "아군OC 공격 별동대 적 유닛과 조우후 명령이 사라져 그대로 멈춤현상이 발생하네 확인좀 해줘 / 적유닛하고 전투 하더라도 지속적으로 공격 명령이 끊기지 않도록"

## 조사 내용

같은 증상(전투 후 공격-이동 명령이 사라져 멈춤)이 [[doc/0575]]에서 이미 한 번 다뤄졌고, 그 수정(`attackMoveDestination`을 "진짜 그 지점에 도착했을 때만" 지우도록 한 것)은 `AllyController.cs`/`EnemyUnitController.cs`에 이미 적용돼 있음을 코드로 확인함(git status상 두 파일 모두 수정 없이 이미 커밋된 상태). 즉 0575가 고친 경로("추격하던 적 위치에 도착한 걸 원래 목적지 도착으로 착각")는 정상 작동 중.

그런데 코드를 처음부터 다시 끝까지 추적한 결과, **0575와는 다른 별도의 구멍**을 발견함 - "추격 중이던 적이 도달 불가능(unreachable)하다고 최종 판정돼 포기하는 경로"에 있음.

`AllyController.ChaseTarget()`(`EnemyUnitController.ChaseTarget()`도 동일하게 복제된 코드, doc/0452) 중 도달 불가 모드 처리:

```csharp
bool targetMoved = !lastMoveAgentToDestination.HasValue || ...;

if (!targetMoved)
    return true; // 대상도 그 자리 그대로 - 도달 불가로 최종 판정, 포기

MoveAgentTo(pos);
return false;
```

이 `return true;`(포기) 순간, `navMeshAgent.isStopped`는 **한 번도 `true`로 설정되지 않는다.** 이전에 걸었던 `SetDestination(적_원래_위치)`가 그대로 남아있고(실제로 도달 불가능한 원본 좌표), 에이전트는 "갈 수 있는 데까지"(partial path 끝)만 가서 물리적으로는 멈춰 있지만, `navMeshAgent.destination`(=SetDestination에 넘긴 원본 좌표) 자체는 여전히 멀리 떨어져 있다.

`Update()`의 도착 판정은 `(transform.position - navMeshAgent.destination).sqrMagnitude <= arriveDistance^2`로 "진짜 그 좌표에 도착했는지"만 보기 때문에, 도달 불가능한 좌표까지의 거리는 영원히 `arriveDistance`보다 크게 남아 이 블록이 절대 실행되지 않는다 → `navMeshAgent.isStopped`가 끝내 `true`가 되지 않는다.

그 사이 `EnemyAttackRange.Update()`는 `ChaseTarget()`이 `true`(포기)를 반환하면 그 대상을 `unreachableTarget`으로 등록해 더 이상 "교전 중"으로 치지 않는다 - 그래서 `AllyController.AttackMoveTick()`의 `HasTargetInRange` 가드는 정상적으로 풀린다:

```csharp
private void AttackMoveTick()
{
    if (attackMoveDestination == null)
        return;

    if (attackRange != null && attackRange.HasTargetInRange)
        return; // 여기는 정상적으로 통과됨 (unreachableTarget 제외 덕분)

    bool groundStopped = !isAirUnit && navMeshAgent.isStopped; // ← 위 이유로 영원히 false
    ...
    if (groundStopped || airStopped)
    {
        ...
        MoveAgentTo(attackMoveDestination.Value); // ← 이 재발령이 영영 안 일어남
    }
}
```

즉 `attackMoveDestination`은 (0575 덕분에) 멀쩡히 남아있는데, `AttackMoveTick`이 "교전이 끝나 정지된 상태"를 판단하는 유일한 신호(`navMeshAgent.isStopped`)가 이 give-up 경로에서만 절대 켜지지 않아서 원래 목적지로 재발령이 영원히 안 걸린다 - 유닛은 그 자리에 물리적으로 멈춘 채 아무 명령도 다시 못 받는다.

지형/장애물/다른 유닛 벽 등으로 도달 불가능한 적과 조우했을 때만 재현되는 조건이라(항상은 아님), 사용자가 본 "적과 조우 후 명령이 사라져 멈춘다"는 증상과 정확히 일치함. 공중 유닛은 `ChaseTarget()`에서 `isAirUnit`이면 항상 `return false;`(포기 개념 자체가 없음)라 이 버그 대상이 아님 - 지상 유닛만 해당.

`AllyController.cs`/`EnemyUnitController.cs` 양쪽 다 완전히 동일한 코드 복제본이라 같은 구멍이 그대로 있음(적 AI 별동대/웨이브도 같은 상황에서 동일하게 멈출 수 있음 - 이번 요청은 아군 OC만 언급했지만 근본 원인은 공용이라 둘 다 고치는 게 맞다고 판단).

## 수정 제안

`ChaseTarget()`이 도달 불가로 최종 포기(`return true`)하는 바로 그 지점에서, `Update()`의 도착 처리 블록이 하는 것과 동일하게 `navMeshAgent.isStopped = true`를 명시적으로 걸어준다. "더 이상 이 대상을 쫓지 않기로 했다"는 것 자체가 "정지 상태"이므로, 이동 상태 플래그를 그 결정과 함께 맞춰주는 게 근본 수정.

### AllyController.cs / EnemyUnitController.cs 공통 - ChaseTarget() 도달 불가 최종 포기 분기

기존 코드:
```csharp
if (!targetMoved)
    return true; // 대상도 그 자리 그대로 - 도달 불가로 최종 판정, 포기
```

변경 코드:
```csharp
if (!targetMoved)
{
    // 포기하는 순간 navMeshAgent.isStopped를 켜지 않으면, SetDestination에 남아있는 원본(도달
    // 불가능한) 좌표까지의 거리가 영원히 arriveDistance보다 커서 Update()의 도착 판정이 절대
    // isStopped를 true로 못 만든다 - 그러면 AttackMoveTick()이 "교전 후 정지"를 감지 못 해
    // attackMoveDestination이 멀쩡히 남아있어도 원래 목적지로 재발령을 영영 못 한다(doc/0581).
    navMeshAgent.isStopped = true;
    return true; // 대상도 그 자리 그대로 - 도달 불가로 최종 판정, 포기
}
```

## 예상 영향

- 지상 유닛이 벽/장애물 너머 등 도달 불가능한 적을 쫓다가 포기하는 모든 경로에 적용 - 아군 OC 공격 별동대/수비대뿐 아니라 적 AI(외계종족/적대 OC) 별동대/웨이브, 플레이어가 A(공격-이동) 명령을 내린 아군 OC 유닛에도 동일하게 적용됨(같은 버그의 근본 수정이라 범위를 좁힐 이유가 없음).
- `UnitController.cs`(플레이어 직접 조종 유닛)는 별도 구조([[doc/0575]] 조사에서 이미 확인)라 이번 범위 밖 - 필요 시 별도 확인.

## 변경 예정 파일

- Assets/Scripts/FogOfWar/Ally/AllyController.cs
- Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs

## 결과

- 사용자 확인 후 `AllyController.cs`/`EnemyUnitController.cs` 양쪽에 위 수정을 그대로 적용함.
- `npx uloop-cli compile`로 컴파일 확인 완료 (0 에러, 0 경고).
