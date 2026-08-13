# 0559 - 다수 유닛 순찰(P) 시 일부만 멈추는 문제 (제안)

**날짜:** 2026-08-13

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 40개(전부 기존
  `FindFirstObjectByType` obsolete 경고 - 이번 변경과 무관).

## 요청 내용

> 여러유닛 선택 후 순찰 시키면 1마리만 순찰하고 나머지는 순찰 목적지 도착후 멈춤
> 2마리 일때만 그럼 4마리일땐 정상작동 3마리일때도 그럼 4마리일땐
> 정상작동하다가 중간에 2마리 멈춤
> 해당 버그 확인해줘

정리하면: 여러 유닛을 선택해 순찰(P) 명령을 내리면 1마리만 계속 왕복하고 나머지는 목적지에
도착한 후 멈춰버린다. 2마리, 3마리에서 재현되고, 4마리는 처음엔 정상 작동하다가 왕복을 반복하는
도중 그중 2마리가 멈춘다.

## 조사 결과 - 원인은 "같은 지점에 여러 유닛이 몰리면 도착 판정을 영원히 통과 못 함"

### 1. 다수 선택 시 모든 유닛이 정확히 같은 좌표를 목적지로 받는다

`Assets/Scripts/System/RTSUnitController.cs:560~572` (`PatrolSelectedUnits`):

```csharp
public void PatrolSelectedUnits(Vector3 end)
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].PatrolUnit(end);
    }
    ...
}
```

선택된 유닛 전부가 동일한 `end` 좌표를 받는다. `PatrolUnit()`
(`Assets/Scripts/Unit/UnitController.cs:1461~1488`)은 각자 현재 위치를 `startPoint`로 잡지만
`endPoint`는 전부 똑같다 - 즉 유닛 수만큼 정확히 같은 한 점을 향해 왕복하게 된다. (참고로
`MoveSelectedUnits`도 동일 패턴이지만, 이동은 "한 번만 대충 도착하면 끝"이라 문제가 드러나지
않는다. 순찰은 매 왕복마다 정확한 도착 판정이 반복 요구되므로 훨씬 취약하다.)

### 2. 순찰의 도착 판정이 너무 엄격해서, 여러 유닛이 몰리면 물리적으로 통과 못 하는 유닛이 생긴다

`PatrolTick()` (`UnitController.cs:1490~1537`)의 지상 유닛 도착 판정:

```csharp
bool arrivedGround =
    !isAirUnit &&
    !navMeshAgent.pathPending &&
    navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance;
```

`navMeshAgent.stoppingDistance`는 유닛 프리팹의 Inspector 값으로, 대부분의 플레이어 유닛
프리팹에서 `1`로 설정되어 있다(예: `Striker.prefab`, `Ironhawk.prefab` 등 다수 확인).
NavMeshAgent 반경(`m_Radius`)은 `0.5` 전후다.

여러 유닛이 같은 좌표로 몰리면, NavMeshAgent의 충돌 회피(avoidance) 때문에 그 좌표를 실제로
점유할 수 있는 유닛은 사실상 1마리뿐이다. 나머지는 반경만큼 밀려나 그 주변에 멈춰 서게 되는데,
뒤로 밀려난 유닛일수록 목적지까지 남은 실제 거리가 `stoppingDistance(1)`보다 커서
`arrivedGround`가 영원히 `true`가 되지 않는다.

`arrivedGround`가 안 되면 `PatrolTick()`은 그냥 `return`하고 다음 구간 전환
(`goingToEnd` 반전, `SetDestination`)을 전혀 실행하지 않는다 - 즉 그 유닛은 목적지 근처에
멈춰선 채로 다시는 순찰을 재개하지 못한다.

- 2~3마리: 한 유닛만 정확한 좌표를 차지하고 나머지는 밀려나 바로 멈춤 → "1마리만 정상, 나머지는
  도착 후 멈춤"과 일치.
