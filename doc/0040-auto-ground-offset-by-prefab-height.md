# 0040. 프리팹 높이 기반 자동 지면 정렬 (BaseStructure 공중 부양 수정)

**날짜:** 2026-07-09

## 요청 내용
> 지금 BaseStructure의 크기에서 y는 인데 다른 건물들은 2란 말이야 지금 BaseStructure가 지어지면 공중에 뜨는데 이걸 좀 자동화 했으면 좋겠네 현재 건설될 위치랑 현재 프리팹의 y크기를 알아내서 그거에 맞게 땅에 붙어서 설치되도록 했으면 좋겠어

## 원인 분석
- `PlacementSystem.cs`의 `GetPlacementWorldPosition()`이 지금까지 **모든 건물에 동일한 고정값** `yOffset = 1f`를 더해서 배치 위치를 계산해왔음 (`// Y 높이 오프셋 (건물 떠있게)`).
- 이 고정값 1은 사실 "메쉬가 피벗 중심으로부터 위/아래로 절반씩 뻗어있는 Unity 기본 Cube 메쉬 기준, localScale.y가 2인 건물의 절반 높이(=1)"에 맞춰 튜닝된 값 — 기존 건물들(`MainBase` 등)이 전부 `localScale.y = 2`라서 우연히 다 맞았던 것.
- `BaseStructure.prefab`은 루트 오브젝트의 `localScale.y = 1`인데(다른 건물의 절반 높이), 여전히 고정값 1을 그대로 더해서 배치하다 보니 **필요한 절반 높이(0.5)보다 0.5만큼 더 떠서** 보이는 것.
- 게다가 완공 시(`BaseStructure.CompleteConstruction()`)에는 `transform.position`(=BaseStructure 자신이 떠 있던 그 위치)을 그대로 실제 건물의 스폰 위치로 재사용하고 있어서, 이번에 BaseStructure의 높이만 따로 고치면 이번엔 반대로 **완성된 건물이 땅에 반쯤 파묻히는** 문제가 새로 생길 수 있음 — 그래서 "지면 좌표"와 "그 위에 얹을 오프셋"을 프리팹별로 분리해서 계산하도록 구조를 바꿔야 함.

## 설계안: 프리팹의 메쉬 바운드 + 스케일로 오프셋을 자동 계산

Unity 기본 Cube 메쉬는 로컬 바운드가 중심 (0,0,0), 반지름(extents) 0.5로 고정되어 있음. 임의의 프리팹에 대해 일반적으로 계산하면:

```
필요한 오프셋 = (메쉬 로컬 바운드의 extents.y - 로컬 바운드의 center.y) × 프리팹의 localScale.y
```

- `MainBase`(scale.y=2, Cube 메쉬 center=0,extents=0.5) → (0.5 - 0) × 2 = **1** → 기존 고정값과 정확히 일치(회귀 없음).
- `BaseStructure`(scale.y=1, 같은 Cube 메쉬) → (0.5 - 0) × 1 = **0.5** → 정확히 필요한 절반 높이.

메쉬가 없는 프리팹(콜라이더만 있는 경우 등)은 안전하게 기존 고정값(1)으로 대체.

### 1. `PlacementSystem.cs`

**"지면 좌표(XZ 중앙정렬 + 그리드 Y)"와 "오프셋"을 분리**:
```csharp
// 기존 코드
    /// <summary>
    /// Grid → World 변환 + 중앙정렬 + Y 오프셋 통합 처리
    /// (프리뷰 / 실제 건물 동일 기준)
    /// </summary>
    private Vector3 GetPlacementWorldPosition(Vector3Int gridPos, Vector2Int size)
    {
        Vector3 basePos = grid.CellToWorld(gridPos);
        Vector3 cellSize = grid.cellSize;

        // XZ 중앙 정렬
        Vector3 centerOffset = new Vector3(
            (size.x - 1) * cellSize.x * 0.5f,
            0,
            (size.y - 1) * cellSize.y * 0.5f
        );

        // Y 높이 오프셋 (건물 떠있게)
        Vector3 heightOffset = Vector3.up * yOffset;

        return basePos + centerOffset + heightOffset;
    }
```
```csharp
// 변경 코드
    /// <summary>
    /// Grid → World 변환 + XZ 중앙정렬 (Y는 그리드 기준 지면 그대로, 오프셋 없음)
    /// </summary>
    private Vector3 GetGroundPosition(Vector3Int gridPos, Vector2Int size)
    {
        Vector3 basePos = grid.CellToWorld(gridPos);
        Vector3 cellSize = grid.cellSize;

        Vector3 centerOffset = new Vector3(
            (size.x - 1) * cellSize.x * 0.5f,
            0,
            (size.y - 1) * cellSize.y * 0.5f
        );

        return basePos + centerOffset;
    }

    // 기존 IsBlocked()용 - 프리팹에 상관없이 대략적인 충돌 검사 박스 중심 높이만 필요하므로 고정 오프셋을 그대로 사용.
    private Vector3 GetPlacementWorldPosition(Vector3Int gridPos, Vector2Int size)
    {
        return GetGroundPosition(gridPos, size) + Vector3.up * yOffset;
    }

    // 프리팹의 메쉬 바운드(로컬)와 스케일을 바탕으로, 피벗이 정확히 지면(바닥)에 닿도록 필요한 y 오프셋을 계산한다.
    // 메쉬가 없으면(콜라이더만 있는 경우 등) 안전한 기존 고정값(yOffset)으로 대체한다.
    // BaseStructure.CompleteConstruction()에서도 재사용하기 위해 static으로 공개.
    public static float GetGroundOffsetY(GameObject prefab)
    {
        if (prefab == null)
            return 1f;

        if (!prefab.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return 1f;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        return (bounds.extents.y - bounds.center.y) * prefab.transform.localScale.y;
    }
```

