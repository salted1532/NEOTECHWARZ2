# 0036. 건물 배치 클릭 → 일꾼이 이동 후 완공

**날짜:** 2026-07-09

## 요청 내용
> 유닛 건설 일꾼이 건물 건설을 구현할껀데 현재 코드를 보면 일꾼을 선택해야 건설모드 버튼이 활성화 되고 건설모드로 가서 건물을 짓는데 프리뷰로 건설할 위치를 보고 클릭시 건설되는데 이제 클릭 시 바로 건설이 아니라 일꾼이 해당 위치로 가서 건물을 짓도록 구현해줘

추가로 물어본 결과, 완공 방식은 **"도착 즉시 완공"**(건설-중 진행바/상태 없음, 도착하는 순간 건물이 완성 상태로 스폰)으로 확정.

## 조사 결과 (현재 코드 상태)
- `PlacementSystem.PlaceStructure()`가 클릭 즉시 그리드 점유 등록 + `Instantiate`까지 한 번에 처리 (`PlacementSystem.cs:69-108`). 일꾼과의 연결이 전혀 없음 — 클릭한 사람(마우스)이 어떤 유닛을 선택했는지도 참조하지 않음.
- `RTSUnitController.SelectUnit()`이 `IsBuildMode()`일 때 즉시 `return`하므로(`RTSUnitController.cs:161`), 건설모드에 진입한 뒤에는 새 유닛 선택이 막힌다. 즉 **건설모드 진입 시점에 선택돼 있던 일꾼이 `selectedUnitList`에 그대로 남아있음** — 이 일꾼을 건설 담당자로 그대로 쓸 수 있음.
- `TryConstructBuilding(int buildingID)`(`RTSUnitController.cs:589`)가 이미 정의돼 있지만 실제로는 **어디서도 호출되지 않는 죽은 코드** — 즉 지금도 건물 배치 시 자원이 실제로 소모되지 않고 있음(버튼 툴팁에 비용만 표시됨). 이번 변경은 이 기존 동작(자원 미소모)을 그대로 유지하고, 자원 소모 로직을 새로 추가하지는 않음(요청 범위 밖의 별개 이슈로 판단, 필요하면 별도 요청으로 처리).
- `GridData`/`PlacementSystem.placedGameObject` 리스트는 현재 철거 기능이 없어 실질적으로 추가만 되고 있음(`GetRepresentationIndex`/`RemoveObjectAt`도 미사용) — 리스트 인덱스를 지우면 다른 항목의 인덱스가 밀리므로, 취소 시에도 리스트에서 항목을 제거하지 않고 자리만 `null`로 비워두는 방식을 그대로 따름.
- `UnitController`에는 이미 "명령 도중 다른 명령이 들어오면 취소" 패턴이 확립돼 있음 (`CancelAttackOrder()`가 `orderedTarget`/`friendlyTarget`/[[0035-follow-ally-unit-order|followTarget]] 등을 정리) — 새 건설 이동 명령도 동일한 패턴을 따름.

## 설계안

### 핵심 흐름
1. 건설모드에서 유효한 위치를 클릭하면(기존 그리드 겹침/장애물 체크는 동일하게 통과해야 함):
   - 그리드 셀을 **클릭 즉시 예약**(`StructureData.AddObjectAt`) — 일꾼이 걸어가는 동안 다른 곳에 겹쳐 짓지 못하도록 기존과 동일하게 즉시 처리.
   - 실제 건물 오브젝트는 아직 생성하지 않음(`placedGameObject`에는 자리만 `null`로 예약).
   - 건설모드 진입 시 선택돼 있던 일꾼(`rtsController.GetSelectedWorker()`)에게 `GoBuild(목적지, 도착시콜백, 취소시콜백)` 명령.
2. 일꾼은 목적지로 이동(`UnitState.Move`, 기존 `MoveTo`와 동일한 지상/공중 이동 로직 재사용)하다가, 목적지 근접 반경(`buildInteractRange`) 안에 들어오면 즉시 "도착 콜백"을 실행 → `PlacementSystem`이 실제로 `Instantiate` 수행.
3. 일꾼이 도착하기 전에 다른 명령(이동/공격/채취/정지 등)을 받아 건설 이동이 취소되면 → "취소 콜백"이 실행되어 예약해둔 그리드 셀을 해제(`StructureData.RemoveObjectAt`). (자원을 소모하지 않으므로 환불 로직은 불필요.)
4. 일꾼이 죽는 경우는 별도 처리하지 않음 — `Die()`가 호출되면 오브젝트 자체가 파괴되므로 `GoBuild`의 코루틴/틱이 더 이상 실행되지 않고, 예약된 그리드 셀은 영구히 빈 채로 남는다(엣지 케이스, 요청 범위 밖이라 이번엔 다루지 않음. 필요하면 별도 요청으로 처리).

