# 0408 - 이동 중(아직 미도착)인 추격에도 1초 간격 재탐색 추가 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 이동중인 유닛에 대한 경로 재탐색을 할때는 경로 재탐색을 1초 간격으로 하도록 해줘

## 조사 결과 - 지금은 "이동 중"에는 재탐색이 아예 없음

`UpdateUnreachableChase()`(`UnitController.cs:664~674`)와 `ChaseTarget()`
(`EnemyUnitController.cs` 동일 구조)의 "아직 이동 중" 분기:

```csharp
if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
{
    // 아직 이동 중 - 도착 전까지는 대상의 실시간 위치로 매 프레임 재탐색하지 않는다 (doc/0391)
    if (!navMeshAgent.hasPath)
        MoveAgentTo(targetPos, false); // 아직 이동을 시작 안 했으면 최초 탐색
    return false;
}
```

지금은 "도착 전까지 재탐색 0번"이다 - 처음 명령이 떨어진 순간의 목적지로 고정 이동하고, 그
지점에 도착(또는 더 갈 수 없어 멈춤)하고 나서야([[0391]]/[[0397]]) 대상이 그 사이 움직였는지 다시
확인한다. 대상이 계속 이동 중인 대상(적/아군)이면, 이동하는 동안엔 그 실시간 위치를 전혀 안 쫓고
"도착 지점에 도착한 뒤에야 한 번에 따라잡는" 식의 홉(hop) 방식으로 움직인다.

요청하신 건 이 구간에 "1초마다 한 번" 재탐색을 넣어서, 이동 중에도 어느 정도는 대상의 최신 위치를
따라가되 매 프레임 재탐색([[0386]]/[[0391]]이 막았던 멈칫거림)은 여전히 피하자는 것.

## 제안하는 수정

`MoveAgentTo()` 자체가 이미 "목적지가 직전과 사실상 같으면(0.5m 이내) 재요청하지 않는" 캐시
([[0386]])를 갖고 있으므로, 1초마다 `MoveAgentTo(targetPos)`를 호출하기만 하면 된다 - 대상이 그
사이 거의 안 움직였으면 캐시가 걸려 사실상 공짜, 움직였으면 그때만 실제로 `SetDestination`.

### `Assets/Scripts/Unit/UnitController.cs`

새 필드 (`chaseWasInAttackRange` 근처):

```csharp
// 이동 중(아직 도착 전)에도 대상의 실시간 위치를 어느 정도는 따라가도록 1초마다 한 번씩만 재탐색한다
// (doc/0408) - 매 프레임 재탐색하면 [[0386]]/[[0391]]이 막았던 멈칫거림이 재발하므로 간격을 둔다.
private const float MovingChaseRepathInterval = 1f;
private float nextMovingChaseRepathTime;
```

"아직 이동 중" 분기 수정:

```csharp
        if (navMeshAgent.pathPending || navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            if (!navMeshAgent.hasPath)
            {
                // 아직 이동을 시작 안 했으면 최초 탐색
                chaseIsUnreachable = false;
                unreachableRepathDelay = UnreachableRepathInitialDelay;
                MoveAgentTo(targetPos, false);
                nextMovingChaseRepathTime = Time.time + MovingChaseRepathInterval;
            }
            else if (Time.time >= nextMovingChaseRepathTime)
            {
                // 이동 중 1초 간격 재탐색 - 대상이 그 사이 거의 안 움직였으면 MoveAgentTo의 0.5m 캐시가
                // 실제 재탐색을 걸러준다 (doc/0408)
                nextMovingChaseRepathTime = Time.time + MovingChaseRepathInterval;
                MoveAgentTo(targetPos, false);
            }
            return false;
        }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 구조이므로 `ChaseTarget()`의 "아직 이동 중" 분기에 동일하게 적용.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()`)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`)

## 요약

- "아직 이동 중"(도착 전) 분기에 `MovingChaseRepathInterval = 1f` 타이머를 추가 - 경로가 이미
  잡혀있는 상태(`hasPath`)에서 1초가 지날 때마다 `MoveAgentTo(targetPos)`를 호출.
- `MoveAgentTo()`의 기존 0.5m 캐시([[0386]])가 그대로 적용되므로, 대상이 그 사이 거의 안 움직였으면
  실제 `SetDestination` 없이 저비용으로 끝남 - 매 프레임 재탐색([[0391]])으로 되돌아가지 않음.
- 최초 탐색(`!hasPath`) 시점에 타이머를 세팅해 다음 1초 뒤부터 적용.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
