# 0393 - 도달 불가 판정 방식 개정: 정지 대상은 즉시 취소, 이동 대상은 재탐색 누적 3회로 판정

**날짜:** 2026-08-03

**구현 완료 (사용자가 정확한 수치/조건을 직접 지정한 직접 지시 - [[0392]]의 판정 로직을 곧바로 개정).**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 재확인 시점에 대상이 그대로면 바로 취소하지 않고 2회 더(최대 약 6초) 재시도한 뒤에야 도달 불가로
> 최종 판정해 강제공격 취소 이렇게 말고 재확인 시점에 대상이 그대로면 바로 취소 + 재탐색 누적이
> 3번정도 쌓이면 도달 불가로 최종 판정해서 강제공격취소 하도록해줘

[[0392]]에서 만든 "대상이 안 움직인 채로 N번 연속 확인되면 취소"(`chaseStationaryStreak`, 유예 2회)
방식을 폐기하고, 두 가지 판정을 분리:

1. 재확인 시점에 대상이 그 자리 그대로면(=이 유닛도 더 못 감 + 대상도 안 움직임) → 유예 없이 **바로**
   도달 불가 판정.
2. 대상이 계속 움직여서 매 재확인마다 재탐색(재추격)이 계속 발생하면 → 그 **재탐색 누적 횟수가 3회**에
   도달하는 순간 "계속 쫓아도 못 잡는다"고 보고 도달 불가 판정.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs`

`chaseStationaryStreak`(재시도 유예 카운터) 필드/상수를 `chaseRepathCount`(재탐색 누적 카운터)로
교체하고 `UpdateUnreachableChase()`의 판정 순서를 변경.

기존([[0392]]):
```csharp
    private const float ChaseRepathInterval = 3f;
    private const int ChaseUnreachableRetries = 2;
    private float chaseRepathTimer;
    private int chaseStationaryStreak;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        // ... (justLeftAttackRange, 타이머 부분 동일) ...

        bool targetMoved = ...;

        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
            chaseStationaryStreak = 0;
            return false;
        }

        bool arrivedOrStuck = !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;
        if (!arrivedOrStuck)
            return false;

        chaseStationaryStreak++;
        return chaseStationaryStreak >= ChaseUnreachableRetries; // 대상이 안 움직인 채로 2번 더 확인되면 취소
    }
```

변경:
```csharp
    private const float ChaseRepathInterval = 3f;
    private const int ChaseUnreachableRepathLimit = 3;
    private float chaseRepathTimer;
    private int chaseRepathCount;
    private bool chaseWasInAttackRange;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        // ... (justLeftAttackRange 진입 시 chaseRepathCount = 0으로 초기화, 타이머 부분 동일) ...

        bool targetMoved = ...;

        if (targetMoved)
        {
            // 대상이 그 사이 움직여서 새 위치로 재탐색 - 이 재탐색이 계속 쌓이면(대상이 계속 도망만
            // 다니면) 그것도 사실상 못 잡는다는 뜻이므로 누적 횟수로 도달 불가를 판정한다.
            MoveAgentTo(targetPos, false);
            chaseRepathCount++;
            return chaseRepathCount >= ChaseUnreachableRepathLimit; // 재탐색이 3회 쌓이면 취소
        }

        bool arrivedOrStuck = !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;
        if (!arrivedOrStuck)
            return false;

        // 갈 수 있는 데까지 다 갔고, 대상도 그 사이 안 움직였다 - 바로 도달 불가로 최종 판정
        return true;
    }
```

`CancelAttackOrder()` / `AttackUnitTarget()` / `AttackFriendlyTarget()`의 초기화 지점 3곳도
`chaseStationaryStreak = 0;` → `chaseRepathCount = 0;`으로 동일하게 교체.

## 요약

- **대상이 멈춤 + 이 유닛도 도달 불가** → 재확인 시점에 즉시 취소 (더 이상 기다리지 않음).
- **대상이 계속 움직임(도망 다님)** → 재탐색이 3회(3초 주기 기준 약 9초) 쌓이면 "계속 쫓아도
  못 잡는다"고 보고 취소.
- 두 조건은 상호 배타적 카운터라서 섞이지 않는다 - 대상이 움직이는 동안은 `chaseRepathCount`만
  올라가고, 대상이 완전히 멈춘 순간에는 카운트 없이 즉시 판정된다.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
