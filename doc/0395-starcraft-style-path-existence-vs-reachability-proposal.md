# 0395 - 스타크래프트식 "공격 가능 vs 도달 가능" 분리 판단 도입 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료. 이후 [[0396]]에서 카운트 기반 포기를 임시 비활성화, [[0397]]에서 그 상태를
최종 확정하며 이 문서의 진행도 감시/카운트 로직 자체가 삭제됨 - 최종 동작은 [[0397]] 참고.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

사용자가 스타크래프트(브루드워)의 추격/포기 메커니즘을 참고 자료로 제시하며 로직 개선을 요청:

> 스타크래프트에서는 "공격할 수 있는가"와 "도달할 수 있는가"를 따로 판단합니다 [...] 아래 조건을
> 두는 것이 자연스럽습니다: 공격 명령 → 경로 계산 → 경로 존재? → (있으면) 이동하며 공격 / (없으면)
> 3~5초 재시도 → 그래도 없으면 포기. 또는 이동 중에도 "이전 거리=25, 5초 후 거리=24.8 → 거의 안
> 줄음 → 막혀있다고 판단 → 포기". 다음 네 가지를 조합: Path Exists / Distance Progress / Stuck
> Timer(2~5초) / Repath Count.

## 조사 결과 - 현재 로직의 빈 구멍

[[0394]]에서 만든 `UpdateUnreachableChase()`는 "도착(또는 더 갈 수 없어 멈춤)" **이벤트**에서만
재확인하고, 그 전까지(`navMeshAgent.pathPending || remainingDistance > stoppingDistance`)는 완전히
아무것도 하지 않는다:

```csharp
        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            if (!navMeshAgent.hasPath)
                MoveAgentTo(targetPos, false);
            return false;   // <- 여기서 그냥 아무 판단 없이 계속 기다림
        }
```

이건 사용자가 참고자료에서 지적한 **"Distance Progress" / "Stuck Timer" 신호가 전혀 없다**는 뜻이다.
즉 유닛이 다른 유닛들에게 둘러싸여 낀 채로(교통 혼잡) `remainingDistance`가 `stoppingDistance` 밑으로
절대 안 내려가는 상태로 무한정 제자리걸음을 해도, 지금 코드는 "아직 이동 중"으로만 보고 영원히
기다린다 - `NavMeshAgent`가 "도착"이라고 스스로 보고해줄 때만 판단하기 때문. 참고자료의 예시("이전
거리 25 → 5초 후 24.8 → 막힘")가 정확히 이 사각지대를 가리키고 있다.

반대로 "도착 이벤트"에서의 판단([[0393]]에서 정한: 대상이 그대로면 즉시 포기 / 대상이 움직여
재탐색이 3회 쌓이면 포기)은 이미 `Path Exists`(실패 시 `MoveAgentTo`가 fallback도 실패)와 `Repath
Count`를 사실상 반영하고 있다.

## 제안: 이동 중에도 "거리 진행도" 감시 추가

기존 이벤트 기반 판단은 그대로 두고, **아직 도착 전(이동 중)**인 구간에 "목표와의 실제 거리가
`ChaseStuckTimeout`(4초, 참고자료 2~5초 범위 중간값) 동안 `ChaseProgressThreshold`(1m) 이상
줄어드는지"를 감시하는 로직을 추가한다. 줄지 않으면 "막혔다"고 보고 기존 재시도 카운터
(`chaseRepathCount` → 의미가 넓어지므로 `chaseStuckCount`로 개명)를 누적, 이 값이
`ChaseUnreachableRepathLimit`(3회, 최대 약 12초)에 도달하면 도달 불가로 포기.

이렇게 하면 참고자료의 4가지 신호가 모두 반영된다:
- **Path Exists**: `MoveAgentTo` 성공/실패(기존, [[0375]]/[[0386]]).
- **Distance Progress + Stuck Timer**: 이번에 추가하는, 이동 중에도 도는 거리 진행도 감시.
- **Repath Count**: 이동 중 막힘 감지든 도착 후 대상 이동 감지든 동일한 카운터로 누적, 3회에서 최종
  포기 (기존 [[0393]]/[[0394]] 로직 재사용).

주의: 이동 중 거리 감시는 **타이머로 매 프레임 `MoveAgentTo`를 다시 부르지 않는다** ([[0391]]의
멈칫거림 원인 재발 방지) - 오직 거리를 관찰만 하고, 실제로 "막혔다"고 확정된 순간에만 카운터를
올릴 뿐 경로 재요청은 하지 않는다(같은 목적지라 다시 요청해도 의미 없음 - 정체는 로컬 회피가 스스로
풀어주길 기다리는 것 외엔 방법이 없다).

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs`

기존([[0394]]):
```csharp
    private const int ChaseUnreachableRepathLimit = 3;
    private int chaseRepathCount;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit)
        {
            MoveAgentTo(targetPos, destinationIsAirborne);
            return false;
        }

        if (justLeftAttackRange)
        {
            chaseRepathCount = 0;
            MoveAgentTo(targetPos, false);
            return false;
        }

        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            if (!navMeshAgent.hasPath)
                MoveAgentTo(targetPos, false);
            return false;
        }

        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
            chaseRepathCount++;
            return chaseRepathCount >= ChaseUnreachableRepathLimit;
        }

        return true;
    }
