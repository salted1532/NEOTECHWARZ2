# 0570. 아군 OC AI 생산 속도 - 유닛별 생산시간 반영

날짜: 2026-08-13

**구현 완료.** 제안된 대로 `Assets/Scripts/System/AllyAIDirector.cs`에 적용, 컴파일 확인(0 errors).

## 요청 내용

> 아군OC 유닛 생산 속도도 적 AI 스크립트 와 같이 유닛별 생산속도를 지켜서 생산하도록 해줘

## 조사 내용

- `EnemyAIDirector`는 스폰 지점마다 `SpawnQueue`(생산 대기열)를 두고, `Update()`에서 매 프레임
  `EnemyProductionOrder.remainTime`을 `data.productionTime`만큼 깎아가며 순서대로(FIFO) 생산한다.
  부족한 유닛은 `FillPool` → `EnqueueProduction`으로 대기열에 "주문"만 걸어두고, 완성되면 그제서야
  `Instantiate`한다. 여러 스폰 지점이 있으면 `LeastLoadedQueue()`(대기열 남은 시간 합이 가장 적은 곳)로
  자동 분산한다. (`Assets/Scripts/System/EnemyAIDirector.cs:217-235, 318-349, 691-779`)
- 반면 `AllyAIDirector.FillPool()`(`Assets/Scripts/System/AllyAIDirector.cs:390-406`)은 부족분을
  `SpawnUnit()`으로 그 자리에서 즉시 `Instantiate`한다 - `UnitData.productionTime`을 전혀 참조하지 않아,
  유닛 종류와 무관하게 사실상 순간 생산된다. `NextSpawnPoint()`로 스폰 지점만 라운드로빈으로 순환할 뿐,
  생산 대기 시간 개념 자체가 없다.
- 즉 적 AI는 "유닛마다 정해진 생산시간만큼 기다렸다가 완성"인데, 아군 OC는 그 시간을 지키지 않고
  즉시 뽑아낸다 - 이번 요청은 이 차이를 없애고 아군도 `EnemyAIDirector`와 동일한 생산시간 대기열
  방식을 쓰도록 맞추는 것.

## 코드 변경 (제안)

### 1. 생산 대기열 클래스 추가 (`EnemyAIDirector`의 `SpawnQueue`/`EnemyProductionOrder`와 동일한 패턴)

**기존 코드** — 없음 (즉시 스폰 방식이라 대기열 자체가 없었음)

**변경 코드**
```csharp
// 생산 대기열 항목 - 완성되면 garrison에 추가된다(EnemyAIDirector.EnemyProductionOrder와 동일한 패턴).
private class AllyProductionOrder
{
    public int unitID;
    public float remainTime;
    public float totalTime;
}

// 스폰 지점 하나의 런타임 생산 대기열(EnemyAIDirector.SpawnQueue와 동일한 패턴 - 생산 건물 파괴 여부는
// 아군 OC 스폰 지점엔 없으므로 그 부분만 뺌).
private class AllySpawnQueue
{
    public Transform spawnPoint;
    public readonly List<AllyProductionOrder> orders = new List<AllyProductionOrder>();
}

private readonly List<AllySpawnQueue> spawnQueues = new List<AllySpawnQueue>();
```

### 2. `Start()` - 스폰 지점마다 큐 생성

**기존 코드**
```csharp
private void Start()
{
    rtsController = FindFirstObjectByType<RTSUnitController>();

    foreach (AllyController unit in defenseUnits)
    {
        ...
    }

    FillPool(CurrentWaveComposition());
    ...
}
```

**변경 코드**
```csharp
private void Start()
{
    rtsController = FindFirstObjectByType<RTSUnitController>();

    foreach (Transform sp in spawnPoints)
        if (sp != null)
            spawnQueues.Add(new AllySpawnQueue { spawnPoint = sp });

    foreach (AllyController unit in defenseUnits)
    {
        ...
    }

    FillPool(CurrentWaveComposition());
    ...
}
```

### 3. `Update()` 추가 - 대기열 진행 (EnemyAIDirector.Update()와 동일한 패턴)

**변경 코드**
```csharp
private void Update()
{
    foreach (AllySpawnQueue sq in spawnQueues)
    {
        if (sq.orders.Count == 0)
            continue;

        AllyProductionOrder front = sq.orders[0];
        front.remainTime -= Time.deltaTime;
        if (front.remainTime > 0f)
            continue;

        sq.orders.RemoveAt(0);

        AllyController unit = SpawnUnit(front.unitID, sq.spawnPoint);
        if (unit != null)
            garrison.Add(unit);
    }
}
```

### 4. `FillPool()` - 즉시 스폰 대신 대기열에 주문

**기존 코드**
```csharp
private void FillPool(List<AllyUnitGroup> composition)
{
    foreach (AllyUnitGroup group in composition)
    {
        int have = garrison.FindAll(u => u != null && u.GetAllyUnitID() == group.unitID).Count;

        while (have < group.count)
        {
            AllyController unit = SpawnUnit(group.unitID);
            if (unit == null)
                break;

            garrison.Add(unit);
            have++;
        }
    }
}
```

