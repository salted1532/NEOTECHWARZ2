# 0392 - 추격 재탐색을 3초 주기로, 공격 후 도망 시 즉시 재탐색, 도달 불가 판정 전 재시도 추가 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료. 이후 doc/0393에서 판정 방식 개정됨 - "몇 번 더 재확인 후 취소" 부분은 폐기,
[[0393]] 참고.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 경로를 재탐색하는 시간을 3초 정도로 하고 만약 공격에 성공했는데 그 유닛이 도망갔을 경우 다시
> 재탐색 하도록해줘
> 그 유닛이 멈췄고 강제공격한 유닛도 해당위치에 도달할수 없으면 강제공격명령을 해제하는것 말고도
> 어느정도 재탐색 하다가 도달할수 없다고 판단되면 멈추도록도 추가해줘

즉 세 가지:
1. 사거리 밖에서 추격 중 대상 위치 재확인을 "매번 도착 시점"이 아니라 **3초 주기**로.
2. 공격 성공 후 대상이 사거리를 벗어나(도망) 다시 추격해야 하면 **대기 없이 즉시** 재탐색.
3. "도착했는데 대상도 안 움직였다" → 즉시 취소가 아니라, **몇 번 더 재확인**해보고 그래도 그대로면
   그때 최종적으로 도달 불가로 판정해 취소.

## 조사 결과

[[0391]]에서 만든 현재 로직(`FriendlyAttackTick`/`AttackOrderTick`)은 "도착(또는 더 갈 수 없어 멈춤)"
이라는 **한 번의 이벤트**에서만 대상 위치를 재확인하고, 그 자리에서 바로 취소 여부를 결정한다. 이번
요청은 그 판정을 시간 기반 주기 + 재시도 유예로 완화해 달라는 것.

- 세 요구사항 모두 하나의 공용 상태 머신으로 처리 가능. `FriendlyAttackTick`과 `AttackOrderTick`은
  거의 동일한 "사거리 밖 추격" 패턴을 반복하고 있어([[0391]]에서도 같은 이유로 페어로 고침), 이번에도
  헬퍼 메서드 하나로 통합해서 중복을 늘리지 않는다.
- "공격 성공 후 도망" 감지는 방금 전 틱에 사거리 안이었는지를 기억해두는 플래그(`chaseWasInAttackRange`)
  하나로 충분 - 사거리 안 → 밖으로 전이되는 그 프레임만 타이머 대기 없이 즉시 재탐색.
- "재시도 유예"는 재탐색 주기(3초)마다 "대상이 안 움직였고, 이 유닛도 실제로 도착/정지 상태"인 경우에만
  1씩 증가하는 카운터(`chaseStationaryStreak`)로 처리. 이 카운터가 임계값(재시도 2회, 즉 최초 정지
  확인 후 최대 약 6초)에 도달해야 최종적으로 도달 불가로 판정한다. 대상이 조금이라도 움직이면 즉시
  0으로 리셋.
- 아직 정상적으로 이동 중(도착 전)인데 3초가 지났고 대상 위치도 그대로라면, 그건 "도달 불가"가 아니라
  "그냥 원래 목적지로 잘 가고 있는 중"이므로 재시도 카운터를 건드리지 않는다 - 실제로 멈춰서
  더 못 가는 상태(`!pathPending && remainingDistance <= stoppingDistance`)일 때만 카운트한다.

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs` - 새 필드 (622번째 줄 `lastMoveAgentToDestination` 아래)

```csharp
    // 도달 불가능할 수 있는 대상(친구 강제공격/명시 추격)을 사거리 밖에서 계속 쫓을 때 쓰는 상태.
    // 매 프레임 실시간 위치로 재탐색하면 [[0391]]처럼 멈칫거리므로 ChaseRepathInterval(3초)마다만
    // 재확인한다. 재확인했는데 대상이 안 움직였고 이 유닛도 실제로 도착/정지한 상태면
    // chaseStationaryStreak을 늘리고, ChaseUnreachableRetries번 연속이면 그때 진짜 도달 불가로
    // 최종 판정한다(그 전까지는 몇 번 더 재확인할 유예를 준다). 방금 사거리 안에서 밖으로 벗어난
    // 프레임(공격 중이던 대상이 도망감)은 타이머 대기 없이 즉시 재탐색한다 (doc/0392).
    private const float ChaseRepathInterval = 3f;
    private const int ChaseUnreachableRetries = 2;
    private float chaseRepathTimer;
    private int chaseStationaryStreak;
    private bool chaseWasInAttackRange;
