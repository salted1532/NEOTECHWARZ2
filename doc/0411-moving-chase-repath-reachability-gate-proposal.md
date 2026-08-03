# 0411 - 이동 중 1초 간격 재탐색에 도달 가능 여부 게이트 추가 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 현재 이동해야할 대상의 유닛(도달 불가능한 위치에 있음)이 이동중일때 계속 재탐색하는데 이
> 경우일때는 마지막 경로로만 이동하고 해당 유닛이 도달가능한 위치로 이동하지 않을 경우에는
> 목적지에 도착하고 나서 그때 다시 재탐색을 했으면 좋겠어

## 조사 결과

[[0408]]에서 넣은 "이동 중(아직 도착 전) 1초 간격 재탐색"은 대상이 도달 가능한지 여부를 확인하지
않고 무조건 1초마다 `MoveAgentTo(targetPos)`를 호출한다:

```csharp
else if (Time.time >= nextMovingChaseRepathTime)
{
    nextMovingChaseRepathTime = Time.time + MovingChaseRepathInterval;
    MoveAgentTo(targetPos, false);
}
```

대상이 도달 불가능한 위치(절벽 위 등)에서 계속 움직이는 경우, 이 1초마다의 호출이 매번
`MoveAgentTo()` 내부의 `SetDestination` 실패 → `NavMesh.SamplePosition` 재탐색(가장 가까운
지점 다시 계산)을 반복시킨다 - [[0403]]/[[0406]]이 "도착 후" 구간에서 막았던 것과 같은 종류의
낭비가 "이동 중" 구간에 새로 생긴 것.

요청하신 동작: 이동 중에 대상이 도달 불가능하면 재탐색하지 말고 지금 가고 있는 마지막 경로 그대로
계속 이동하다가, **도착한 뒤에**(`navMeshAgent.remainingDistance <= stoppingDistance`) 그때 가서
[[0403]]/[[0406]]의 기존 로직(도달 가능/불가 판정 + 쿨다운)이 처리하게 한다.

## 제안하는 수정

이동 중 1초 주기 재탐색 분기에 `IsPositionReachable()`([[0403]]) 게이트를 추가한다: 도달
가능할 때만 실제로 `MoveAgentTo()`를 호출하고, 도달 불가면 아무것도 안 하고(마지막 경로 유지)
다음 1초 뒤에 다시 저비용으로만 확인한다.

### `Assets/Scripts/Unit/UnitController.cs`

```csharp
            else if (Time.time >= nextMovingChaseRepathTime)
            {
                nextMovingChaseRepathTime = Time.time + MovingChaseRepathInterval;
                if (IsPositionReachable(targetPos))
                {
                    // 이동 중 1초 간격 재탐색 - 대상이 그 사이 거의 안 움직였으면 MoveAgentTo의 0.5m
                    // 캐시가 실제 재탐색을 걸러준다 (doc/0408)
                    MoveAgentTo(targetPos, false);
                }
                // else: 도달 불가 - 재탐색하지 않고 마지막 경로 그대로 이동, 도착 이벤트에서 다시
                // 판정한다 (doc/0403/0406, doc/0411)
            }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 구조로 동일하게 적용.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()`)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`)

## 요약

- 이동 중(도착 전) 1초 간격 재탐색 분기에 `IsPositionReachable()` 게이트를 추가.
- 도달 가능하면 기존대로 `MoveAgentTo()` 실행, 도달 불가면 아무것도 안 하고 마지막 경로 그대로
  이동 - 재탐색은 도착 이벤트에서 [[0403]]/[[0406]]이 처리.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