**변경 코드**
```csharp
private void FillPool(List<AllyUnitGroup> composition)
{
    garrison.RemoveAll(u => u == null);

    foreach (AllyUnitGroup group in composition)
    {
        int have = garrison.FindAll(u => u != null && u.GetAllyUnitID() == group.unitID).Count
            + PendingCount(group.unitID);

        for (int i = have; i < group.count; i++)
            EnqueueProduction(group.unitID);
    }
}

// 이미 생산 대기열에 들어가 있어 완성을 기다리는 중인 unitID 개수(EnemyAIDirector.PendingCount와 동일한
// 패턴) - 안 세면 ReinforceRoutine이 돌 때마다 같은 부족분을 중복 주문하게 된다.
private int PendingCount(int unitID)
{
    int count = 0;
    foreach (AllySpawnQueue sq in spawnQueues)
        foreach (AllyProductionOrder order in sq.orders)
            if (order.unitID == unitID)
                count++;
    return count;
}

private void EnqueueProduction(int unitID)
{
    if (rtsController == null)
        return;

    UnitData data = rtsController.GetEnemyUnitData(unitID);
    if (data == null || data.AllyPrefab == null)
        return;

    AllySpawnQueue sq = LeastLoadedQueue();
    if (sq == null)
        return; // 스폰 지점이 하나도 없음

    sq.orders.Add(new AllyProductionOrder
    {
        unitID = unitID,
        remainTime = data.productionTime,
        totalTime = data.productionTime,
    });
}

// 남은 생산 시간의 합이 가장 적은 스폰 지점을 고른다(EnemyAIDirector.LeastLoadedQueue와 동일한 패턴).
private AllySpawnQueue LeastLoadedQueue()
{
    AllySpawnQueue best = null;
    float bestLoad = float.MaxValue;

    foreach (AllySpawnQueue sq in spawnQueues)
    {
        float load = QueueLoad(sq);
        if (load < bestLoad)
        {
            bestLoad = load;
            best = sq;
        }
    }

    return best;
}

private float QueueLoad(AllySpawnQueue sq)
{
    float total = 0f;
    for (int i = 0; i < sq.orders.Count; i++)
        total += i == 0 ? sq.orders[i].remainTime : sq.orders[i].totalTime;
    return total;
}
```

### 5. `SpawnUnit()` - 스폰 지점을 파라미터로 받도록 (라운드로빈 대신 대기열이 이미 분산을 담당)

**기존 코드**
```csharp
private AllyController SpawnUnit(int unitID)
{
    UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(unitID) : null;
    Transform spawnPoint = NextSpawnPoint();
    if (data == null || data.AllyPrefab == null || spawnPoint == null)
        return null;

    GameObject spawned = Instantiate(data.AllyPrefab, spawnPoint.position, spawnPoint.rotation);
    if (!spawned.TryGetComponent<AllyController>(out AllyController unit))
        return null;

    unit.MoveTo(rallyPoint != null ? rallyPoint.position : DefaultRallyPosition());
    return unit;
}

// spawnPoints를 라운드로빈으로 순환하며 다음 스폰 지점을 고른다 ...
private Transform NextSpawnPoint()
{
    ...
}
```

**변경 코드**
```csharp
private AllyController SpawnUnit(int unitID, Transform spawnPoint)
{
    UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(unitID) : null;
    if (data == null || data.AllyPrefab == null || spawnPoint == null)
        return null;

    GameObject spawned = Instantiate(data.AllyPrefab, spawnPoint.position, spawnPoint.rotation);
    if (!spawned.TryGetComponent<AllyController>(out AllyController unit))
        return null;

    unit.MoveTo(rallyPoint != null ? rallyPoint.position : DefaultRallyPosition());
    return unit;
}
```
`NextSpawnPoint()`는 삭제 (더 이상 쓰이지 않음 - `LeastLoadedQueue()`가 분산을 대신 담당).

### 6. `TakeSquad()` - `garrison.RemoveAll` 시 대기열은 그대로 유지 (변경 없음, 참고용)

TakeSquad는 완성된 garrison에서만 뽑으므로 변경 불필요.

## 영향

- `RespawnDeadDefenseUnits()`(배치형 방어 유닛 재생산)는 `EnemyAIDirector`와 마찬가지로 이 대기열과
  무관하게 즉시 스폰 - 변경 없음(의도된 동작, doc/0552/0558 참고).
- 웨이브 발사 타이밍 자체(`WaitUntilReady`)는 이미 "구성이 갖춰질 때까지 대기"라 로직 변경 없음 - 다만
  이제 그 대기가 실제 생산 시간만큼 걸리게 됨(적 AI와 동일해짐).
- `NextSpawnPoint()` 삭제로 인스펙터 동작 변화 없음(내부 전용 메서드).

## 변경 파일

- `Assets/Scripts/System/AllyAIDirector.cs`