- 4마리: 처음 한두 바퀴는 대형이 우연히 넉넉해서 여러 유닛이 `stoppingDistance` 안쪽까지 들어갈
  수 있지만, 왕복을 반복할수록 유닛들의 실제 위치가 조금씩 어긋나면서(서로 다른 타이밍에 반대편
  끝에 도착) 결국 그중 일부가 다시 밀려나 멈춘다 → "정상 작동하다가 중간에 2마리 멈춤"과 일치.

참고로 `GatherTick()`은 이미 같은 종류의 문제(자원 노드 등 한 지점에 여러 유닛이 몰리는 상황)를
겪었고, 그래서 `arriveDistance(0.5)`보다 훨씬 넉넉한 전용 허용 거리
`gatherInteractRange = 2f`를 따로 두고 있다(`UnitController.cs:227`, 주석: "장애물 특성상
arriveDistance보다 넉넉하게"). `buildInteractRange`도 동일한 이유로 존재한다
(`UnitController.cs:293`). 순찰만 이 패턴 없이 `navMeshAgent.stoppingDistance`(값이 작고,
유닛마다 제각각)를 그대로 쓰고 있어서 이번 문제가 생겼다.

## 제안하는 수정

기존 `gatherInteractRange`/`buildInteractRange`와 동일한 패턴으로, 순찰 전용 허용 거리
`patrolInteractRange`(예: `2f`)를 추가하고, `PatrolTick()`의 지상 유닛 도착 판정을
`navMeshAgent.remainingDistance`(경로 기반, 회피로 우회하면 값이 튈 수 있음) 대신 목적지까지의
실제 수평 거리로 바꿔 이 허용 거리와 비교한다. (공중 유닛 분기는 이미 실제 거리로 비교하고 있어
그대로 둔다.)

### `Assets/Scripts/Unit/UnitController.cs`

`gatherInteractRange` 옆에 필드 추가 (227번째 줄 부근):

```csharp
[SerializeField] private float patrolInteractRange = 2f; // 다수 유닛이 같은 순찰 지점에 몰릴 때 서로 밀려나도
                                                           // 도착 판정을 통과하도록 stoppingDistance보다 넉넉하게 (doc/0559)
```

`PatrolTick()` 도착 판정 (1495~1498번째 줄):

```csharp
bool arrivedGround =
    !isAirUnit &&
    !navMeshAgent.pathPending &&
    ((transform.position - (goingToEnd ? endPoint : startPoint)).sqrMagnitude
        <= patrolInteractRange * patrolInteractRange);
```

- `goingToEnd`가 이번 구간에서 향하고 있는 목적지(`endPoint`/`startPoint`)를 가리키므로 그걸
  기준으로 실제 거리를 잰다.
- `navMeshAgent.stoppingDistance`(유닛마다 제각각, 대체로 1)보다 넉넉한 고정값(2)을 씀으로써
  여러 유닛이 몰려도 밀려난 유닛이 그 반경 안에는 들어올 수 있게 한다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`patrolInteractRange` 필드 추가, `PatrolTick()`
  지상 유닛 도착 판정 수정)

## 요약

- 원인: `PatrolSelectedUnits`가 선택된 모든 유닛에게 정확히 같은 순찰 목적지 좌표를 주는데,
  `PatrolTick()`의 도착 판정(`navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance`,
  대체로 1)이 너무 엄격해서 같은 좌표에 여러 유닛이 몰리면 회피로 밀려난 유닛은 그 문턱을 영원히
  통과하지 못하고 그 자리에 멈춘다. 유닛 수/왕복 횟수에 따라 어떤 유닛이 밀려나는지가 매번
  달라져서 "2/3마리는 항상, 4마리는 도중에" 식으로 불규칙하게 보인다.
- 수정 제안: `gatherInteractRange`/`buildInteractRange`와 동일한 패턴으로 순찰 전용 허용 거리
  `patrolInteractRange(2f)`를 추가하고, 지상 유닛 도착 판정을 실제 목적지까지의 거리 기준으로
  바꿔 이 값과 비교한다.
- 아직 코드에 반영하지 않음 - 승인 대기.
