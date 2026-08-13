# 0564 - 아군 OC 유닛 생산 시 집결지로 이동

## 날짜
2026-08-13

## 요청 내용
"아군OC이 유닛 생산시 집결지로 모이도록"

→ 아군 OC 유닛이 생산되면(스폰되는 즉시) `rallyPoint`로 이동해야 하는데, 현재는 스폰 지점에 그대로
멈춰 서 있다가 공격 웨이브가 발사될 때(`LaunchWave` → `AssembleAtRally`)만 이동한다.

## 원인 확인
`EnemyAIDirector.Update()`(생산 대기열 완료 시점)는 스폰 직후 바로 집결지로 보낸다:
```csharp
// EnemyAIDirector.cs:340-346
GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
{
    front.destinationPool.Add(unit);
    unit.MoveTo(DefaultRallyPosition()); // 생산되자마자 집결지로 - 웨이브/별동대 공통(doc/0545)
}
```

반면 `AllyAIDirector.SpawnUnit()`(`Assets/Scripts/System/AllyAIDirector.cs:398`)은 스폰만 하고
`MoveTo`를 호출하지 않는다:
```csharp
private AllyController SpawnUnit(int unitID)
{
    UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(unitID) : null;
    Transform spawnPoint = NextSpawnPoint();
    if (data == null || data.AllyPrefab == null || spawnPoint == null)
        return null;

    GameObject spawned = Instantiate(data.AllyPrefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<AllyController>(out AllyController unit) ? unit : null;
}
```
그래서 생산된 아군 유닛은 웨이브로 뽑혀 나가기 전까지 스폰 지점에 그대로 서 있다.

## 설계안
`EnemyAIDirector`와 동일하게, 스폰 직후 `rallyPoint`(없으면 `DefaultRallyPosition()`)로 `MoveTo`를
호출한다. `AllyAIDirector`는 이미 `DefaultRallyPosition()`(spawnPoints 중 첫 유효한 위치, 없으면 자기
위치)과 `AssembleAtRally()`에서 쓰는 것과 같은 우선순위(`rallyPoint != null ? rallyPoint.position :
DefaultRallyPosition()`)를 갖고 있으므로 그대로 재사용한다.

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

    unit.MoveTo(rallyPoint != null ? rallyPoint.position : DefaultRallyPosition()); // 생산되자마자 집결지로 (EnemyAIDirector와 동일한 패턴, doc/0545)
    return unit;
}
```

방어 유닛 재생산(`RespawnDeadDefenseUnits`)은 건드리지 않는다 - 그건 "자기 자리를 지키는" 역할이라
이동하면 안 됨(doc/0552).

## 영향받는 파일
- `Assets/Scripts/System/AllyAIDirector.cs` - `SpawnUnit()` 본문 수정(위 설계안 그대로 적용).
- `Docs/AllyAIDirector.md` - "보충 생산" 절에 스폰 직후 집결지 이동 설명 추가.

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(기존 베이스라인과 동일 - 새 경고 없음).
