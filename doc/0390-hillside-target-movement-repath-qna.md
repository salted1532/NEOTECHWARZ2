# 0390 - 도달 불가 언덕 위 대상이 움직일 때 경로 재탐색 여부 (질문 답변)

**날짜:** 2026-08-03

**코드 변경 없음 - 질문 답변만.**

## 질문

> 올라갈수 없는 언덕위에 있는 대상 공격 대한 처리에서 언덕위의 대상이 움직이는 경우에는 계속
> 경로를 재탐색하나?

## 답변

두 국면으로 나뉜다 (`Assets/Scripts/Unit/UnitController.cs`).

**1) 아직 "도달 불가" 판정이 나기 전(추격 중)에는 재탐색한다.**

- `AttackOrderTick()`(명시 지정 추격, 우클릭)과 `FriendlyAttackTick()`(아군 강제공격) 둘 다 매 프레임
  대상의 **현재** `transform.position`으로 `MoveAgentTo(...)`를 다시 호출한다.
- `MoveAgentTo`에는 [[0386]]에서 추가한 목적지 캐시가 있어서, 직전 목적지와 0.5m
  (`RedundantDestinationEpsilon`) 미만 차이면 `SetDestination`을 다시 부르지 않고 기존 경로를
  유지한다. 즉 대상이 0.5m 넘게 움직이면 매 프레임 `SetDestination`이 다시 호출되어 실제로 경로가
  재탐색된다.

**2) "갈 수 있는 데까지 다 갔다"고 판정되는 순간, 그 유닛의 공격 명령 자체가 취소되어 더 이상
쫓아가지 않는다.**

- 판정 조건: `!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance`
  (사거리 밖인데도 이게 참이면 "도달 불가"로 간주).
- 이 순간 `CancelAttackOrder()` + `HaltInPlace()`가 호출되어 `orderedTarget`/`friendlyTarget`이
  `null`로 초기화된다([[0384]]).
- 이 취소는 그 프레임 한 번만 판단하는 것이고, 취소된 이후로는 `AttackOrderTick`/`FriendlyAttackTick`이
  더 이상 그 대상을 추적하지 않는다 - 대상이 그 뒤에 움직여서 다시 도달 가능한 위치로 내려와도
  **재탐색하지 않는다**. 명시적 공격 명령이 이미 끝났기 때문에, 다시 쫓아가려면 플레이어가 새로
  공격 명령을 내려야 한다.

**요약:** 도달 가능 거리 안에서 쫓아가는 동안에는 대상의 이동을 계속 따라가며 경로를 재탐색하지만,
한 번 "더 갈 수 없다"고 판정되어 공격 명령이 취소되면 그 시점 이후 대상이 움직이는 것과 무관하게
재탐색을 멈춘다(명령 자체가 사라졌으므로).

별개로, 명시 지정 명령이 없는 순수 자동교전([[0388]]의 `GetEngagedOrClosestTarget`/
`GetEngagedOrClosestEnemy`)은 이 취소 로직과 별개 경로라 여기서는 다루지 않음 - 필요하면 후속으로
확인.

## 관련 문서

- [[0384]] - 도달 불가 대상에 대한 자동 공격 명령 취소
- [[0386]] - 목적지 캐시(중복 SetDestination 방지)
- [[0388]] - 자동교전 대상 유지(히스테리시스)
