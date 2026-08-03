# 0415 - 도달 가능/불가 두 모드로 명확히 분리한 추격 로직 재설계 (제안, [[0414]] 대체)

**날짜:** 2026-08-04

**승인 후 구현 완료. [[0414]](0397로 단순 복귀)는 이 문서로 대체 - 진행 안 함.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

[[0414]]("0397로 되돌리기")를 제안했으나, 사용자가 그 대신 구체적인 새 설계를 제시:

> 추적자(유닛)이 목적지(대상 유닛)을 강제공격, 이동(따라가기), 적 강제공격 등의 명령이 내려졌을
> 때 목적지에 대해 도달 가능한지 확인.
> 도달 가능하면 -> 이동 -> 재탐색 -> 이동경로 갱신 -> 이동 무한반복
> 도달 불가능 위치일시 -> 가장 가까운 경로 탐색 -> 이동 -> 도착 시 재탐색 -> 도달 가능 확인
> 여기서 재탐색은 목적지와의 경로를 확인하고 도달 가능한지 확인하는걸 의미한다
> [...] 목적지가 도달할수 없는 위치에서 이동하더라도 처리가 가능하고 중간에 목적지가 도달
> 가능한 위치로 이동했을시 로직에서 자동으로 [도달 가능 루프]로 빠져나와 정상작동

## 조사 결과 - 지금 코드와 다른 점

지금 코드([[0403]]~[[0413]])는 "이동 중이면(아직 도착 전) 무조건 재탐색 안 함"이라는 게이트를
**도달 가능/불가 여부와 상관없이 똑같이** 적용한다. 사용자가 원하는 설계는 이 게이트를
**도달 불가 상태일 때만** 적용하고, 도달 가능 상태에서는 게이트 없이 계속 실시간으로 대상
위치를 쫓아가며 재확인하라는 것 - 이렇게 나누면:

- **도달 가능**: 매 프레임(또는 대상이 조금이라도 움직일 때마다) `MoveAgentTo()`로 계속
  실시간 추적. `MoveAgentTo()` 자체에 이미 있는 "직전과 사실상 같은 목적지면 재요청 안 함"
  캐시([[0386]], 0.5m)가 있어서 대상이 안 움직이면 사실상 공짜 - 이건 이미 `FollowTick()`이
  쓰고 있는 것과 같은 패턴이라 검증된 방식이다.
- **도달 불가**: 가장 가까운 위치로 이동하는 동안은(아직 도착 전) 전혀 재확인하지 않고,
  **도착했을 때만** 다시 도달 가능한지 확인한다. 가능해졌으면 도달 가능 모드로 전환.

## 조사 결과 - [[0412]] 라이브 테스트에서 발견한 진짜 근본 원인

[[0412]]에서 관찰된 "쿨다운이 코드보다 훨씬 자주 도는" 현상을 다시 파봤다. `MoveAgentTo()`가
완전히 실패하면(경사로 없이 끊긴 곳이라 `SetDestination`도 `SamplePosition` 폴백도 둘 다
실패) 이렇게 처리한다:

```csharp
lastMoveAgentToDestination = null;   // <- 실패하면 "마지막으로 시도한 위치" 기록을 지워버림
return false;
```

`UpdateUnreachableChase`/`ChaseTarget`의 "대상이 그 사이 움직였는지" 판정은 전부
`lastMoveAgentToDestination`과 비교하는 방식인데, 완전히 도달 불가능한 대상(경사로조차 없는
곳)에서는 `MoveAgentTo`가 매번 실패하고 그때마다 이 값이 `null`로 초기화된다. 그러면 다음
판정 때 `!lastMoveAgentToDestination.HasValue`가 항상 `true`가 되어 **대상이 실제로는 1mm도
안 움직였어도 "대상이 움직였다"고 매번 잘못 판정**한다 - 이게 [[0397]] 때부터 쭉 있던 근본
버그였고, [[0403]]~[[0413]]에서 쌓은 여러 겹의 쿨다운/타이머는 이 근본 원인을 고치지 않고
위에서 억누르려고만 해서 예측 못 한 빈도로 새어나온 것으로 보인다.

## 제안하는 수정

### 1. 근본 원인 수정: `MoveAgentTo()`가 완전히 실패해도 시도한 위치는 기억한다

`Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
공통:

```csharp
            lastMoveAgentToDestination = null;
            return false;
```
->
```csharp
            // 실패했더라도 "이 위치로 시도했다"는 기록은 남긴다 - 안 그러면 다음 판정에서 대상이
            // 실제로는 안 움직였는데도 "직전 기록이 없으니 움직인 것"으로 잘못 판정해서 완전히
            // 도달 불가능한 대상에게 매 프레임 재시도를 반복하게 된다 (doc/0415).
            lastMoveAgentToDestination = destination;
            return false;
