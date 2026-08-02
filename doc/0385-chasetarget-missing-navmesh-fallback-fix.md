# 0385 - ChaseTarget()에 누락된 NavMesh fallback 적용 (도달 불가능 대상 자동교전 루프 수정)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 아군유닛을 기준으로 테스트해봤는데 도달할수 없는 위치에 있는 대상에게 최대한 가까이 가야하는데
> 아에 움직이지도 못할정도로 루프에 빠진거 같아 올라갈수 없는 언덕에 대한 처리와 같이 최대한
> 가까이 가도록하는게 좋을거 같아

[[0384]](0384-auto-cancel-unreachable-attack-order-proposal.md)로 명시 공격 명령(우클릭/아군
강제공격)의 취소 자체는 정상 동작하지만, 취소 직후 다른 경로(자동 감지 추격)를 통해 같은 대상을
다시 쫓아가려다 이동조차 못 하고 멈추는 문제가 남아있었음.

## 조사 결과

- `AttackRange.Update()`(`Assets/Scripts/Unit/AttackRange.cs:71~95`)는 매 프레임 "사거리
  (`UnitRange`) 밖이지만 감지 콜라이더(`UnitRange` + 5, doc/0239) 안에 있는 적"을 발견하면, 유닛이
  Idle 상태인 한 `unitController.ChaseTarget(target.transform.position)`을 자동으로 호출한다
  (90~93번째 줄). 명시적 공격 명령이 없어도 이 자동교전은 항상 동작한다.
- 그런데 `UnitController.ChaseTarget()`(`UnitController.cs:1068`)은 [[0375]]에서 만든
  `MoveAgentTo()`(NavMesh 샘플링 fallback 포함)를 거치지 않고 `navMeshAgent.SetDestination(pos)`를
  **직접** 호출하고 있었다 - 0375 당시 지상/공중 유닛 이동을 전부 `MoveAgentTo()` 한 곳으로 모은다고
  했지만 이 호출부만 빠뜨림. 대상이 경사로 없는 언덕 위 등 도달 불가능한 위치면 이 `SetDestination`
  호출은 조용히 `false`를 반환하고 끝 - fallback이 없으니 목적지가 갱신되지 않고 유닛은 제자리에서
  전혀 움직이지 않는다.
- 실제로 벌어지는 순서:
  1. 우클릭 공격 명령(`AttackUnitTarget`)은 `MoveAgentTo()`를 쓰므로 정상적으로 가장 가까운 지점까지
     이동하고, 사거리 안에 끝내 못 들어오면 [[0384]]가 명령을 취소한다(`CancelAttackOrder()` +
     `HaltInPlace()` → Idle 전환).
  2. 취소된 직후에도 그 적은 여전히 (사거리보다 넓은) 감지 콜라이더 안에 남아있고, 유닛은 Idle이므로
     `AttackRange.Update()`가 같은 적을 다시 골라 `ChaseTarget()`을 호출한다.
  3. `ChaseTarget()`의 fallback 없는 `SetDestination(도달 불가능 좌표)`가 조용히 실패 → 유닛은 그
     자리에서 전혀 움직이지 않음.
  4. `AttackRange.Update()`가 매 프레임 위 과정을 반복 → 눈으로 보기엔 "아예 움직이지도 못하는 채로
     루프에 빠진" 것처럼 보임. (명시 명령이 아니라 자동교전이 매 프레임 재시도하는 구조라
     [[0384]]의 취소 로직이 적용되는 지점(`AttackOrderTick`/`FriendlyAttackTick`)까지 아예 도달하지
     않는다.)
- 적 AI 쪽(`EnemyUnitController.ChaseTarget()`, `EnemyUnitController.cs:316~321`)은 이미
  `MoveAgentTo()`를 쓰고 있어서 이 문제가 없다 - 플레이어 쪽 `UnitController.ChaseTarget()`만 빠짐.

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs` - `ChaseTarget()` (1068~1084번째 줄)

기존 코드:
```csharp
    public void ChaseTarget(Vector3 pos)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        UnitcurrentState = UnitState.Idle;
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(pos);
        }
        else
        {
            targetPosition = AirTargetPosition(pos);
            isMovingAirUnit = true;
        }
    }
```

변경 코드:
```csharp
    public void ChaseTarget(Vector3 pos)
    {
        CancelGatheringForNewCommand();

        arrived = false;
        UnitcurrentState = UnitState.Idle;
        MoveAgentTo(pos); // NavMesh fallback(doc/0375) 재사용 - 도달 불가능한 대상도 가장 가까운 지점까지는 이동한다
    }
```

기존 if/else 본문은 `MoveAgentTo()`(`UnitController.cs:612`)와 완전히 동일한 코드를 fallback만 뺀 채
중복해둔 것이라, 그대로 `MoveAgentTo()` 호출로 교체하면 된다.

## 열린 질문

- 도달 불가능한 대상은 자동교전이 취소되지 않고 계속 남아 매 프레임 `ChaseTarget → MoveAgentTo`를
  반복 호출하게 된다. 다만 유닛이 이미 가장 가까운 지점에 도착한 뒤에는 `SetDestination`이 사실상
  같은 목적지로의 재요청(비용 거의 없음)만 반복되고, 실제로는 그 자리에 가만히 서 있는 정상적인
  "자동교전 대기" 상태로 보이므로 별도 취소 처리는 추가하지 않음(0375에서 적 AI 쪽에 대해 이미 같은
  결론을 내린 것과 동일한 이유 - 명시 명령이 아니라 자동 감지 동작이라 "취소할 명령"이 없음).

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
