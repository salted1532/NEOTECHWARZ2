# 0391 - 도달 불가 언덕 위 아군을 강제공격 중 대상이 계속 움직일 때 멈칫거림(재탐색 진동) 수정 제안

**날짜:** 2026-08-03

**승인 후 구현 완료 (FriendlyAttackTick + AttackOrderTick 둘 다 적용). 컴파일 확인은 Unity CLI Loop
서버 미실행으로 대기 중.**

## 검증

- `npx uloop-cli compile`: Unity Editor는 실행 중이나 Unity CLI Loop 서버가 꺼져있어 확인 불가
  (`Window > Unity CLI Loop > Server`로 켠 뒤 재확인 필요).

## 요청 내용

> 도달할수 없는 언덕위의 아군을 강제 공격하고 그 위에 있는 아군이 계속해서 움직이면 강제공격한
> 지상의 아군은 경로를 계속 재탐색 하느라 멈칫멈칫한단 말이야 이걸 해결하기 위해선 첫 강제공격
> 명령을 하여 탐색된 가장 가까운 경로(언덕위에 있는 아군유닛에게서 가장 가까운 경로 언덕아래)를
> 먼저 갔다가 다시 재탐색 -> 도착 -> 재탐색을 하는데 그 언덕위에 유닛이 멈추면 그때 탐색을 멈추고
> 강제공격 명령이 멈추는 식으로 하면 어떨까

## 조사 결과

- [[0390]]에서 확인했듯, `FriendlyAttackTick()`(`Assets/Scripts/Unit/UnitController.cs:763`)은
  사거리 밖이고 아직 "도달 불가" 판정 전이면 **매 프레임** `MoveAgentTo(friendlyTarget.transform.position, ...)`
  를 호출한다.
- `MoveAgentTo`([[0386]], 622번째 줄)는 직전에 성공한 원본 목적지(`lastMoveAgentToDestination`)와
  이번 목적지 차이가 0.5m(`RedundantDestinationEpsilon`) 미만이면 재요청을 건너뛴다. 하지만 언덕 위
  아군이 계속 움직이면 매 프레임 원본 목적지(아군의 실시간 위치)가 조금씩 달라지므로, 누적 이동이
  0.5m를 넘는 순간마다 `SetDestination`이 다시 불린다.
- 대상이 언덕 위(도달 불가 지역)에 있으므로 `SetDestination(원본 목적지)`는 매번 실패하고, 매번
  `NavMesh.SamplePosition`으로 "지금 이 순간 대상 위치에서 가장 가까운 navmesh 지점"을 다시 찾는다
  - 대상이 계속 움직이므로 이 폴백 지점도 호출마다 미세하게 달라진다. 그 결과 유닛이 도착하기도
    전에 목적지가 계속 바뀌면서 경로가 반복 재계산되어 "멈칫멈칫"하는 것으로 보인다.
  - 현재의 "도달 불가 → 취소"([[0384]]) 판정도 `remainingDistance <= stoppingDistance`가 참이 되는
    순간만 보는데, 목적지 자체가 계속 흔들리면 이 조건이 좀처럼 안정적으로 참이 되지 않아 취소도
    잘 발동하지 않고 계속 흔들린다.
- 제안하신 방식(도착 이벤트 기준으로만 재탐색, 대상이 멈춰있을 때만 진짜로 포기)이 정확히 이 문제의
  근본 원인(매 프레임 재탐색)을 없앤다. 구현 방향:
  1. 아직 목적지로 이동 중(`pathPending` 이거나 `remainingDistance > stoppingDistance`)이면 이미
     경로가 잡혀 있는 한(`navMeshAgent.hasPath`) `MoveAgentTo`를 다시 부르지 않고 그대로 둔다(도착할
     때까지 대상의 실시간 위치를 쫓지 않음 - 대상이 그 사이 조금 움직여도 무시).
  2. 도착(또는 더 갈 수 없어 멈춤) 판정이 나면, 그 시점 대상의 현재 위치를 마지막으로 탐색했던 목적지
     (`lastMoveAgentToDestination`, 이미 [[0386]]에서 추적 중)와 비교한다.
     - 0.5m(기존 `RedundantDestinationEpsilon` 재사용) 넘게 움직였으면 → 새 위치로 재탐색
       (`MoveAgentTo` 재호출)하고 계속 추격.
     - 거의 안 움직였으면 → 대상이 멈춰있다고 판단, 여기서 진짜로 [[0384]]처럼 공격 명령 취소 +
       정지.