```

변경:
```csharp
    // 이동 중(도착 전) "실제 거리가 줄고 있는가"를 감시하는 데 쓰는 임계값. 스타크래프트류 RTS의
    // Path Exists(존재) vs Reachable(도달 가능)을 분리한 판단 참고 - NavMeshAgent가 "도착"이라고
    // 보고하지 않아도(다른 유닛에 낀 정체 등) 목표와의 거리가 이 시간 동안 이만큼도 안 줄면 막힌
    // 것으로 본다 (doc/0395).
    private const float ChaseStuckTimeout = 4f;
    private const float ChaseProgressThreshold = 1f;
    private const int ChaseUnreachableRepathLimit = 3;
    private float chaseStuckTimer;
    private float? chaseProgressBaselineDistance;
    private int chaseStuckCount;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit)
        {
            MoveAgentTo(targetPos, destinationIsAirborne);
            return false;
        }

        if (justLeftAttackRange)
        {
            // 방금까지 사거리 안(공격 중)이었는데 대상이 도망가서 벗어남 - 즉시 재탐색, 진행 감시 새로 시작
            chaseStuckTimer = 0f;
            chaseProgressBaselineDistance = null;
            chaseStuckCount = 0;
            MoveAgentTo(targetPos, false);
            return false;
        }

        bool arrivedOrStuck = !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;

        if (arrivedOrStuck)
        {
            // 갈 수 있는 데까지 다 감 - 그 사이 대상이 움직였는지 확인
            bool targetMoved = !lastMoveAgentToDestination.HasValue ||
                (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

            if (targetMoved)
            {
                MoveAgentTo(targetPos, false);
                chaseStuckTimer = 0f;
                chaseProgressBaselineDistance = null;
                chaseStuckCount++;
                return chaseStuckCount >= ChaseUnreachableRepathLimit;
            }

            return true; // 도착했고 대상도 그대로 - 바로 도달 불가로 최종 판정
        }

        // 아직 정상적으로 이동 중(도착 전) - 그런데도 실제 거리가 안 줄면(다른 유닛에 낀 정체 등으로
        // 막힘) 그것도 사실상 도달 실패로 본다.
        if (!navMeshAgent.hasPath)
            MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색

        float distance = Vector3.Distance(transform.position, targetPos);

        if (!chaseProgressBaselineDistance.HasValue)
        {
            chaseProgressBaselineDistance = distance;
            chaseStuckTimer = 0f;
            return false;
        }

        chaseStuckTimer += Time.deltaTime;
        if (chaseStuckTimer < ChaseStuckTimeout)
            return false; // 아직 진행도를 판단할 시간이 안 됨

        float progressed = chaseProgressBaselineDistance.Value - distance;
        chaseStuckTimer = 0f;
        chaseProgressBaselineDistance = distance;

        if (progressed >= ChaseProgressThreshold)
        {
            chaseStuckCount = 0; // 실제로 가까워지고 있다 - 정상 진행 중이니 막힘 카운트 리셋
            return false;
        }

        // ChaseStuckTimeout(4초) 동안 ChaseProgressThreshold(1m)도 안 가까워짐 - 막힌 것으로 판정
        chaseStuckCount++;
        return chaseStuckCount >= ChaseUnreachableRepathLimit;
    }
```

`CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의 초기화 지점 3곳에서
`chaseRepathCount = 0;` → `chaseStuckTimer = 0f; chaseProgressBaselineDistance = null; chaseStuckCount = 0;`
로 교체.

## 열린 질문

- `ChaseStuckTimeout`(4초)/`ChaseProgressThreshold`(1m)/`ChaseUnreachableRepathLimit`(3회, 이동 중
  막힘 기준 최대 약 12초)는 임의값 - 너무 오래 매달리면 줄이면 됨.
- 이번 제안은 "이동 중 거리 진행도 감시"만 새로 추가한 것 - "도착 이벤트에서의 즉시 판단/재탐색
  누적"([[0393]]/[[0394]])은 그대로 유지. 두 경로가 같은 `chaseStuckCount`를 공유해서, 이동 중
  막혔다가 겨우 도착 이벤트까지 갔는데 대상이 또 움직인 경우처럼 두 상황이 섞여도 누적이 이어진다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
