# 0394 - 3초 재탐색 타이머 제거, 도착 이벤트 기준으로만 재탐색 (누적 3회 판정 유지)

**날짜:** 2026-08-03

**구현 완료 (사용자가 정확히 지정한 직접 지시 - [[0393]] 판정 방식은 유지하고 시간 주기만 제거).**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 3초마다 재탐색을 빼줘 경로를 탐색하고 도착하고 재탐색 누적 3회로 해줘

[[0393]]에서 넣은 `ChaseRepathInterval`(3초 타이머) 게이트를 완전히 제거하고, [[0391]]의 "도착
이벤트에서만 재확인" 방식으로 되돌린다. 취소 판정 로직 자체([[0393]]에서 정한: 대상이 그대로면
즉시 취소 / 대상이 움직여 재탐색이 쌓이면 3회에서 취소)는 그대로 유지.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs`

`chaseRepathTimer`/`ChaseRepathInterval` 필드·상수와 `Time.deltaTime` 누적 게이트를 제거하고,
[[0391]]에서 쓰던 "아직 이동 중이면 도착까지 대기" 게이트로 교체.

기존([[0393]]):
```csharp
    private const float ChaseRepathInterval = 3f;
    private const int ChaseUnreachableRepathLimit = 3;
    private float chaseRepathTimer;
    private int chaseRepathCount;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit) { ... }

        if (justLeftAttackRange)
        {
            chaseRepathTimer = 0f;
            chaseRepathCount = 0;
            MoveAgentTo(targetPos, false);
            return false;
        }

        chaseRepathTimer += Time.deltaTime;
        if (chaseRepathTimer < ChaseRepathInterval)
            return false; // 3초 주기 전

        chaseRepathTimer = 0f;

        bool targetMoved = ...;
        if (targetMoved) { ... chaseRepathCount++; return chaseRepathCount >= ChaseUnreachableRepathLimit; }

        bool arrivedOrStuck = !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;
        if (!arrivedOrStuck)
            return false;

        return true;
    }
```

변경:
```csharp
    private const int ChaseUnreachableRepathLimit = 3;
    private int chaseRepathCount;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit) { ... }

        if (justLeftAttackRange)
        {
            chaseRepathCount = 0;
            MoveAgentTo(targetPos, false);
            return false;
        }

        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            // 아직 이동 중 - 도착 전까지는 재탐색하지 않는다 (doc/0391)
            if (!navMeshAgent.hasPath)
                MoveAgentTo(targetPos, false); // 최초 탐색
            return false;
        }

        // 도착(또는 더 갈 수 없어 멈춤)
        bool targetMoved = ...;
        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
            chaseRepathCount++;
            return chaseRepathCount >= ChaseUnreachableRepathLimit; // 재탐색 누적 3회면 취소
        }

        return true; // 도착했는데 대상도 그대로 - 바로 도달 불가 판정
    }
```

`CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의 초기화 지점에서도
`chaseRepathTimer = 0f;` 라인 제거, `chaseRepathCount = 0;`만 남김.

## 요약

- 경로 탐색 → 이동 → **도착** → (대상이 그 사이 움직였으면) 재탐색 → 이동 → 도착 → ... 반복.
- 도착할 때마다 대상이 그대로면 **즉시** 도달 불가 판정 + 강제공격 취소.
- 도착할 때마다 대상이 움직여서 재탐색이 발생하면 그 횟수를 누적, **3회**째에 도달 불가로 판정.
- 시간 기반 주기는 완전히 빠졌다 - 오직 "도착" 이벤트에서만 재확인한다.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
