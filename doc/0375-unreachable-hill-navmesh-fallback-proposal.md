# 0375 - 경사로 없이 끊긴 언덕(NavMesh 미연결 지점) 이동/공격 예외처리

**날짜:** 2026-08-03

**승인 후 구현 완료.** 두 파일 모두 적용.

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존에도 있던 무관한 경고와 동일 - 이번
  변경으로 새로 생긴 경고 없음).

## 요청 내용

> 올라갈수 없는 언덕에 대한 처리
> ㄴNavmeshagent가 경사로가 있는경우는 언덕위로 정상작동하는데 만약 경사로가 연결되지 않은
> 언덕 위에 경우는 올라가거나 공격하러 가는게 불가능 하기때문에 이에 따른 이동하는데 문제가 생기는데
> 이러한 경우에 대한 예외처리를 해줘 만약 올라가는 곳이 막혀 있는 경우나 올라가지 못하는 언덕일
> 경우(navmesh가 길을 못찾았을 경우)는 그 위치에 가장 가까운 위치로만 가도록 했으면 좋겠어

## 조사 결과

- 지상 유닛 이동은 전부 `MoveAgentTo()` 한 곳으로 모여서 나간다:
  - `UnitController.cs:606` (플레이어 유닛) - `AttackUnitTarget`, `AttackMoveTo`, `FriendlyAttackTick`,
    `FollowTick`, `FollowBuilding`, 채집/반납 이동 등 전부 이 한 함수를 거침.
  - `EnemyUnitController.cs:323` (적 유닛) - `MoveTo`, `AttackMoveTo`, `ChaseTarget` 등이 이 함수를 거침.
  - 두 함수 모두 지상 유닛이면 그냥 `navMeshAgent.SetDestination(destination)`만 호출하고 끝.
- **경사로로 실제로 연결된 언덕**(같은 NavMesh 연결 요소)은 손댈 필요 없음 - Unity NavMeshAgent가 이미
  `PathPartial` 등을 알아서 계산해서 갈 수 있는 데까지 스스로 이동하고, `remainingDistance` 기준의 도착
  판정(`UnitController.cs:424`, `1282`)도 정상 동작함.
- 문제가 되는 경우는 **`SetDestination()`이 아예 `false`를 반환**하는 경우 - 목적지 좌표 근처에서
  NavMesh 샘플링 자체가 실패하는 경우(경사로가 없어 그 지점 주변 NavMesh 표본 반경 안에 밟을 수 있는
  지점이 안 잡히는 경우 등). 이땐 `navMeshAgent.destination`이 갱신되지 않고 유닛이 그 자리에서 아예
  움직이지 않는다 - 코드 내 기존 주석(`UnitController.cs:602~605`, `1655~1660`)에서도 이미 인지하고
  있던 문제이고, 현재는 채집 반납 이동(`GatherTick`/`MovingToBase`) 한 곳에서만 실패를 감지해서
  "그 자리에 멈추고 포기"하는 처리를 해뒀음. 공격/이동/추격 등 나머지 모든 호출부는 이 실패를 그냥
  무시하고 있어서, 못 오르는 언덕을 명령 목적지나 공격 대상으로 잡으면 유닛이 조용히 제자리에 멈춰버림.

## 코드 변경 (제안)

두 파일의 `MoveAgentTo()`에서 `SetDestination()`이 실패했을 때만, 더 넓은 반경으로
`NavMesh.SamplePosition`을 다시 시도해서 "그 근방에서 가장 가까운 NavMesh 위 지점"으로 목적지를
재설정한다. 그 지점이 여전히 도달 불가능한 영역(끊긴 언덕 위)이더라도, Unity NavMeshAgent가 알아서
`PathPartial`로 처리해서 실제로 갈 수 있는 데까지만 이동하게 된다 - 즉 "가장 가까운 위치까지만 이동"
요구사항을 Unity 기본 파샬 패스 동작에 그대로 맡기는 방식.

### `Assets/Scripts/Unit/UnitController.cs`

기존 코드 (606번째 줄):
```csharp
    private bool MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            return navMeshAgent.SetDestination(destination);
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
            return true;
        }
    }
```

변경 코드:
```csharp
    // 목적지 지점 근처에 NavMesh 샘플이 아예 안 잡히는 경우(경사로 없이 끊긴 언덕 위 등)
    // SetDestination이 실패하며 유닛이 조용히 멈춰버리므로, 더 넓은 반경으로 가장 가까운 NavMesh 지점을
    // 찾아 재시도한다. 경사로로 실제 연결된 언덕은 SetDestination이 바로 성공하고 Unity가 알아서
    // PathPartial로 갈 수 있는 데까지만 이동하므로 이 fallback을 타지 않는다 (doc/0375).
    private const float UnreachableDestinationSampleRadius = 20f;

    private bool MoveAgentTo(Vector3 destination, bool destinationIsAirborne = false)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            if (navMeshAgent.SetDestination(destination))
                return true;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas))
                return navMeshAgent.SetDestination(hit.position);

            return false;
        }
        else
        {
            targetPosition = AirTargetPosition(destination, destinationIsAirborne);
            isMovingAirUnit = true;
            return true;
        }
    }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

기존 코드 (323번째 줄):
```csharp
    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(destination);
        }
        else
        {
            targetPosition = AirTargetPosition(destination);
            isMovingAirUnit = true;
        }
    }
```

변경 코드:
```csharp
    // UnitController.MoveAgentTo와 동일한 fallback (doc/0375) - 경사로 없이 끊긴 언덕 등으로
    // SetDestination이 실패하면 더 넓은 반경으로 가장 가까운 NavMesh 지점을 찾아 재시도한다.
    private const float UnreachableDestinationSampleRadius = 20f;

    private void MoveAgentTo(Vector3 destination)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = false;
            if (!navMeshAgent.SetDestination(destination) &&
                NavMesh.SamplePosition(destination, out NavMeshHit hit, UnreachableDestinationSampleRadius, NavMesh.AllAreas))
            {
                navMeshAgent.SetDestination(hit.position);
            }
        }
        else
        {
            targetPosition = AirTargetPosition(destination);
            isMovingAirUnit = true;
        }
    }
```

## 열린 질문

- `UnreachableDestinationSampleRadius`(20m)는 임의로 잡은 값 - 맵 스케일에 비해 너무 좁거나 넓으면
  조절 필요. 일단 유닛 몇 기 반경(대략 언덕 하나 크기) 정도로 넉넉히 잡음.
- 이 fallback을 타도 여전히 반경 안에 NavMesh 표본이 하나도 없으면(진짜 완전히 고립된 영역) 그대로
  `false` 반환 - 기존처럼 제자리에 멈춤 (더 이상 손쓸 방법이 없는 극단적 케이스라 별도 처리 안 함).
- 성능: `FriendlyAttackTick`/`FollowTick`처럼 매 프레임 `MoveAgentTo`를 호출하는 곳에서 목적지가
  계속 도달 불가능하면 매 프레임 `SetDestination` 실패 + `NavMesh.SamplePosition` 재시도가 반복됨.
  현재 규모에서는 문제없을 것으로 보고 캐싱/쓰로틀링은 추가하지 않음 - 나중에 체감되면 추가.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
