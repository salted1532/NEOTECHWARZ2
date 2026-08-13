# 0538 - EnemyAIDirector 점령지 탈환 별동대 전용 병력 풀 분리 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
(doc/0536, 0537에서 "공격 웨이브와 점령지 탈환이 같은 `garrison`을 놓고 경쟁한다"는 걸 확인한 뒤)
"별도에 별동대를 꾸리도록 하는게 좋을거 같아"

→ 공격 웨이브용 병력과 점령지 탈환 별동대용 병력을 **완전히 분리된 풀**로 관리해달라는 요청. 이 문서는
제안일 뿐, 아직 코드 수정 안 함.

## 기존 코드 조사
지금 `garrison`(단일 리스트) 하나를 `LaunchWave()`와 `RaidRoutine()`이 똑같이 `TakeSquad(size)`로
나눠 쓴다(doc/0537) - "누가 먼저 뽑아가느냐" 경쟁 구조라, 웨이브가 크게 나가는 타이밍에 별동대 차출이
겹치면 별동대가 빈손이 될 수 있다.

`SpawnUnit()`은 현재 스폰과 동시에 `garrison`에 직접 추가하는 구조(`void` 반환) - 풀을 두 개로 나누려면
"스폰만 하고 어느 풀에 넣을지는 호출부가 결정"하는 형태로 바꿔야 함.

**부수 발견(기존 버그)**: `SpawnUnit()`이 실패(예: `attackUnitIDs`가 비어있거나 `GetEnemyUnitData`가
null)하면 아무것도 안 하고 조용히 리턴하는데, 이걸 부르는 `while (garrison.Count < garrisonTarget)
SpawnUnit();` 루프는 `garrison.Count`가 절대 안 늘어나므로 **무한 루프**에 빠진다(`Start()`와
`ReinforceRoutine()` 둘 다 이 패턴). 이번에 `SpawnUnit()`을 어차피 고치는 김에 같이 고침(스코프 안 벗어남 -
같은 함수).

## 설계안

### 풀 분리
```
List<EnemyUnitController> garrison;      // 공격 웨이브 전용 (기존과 동일한 이름/용도)
List<EnemyUnitController> raidGarrison;  // 점령지 탈환 별동대 전용 (신규)
int garrisonTarget = 6;                  // 기존 - 웨이브 성장에 맞춰 자동 상향(doc/0533)
int raidGarrisonTarget = 3;              // 신규 - raidSquadSize(기본 3)와 동일, 여유분 없이 고정
```
`deployed`(HashSet)는 그대로 공용으로 둔다 - "이미 나갔다"는 표시일 뿐, 어느 풀 출신인지는 상관없이
한 번 나간 유닛은 재사용하지 않으면 되므로 굳이 풀별로 나눌 이유가 없음.

### `SpawnUnit()` - 반환형으로 변경
```
EnemyUnitController SpawnUnit() {
    if (attackUnitIDs.Count == 0 || rtsController == null) return null;
    var data = rtsController.GetEnemyUnitData(attackUnitIDs[Random.Range(0, attackUnitIDs.Count)]);
    if (data == null || data.Prefab == null) return null;
    var spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<EnemyUnitController>(out var unit) ? unit : null;
}
```

### `FillPool()` - 목표 인원까지 채우는 공용 헬퍼 (무한 루프 방지 포함)
```
void FillPool(List<EnemyUnitController> pool, int target) {
    while (pool.Count < target) {
        EnemyUnitController unit = SpawnUnit();
        if (unit == null) break; // 스폰 실패 - 여기서 멈춰야 무한 루프 안 됨(기존 버그 수정)
        pool.Add(unit);
    }
}
```
`Start()`(초기 충원)와 `ReinforceRoutine()`(주기 충원) 둘 다 `FillPool(garrison, garrisonTarget)` +
`FillPool(raidGarrison, raidGarrisonTarget)`를 호출하도록 바뀜 - 한 코루틴이 두 풀을 같은 주기에 같이
관리(별도 코루틴/인터벌 불필요, `reinforceCheckInterval` 하나로 충분).

### `TakeSquad()` - 어느 풀에서 뽑을지 매개변수로 받도록
```
List<EnemyUnitController> TakeSquad(List<EnemyUnitController> pool, int size) { ... } // 로직 동일, pool만 매개변수화
```
- `LaunchWave()` → `TakeSquad(garrison, size)`
- `RaidRoutine()` → `TakeSquad(raidGarrison, raidSquadSize)`

이제 웨이브가 아무리 크게 나가도 `raidGarrison`은 안 건드리므로, 별동대가 빈손이 되는 경쟁 상황이
사라짐(doc/0536/0537에서 확인한 문제 해결).

