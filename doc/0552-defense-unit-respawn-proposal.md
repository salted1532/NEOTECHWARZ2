# 0552 - 배치형 방어 유닛 죽으면 같은 자리에 재생산 (EnemyAIDirector + AllyAIDirector)

## 날짜
2026-08-13

## 문서 번호 정정 메모
`AllyAIDirector.cs`/`EnemyBuildingController.cs`/`UnitDataSO.cs`의 이전 세 커밋(AllyPrefab 자동 조회,
디버그 리스트 3종 + Hive Core 우선순위, nextWaveCountdown)에서 코드 주석에 `doc/0552`/`doc/0553`/
`doc/0554`라고 남겼었는데, 실제로는 그 문서들을 따로 만들지 않고 전부 `doc/0543`(Ally AI Director
설계안)의 "수정 요청 2/3/4" 절에 이어서 적었었다 - 즉 그 번호들은 실존하지 않는 문서를 가리키고 있었음.
이번에 그 코드 주석을 전부 `doc/0543`으로 정정했고, 이번 세션(배치형 방어 유닛 재생산)에 실제로 비어있던
`0552`번을 이 문서에 배정한다. `doc/0543`은 계속 Ally AI Director 자체의 누적 변경 로그로 쓰고, 이후
독립적인 새 기능은 이렇게 새 번호로 분리한다.

## 요청 내용
"적AI 랑 아군OC도 만약 프리팹으로 배치된 유닛(방어유닛인데) 이게 죽으면 그 자리에 같은 유닛이
배치되도록 생산시키도록해줘. 부서진 건물은 다시 건설하는건 아직 추가 할 생각이 없어"

→ 미션 제작자가 씬에 미리 세워둔("프리팹으로 배치된") 고정 수비 유닛이 죽으면, 같은 자리에 같은
종류의 유닛을 다시 생산해 세운다. `EnemyAIDirector`/`AllyAIDirector` 둘 다 적용. 건물 파괴 후 재건축은
명시적으로 범위 밖.

## 기존 구조와의 차이
두 director 모두 지금까진 "자기가 스폰한 유닛"(`garrison`/`raidGarrison`)만 추적해서 죽으면 다시
채웠다. 미션 씬에 처음부터 손으로 배치해둔 유닛(웨이브/별동대와 무관하게 특정 지점을 지키는 수비대)은
이 풀에 속하지 않아서 죽으면 그냥 사라진 채로 끝났다 - 이번 요청은 그 빈틈을 채우는 것.

## 설계
### 새 인스펙터 필드
```csharp
[Header("배치형 방어 유닛 (씬에 미리 세워둔 고정 수비 유닛 - 죽으면 같은 자리에 같은 종류로 즉시 재생산. 건물 재건은 범위 밖)")]
[SerializeField] private List<EnemyUnitController> defenseUnits;   // EnemyAIDirector
[SerializeField] private List<AllyController> defenseUnits;        // AllyAIDirector
```
미션 제작자가 씬에 미리 배치해둔 유닛 인스턴스를 이 리스트에 드래그해 등록한다.

### 내부 추적 - DefenseSlot
```csharp
private class DefenseSlot
{
    public int unitID;
    public Vector3 position;
    public Quaternion rotation;
    public EnemyUnitController current; // AllyAIDirector에서는 AllyController
}

private readonly List<DefenseSlot> defenseSlots = new List<DefenseSlot>();
```
`Start()`에서 `defenseUnits`의 각 유닛을 순회하며 그 시점의 위치/방향/`unitID`를 캡처해 슬롯을 만든다
(그 유닛이 죽어도 슬롯 자체는 남아있고 `current`만 null이 됨 - "몇 개의 방어 초소가 있고 각각 어디를
지키는지"라는 정보 자체는 유닛의 생사와 무관하게 유지).

`garrison`/`raidGarrison`처럼 웨이브·별동대 차출 대상 풀에 넣지 않는다 - 이 유닛들은 "그 자리를
지키는" 역할이라 다른 시스템이 빼가면 안 됨(완전히 별도 풀로 격리, `raidGarrison`을 `garrison`과
분리했던 것과 동일한 이유, doc/0538).

### 재생산 - RespawnDeadDefenseUnits()
`ReinforceRoutine`(기존 20초 주기)에 한 단계 추가 - 새 코루틴을 만들지 않고 기존 "손실 보충" 루틴에
합류시킨다(정확히 같은 성격의 일이라 별도 타이머가 필요 없음).

```csharp
private void RespawnDeadDefenseUnits()
{
    foreach (DefenseSlot slot in defenseSlots)
    {
        if (slot.current != null)
            continue;

        UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(slot.unitID) : null;
        if (data == null || data.Prefab == null) // AllyAIDirector는 data.AllyPrefab
            continue;

        GameObject spawned = Instantiate(data.Prefab, slot.position, slot.rotation);
        if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            slot.current = unit;
    }
}
```
`EnemyAIDirector`가 이미 갖고 있는 생산 대기열(`SpawnQueue`/`EnqueueProduction`, doc/0544)은
`spawnPoints` 기준으로만 동작하는데, 방어 유닛의 위치는 스폰 지점이 아니라 임의의 수비 지점이라 그
큐를 재사용할 수 없다 - 그래서 이 기능은 즉시 `Instantiate`하는 별도의 단순한 경로를 쓴다(생산 시간
시뮬레이션 없음 - 요청에 없었고, "그 자리를 비워두지 않는다"가 핵심이라 지연시킬 이유도 없음).

### 건물 재건축은 안 함
`EnemyBuildingController`/`AllyBuildingController`가 파괴되면(`OnDestroy()`에서
`ActiveBuildings.Remove`) 그걸로 끝 - 이번 요청에서 명시적으로 제외됐으므로 아무 로직도 추가하지 않음.

## 영향받는 파일
- 변경: `Assets/Scripts/System/EnemyAIDirector.cs` - `defenseUnits` 필드, `DefenseSlot` 내부 클래스,
  `Start()`에서 슬롯 캡처, `ReinforceRoutine()`에서 `RespawnDeadDefenseUnits()` 호출.
- 변경: `Assets/Scripts/System/AllyAIDirector.cs` - 위와 동일한 구조(`AllyController`/`AllyPrefab`
  버전).
- 변경(문서 번호 정정만): `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`,
  `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - 주석의 `doc/0552`/`doc/0553` 참조를 실제 문서인
  `doc/0543`으로 정정(코드 동작 변경 없음).

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(기존 베이스라인과 동일 - 새 경고 없음).

## 남은 작업
씬에 미리 배치해둔 방어 유닛들을 각 director의 `defenseUnits` 리스트에 실제로 드래그해 연결하는 건
미션 제작 단계 - 스크립트만으로는 아무 유닛도 아직 등록되지 않음.
