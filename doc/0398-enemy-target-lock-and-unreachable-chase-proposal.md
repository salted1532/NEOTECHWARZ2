# 0398 - 적 유닛 "타겟 락"과 도달 불가 추격 포기 설계 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

사용자가 스타크래프트의 타겟 락(한 번 목표를 정하면 죽거나/시야를 잃거나/도달 불가/새 명령/더 높은
우선순위 목표가 아니면 웬만하면 바꾸지 않는 성향) 메커니즘을 참고 자료로 제시하고, 이걸 참고해서
**적 유닛(EnemyUnitController/EnemyAttackRange)**에도 비슷한 로직을 적용해줄 것을 요청. 구체적으로
지적한 버그:

> 적 AttackRange 범위 안에 아군이 들어옴 → 그 위치로 추적 → 도달할 수 없는 언덕위이면 가장 가까운
> 위치를 탐색 → 이동. 이동 중 다시 AttackRange 안에 아군이 들어갔다 나오는데, 그 경우 그 유닛을
> 추적하게 되어있어서 그 과정에서 멈칫거리는 것 같다.

## 조사 결과 - 지금 적 AI가 서 있는 위치

플레이어 쪽(`UnitController`)은 이번 세션에서 여러 번 개정을 거쳐 [[0397]]로 확정됐다: "도착
이벤트에서만 재탐색, 대상이 그 자리 그대로면 포기, 움직였으면 계속 추격(횟수 제한 없음)". 반면
**적 쪽(`EnemyUnitController`/`EnemyAttackRange`)은 이 개정을 하나도 못 받았다**:

- `EnemyAttackRange.Update()`(107번째 줄)는 매 프레임 `GetEngagedOrClosestTarget()`으로 뽑은 대상이
  사거리 밖이고 `IsIdle()`이면 `enemyUnit.ChaseTarget(target.transform.position)`을 **매 프레임**
  호출한다.
- `EnemyUnitController.ChaseTarget(pos)`(321번째 줄)는 그 위치로 곧장 `MoveAgentTo(pos)`를 호출한다
  - "아직 이동 중이면 대기, 도착 이벤트에서만 재탐색" 같은 게이트가 전혀 없다. `MoveAgentTo`
    자체엔 [[0386]]의 0.5m 오차 캐시가 있지만, 아군이 언덕 위에서 계속 조금씩 움직이면 그 캐시를
    금방 넘어서서 매번 `SetDestination`(+도달 불가 지역이라 매번 `NavMesh.SamplePosition` 폴백까지)이
    다시 실행된다 - 정확히 플레이어 쪽에서 고쳤던 [[0391]] 버그의 재발이다.
- 도달 불가 상태에서 "포기하고 다른 대상을 찾는" 개념 자체가 없다. `GetEngagedOrClosestTarget()`
  ([[0388]])은 "물고 있던 대상이 감지 범위+여유 밖으로 완전히 나가기 전까지는 계속 우선시"하는
  히스테리시스만 있어서, 도달 불가능한 대상은 감지 범위 안에 계속 머물러 있는 한 **영원히 그 대상만
  붙잡고 있는다** - 사용자가 언급한 "저글링2 등장 시 갈아탐" 사례가 지금 구조에서는 절대 발생하지
  않는다.

정리하면: 사용자가 참고자료로 준 "타겟 락" 원칙 중 **"죽음"/"도메인 불가"/"은신"** 조건은 이미
`CanEngage`로 커버되고, **"시야 이탈"**은 [[0388]]의 히스테리시스 거리로 어느 정도 커버되지만,
**"도달 불가"** 조건만 완전히 빠져있다 - 그래서 (1) 멈칫거림과 (2) 새 대상으로 못 넘어가는 문제가
동시에 발생한다.

## 설계안

### 1. `EnemyUnitController.ChaseTarget()` - 도착 이벤트 기준 재탐색 + 포기 신호

[[0397]]에서 확정한 `UnitController.UpdateUnreachableChase()`와 동일한 판단 구조를 그대로 적용하되,
적 유닛은 "공격 명령 취소" 같은 개념이 없으므로 대신 **"이 목적지는 포기했다"를 호출자(EnemyAttackRange)에게
알리는 `bool` 반환값**으로 노출한다.

```csharp
// Idle 상태에서 사거리 밖의 감지된 상대에게 다가갈 때 EnemyAttackRange가 호출한다.
// 반환값 true면 "도착했는데 대상이 그 자리 그대로 있어서 더는 다가갈 수 없다"는 뜻 - 호출자가
// 이 대상을 포기하고 다른 대상을 찾아야 한다 (doc/0398, [[0397]]과 동일한 판단 구조).
public bool ChaseTarget(Vector3 pos)
{
    arrived = false;
    currentState = EnemyState.Idle;

    if (isAirUnit)
    {
        MoveAgentTo(pos);
        return false;
    }

    if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
    {
        // 아직 이동 중 - 도착 전까지는 매 프레임 재탐색하지 않는다 (멈칫거림 방지, doc/0391 재적용)
        if (!navMeshAgent.hasPath)
            MoveAgentTo(pos); // 아직 이동을 시작 안 했으면 최초 탐색
        return false;
    }

    // 도착(또는 더 갈 수 없어 멈춤) - 그 사이 대상이 움직였는지 확인
    bool targetMoved = !lastMoveAgentToDestination.HasValue ||
        (lastMoveAgentToDestination.Value - pos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

    if (targetMoved)
    {
        MoveAgentTo(pos); // 새 위치로 재탐색하고 계속 추격
        return false;
    }

    return true; // 도착했고 대상도 그 사이 안 움직였다 - 도달 불가로 최종 판정, 포기
}
```