```

### 새 헬퍼 메서드 (`MoveAgentTo` 근처에 추가)

```csharp
    // 사거리 밖에서 대상을 계속 쫓을 때 FriendlyAttackTick/AttackOrderTick이 공용으로 쓰는 이동 갱신.
    // 반환값 true면 호출자가 도달 불가로 최종 판정된 것 - CancelAttackOrder() + HaltInPlace()로
    // 마무리해야 한다 (doc/0392).
    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit)
        {
            MoveAgentTo(targetPos, destinationIsAirborne);
            return false;
        }

        if (justLeftAttackRange)
        {
            // 방금까지 사거리 안(공격 중)이었는데 대상이 도망가서 벗어남 - 대기 없이 바로 재탐색
            chaseRepathTimer = 0f;
            chaseStationaryStreak = 0;
            MoveAgentTo(targetPos, false);
            return false;
        }

        chaseRepathTimer += Time.deltaTime;
        if (chaseRepathTimer < ChaseRepathInterval)
            return false; // 재탐색 주기 전 - 기존 경로/정지 상태 그대로 유지

        chaseRepathTimer = 0f;

        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
            chaseStationaryStreak = 0;
            return false;
        }

        bool arrivedOrStuck = !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;
        if (!arrivedOrStuck)
            return false; // 대상은 그대로인데 아직 정상적으로 이동 중 - 도달 불가 판정 대상 아님

        chaseStationaryStreak++;
        return chaseStationaryStreak >= ChaseUnreachableRetries;
    }
```

### `FriendlyAttackTick()` ([[0391]]에서 바꾼 버전 기준) 변경

기존:
```csharp
        Vector3 targetPos = friendlyTarget.transform.position;
        float sqrDistance = (transform.position - targetPos).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(targetPos, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
            return;
        }

        if (isAirUnit)
        {
            MoveAgentTo(targetPos, IsAirborne(friendlyTarget)); // 사거리 밖: 거리 상관없이 끝까지 추격
            return;
        }

        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            if (!navMeshAgent.hasPath)
                MoveAgentTo(targetPos, false);
            return;
        }

        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
        }
        else
        {
            CancelAttackOrder();
            HaltInPlace();
        }
    }
```

변경:
```csharp
        Vector3 targetPos = friendlyTarget.transform.position;
        float sqrDistance = (transform.position - targetPos).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(targetPos, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
            chaseWasInAttackRange = true;
            return;
        }

        bool justLeftAttackRange = chaseWasInAttackRange;
        chaseWasInAttackRange = false;

        if (UpdateUnreachableChase(targetPos, IsAirborne(friendlyTarget), justLeftAttackRange))
        {
            // 재탐색을 몇 번 더 해봐도 대상이 계속 그 자리 + 이 유닛도 더 못 감 - 진짜 도달 불가 (doc/0384/0392)
            CancelAttackOrder();
            HaltInPlace();
        }
    }
```

### `AttackOrderTick()` 변경

기존(사거리 밖 분기):
```csharp
                if (!inAttackRange)
                {
                    if (isAirUnit)
                    {
                        MoveAgentTo(attackMoveDestination.Value);
                        return;
                    }

                    if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
                    {
                        if (!navMeshAgent.hasPath)
                            MoveAgentTo(attackMoveDestination.Value);
                        return;
                    }

                    bool targetMoved = !lastMoveAgentToDestination.HasValue ||
                        (lastMoveAgentToDestination.Value - attackMoveDestination.Value).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

                    if (targetMoved)
                    {
                        MoveAgentTo(attackMoveDestination.Value);
                    }
                    else
                    {
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }
                }
```

변경:
```csharp
                if (inAttackRange)
                {
                    chaseWasInAttackRange = true;
                }
                else
                {
                    bool justLeftAttackRange = chaseWasInAttackRange;
                    chaseWasInAttackRange = false;

                    if (UpdateUnreachableChase(attackMoveDestination.Value, false, justLeftAttackRange))
                    {
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }
                }
```

(참고: `inAttackRange`일 때의 실제 공격은 `AttackRange.cs`가 별도로 `unitController.Attack(...)`을 호출하므로
여기선 상태 플래그만 갱신하면 된다.)

### 명령 발급/취소 시 상태 리셋

`AttackUnitTarget()`, `AttackFriendlyTarget()`(새 명령 시작), `CancelAttackOrder()`(명령 종료) 각각에
`chaseRepathTimer = 0f; chaseStationaryStreak = 0; chaseWasInAttackRange = false;` 3줄 추가 - 이전
명령의 타이머/재시도 카운트가 다음 명령으로 새 나가지 않도록.

## 열린 질문

- 재시도 횟수(`ChaseUnreachableRetries = 2`, 최초 정지 확인 포함 최대 약 6~9초)와 주기(3초)는 임의값 -
  너무 오래 매달려 있는 느낌이면 줄이면 됨.
- "공격 성공 후 도망" 즉시 재탐색은 `FriendlyAttackTick`/`AttackOrderTick`이 매 프레임 도는 한 이론상
  다음 프레임(사실상 즉시)에 반응함.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
