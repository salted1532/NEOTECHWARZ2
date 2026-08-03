# 0406 - [[0405]] 참고자료의 쿨다운/백오프 방식 실제 적용 실험 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 그냥 다시한번 적용시켜볼래 그래도 결과를 보고 싶네 제안한 로직대로 코드 수정해줘

[[0405]]에서 "과거에 시도했다가 되돌린 방향이고, [[0403]]이 이미 다른 방식으로 해결했다"고
권고했지만, 사용자가 직접 결과를 비교해보고 싶다며 참고자료의 쿨다운/백오프 방식을 실제로
적용해볼 것을 요청.

## 적용 방식

참고자료의 "실패 횟수에 따라 재탐색 간격 증가"(0.2 → 0.5 → 1 → 2 → 4초, 성공 시 초기화) +
"명시적 Unreachable 상태(그 상태에서는 MoveAgentTo/CalculatePath 호출 안 함)"를 결합한다.
"마지막 실패 위치 캐싱"은 이 쿨다운 상태 자체가 대체(쿨다운 중엔 애초에 재확인을 안 하므로 같은
위치든 다른 위치든 어차피 안 건드림). `NavMeshPathStatus` 활용은 기존 `IsPositionReachable()`
([[0403]])을 그대로 재사용.

상태 전이:
- **평소(Unreachable 아님)**: 대상이 움직이면 그때그때 `IsPositionReachable()`로 확인. 도달
  가능하면 재탐색. 도달 불가면 가장 가까운 위치로 1회 이동하고 **Unreachable 상태 진입**
  (쿨다운 0.2초로 시작).
- **Unreachable 상태**: 쿨다운이 끝나기 전까지는 `MoveAgentTo`도 `IsPositionReachable`
  (`NavMesh.CalculatePath`)도 아예 호출하지 않는다. 쿨다운이 끝나면 딱 한 번 확인:
  - 도달 가능해짐 → Unreachable 상태 해제, 쿨다운 초기화(0.2초), 재탐색.
  - 여전히 도달 불가 → 가장 가까운 위치로 다시 1회 이동, 다음 쿨다운은 2배(최대 4초)로 늘림.
- 대상이 완전히 멈춘 경우(움직이지 않음)의 "즉시 포기" 판정([[0397]])은 그대로 유지 - 이번
  요청은 "재탐색 빈도"만 바꾸는 것이지 포기 조건 자체를 바꾸는 게 아니다.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs`

새 필드 (`chaseWasInAttackRange` 근처):

```csharp
private bool chaseWasInAttackRange;

// [[0405]] 참고자료의 쿨다운/백오프 방식을 실제로 비교해보기 위한 실험 적용 (doc/0406).
// 도달 불가 상태로 전환되면 이 쿨다운이 끝날 때까지 MoveAgentTo/IsPositionReachable(CalculatePath)를
// 아예 호출하지 않는다. 실패가 반복될수록 다음 쿨다운을 2배로 늘린다(최대 4초), 성공하면 초기화.
private const float UnreachableRepathInitialDelay = 0.2f;
private const float UnreachableRepathMaxDelay = 4f;
private bool chaseIsUnreachable;
private float unreachableRepathDelay = UnreachableRepathInitialDelay;
private float nextUnreachableRepathTime;
```

`UpdateUnreachableChase()`의 `justLeftAttackRange` 분기와 `targetMoved` 분기 수정:

```csharp
        if (justLeftAttackRange)
        {
            // 방금까지 사거리 안(공격 중)이었는데 대상이 도망가서 벗어남 - 즉시 재탐색, 쿨다운 상태도 해제
            chaseIsUnreachable = false;
            unreachableRepathDelay = UnreachableRepathInitialDelay;
            MoveAgentTo(targetPos, false);
            return false;
        }

        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            // 아직 이동 중 - 도착 전까지는 대상의 실시간 위치로 매 프레임 재탐색하지 않는다 (doc/0391)
            if (!navMeshAgent.hasPath)
            {
                chaseIsUnreachable = false; // 새로 경로를 잡는 시점 - 이전 쿨다운 상태를 이어받지 않는다
                unreachableRepathDelay = UnreachableRepathInitialDelay;
                MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색
            }
            return false;
        }

        // 도착(또는 더 갈 수 없어 멈춤) - 그 사이 대상이 움직였는지 확인
        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            if (chaseIsUnreachable)
            {
                if (Time.time < nextUnreachableRepathTime)
                    return false; // 쿨다운 중 - MoveAgentTo/CalculatePath 둘 다 호출하지 않는다

                if (IsPositionReachable(targetPos))
                {
                    chaseIsUnreachable = false;
                    unreachableRepathDelay = UnreachableRepathInitialDelay;
                    MoveAgentTo(targetPos, false);
                }
                else
                {
                    MoveAgentTo(targetPos, false); // 여전히 도달 불가 - 가장 가까운 위치로만 갱신 이동
                    unreachableRepathDelay = Mathf.Min(unreachableRepathDelay * 2f, UnreachableRepathMaxDelay);
                    nextUnreachableRepathTime = Time.time + unreachableRepathDelay;
                }

                return false;
            }

            if (IsPositionReachable(targetPos))
            {
                MoveAgentTo(targetPos, false);
            }
            else
            {
                // 방금 막 도달 불가로 전환 - 가장 가까운 위치로 1회 이동하고 쿨다운 상태로 진입
                MoveAgentTo(targetPos, false);
                chaseIsUnreachable = true;
                unreachableRepathDelay = UnreachableRepathInitialDelay;
                nextUnreachableRepathTime = Time.time + unreachableRepathDelay;
            }

            return false;
        }

        // 도착했고 대상도 그 사이 안 움직였다 - 진짜 도달 불가로 최종 판정
        return true;