### 2. `EnemyAttackRange` - 포기한 대상은 블랙리스트, 다른 대상에게 양보

```csharp
// ChaseTarget()이 "도달 불가"로 판정해 포기한 대상. 다시 나타나기(트리거 밖으로 나갔다 들어오기) 전까지는
// GetClosestTarget() 후보에서 제외해서, 매번 같은 도달 불가 대상을 다시 골랐다가 다시 포기하는 것을
// 반복하지 않는다 - 그래야 저글링2처럼 다른(도달 가능한) 대상이 있으면 그쪽으로 넘어간다 (doc/0398).
private GameObject unreachableTarget;

private void Update()
{
    targetsInRange.RemoveAll(target => target == null);

    GameObject target = GetEngagedOrClosestTarget();
    if (target == null)
        return;

    float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;

    if (enemyUnit.IsAttack() || enemyUnit.IsIdle())
    {
        if (sqrDistance <= UnitRange * UnitRange)
        {
            enemyUnit.Attack(target.transform.position, target);
        }
        else if (enemyUnit.IsIdle())
        {
            if (enemyUnit.ChaseTarget(target.transform.position))
            {
                // 도달 불가로 최종 판정 - 이 대상은 포기하고 다음 프레임부터 다른 대상을 찾는다
                unreachableTarget = target;
                if (engagedTarget == target)
                    engagedTarget = null;
            }
        }
    }
}

private void OnTriggerExit(Collider other)
{
    if (!IsValidTarget(other))
        return;

    targetsInRange.Remove(other.gameObject);

    if (unreachableTarget == other.gameObject)
        unreachableTarget = null; // 감지 범위를 완전히 벗어났다 다시 들어오면 다시 시도해볼 기회를 준다
}

private GameObject GetClosestTarget()
{
    GameObject closest = null;
    float closestSqrDist = float.MaxValue;

    foreach (GameObject target in targetsInRange)
    {
        if (target == null)
            continue;

        if (target == unreachableTarget) // 도달 불가로 이미 포기한 대상은 다시 뽑지 않는다
            continue;

        if (!CanEngage(target))
            continue;

        float sqrDist = (target.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < closestSqrDist)
        {
            closestSqrDist = sqrDist;
            closest = target;
        }
    }

    return closest;
}
```

`HasTargetInRange`/`HasTargetInAttackRange`도 `unreachableTarget`을 건너뛰도록 같은 필터를 넣어야
한다 - 안 그러면 도달 불가 대상이 넓은 감지 콜라이더 안에 계속 머무는 동안 `AttackMoveTick()`이
"아직 교전 중"으로 착각해서 공격-이동 재개를 영원히 막는 **새로운 멈춤 버그**가 생긴다 (조사 중
발견 - 사용자가 보고한 멈칫거림과는 별개의 잠재 버그).

```csharp
public bool HasTargetInRange
{
    get
    {
        foreach (GameObject target in targetsInRange)
        {
            if (target != null && target != unreachableTarget)
                return true;
        }

        return false;
    }
}
```

`HasTargetInAttackRange`는 `GetClosestTarget()`을 그대로 쓰므로(이미 `unreachableTarget` 필터가
들어감) 별도 수정 불필요.

## 스타크래프트 참고자료의 "타겟 락" 조건과 현재/제안 상태 매핑

| 조건 | 현재 상태 |
|---|---|
| 대상이 죽음 | 이미 구현됨 (`targetsInRange.RemoveAll(target => target == null)`) |
| 도메인 불가/은신 | 이미 구현됨 (`CanEngage`) |
| 시야 이탈(감지 범위 완전히 벗어남) | 이미 구현됨 ([[0388]] 히스테리시스 - 사거리+마진+3m 밖으로 나가야 포기) |
| **도달 불가** | **이번에 추가하는 부분** |
| 플레이어의 새 명령 | 해당 없음 - 적 AI는 플레이어가 직접 명령하지 않음 |
| 더 높은 우선순위 목표 등장 | 미구현 - 우선순위 체계 자체가 없음(플레이어 쪽도 없음). 필요하면 별도 논의로 분리 제안 (예: 일꾼보다 공격 유닛 우선 등) - 이번 제안 범위 밖 |

## 열린 질문

1. `unreachableTarget`은 감지 범위를 완전히 벗어나야만(트리거 Exit) 다시 시도 대상이 된다 - 그
   전에 언덕을 내려와서 도달 가능해져도 다시 시도하지 않는다. [[0397]]에서 "단순함이 낫다"고
   확정한 것과 같은 방향의 트레이드오프라 일단 이렇게 제안하지만, 필요하면 나중에 "일정 시간 후
   재시도" 같은 걸 추가할 수 있음.
2. "더 높은 우선순위 목표 등장" 조건은 이번 제안에 포함하지 않음 - 필요하면 별도 요청으로.
3. `HasTargetInRange`가 막는 "공격-이동 멈춤" 버그는 이번 조사 중 발견한 것으로, 사용자가 보고한
   멈칫거림과는 별개 문제 - 같이 고치는 게 맞다고 보고 포함시켰음. 원치 않으면 빼고 진행 가능.

## 영향받는 파일 (예정)

- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
