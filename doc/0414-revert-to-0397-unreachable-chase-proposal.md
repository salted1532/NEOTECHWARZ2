# 0414 - 도달 불가 추격 로직을 [[0397]] 버전으로 되돌리기 (제안)

**날짜:** 2026-08-04

**대체됨 - 진행 안 함.** 사용자가 단순 복귀 대신 구체적인 새 설계를 제시해서 [[0415]]로
대체됐다. [[0415]] 참고.

## 요청 내용

> 0397 문서의 버전으로 다시 변경해줄래

[[0403]]~[[0413]]에서 쌓아온 "도달 가능 여부 확인(`IsPositionReachable`) + 도달 불가 쿨다운
상태(`chaseIsUnreachable`) + 이동 중 1초 재탐색 + 디버그 로그" 전체를 걷어내고, [[0397]]에서
확정했던 가장 단순한 버전("도착 이벤트에서만 재확인, 대상이 그대로면 즉시 포기, 움직였으면
횟수 제한 없이 계속 추격")으로 되돌린다. [[0412]] 라이브 테스트에서 나온 원인 미확정 이상
현상(쿨다운 간격 오작동, 대량 에러, 유닛 원인불명 파괴) 때문에, 새로 쌓은 로직 전체를 걷어내고
검증된 단순 버전으로 되돌리려는 것으로 이해했다.

## 되돌아갈 코드 ([[0397]] 그대로)

### `Assets/Scripts/Unit/UnitController.cs`

```csharp
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
            return false;
        }

        return true; // 도착했고 대상도 그 사이 안 움직였다 - 도달 불가로 최종 판정
    }
```

`IsPositionReachable()`/`reachabilityProbePath` 헬퍼, `UnreachableRepathInterval`/
`chaseIsUnreachable`/`nextUnreachableRepathTime`/`MovingChaseRepathInterval`/
`nextMovingChaseRepathTime` 필드, `Debug.Log("[도달 불가 추격] ...")` 4곳 전부 삭제.
`CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의
`chaseIsUnreachable = false;` 초기화 라인도 함께 삭제.

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

```csharp
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
            if (!navMeshAgent.hasPath)
                MoveAgentTo(pos);
            return false;
        }

        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - pos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            MoveAgentTo(pos);
            return false;
        }

        return true;
    }
```

동일하게 `IsPositionReachable()`/`reachabilityProbePath`/쿨다운 필드/디버그 로그 전부 제거.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()` + 관련 필드/헬퍼,
  `CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의 초기화 라인)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()` + 관련 필드/헬퍼)

## 상태

이 문서는 제안 단계다. 승인 시 위 변경을 적용하고, 컴파일 확인 후 이 문서에 결과를 기록한다.
