# 0565 - 아군 OC 유닛 우선순위(건물보다 유닛) + 공격 대상 없을 때 명령 보류 (제안)

**날짜:** 2026-08-13

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 40개(전부 기존
  `FindFirstObjectByType` obsolete 경고 - 이번 변경과 무관).

## 요청 내용

> 아군OC 유닛들도 건물 공격중 적유닛이 우선순위가 높도록
> 그리고 공격하러 갈 건물이 없으면 공격 웨이브 유닛을 생산해놓고 공격 명령은 보류 된 상태로
> 오류 발생시키지 않기

두 가지 요청으로 나뉜다.

## 요청 1 - 건물 공격 중에도 사거리 내 적 유닛이 있으면 그쪽을 우선

### 조사 결과

플레이어 유닛의 자동교전(`Assets/Scripts/Unit/AttackRange.cs`)은 이미 이 우선순위를 갖고 있다
(doc/0460):

```csharp
// AttackRange.cs:137~144
private GameObject GetEngagedOrClosestEnemy()
{
    // 사거리 내 실제 적 유닛(건물 제외)이 있으면 항상 최우선 - 건물을 자동공격/공격-이동으로 물고
    // 있던 중이었어도 즉시 교체한다(doc/0460).
    GameObject priorityEnemyUnit = GetClosestEnemy(requireUnit: true);
    if (priorityEnemyUnit != null)
        return engagedEnemy = priorityEnemyUnit;
    ...
```

반면 아군 OC(`AllyController`)가 쓰는 `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`(적 AI와
공용 — `AllyAttackRange`가 상속)의 `GetEngagedOrClosestTarget()`은 이런 구분이 없다 — 건물이든
유닛이든 그냥 "지금 사거리 안의 가장 가까운 대상"만 보고, 한 번 물기 시작한 대상은 시야를 완전히
벗어나기 전까진 계속 우선한다(doc/0388 sticky 로직). 그래서 아군 OC가 건물을 공격 중일 때 적 유닛이
다가와도 계속 건물만 때린다.

`EnemyAttackRange`는 `EnemyUnitController`(실제 적 AI)와 `AllyAttackRange`(아군 OC) 둘 다가
상속해서 쓰는 공용 클래스라서, 이 우선순위를 그냥 켜버리면 적 AI의 기존 동작(건물/유닛 동일
우선순위)까지 같이 바뀐다 — 이번 요청은 "아군 OC 유닛들도"(플레이어처럼)라고만 했으므로 적 AI 쪽은
건드리지 않는다. `protected virtual` 플래그를 두고 `AllyAttackRange`에서만 켠다.

### 제안하는 수정

`Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`:

```csharp
// 하위 클래스가 켜면(AllyAttackRange, doc/0565) 사거리 안에 유닛(건물 제외)이 있을 때 항상 그 유닛을
// 최우선으로 삼는다 - 건물을 공격/추격 중이었어도 즉시 교체한다(플레이어 쪽
// AttackRange.GetEngagedOrClosestEnemy와 동일한 doc/0460 패턴). 기본값 false로 두어
// EnemyUnitController(적 AI)의 기존 동작(건물/유닛 동일 우선순위)은 그대로 유지한다.
protected virtual bool PrioritizeUnitTargets => false;

private GameObject GetEngagedOrClosestTarget()
{
    if (PrioritizeUnitTargets)
    {
        GameObject priorityUnit = GetClosestTarget(requireUnit: true);
        if (priorityUnit != null)
            return engagedTarget = priorityUnit;
    }

    if (engagedTarget != null && CanEngage(engagedTarget))
    {
        float loseSightRange = UnitRange + DetectionRangeMargin + EngagedTargetLoseSightMargin;
        float sqrDist = (transform.position - engagedTarget.transform.position).sqrMagnitude;
        if (sqrDist <= loseSightRange * loseSightRange)
            return engagedTarget;
    }

    return engagedTarget = GetClosestTarget();
}

private GameObject GetClosestTarget(bool requireUnit = false)
{
    GameObject closest = null;
    float closestSqrDist = float.MaxValue;

    foreach (GameObject target in targetsInRange)
    {
        if (target == null)
            continue;
        if (target == unreachableTarget)
            continue;
        if (!CanEngage(target))
            continue;
        if (requireUnit && !IsUnitTarget(target))
            continue;

        float sqrDist = (target.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < closestSqrDist)
        {
            closestSqrDist = sqrDist;
            closest = target;
        }
    }

    return closest;
}

// target이 "유닛"인지(건물이 아닌지) - 아군 OC가 볼 수 있는 EnemyUnitController(외계종족/적대 OC
// 유닛), 적 AI가 볼 수 있는 UnitController(플레이어 유닛)/AllyController(아군 OC 유닛) 셋 중 하나면
// 유닛으로 친다. 나머지(BuildingController/EnemyBuildingController 계열)는 건물로 취급한다.
private static bool IsUnitTarget(GameObject target) =>
    target.GetComponent<UnitController>() != null ||
    target.GetComponent<AllyController>() != null ||
    target.GetComponent<EnemyUnitController>() != null;
```

