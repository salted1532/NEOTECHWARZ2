# 0534 - EnemyAIDirector 웨이브 목표 동적 재지정(MainBase 랜덤 → 파괴 시 재조준) 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
- "Attack target을 플레이어의 건물리스트에서 MainBase중 하나를 골라서 공격하도록 할수 있나?" (질문 →
  가능함, `UnitController.FindNearestMainBase()`와 동일한 `rtsController.BuildingList` + `CompareTag`
  패턴 재사용 가능하다고 답변함)
- "공격한다는게 땅공격을 말하는거야" / "지정공격이 아님" (확인 질문 → `AttackMoveTo(Vector3)`는 좌표 기반
  땅공격이지 특정 오브젝트를 락온하는 지정공격이 아니라고 답변함)
- **이번 요청**: "매번 웨이브마다 동적으로 랜덤하게 정하도록 해주고 만약 웨이브에서 해당 건물(메인기지)를
  부수면 즉시 다른 메인기지 위치를 공격하도록 해줘 별동대가 다 죽을때까지 웨이브는 끝나지 않음. 만약
  플레이어의 메인기지가 하나도 안남으면 플레이어의 건물 리스트중 아무거나 공격 명령을 내리도록"

→ doc/0532 결정 사항 #1("attackTarget은 인스펙터 고정 지정")을 뒤집는 변경. 이 문서는 제안일 뿐, 아직
코드 수정 안 함.

## 기존 코드 조사
지금 `LaunchWave()`(`EnemyAIDirector.cs:112-131`)는 고정 `Transform attackTarget` 하나로 매 웨이브 항상
같은 좌표를 쓰고, 부대를 보낸 뒤로는 아무것도 지켜보지 않는다("보내고 끝", doc/0532 결정 사항 #4). 요청은
이 부분을 다음처럼 바꿔달라는 것:
1. 목표를 고정 좌표가 아니라 **웨이브마다 랜덤한 플레이어 MainBase**로.
2. 그 목표가 **파괴되면 즉시 다른 MainBase로 재조준** - 즉 이 웨이브 전용으로 "목표가 죽을 때까지
   지켜보는" 로직이 새로 필요함(기존엔 없었음).
3. **웨이브(이 부대)는 전멸할 때까지 안 끝남** - 재조준을 계속 반복.
4. MainBase가 하나도 안 남으면 **플레이어 건물 아무거나**로 폴백.

`RTSUnitController.BuildingList`(public, `RTSUnitController.cs:52`)가 플레이어 건물 전체 목록이고,
`UnitController.FindNearestMainBase()`(`UnitController.cs:1963`)가 이미 `CompareTag("MainBase")`로 같은
목록을 걸러 쓰는 선례 - 그대로 재사용.

## 설계안

### `attackTarget`(Transform 필드) 제거
doc/0532에서 "인스펙터 고정 지정"으로 확정했던 필드지만, 이번 요청으로 완전히 동적 선정으로 바뀌므로
더 이상 안 씀 - 필드 삭제.

### `PickAttackTarget()` - 목표 선정
```
BuildingController PickAttackTarget() {
    // MainBase 중 무작위
    var mainBases = rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase"));
    if (mainBases.Count > 0) return mainBases[Random.Range(0, mainBases.Count)];

    // MainBase가 하나도 없으면 플레이어 건물 아무거나
    var any = rtsController.BuildingList.FindAll(b => b != null);
    return any.Count > 0 ? any[Random.Range(0, any.Count)] : null; // 건물이 아예 없으면 null(더 공격할 게 없음)
}
```

### `LaunchWave()` - 부대를 보낸 뒤 별도 감시 코루틴을 띄우고 바로 리턴
```
IEnumerator LaunchWave() {
    ... (기존 스폰/집결 로직 동일) ...
    StartCoroutine(RunWaveSquad(squad)); // 감시는 별도 코루틴 - AttackWaveRoutine의 다음 웨이브 스케줄을 막지 않음
}
```
**주의**: "웨이브는 끝나지 않는다"는 **이 부대(squad)의 전투가 안 끝난다**는 뜻으로 해석 - `waveTimes`에
따른 **다음 웨이브 발사 스케줄 자체는 별개로 계속 진행**된다(이전 부대가 아직 싸우는 중이어도 다음
시각이 되면 새 부대가 또 나감). 이렇게 안 하면 부대 하나가 안 죽고 버티는 동안 `AttackWaveRoutine`
전체가 멈춰버려서 "1.5배씩 늘어나는 웨이브"(doc/0533) 자체가 안 굴러감 - 이 해석이 맞는지 아래 확인
요청.

### `RunWaveSquad()` - 목표 파괴 감시 + 재조준, 전멸 시 종료
```
IEnumerator RunWaveSquad(List<EnemyUnitController> squad) {
    BuildingController target = null;
    while (true) {
        squad.RemoveAll(u => u == null);
        if (squad.Count == 0) yield break; // 전멸 - 이 웨이브 종료

        if (target == null) { // 최초 또는 방금 파괴됨
            target = PickAttackTarget();
            if (target == null) yield break; // 플레이어 건물이 하나도 안 남음 - 더 할 게 없음
            foreach (unit in squad) unit?.AttackMoveTo(target.transform.position);
        }

        yield return null; // 매 프레임 target == null(파괴됨) 여부만 확인 - 즉시 재조준
    }
}
```
매 프레임 확인하는 이유: "부수면 즉시 다른 메인기지를 공격"이라는 요청이 지연 없는 즉시 반응을 원하는
것으로 판단 - 동시에 활성화된 웨이브 부대 수가 많지 않아(director 하나당 최대 waveSize/2 정도) 매 프레임
검사해도 비용이 무시할 만함.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **"웨이브는 안 끝난다"의 범위**: 이 부대의 전투만 안 끝나고, 다음 예정된 웨이브(`waveTimes` 스케줄)는
   별개로 계속 진행. 설계안 그대로 확정.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs` - `attackTarget` 필드 제거, `PickAttackTarget()` /
  `RunWaveSquad()` 신규 추가, `LaunchWave()`가 감시 코루틴을 띄우도록 수정

## 코드 변경

### 기존 코드
```csharp
[SerializeField] private int maxWaveSize = 20; // 1.5배씩 계속 커지는 걸 막는 상한(0이면 무제한, doc/0533)
[SerializeField] private Transform attackTarget;
```
```csharp
List<EnemyUnitController> squad = TakeSquad(size);
if (squad.Count == 0)
    yield break;

if (assembleBeforeAttack)
    yield return AssembleAtRally(squad);

foreach (EnemyUnitController unit in squad)
    if (unit != null)
        unit.AttackMoveTo(attackTarget.position);
}
```

### 변경 코드
```csharp
[SerializeField] private int maxWaveSize = 20; // 1.5배씩 계속 커지는 걸 막는 상한(0이면 무제한, doc/0533)
```
```csharp
List<EnemyUnitController> squad = TakeSquad(size);
if (squad.Count == 0)
    yield break;