### 영향 없음
`HandleBaseAttacked()`(doc/0535)는 애초에 `garrison`을 안 쓰고 `Physics.OverlapSphere`로 씬 전체를
스캔하므로 이번 분리와 무관 - 그대로 둠.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **`raidGarrisonTarget` 기본값**: **3**(여유분 없이 `raidSquadSize`와 동일) - "점령지 별동대는 적어야
   해서" 늘리지도 줄이지도 않는 고정값으로. `garrisonTarget`처럼 자동으로 커지는 로직(doc/0533)은
   `raidGarrisonTarget`엔 아예 적용 안 함 - 애초에 `raidSquadSize` 자체가 웨이브처럼 커지는 값이 아니라
   인스펙터 고정값이므로 추가 로직 없이 자연히 고정으로 유지됨.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 기존 코드
```csharp
[Header("점령지 탈환")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;
[SerializeField] private int raidSquadSize = 3;
```
```csharp
private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();
```
```csharp
rtsController = FindFirstObjectByType<RTSUnitController>();

while (garrison.Count < garrisonTarget)
    SpawnUnit();
```
```csharp
List<EnemyUnitController> squad = TakeSquad(size); // LaunchWave()
```
```csharp
List<EnemyUnitController> squad = TakeSquad(raidSquadSize); // RaidRoutine()
```
```csharp
private IEnumerator ReinforceRoutine()
{
    WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
    while (true)
    {
        yield return wait;

        garrison.RemoveAll(u => u == null);
        deployed.RemoveWhere(u => u == null);

        while (garrison.Count < garrisonTarget)
            SpawnUnit();
    }
}

private void SpawnUnit()
{
    if (attackUnitIDs.Count == 0 || rtsController == null)
        return;

    int id = attackUnitIDs[Random.Range(0, attackUnitIDs.Count)];
    UnitData data = rtsController.GetEnemyUnitData(id);
    if (data == null || data.Prefab == null)
        return;

    GameObject spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
    if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
        garrison.Add(unit);
}
```
```csharp
private List<EnemyUnitController> TakeSquad(int size)
{
    garrison.RemoveAll(u => u == null);

    List<EnemyUnitController> squad = new List<EnemyUnitController>();
    foreach (EnemyUnitController unit in garrison)
    {
        if (squad.Count >= size)
            break;
        if (deployed.Contains(unit))
            continue;

        squad.Add(unit);
        deployed.Add(unit);
    }

    return squad;
}
```

### 변경 코드
```csharp
[Header("점령지 탈환")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;
[SerializeField] private int raidSquadSize = 3;
[SerializeField] private int raidGarrisonTarget = 3; // 별동대 전용 대기 인원 - 웨이브와 안 겹치게 별도 풀(doc/0538), 고정값(늘거나 줄지 않음)
```
```csharp
private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();

// 점령지 탈환 별동대 전용 병력 풀(doc/0538) - garrison과 완전히 분리, raidGarrisonTarget으로 유지.
private readonly List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```
```csharp
rtsController = FindFirstObjectByType<RTSUnitController>();

FillPool(garrison, garrisonTarget);
FillPool(raidGarrison, raidGarrisonTarget);
```
```csharp
List<EnemyUnitController> squad = TakeSquad(garrison, size); // LaunchWave()
```
```csharp
List<EnemyUnitController> squad = TakeSquad(raidGarrison, raidSquadSize); // RaidRoutine()
```
```csharp
private IEnumerator ReinforceRoutine()
{
    WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
    while (true)
    {
        yield return wait;

        garrison.RemoveAll(u => u == null);
        raidGarrison.RemoveAll(u => u == null);
        deployed.RemoveWhere(u => u == null);

        FillPool(garrison, garrisonTarget);
        FillPool(raidGarrison, raidGarrisonTarget);
    }
}

// pool이 target 인원에 도달할 때까지 계속 스폰해서 채운다. 스폰이 실패하면(attackUnitIDs가 비었거나
// 데이터를 못 찾음) 즉시 멈춘다 - 안 그러면 pool.Count가 영영 안 늘어나 무한 루프에 빠진다(doc/0538,
// FillPool 도입 전엔 이 가드가 없어서 잠재 버그였음).
private void FillPool(List<EnemyUnitController> pool, int target)
{
    while (pool.Count < target)
    {
        EnemyUnitController unit = SpawnUnit();
        if (unit == null)
            break;

        pool.Add(unit);
    }
}

private EnemyUnitController SpawnUnit()
{
    if (attackUnitIDs.Count == 0 || rtsController == null)
        return null;

    int id = attackUnitIDs[Random.Range(0, attackUnitIDs.Count)];
    UnitData data = rtsController.GetEnemyUnitData(id);
    if (data == null || data.Prefab == null)
        return null;

    GameObject spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit) ? unit : null;
}
```
```csharp
private List<EnemyUnitController> TakeSquad(List<EnemyUnitController> pool, int size)
{
    pool.RemoveAll(u => u == null);

    List<EnemyUnitController> squad = new List<EnemyUnitController>();
    foreach (EnemyUnitController unit in pool)
    {
        if (squad.Count >= size)
            break;
        if (deployed.Contains(unit))
            continue;

        squad.Add(unit);
        deployed.Add(unit);
    }

    return squad;
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).