`Assets/Scripts/FogOfWar/Ally/AllyAttackRange.cs`에 오버라이드 한 줄 추가:

```csharp
public class AllyAttackRange : EnemyAttackRange
{
    private void Reset()
    {
        targetTags = new[] { "Enemy" };
    }

    protected override bool PrioritizeUnitTargets => true; // doc/0565 - 건물보다 적 유닛 우선
}
```

## 요청 2 - 공격할 건물이 없으면 웨이브를 생산만 해두고 공격 명령은 보류

### 조사 결과

`Assets/Scripts/System/AllyAIDirector.cs`의 `LaunchWave()`/`RunWaveSquad()`(202~254번째 줄)는
현재 목표가 없어도(`PickAttackTarget()`이 `null`) 이미 `TakeSquad()`로 `garrison`에서 병력을
차출해 `deployed`로 등록해버린 뒤에야 대상 없음을 확인하고 `RunWaveSquad`가 조용히 종료한다 - 예외는
안 나지만, 그 병력은 아무것도 안 하고 영원히 `deployed`에 남아 다음 웨이브에도 재사용되지 않는다
("낭비"). `AttackWaveRoutine()`은 그다음 사이클로 그냥 넘어가 버려서, 목표가 없는 상태가 계속되면
매 사이클(`WaveIntervalFor`)마다 병력을 계속 헛되이 소모한다.

### 제안하는 수정

`WaitUntilReady`(doc/0560)와 같은 패턴으로, 공격할 대상이 생길 때까지 폴링 대기하는 단계를
`WaitUntilReady`보다 앞에 추가한다 - 그동안도 `ReinforceRoutine()`은 별개 코루틴이라 계속 생산을
이어간다(요청하신 "생산해놓고 ... 보류"). `TakeSquad()`는 대상이 확인된 뒤(`LaunchWave()` 안)에만
호출되므로 목표 없는 동안은 병력이 `garrison`에 그대로 남아 낭비되지 않는다.

`Assets/Scripts/System/AllyAIDirector.cs`의 `AttackWaveRoutine()` (135~144번째 줄):

```csharp
private IEnumerator AttackWaveRoutine()
{
    while (true)
    {
        yield return CountdownSeconds(WaveIntervalFor(waveIndex));

        yield return WaitUntilTargetExists(); // doc/0565 - 공격할 건물이 없으면 보류(생산은 계속)
        yield return WaitUntilReady(CurrentWaveComposition());
        yield return LaunchWave(); // doc/0560: 별동대가 전멸할 때까지 여기서 대기
    }
}

// 공격할 적대 건물이 하나도 없으면 계속 폴링 대기한다(그동안도 ReinforceRoutine은 별개 코루틴이라
// 계속 생산됨) - 대상이 생기는 즉시(플레이어가 새로 짓거나 파괴됐던 건물이 다시 있는 등) 재개된다.
private IEnumerator WaitUntilTargetExists()
{
    while (PickAttackTarget() == null)
        yield return new WaitForSeconds(1f);
}
```

`EnemyAIDirector`(OC/Spore Brood 진영)는 이미 웨이브 루프 안에서 `IsPlayerDefeated()`로 대상
소멸을 확인해 스케줄 자체를 접는 방식(doc/0547)이라 이번 수정과는 대상이 달라 손대지 않는다 -
`AllyAIDirector`엔 그런 확인이 아예 없었던 것이 이번에 드러난 차이.

## 요청 2 보완 - 이미 파견된 별동대는 목표를 잃어도 포기하지 않고 계속 재탐색

추가 요청:

> 목표 건물이 없으면 바로 다음 건물을 찾아서 거기고 공격 명령이 가도록 하고
> 해당 별동대가 죽어야 다음 웨이브가 돌면서 유닛을 생산하도록 해줘

### 조사 결과

건물이 파괴돼 현재 목표(`target`)가 사라지면(Unity가 파괴된 `MonoBehaviour` 참조를 `== null`로
만들어주므로) `RunWaveSquad()`가 다음 프레임에 바로 감지해서 `PickAttackTarget()`으로 새 목표를
찾아 재발령하는 것 자체는 이미 잘 동작한다 - 여기까진 문제 없음.

문제는 그 재탐색에서도 목표가 없을 때(`PickAttackTarget()`이 `null`, 즉 공격 가능한 건물이 하나도
안 남음)다:

```csharp
// AllyAIDirector.cs:241~250 (현재)
if (target == null)
{
    target = PickAttackTarget();
    if (target == null)
        yield break; // 적대 세력 건물이 하나도 안 남음 - 더 공격할 곳이 없음

    foreach (AllyController unit in squad)
        if (unit != null)
            unit.AttackMoveTo(target.transform.position);
}
```