```

`CancelAttackOrder()`/`AttackUnitTarget()`/`AttackFriendlyTarget()`의 `chaseWasInAttackRange = false;`
옆에 `chaseIsUnreachable = false;`도 추가(새 명령 시작 시 이전 쿨다운 상태가 새 대상으로 새 나가지
않도록).

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 구조로 `ChaseTarget()`에 동일하게 적용. 단, 이 파일은 `AttackUnitTarget` 같은 별도의 "명령
시작" 함수가 없고 `EnemyAttackRange`가 매 프레임 위치만 넘겨서 호출하므로, 대상이 바뀌는 시점의
명시적 리셋 지점이 없다 - `!navMeshAgent.hasPath`(최초 탐색) 분기에서의 리셋으로 대부분 커버되지만,
아주 드물게(이전 대상이 쿨다운 중일 때 완전히 새 대상으로 바뀌는 경우) 새 대상도 잠깐 이전 쿨다운을
이어받아 최대 4초까지 늦게 반응할 수 있다 - 참고자료 그대로 실험해보는 목적이므로 이 정도 오차는
감수한다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()` + 명령 시작 함수 3곳)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`)

## 요약

- [[0405]]의 권고("현재 유지")와 반대로, 사용자가 직접 결과를 비교해보고 싶다고 해서 참고자료의
  쿨다운/백오프 방식을 실제로 적용함.
- 도달 불가 상태 진입 시 가장 가까운 위치로 1회 이동 + `chaseIsUnreachable = true`, 이후 쿨다운이
  끝나기 전까지는 `MoveAgentTo`/`IsPositionReachable`(`NavMesh.CalculatePath`) 둘 다 호출 안 함.
  쿨다운은 0.2초로 시작해 실패마다 2배(최대 4초), 성공하면 초기화.
- 대상이 완전히 멈추면 즉시 포기하는 [[0397]] 판정은 그대로 유지 - 이번 변경은 재탐색 "빈도"만
  건드림.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용. 새 명령 시작 지점
  (`CancelAttackOrder`/`AttackUnitTarget`/`AttackFriendlyTarget`, `!hasPath` 최초 탐색 분기)에서
  쿨다운 상태를 초기화해 이전 명령/대상의 쿨다운이 새 것으로 새 나가지 않게 함. 단
  `EnemyUnitController`는 대상 전환을 알리는 별도 지점이 없어 아주 드물게(쿨다운 중 대상이
  바뀌는 경우) 최대 4초까지 늦게 반응할 수 있음 - 실험 목적상 감수.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
- [[0403]] 방식과 실제 플레이 비교는 사용자가 직접 확인 예정.
