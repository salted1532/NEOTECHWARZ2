# 0397 - 도달 불가 추격 로직 최종 확정 (죽은 카운트 코드 정리)

**날짜:** 2026-08-03

**구현 완료 (사용자가 [[0396]] 상태를 최종본으로 확정).**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).
- `grep`으로 `chaseStuckTimer`/`chaseProgressBaselineDistance`/`chaseStuckCount`/
  `ChaseStuckTimeout`/`ChaseProgressThreshold`/`ChaseUnreachableRepathLimit` 잔여 참조 없음 확인.

## 요청 내용

> 이 버전이 제일 좋은거 같네 이대로 마무리하자

[[0396]]에서 "3회 누적 시 포기" 판정을 임시로 비활성화(카운트는 계속 쌓이지만 취소는 안 함)해서
확인해본 결과, 그 상태(= 대상이 계속 움직이는 한 재탐색만 하고 절대 포기하지 않음, 대상이 완전히
멈춘 순간에만 즉시 포기)가 가장 자연스럽다고 확정. 하지만 [[0396]]은 "확인용 임시 조치"라
`chaseStuckTimer`/`chaseProgressBaselineDistance`/`chaseStuckCount`와 [[0395]]에서 추가한 "이동 중
거리 진행도 감시" 블록 전체가 **아무 것도 취소하지 않으면서 계산만 하는 죽은 코드**로 남아있었다.
최종본으로 확정된 이상 이 죽은 상태/로직을 걷어내고, 실제로 동작하는 로직만 남긴다.

## 최종 확정된 동작

- 사거리 밖에서 추격 중, **도착(또는 더 갈 수 없어 멈춤) 이벤트**에서만 대상 위치를 재확인한다
  (이동 중엔 재탐색 안 함 - [[0391]] 멈칫거림 방지).
- 도착 시점에 대상이 그 자리 그대로면 → **즉시** 도달 불가로 판정, 강제공격/추격 명령 취소.
- 도착 시점에 대상이 그 사이 움직였으면 → 새 위치로 재탐색하고 계속 추격 (횟수 제한 없음 - 대상이
  계속 움직이는 한 끝까지 쫓아간다).
- 공격 성공 후 대상이 사거리를 벗어나 도망가면 → 대기 없이 즉시 재탐색 ([[0392]]에서 추가).
- "이동 중 거리 진행도가 안 줄면 막힘으로 판정"([[0395]])하던 로직은 삭제 - 최종적으로 카운트 기반
  포기 자체를 없애기로 했으므로 그 감시도 더 이상 의미가 없다.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs`

`UpdateUnreachableChase()`를 재작성해서 [[0395]]의 스타크래프트식 다중 신호(거리 진행도/막힘
타이머/재탐색 횟수 누적) 조합을 걷어내고, [[0391]]의 "도착 이벤트 기준" 구조로 단순화하되
"대상이 움직였으면 절대 포기하지 않는다"는 [[0396]]에서 확정된 동작만 반영:

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

`ChaseStuckTimeout`/`ChaseProgressThreshold`/`ChaseUnreachableRepathLimit` 상수와
`chaseStuckTimer`/`chaseProgressBaselineDistance`/`chaseStuckCount` 필드 삭제. `CancelAttackOrder()`/
`AttackUnitTarget()`/`AttackFriendlyTarget()`의 초기화 지점 3곳도 해당 필드 리셋 라인을 제거하고
`chaseWasInAttackRange = false;`만 남김.

## 요약

- 최종 판정: **대상이 멈춰있을 때만** 포기, **움직이는 한 계속 쫓는다**. 재탐색 횟수 제한이나 이동 중
  진행도 감시 같은 부가 조건은 결국 전부 제거됨 - [[0393]]/[[0394]]/[[0395]]는 이 결론에 도달하기까지의
  중간 시행착오 기록으로 남는다.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