이 경우 `RunWaveSquad`가 **부대가 아직 살아있는데도** 그냥 끝나버린다(`yield break`). 그러면
`LaunchWave()`도 끝나고, `AttackWaveRoutine()`이 다음 사이클로 넘어가 버린다 - "별동대가 다
죽어야 다음 웨이브"(doc/0560)라는 규칙이 "목표를 다 잃으면"까지 걸려서 깨진다. 이 부대는 죽지도
않았는데 더는 관리되지 않는 채로(`deployed`에는 계속 남아 다음 웨이브 차출 대상에서도 제외된
채로) 방치된다.

### 제안하는 수정

`yield break`를 없애고, 목표가 없으면 그냥 다음 프레임에 다시 찾아본다 - 이미 매 프레임 도는
루프라 별도 폴링 타이머 없이도 목표가 생기는 즉시(0.multiple프레임 이내) 반응한다. 코루틴은
오직 부대가 전멸했을 때(위쪽의 `squad.Count == 0` 체크)만 끝난다.

```csharp
private IEnumerator RunWaveSquad(List<AllyController> squad)
{
    EnemyBuildingController target = null;

    while (true)
    {
        squad.RemoveAll(u => u == null);
        if (squad.Count == 0)
            yield break; // 전멸 - 이 웨이브 종료

        if (target == null)
        {
            target = PickAttackTarget();
            if (target != null)
            {
                foreach (AllyController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }
            // doc/0565: 목표가 없어도 포기하지 않는다 - yield break 대신 다음 프레임에 다시 찾는다.
            // 부대가 전멸하기 전까진 이 코루틴이 끝나지 않아야 "전멸해야 다음 웨이브"(doc/0560)
            // 규칙이 "목표를 다 잃으면"에서 새지 않는다.
        }

        yield return null;
    }
}
```

이렇게 하면 `AllyAIDirector.AttackWaveRoutine()`의 `WaitUntilTargetExists()`(요청 2)는 "아직 한
번도 파견 안 한 부대"의 최초 출발만 보류하고, 이미 파견된 부대는 목표를 잃어도 이 수정 덕분에
스스로 계속 재탐색하며 살아있는 한 절대 "끝난 것"으로 치지 않는다 - 두 로직이 자연스럽게 맞물린다.

## 영향받는 파일 (예정)

- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` (`PrioritizeUnitTargets` 가상 프로퍼티 신규,
  `GetEngagedOrClosestTarget`/`GetClosestTarget` 수정, `IsUnitTarget` 신규)
- `Assets/Scripts/FogOfWar/Ally/AllyAttackRange.cs` (`PrioritizeUnitTargets` 오버라이드 추가)
- `Assets/Scripts/System/AllyAIDirector.cs` (`AttackWaveRoutine`에 `WaitUntilTargetExists` 삽입,
  메서드 신규, `RunWaveSquad`의 목표 없음 `yield break` 제거)

## 영향받지 않는 부분

- `EnemyUnitController`(적 AI)의 자동교전 우선순위 - `PrioritizeUnitTargets` 기본값 `false`라
  기존 그대로(건물/유닛 동일 우선순위).
- `EnemyAIDirector`의 웨이브/별동대 대상 소멸 처리 - 이미 별도 로직(`IsPlayerDefeated`)이 있어
  변경 없음.

## 요약

- `EnemyAttackRange`에 `protected virtual bool PrioritizeUnitTargets`(기본 false)를 추가하고
  `AllyAttackRange`에서만 `true`로 오버라이드 - 아군 OC만 사거리 내 적 유닛을 건물보다 항상 우선하게
  된다(플레이어 쪽 `AttackRange.cs`와 동일한 doc/0460 패턴). 적 AI 쪽 동작은 그대로.
- `AllyAIDirector.AttackWaveRoutine()`에 `WaitUntilTargetExists()` 폴링 단계를 추가해, 공격할
  건물이 하나도 없는 동안은 병력 차출/공격 명령을 보류한다 - 생산(`ReinforceRoutine`)은 별개
  코루틴이라 그대로 계속되고, 목표가 없다고 병력을 헛되이 소모(deployed로 등록만 되고 방치)하는 일이
  없어진다. 예외 상황이 아니라 정상적인 폴링 대기라 오류는 발생하지 않는다.
- `RunWaveSquad()`에서 목표 없음(`yield break`) 조기 종료를 없애 - 이미 파견된 부대는 목표를 다
  잃어도 죽기 전까진 절대 "끝난 것"으로 치지 않고 계속 재탐색한다. 그 덕에 doc/0560의 "별동대가
  전멸해야 다음 웨이브" 규칙이 "목표를 다 잃으면"에서 새지 않는다.
- 아직 코드에 반영하지 않음 - 승인 대기.