if (assembleBeforeAttack)
    yield return AssembleAtRally(squad);

// 목표 파괴 시 재조준/전멸 시 종료 감시는 별도 코루틴 - 여기서 기다리면 이 부대가 다 죽을 때까지
// AttackWaveRoutine의 다음 웨이브 스케줄이 막혀버린다(doc/0534).
StartCoroutine(RunWaveSquad(squad));
}

// 부대가 전멸할 때까지: 목표가 없으면(최초, 또는 방금 파괴됨) PickAttackTarget()으로 다시 뽑아
// 전원에게 재발령한다(doc/0534). "즉시 재조준"이 요청 사항이라 매 프레임 확인한다 - 동시에 활성화된
// 웨이브 부대 수가 적어 비용은 무시할 만함.
private IEnumerator RunWaveSquad(List<EnemyUnitController> squad)
{
    BuildingController target = null;

    while (true)
    {
        squad.RemoveAll(u => u == null);
        if (squad.Count == 0)
            yield break; // 전멸 - 이 웨이브 종료

        if (target == null)
        {
            target = PickAttackTarget();
            if (target == null)
                yield break; // 플레이어 건물이 하나도 안 남음 - 더 공격할 곳이 없음

            foreach (EnemyUnitController unit in squad)
                if (unit != null)
                    unit.AttackMoveTo(target.transform.position);
        }

        yield return null;
    }
}

// 플레이어 MainBase 중 무작위 하나, MainBase가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
private BuildingController PickAttackTarget()
{
    if (rtsController == null)
        return null;

    List<BuildingController> mainBases = rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase"));
    if (mainBases.Count > 0)
        return mainBases[Random.Range(0, mainBases.Count)];

    List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
    return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).

## 참고
`raidTargets`(점령지 탈환 별동대, doc/0532)는 이번 변경과 무관 - 그쪽은 여전히 `CaptureSystem` 기반
고정 리스트를 그대로 씀. 이번 동적 재조준은 "공격 웨이브"(`LaunchWave`/`RunWaveSquad`)에만 적용됨.