- `AttackOrderTick()`(1019번째 줄, 명시 지정 추격 - 우클릭 적 공격)도 완전히 같은 구조(사거리 밖일
  때 매 프레임 `MoveAgentTo` 호출 + 도착 즉시 취소)라 적이 도달 불가 지형 위에서 움직여 다닐 때
  동일한 멈칫거림이 발생할 것으로 보인다. 이번 요청은 아군 강제공격 사례를 지목했지만, 근본 원인이
  같은 코드 패턴이라 함께 고칠지 여부를 열린 질문으로 남긴다.

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs` - `FriendlyAttackTick()`

기존 코드(763~798번째 줄):
```csharp
    private void FriendlyAttackTick()
    {
        if (!hasFriendlyOrder)
            return;

        if (friendlyTarget == null)
        {
            // 대상이 죽어서 파괴됨: 정지된 채로 남지 않도록 여기서 직접 마무리 처리
            hasFriendlyOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();

            UnitcurrentState = UnitState.Idle;
            return;
        }

        float sqrDistance = (transform.position - friendlyTarget.transform.position).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
        else if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            // 갈 수 있는 데까지 다 가서 멈췄는데도 사거리 밖(경사로 없는 언덕 위 등 도달 불가능한 대상) -
            // 공격 명령을 취소한다 (doc/0384).
            CancelAttackOrder();
            HaltInPlace();
        }
        else
        {
            MoveAgentTo(friendlyTarget.transform.position, IsAirborne(friendlyTarget)); // 사거리 밖: 거리 상관없이 끝까지 추격
        }
    }
```

변경 코드:
```csharp
    private void FriendlyAttackTick()
    {
        if (!hasFriendlyOrder)
            return;

        if (friendlyTarget == null)
        {
            // 대상이 죽어서 파괴됨: 정지된 채로 남지 않도록 여기서 직접 마무리 처리
            hasFriendlyOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();

            UnitcurrentState = UnitState.Idle;
            return;
        }

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
            // 아직 이동 중 - 대상이 그 사이 조금씩 움직여도 도착하기 전까지는 재탐색하지 않는다.
            // 매 프레임 실시간 위치로 SetDestination을 다시 부르면 경로가 계속 재계산되어
            // 멈칫거리는 문제가 있었다 (doc/0391).
            if (!navMeshAgent.hasPath)
                MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색
            return;
        }

        // 도착(또는 더 갈 수 없어 멈춤) - 그 사이 대상이 움직였으면 새 위치로 재탐색, 안 움직였으면 포기
        bool targetMoved = !lastMoveAgentToDestination.HasValue ||
            (lastMoveAgentToDestination.Value - targetPos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon;

        if (targetMoved)
        {
            MoveAgentTo(targetPos, false);
        }
        else
        {
            // 갈 수 있는 데까지 다 갔고, 대상도 그 사이 멈춰있었다 - 진짜 도달 불가로 판정하고
            // 공격 명령을 취소한다 (doc/0384).
            CancelAttackOrder();
            HaltInPlace();
        }
    }
```

### (선택) `AttackOrderTick()` - 동일 문제, 함께 고칠지 확인 필요

기존 코드(1040~1060번째 줄) 중 사거리 밖 분기:
```csharp
                if (!inAttackRange)
                {
                    // 갈 수 있는 데까지 다 가서 멈췄는데도 사거리 밖(경사로 없는 언덕 위 등 도달 불가능한
                    // 대상, doc/0375 fallback으로 가장 가까운 지점까지만 이동한 경우 포함) - 공격 명령을
                    // 취소한다 (doc/0384).
                    if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
                    {
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }

                    MoveAgentTo(attackMoveDestination.Value); // 사거리 밖: 계속 추격 이동
                }
```

변경 코드(동일 패턴 적용):
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
                        // FriendlyAttackTick과 동일한 이유로 도착 전까지는 재탐색하지 않는다 (doc/0391).
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
                        // 갈 수 있는 데까지 다 갔고, 대상도 그 사이 멈춰있었다 - 진짜 도달 불가 (doc/0384).
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }
                }
```

## 열린 질문

1. **`AttackOrderTick()`(적 우클릭 추격)도 같이 고칠까요?** 동일한 근본 원인이라 적이 도달 불가
   지형 위에서 움직일 때 같은 멈칫거림이 있을 가능성이 높습니다. 이번 보고는 아군 강제공격
   사례라 `FriendlyAttackTick()`만 먼저 고치고 `AttackOrderTick()`은 나중에 별도로 확인해도 됩니다.
2. `EnemyUnitController.ChaseTarget()`(적 AI가 플레이어를 쫓는 경로)에는 애초에 [[0384]]류의
   "도달 불가 → 취소" 로직 자체가 없어서 이번 멈칫거림 수정 대상이 아닙니다 (플레이어가 도달 불가
   지형 위에서 계속 움직이면 적 AI는 그냥 계속 쫓아만 옴 - 별개 사안, 필요하면 따로 다뤄야 함).
3. 재탐색 판정 임계값을 기존 `RedundantDestinationEpsilon`(0.5m)을 그대로 재사용했습니다. "대상이
   멈췄다"고 판정하기엔 너무 민감(엄격)할 수도 있어서, 필요하면 별도 상수로 분리해 더 크게 잡을 수
   있습니다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`FriendlyAttackTick()` 필수, `AttackOrderTick()`은 질문 1
  답변에 따라 결정)