```

### 2. `UpdateUnreachableChase()`/`ChaseTarget()`을 도달 가능/불가 두 모드로 재작성

쿨다운 타이머(`UnreachableRepathInterval`/`nextUnreachableRepathTime`) 및 이동 중 1초 재탐색
타이머(`MovingChaseRepathInterval`/`nextMovingChaseRepathTime`)를 전부 제거하고, 상태 플래그
하나(`chaseIsUnreachable` - "마지막 재탐색에서 도달 불가로 판정됐는가")로 단순화한다.

`Assets/Scripts/Unit/UnitController.cs`:

```csharp
    private bool chaseWasInAttackRange;

    // 마지막 재탐색에서 도달 불가로 판정됐는지 - 이 값에 따라 아래 두 모드로 나뉜다 (doc/0415).
    private bool chaseIsUnreachable;

    private bool UpdateUnreachableChase(Vector3 targetPos, bool destinationIsAirborne, bool justLeftAttackRange)
    {
        if (isAirUnit)
        {
            MoveAgentTo(targetPos, destinationIsAirborne);
            return false;
        }

        if (justLeftAttackRange)
        {
            chaseIsUnreachable = false;
            MoveAgentTo(targetPos, false);
            return false;
        }

        if (chaseIsUnreachable)
        {
            // 도달 불가 모드: 가장 가까운 위치로 이동하는 동안은(아직 도착 전) 재탐색하지 않는다.
            if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                if (!navMeshAgent.hasPath)
                    MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색
                return false;
            }

            // 도착(또는 더 갈 수 없어 멈춤) - 여기서만 재탐색(도달 가능 여부 재확인)한다.
            if (IsPositionReachable(targetPos))
            {
                chaseIsUnreachable = false;
                MoveAgentTo(targetPos, false); // 도달 가능해짐 - 도달 가능 모드로 전환
                return false;
            }

            bool targetMoved = !lastMoveAgentToDestination.HasValue ||
                (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

            if (!targetMoved)
                return true; // 대상도 그 자리 그대로 - 진짜 도달 불가로 최종 판정

            MoveAgentTo(targetPos, false); // 새 위치 기준으로 가장 가까운 위치로 다시 이동
            return false;
        }

        // 도달 가능 모드: 게이트 없이 매 프레임 실시간으로 계속 추적/재확인한다.
        // MoveAgentTo의 0.5m 캐시([[0386]])가 있어서 대상이 거의 안 움직이면 사실상 공짜 -
        // FollowTick()이 이미 쓰고 있는 것과 같은 패턴.
        if (!IsPositionReachable(targetPos))
        {
            chaseIsUnreachable = true; // 방금 도달 불가로 전환
        }

        MoveAgentTo(targetPos, false);
        return false;
    }
```

`CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의 `chaseIsUnreachable = false;`
초기화는 그대로 유지(명칭 안 바뀜). `UnreachableRepathInterval`/`nextUnreachableRepathTime`/
`MovingChaseRepathInterval`/`nextMovingChaseRepathTime` 필드와 관련 코드는 전부 삭제.
`Debug.Log`는 상태 전환 시점(도달 불가로 전환 / 도달 가능해짐) 두 곳만 남기고 나머지(간격 안내
문구 등)는 제거.

`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`의 `ChaseTarget()`도 같은 구조로 재작성
(`justLeftAttackRange` 매개변수가 없다는 점만 다름).

## 요청하신 흐름과의 대조

| 요청하신 설계 | 이 제안 |
|---|---|
| 명령 시 목적지 도달 가능한지 확인 | `chaseIsUnreachable` 초기값 `false`(도달 가능 가정) - 첫 틱에서 바로 `IsPositionReachable()`로 실제 확인 |
| 가능 -> 이동 -> 재탐색 -> 갱신 -> 무한반복 | `chaseIsUnreachable == false` 분기 - 게이트 없이 매 프레임 `IsPositionReachable`+`MoveAgentTo` 반복 |
| 불가 -> 가장 가까운 경로 탐색 -> 이동 -> 도착 시 재탐색 -> 도달 가능 확인 | `chaseIsUnreachable == true` 분기 - 이동 중엔 대기, 도착 시에만 `IsPositionReachable()` 재확인 |
| 중간에 도달 가능해지면 자동으로 가능 루프로 복귀 | 도착 시 재확인에서 `IsPositionReachable() == true`면 `chaseIsUnreachable = false`로 전환 |

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`MoveAgentTo()` 실패 처리, `UpdateUnreachableChase()`
  전체 재작성, 관련 필드 정리)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`MoveAgentTo()` 실패 처리,
  `ChaseTarget()` 전체 재작성, 관련 필드 정리)

## 요약

- 근본 원인 수정: `MoveAgentTo()`가 완전히 실패해도 `lastMoveAgentToDestination`을 `null`로
  지우지 않고 시도한 위치를 기억하도록 변경 - "대상이 안 움직였는데도 움직였다고 오판"하던
  버그를 고침.
- `UpdateUnreachableChase()`/`ChaseTarget()`을 `chaseIsUnreachable` 플래그 하나로 두 모드
  분리: 도달 가능 모드(게이트 없이 매 프레임 실시간 추적) / 도달 불가 모드(이동 중엔 대기,
  도착 시에만 재확인).
- 타이머/쿨다운 상수(`UnreachableRepathInterval`, `nextUnreachableRepathTime`,
  `MovingChaseRepathInterval`, `nextMovingChaseRepathTime`) 전부 삭제.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
