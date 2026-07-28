# 0267 - 버그: 유닛 생성 소리 안 남 (Start() vs Awake() 타이밍 경합)

**날짜:** 2026-07-28

## 요청 내용

> 유닛들이 건물에서 스폰했을때 스폰소리 안나는데 이것좀 확인해줘

## 원인

`Assets/Scripts/UnitSpawner/UnitSpawner.cs`의 `Spawn(int unitID)`가 유닛을 생성하는 코드:

```csharp
GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

spawnunit.GetComponent<UnitAudio>()?.PlaySpawnSound();
```

`Instantiate()`가 끝나자마자 **같은 프레임, 같은 호출 스택**에서 곧바로 `PlaySpawnSound()`를 호출한다.
그런데 `UnitAudio.cs`는 doc/0255 구현 당시 `bank`(사운드뱅크) 조회를 `Start()`에서 했다:

```csharp
private void Start()
{
    rtsController = FindFirstObjectByType<RTSUnitController>();
    ...
    bank = data?.soundBank;
}
```

유니티에서 `Awake()`는 `Instantiate()` 호출 안에서 즉시(동기) 실행되지만, `Start()`는 그 프레임의
`Instantiate()` 직후가 아니라 **다음 Update 전**에야 실행된다. 즉 `PlaySpawnSound()`가 호출되는
시점엔 아직 `Start()`가 돌지 않아서 `bank`가 `null`인 상태였고, `PlaySpawnSound()`는

```csharp
public void PlaySpawnSound()
{
    if (bank == null)
        return; // 항상 여기서 조용히 빠져나감
    ...
}
```

로 조용히(에러 없이) 아무것도 안 하고 끝났다 - 그래서 생성 SFX/음성이 전혀 재생되지 않았던 것.

같은 원인으로 **건물 건설 시작 소리(`constructLoopSFX`)도 똑같이 조용히 씹히고 있었다.**
`PlacementSystem.StartConstruction()`도 `Instantiate(baseStructurePrefab, ...)` 직후 같은 프레임에
`BaseStructure.Initialize()` → `BuildingAudio.PlayConstructLoop()`를 호출하는데, `BuildingAudio.cs`도
`rtsController`를 `Start()`에서 구하고 있어서 동일한 경합이 있었다. (건설 완료/파괴/선택 음성은
그 오브젝트가 이미 몇 프레임 이상 살아있는 상태에서 트리거되므로 이 버그의 영향을 받지 않는다.)

## 코드 변경

### `Assets/Scripts/Audio/UnitAudio.cs`

Before:
```csharp
    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        if (rtsController == null)
            return;

        UnitData data = unitController != null
            ? rtsController.GetUnitData(unitController.GetUnitID())
            : (enemyUnitController != null ? rtsController.GetEnemyUnitData(enemyUnitController.GetEnemyUnitID()) : null);

        bank = data?.soundBank;
    }
```

After:
```csharp
    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
        healthManager = GetComponent<HealthManager>();

        rtsController = FindFirstObjectByType<RTSUnitController>();
        if (rtsController == null)
            return;

        UnitData data = unitController != null
            ? rtsController.GetUnitData(unitController.GetUnitID())
            : (enemyUnitController != null ? rtsController.GetEnemyUnitData(enemyUnitController.GetEnemyUnitID()) : null);

        bank = data?.soundBank;
    }
```
(`Start()` 메서드 자체를 삭제 - 더 이상 필요 없음)

### `Assets/Scripts/Audio/BuildingAudio.cs`

Before:
```csharp
    private void Awake()
    {
        buildingController = GetComponent<BuildingController>();
        baseStructure = GetComponent<BaseStructure>();
        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

After:
```csharp
    private void Awake()
    {
        buildingController = GetComponent<BuildingController>();
        baseStructure = GetComponent<BaseStructure>();
        healthManager = GetComponent<HealthManager>();

        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```
(`Start()` 삭제)

`RTSUnitController.unitDatabase`/`buildingDatabase` 등은 인스펙터에 미리 연결된 에셋 참조라 씬 로드
시점에 이미 값이 채워져 있고, `RTSUnitController` 자신의 `Awake()`가 먼저 실행됐는지 여부와 무관하게
`GetUnitData`/`GetBuildingData` 조회가 가능하므로, `Awake()`로 옮겨도 안전하다.

## 요약/영향받는 파일

- `Assets/Scripts/Audio/UnitAudio.cs`: `bank` 조회를 `Start()` → `Awake()`로 이동.
- `Assets/Scripts/Audio/BuildingAudio.cs`: `rtsController` 조회를 `Start()` → `Awake()`로 이동.
- 이제 유닛 생성 시 스폰 SFX/음성이 정상 재생되고, 건물 건설 시작 시 `constructLoopSFX`도 정상
  재생된다.