**`PlaceStructure()`가 프리팹별 오프셋을 사용하도록 수정**:
```csharp
// 기존 코드
        Vector3 spawnPos = GetPlacementWorldPosition(gridPos, data.Size);

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);

        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, spawnPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));
```
```csharp
// 변경 코드
        Vector3 groundPos = GetGroundPosition(gridPos, data.Size);
        Vector3 spawnPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab); // 완공될 건물 기준 높이 (고스트/일꾼 목적지용)

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);

        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));
```

**`StartConstruction()`이 BaseStructure 자신의 높이로 따로 오프셋을 계산하고, 완공 시 쓸 "지면 좌표"를 BaseStructure에 넘겨주도록 수정**:
```csharp
// 기존 코드
    private void StartConstruction(BuildingData data, Vector3 spawnPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        GameObject obj = Instantiate(baseStructurePrefab, spawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        structure.Initialize(data.ID, data.productionTime);

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }
```
```csharp
// 변경 코드
    private void StartConstruction(BuildingData data, Vector3 groundPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        Vector3 structureSpawnPos = groundPos + Vector3.up * GetGroundOffsetY(baseStructurePrefab); // BaseStructure 자신의 높이 기준

        GameObject obj = Instantiate(baseStructurePrefab, structureSpawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        structure.Initialize(data.ID, data.productionTime, groundPos); // 완공 시 실제 건물을 다시 지면 기준으로 배치하기 위해 groundPos도 함께 전달

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }
```

**마우스 프리뷰(hover)도 동일하게 프리팹 기준 오프셋 사용**:
```csharp
// 기존 코드
            Vector3 previewPos = GetPlacementWorldPosition(gridPos, data.Size);
```
```csharp
// 변경 코드
            Vector3 previewPos = GetGroundPosition(gridPos, data.Size) + Vector3.up * GetGroundOffsetY(data.Prefab);
```

(`IsBlocked()`의 물리 충돌 검사 박스 중심은 프리팹 상관없이 대략적인 높이만 필요하므로 그대로 기존 `GetPlacementWorldPosition(gridPos, size)`(고정 오프셋)를 계속 사용 - 변경 없음.)

### 2. `BaseStructure.cs` — 완공 시 "지면 좌표 + 실제 건물 자신의 높이"로 다시 배치

```csharp
// 기존 코드
    private int buildingID;
    private float remainingBuildTime;
    ...
    public void Initialize(int buildingID, float buildTime)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        ...
    }
```
```csharp
// 변경 코드
    private int buildingID;
    private float remainingBuildTime;
    private Vector3 groundPosition; // 완공 시 실제 건물을 다시 배치할 지면 좌표(오프셋 없는 순수 지면 위치)
    ...
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        ...
    }
```

```csharp
// 기존 코드 (CompleteConstruction)
        if (data != null && data.Prefab != null)
        {
            GameObject obj = Instantiate(data.Prefab, transform.position, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;
        }
```
```csharp
// 변경 코드
        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;
        }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **자동 계산 기준**: 프리팹의 `MeshFilter.sharedMesh.bounds`(로컬 바운드) × `localScale.y`로 "피벗에서 바닥까지 거리"를 구함 - 지금 쓰는 모든 건물/BaseStructure가 Unity 기본 Cube 메쉬라서 정확히 맞아떨어짐. 메쉬가 없는 경우엔 기존 고정값(1)으로 안전하게 대체.
- **기존 건물들 회귀 없음**: `localScale.y = 2`인 기존 건물들은 계산 결과가 정확히 기존 고정값(1)과 같아서 위치가 달라지지 않음 - 오직 `BaseStructure`(scale.y=1)만 실제로 바뀜(0.5로 낮아져서 땅에 붙음).
- **완공된 건물의 최종 위치**: `BaseStructure.transform.position`을 그대로 재사용하지 않고, 클릭 시점의 "순수 지면 좌표"를 따로 기억해뒀다가 완공 시 그 좌표 + 완성된 건물 자신의 오프셋으로 다시 계산 - BaseStructure와 완성 건물의 높이가 서로 달라도 항상 정확히 지면에 붙음.
- **`IsBlocked()`의 충돌 검사**는 그대로 둠(대략적인 박스 중심 높이만 필요해서 프리팹별로 정밀할 필요 없음).

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BaseStructure.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