### 1. `UnitController.cs`

**필드 추가** (`followTarget`/`hasFollowOrder` 아래):
```csharp
// 기존 코드
    // ===== 아군 유닛 우클릭 = 계속 따라다니기 (공격 명령 아님, Idle 상태 유지) =====
    // Attack 상태가 아니라 Idle로 유지해야 AttackRange가 사거리 내 적을 자동으로 교전한다 (AttackMoveTo와 동일한 이유).
    private UnitController followTarget;
    private bool hasFollowOrder;
```
```csharp
// 변경 코드
    // ===== 아군 유닛 우클릭 = 계속 따라다니기 (공격 명령 아님, Idle 상태 유지) =====
    // Attack 상태가 아니라 Idle로 유지해야 AttackRange가 사거리 내 적을 자동으로 교전한다 (AttackMoveTo와 동일한 이유).
    private UnitController followTarget;
    private bool hasFollowOrder;

    // ===== 건설 이동 (건설모드에서 위치 클릭 시 일꾼이 그 자리로 이동 후 완공) =====
    [SerializeField] private float buildInteractRange = 2f; // 건설 위치 도착 판정 거리 (gatherInteractRange와 동일한 이유)
    private Vector3 buildDestination;
    private System.Action onBuildArrived;
    private System.Action onBuildCancelled;
    private bool hasBuildOrder;
```

**진입점 + 취소 헬퍼 추가** (`FollowTick()` 아래):
```csharp
    // 건설모드에서 건물 위치를 클릭했을 때 PlacementSystem이 호출한다.
    // destination에 도착하면 onArrived(실제 건물 스폰)를, 도착 전에 다른 명령으로 취소되면 onCancelled(그리드 예약 해제)를 실행한다.
    public void GoBuild(Vector3 destination, System.Action onArrived, System.Action onCancelled)
    {
        CancelGatheringForNewCommand();
        CancelAttackOrder(); // 이전 건설 이동이 있었다면 여기서 먼저 취소 콜백이 실행됨

        buildDestination = destination;
        onBuildArrived = onArrived;
        onBuildCancelled = onCancelled;
        hasBuildOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Move;
        MoveAgentTo(destination);
    }

    // 진행 중이던 건설 이동을 취소하고(다른 명령으로 대체됨) 취소 콜백을 실행한다.
    private void CancelBuildOrder()
    {
        if (!hasBuildOrder)
            return;

        hasBuildOrder = false;
        System.Action cancelled = onBuildCancelled;
        onBuildArrived = null;
        onBuildCancelled = null;

        cancelled?.Invoke();
    }

    // 건설 이동을 매 프레임 갱신한다: 목적지 근접 반경 안에 들어오면 도착 콜백을 실행하고 Idle로 전환한다.
    private void BuildTick()
    {
        if (!hasBuildOrder)
            return;

        if (Vector3.Distance(transform.position, buildDestination) > buildInteractRange)
            return;

        hasBuildOrder = false;

        if (!isAirUnit)
            navMeshAgent.ResetPath();

        arrived = true;
        UnitcurrentState = UnitState.Idle;

        System.Action arrivedCallback = onBuildArrived;
        onBuildArrived = null;
        onBuildCancelled = null;

        arrivedCallback?.Invoke();
    }
```

**`CancelAttackOrder()`에 건설 취소 연결**:
```csharp
// 기존 코드
    private void CancelAttackOrder()
    {
        orderedTarget = null;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        hasFriendlyOrder = false;
        attackMoveDestination = null;
        followTarget = null;
        hasFollowOrder = false;
    }
```
```csharp
// 변경 코드
    private void CancelAttackOrder()
    {
        orderedTarget = null;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        hasFriendlyOrder = false;
        attackMoveDestination = null;
        followTarget = null;
        hasFollowOrder = false;

        CancelBuildOrder();
    }
```
(`AttackUnitTarget()`/`AttackMoveTo()`/`AttackFriendlyTarget()`는 `CancelAttackOrder()`를 거치지 않고 직접 필드를 세팅하므로, [[0035-follow-ally-unit-order|팔로우 취소]]와 동일하게 이 세 곳에도 각각 `CancelBuildOrder();` 호출을 추가.)

**`Update()`에 `BuildTick()` 호출 추가**:
```csharp
// 기존 코드
        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
        FollowTick();
```
```csharp
// 변경 코드
        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
        FollowTick();
        BuildTick();
```

### 2. `RTSUnitController.cs` — 건설 담당 일꾼 조회

