# 0575. 점령 별동대가 전투 후 멈추는 버그 수정

- 날짜: 2026-08-14

## 요청 내용

- "점령 별동대 중간에 적을 만나고 끝나더라도 다시 이동하도록 지속적으로 이동공격 명령을 내리도록 해야할거 같아 / 전투가 끝나고도 그냥 그대로 멈춰버리네"

## 조사 내용

`EnemyAIDirector.RaidRoutine()`(점령지 탈환 별동대)은 `unit.AttackMoveTo(target.transform.position)`를 편성 시 딱 한 번만 호출하고, 이후엔 개별 유닛의 `AttackMoveTick()`이 알아서 전투 종료 후 이동을 재개하는 구조에 의존한다(`AttackWaveRoutine`의 `RunWaveSquad`도 동일 - 목표가 안 바뀌면 재발령하지 않음).

`EnemyUnitController.cs`/`AllyController.cs`의 `Update()` 도착 판정에 버그가 있었다:

1. 자동교전 중 사거리 밖 상대를 추격하면 `ChaseTarget()` → `MoveAgentTo(적_위치)`가 호출되어 `navMeshAgent.destination`(지상)/`targetPosition`(공중)이 일시적으로 "적의 위치"로 바뀐다.
2. 그 상태로 적에게 가까워지면, `Update()`의 도착 판정(`transform.position`과 `navMeshAgent.destination`/`targetPosition`의 거리)이 "원래 attackMoveDestination에 도착했다"고 착각해서 `attackMoveDestination`을 무조건 `null`로 지운다.
3. 전투가 끝나도 `AttackMoveTick()`은 `if (attackMoveDestination == null) return;`이라 아무 것도 하지 않는다 - 그 자리에 영구히 멈춘다.

`RaidRoutine()`은 목표 건물이 바뀌지 않는 한 재발령하지 않으므로, 이 버그가 있으면 정말로 아무도 다시 명령을 내려주지 않아 그대로 멈춘다. `AllyController.cs`에도 동일한 코드가 그대로 복제돼 있어 아군 OC 공격 웨이브도 같은 문제가 있었다.

플레이어가 조종하는 `UnitController.cs`도 구조는 비슷하지만(`orderedTarget`/`friendlyTarget`/`followTarget`이 있을 때만 도착 판정을 건너뛰는 가드가 있음), 순수 자동교전(명시 대상 지정 없는 공격-이동 중 자동 교전) 경로는 이 가드로 걸러지지 않아 같은 취약점이 남아있을 수 있음 - 이번 리포트 범위 밖이라 손대지 않음(필요 시 별도 확인).

## 코드 변경

`attackMoveDestination`을 지우기 전에, 실제로 "그 지점(`attackMoveDestination`)"에 도착했는지 별도로 확인하도록 조건을 추가. 도착한 지점이 추격 중이던 적의 위치일 뿐이면 `attackMoveDestination`을 그대로 유지해, 전투가 끝난 뒤 `AttackMoveTick()`이 원래 목적지로 이동을 재개할 수 있게 한다. 지상/공중 판정 두 곳 모두, `EnemyUnitController.cs`/`AllyController.cs` 두 파일 모두 동일하게 수정.

### EnemyUnitController.cs / AllyController.cs - 공중 유닛 도착 판정

기존 코드:
```csharp
if (arrivedHorizontally && arrivedVertically)
{
    isMovingAirUnit = false;
    currentState = EnemyState.Idle; // AllyController는 AllyState.Idle
    attackMoveDestination = null;
}
```
변경 코드:
```csharp
if (arrivedHorizontally && arrivedVertically)
{
    isMovingAirUnit = false;
    currentState = EnemyState.Idle; // AllyController는 AllyState.Idle

    // 추격(ChaseTarget) 중엔 targetPosition이 일시적으로 적 위치로 바뀌어 있을 수 있다 -
    // 실제 attackMoveDestination에 도착했을 때만 지워야, 전투가 끝난 뒤 AttackMoveTick이
    // 원래 목적지로 이동을 재개할 수 있다(doc/0575).
    if (attackMoveDestination == null ||
        (transform.position - attackMoveDestination.Value).sqrMagnitude <= arriveDistance * arriveDistance)
    {
        attackMoveDestination = null;
    }
}
```

### EnemyUnitController.cs / AllyController.cs - 지상 유닛 도착 판정

기존 코드:
```csharp
navMeshAgent.isStopped = true;
currentState = EnemyState.Idle; // AllyController는 AllyState.Idle
attackMoveDestination = null;
```
변경 코드:
```csharp
navMeshAgent.isStopped = true;
currentState = EnemyState.Idle; // AllyController는 AllyState.Idle

// 추격(ChaseTarget) 중엔 navMeshAgent.destination이 일시적으로 적 위치로 바뀌어 있을
// 수 있다 - 실제 attackMoveDestination에 도착했을 때만 지워야, 전투가 끝난 뒤
// AttackMoveTick이 원래 목적지로 이동을 재개할 수 있다(doc/0575).
if (attackMoveDestination == null ||
    (transform.position - attackMoveDestination.Value).sqrMagnitude <= arriveDistance * arriveDistance)
{
    attackMoveDestination = null;
}
```

## 요약 / 남은 작업

- `npx uloop-cli compile`로 컴파일 확인 완료 (0 에러, 기존 경고 40개는 이 변경과 무관한 `FindFirstObjectByType` deprecated 경고).
- `UnitController.cs`(플레이어 유닛)의 순수 자동교전(명시 대상 없는 공격-이동) 경로도 같은 취약점이 있어 보이나, 이번 요청 범위 밖이라 미수정 - 필요하면 별도 요청으로.

## 변경된 파일

- Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs
- Assets/Scripts/FogOfWar/Ally/AllyController.cs