```csharp
    // 건설모드 진입 시점에 선택돼 있던 일꾼을 건설 담당자로 그대로 사용한다.
    // (SelectUnit()이 IsBuildMode() 중엔 새 선택을 막으므로, 건설모드에 있는 한 selectedUnitList는 그대로 유지된다)
    public UnitController GetSelectedWorker()
    {
        if (selectedUnitList.Count == 0)
            return null;

        UnitController unit = selectedUnitList[0];
        return unit != null && unit.CompareTag("Worker") ? unit : null;
    }
```

### 3. `PlacementSystem.cs` — 즉시 배치 → "예약 + 일꾼 이동 + 도착 시 완공"으로 변경

**필드 추가**:
```csharp
// 기존 코드
    private Vector3Int lastDectectedPosition = Vector3Int.zero;
```
```csharp
// 변경 코드
    private Vector3Int lastDectectedPosition = Vector3Int.zero;

    private RTSUnitController rtsController;
```

**`Start()`에서 참조 확보** (다른 컨트롤러들과 동일한 `FindFirstObjectByType` 패턴):
```csharp
// 기존 코드
    void Start()
    {
        StopPlacement();
        StructureData = new();
    }
```
```csharp
// 변경 코드
    void Start()
    {
        StopPlacement();
        StructureData = new();
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

**`PlaceStructure()` 재구성**:
```csharp
// 기존 코드
    private void PlaceStructure()
    {
        if (selectedObjectIndex < 0) return;
        if (inputManager.IsPointerOverUI()) return;

        Vector3 mousePos = inputManager.GetSelectedMapPosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        var data = database.buildingData[selectedObjectIndex];

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
            return;

        // ⭐ 유닛 체크 추가
        if (IsBlocked(mousePos, data.Size))
            return;

        Vector3 spawnPos = GetPlacementWorldPosition(gridPos, data.Size);

        GameObject obj = Instantiate(data.Prefab);

        // NavMeshObstacle 다시 활성화
        var obstacle = obj.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
        obj.transform.position = spawnPos;

        placedGameObject.Add(obj);

        StructureData.AddObjectAt(
            gridPos,
            data.Size,
            data.ID,
            placedGameObject.Count - 1
        );

        preview.UpdatePosition(spawnPos, false);
    }
```
```csharp
// 변경 코드
    private void PlaceStructure()
    {
        if (selectedObjectIndex < 0) return;
        if (inputManager.IsPointerOverUI()) return;

        Vector3 mousePos = inputManager.GetSelectedMapPosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        var data = database.buildingData[selectedObjectIndex];

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
            return;

        // ⭐ 유닛 체크 추가
        if (IsBlocked(mousePos, data.Size))
            return;

        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        Vector3 spawnPos = GetPlacementWorldPosition(gridPos, data.Size);

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        worker.GoBuild(
            spawnPos,
            onArrived: () => CompleteConstruction(data, spawnPos, placedIndex),
            onCancelled: () => CancelReservedConstruction(gridPos));

        preview.UpdatePosition(spawnPos, false);
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 실제 건물을 생성한다.
    private void CompleteConstruction(BuildingData data, Vector3 spawnPos, int placedIndex)
    {
        GameObject obj = Instantiate(data.Prefab);

        var obstacle = obj.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
        obj.transform.position = spawnPos;

        placedGameObject[placedIndex] = obj;
    }

    // 일꾼이 도착하기 전에 다른 명령으로 건설 이동이 취소됐을 때(GoBuild 콜백) 예약해둔 그리드 셀을 비워준다.
    private void CancelReservedConstruction(Vector3Int gridPos)
    {
        StructureData.RemoveObjectAt(gridPos);
    }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **완공 방식**: 도착 즉시 완공 (진행바/건설-중 상태 없음) — 앞서 확인받은 대로.
- **건설 담당 일꾼**: 건설모드 진입 시점에 선택돼 있던 일꾼(`selectedUnitList[0]`) 한 명만 이동. 여러 일꾼을 선택한 채로 건설모드에 들어가는 것은 현재 UI 흐름상 어차피 `UnitState.Worker` 패널(건설 버튼)이 단일 일꾼 선택 기준으로 뜨는 경우가 대부분이라 별도 분배 로직은 만들지 않음.
- **자원 소모**: 기존과 동일하게 소모하지 않음 (원래도 안 되고 있던 부분이라 이번 변경으로 새로 건드리지 않음).
- **그리드 예약 취소 시**: 다른 명령으로 일꾼의 건설 이동이 취소되면 그리드 예약도 함께 해제(그 자리에 다시 지을 수 있게 됨).
- **일꾼이 이동 중 사망**: 별도 처리 없음(엣지 케이스, 이번 범위 밖).
- **건설 중 시각적 표시(고스트/스캐폴드)**: 없음 — 일꾼이 도착하기 전까지는 그 자리에 아무것도 보이지 않다가, 도착하는 순간 완성된 건물이 나타남.

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
