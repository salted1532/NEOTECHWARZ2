# 0054. 건물 이동(리프트 이륙 / 착륙) 시스템

**날짜:** 2026-07-12

## 요청 내용
> 건물 이동 - 건물에 리프트 버튼 추가, 버튼 누를 시 건물이 공중에 뜨고(공중유닛과 같이 공중에 뜸) 그리드에서 자신의 위치를 삭제한다. 공중에 떠있는 건물에는 리프트 버튼이 착륙버튼으로 바뀌고, 그 버튼을 누르면 프리뷰가 보이고 다시 내릴 곳을 정할 수 있다. 내릴 곳을 클릭하면 그리드에 추가되고 프리뷰가 그 자리에 남고, 그곳으로 이동해서 착륙하여 내려앉고 다시 지상에 있는 상태로 변경된다. 스프라이트/씬 연결은 직접 할 테니 코드적인 부분과 필드만 추가해달라.

## 조사 결과 (현재 코드 상태)
- 완공된 건물은 `BuildingController`(`Assets/Scripts/Building/BuildingController.cs`)가 붙어있고, 건설 중인 상태(`BaseStructure`)가 건설시간을 다 채우면 `BaseStructure.CompleteConstruction()`이 실제 건물 프리팹을 `Instantiate`한다.
- 그리드 점유 정보는 `PlacementSystem`이 들고 있는 `GridData StructureData`(private)가 전담한다. `PlaceStructure()`가 클릭 즉시 `StructureData.AddObjectAt(gridPos, size, ID, placedIndex)`로 셀을 예약하고, `GridData.RemoveObjectAt(gridPosition)`로 셀을 해제할 수 있다. 다만 현재는 어떤 건물도(파괴돼도) 이 셀을 해제하는 코드가 없다 — 이번 리프트 기능이 "그리드에서 자신의 위치 삭제"를 요구하므로, 이번 기회에 `PlacementSystem`에 공개 해제/점유 API를 추가한다.
- 공중 이동은 이미 `UnitController`(공중 유닛)에 구현된 패턴이 있다: `isAirUnit`일 때 `Update()`에서 `Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime)`로 목표 좌표까지 직접 좌표 보간 이동하고, 목표는 항상 "지면 좌표 + Vector3.up * 5f"로 계산한다. 건물 리프트도 같은 방식(수직 상승 → 수평 이동 → 수직 하강)을 그대로 재사용할 수 있다.
- 건설 중 표시되는 "고정 고스트"(`PreviewSystem.SpawnConstructionGhost`)와, 클릭한 자리에 그리드를 즉시 예약해두고 유닛이 도착하면 없애는 패턴(`PlacementSystem.PlaceStructure` → `StartConstruction`)이 이미 있다. "클릭 시 그리드에 추가되고 프리뷰가 그 자리에 남고, 그곳으로 이동 후 착륙"이라는 요구사항은 이 기존 패턴(건설 고스트)과 정확히 동일한 모양이므로 그대로 재사용한다.
- `BuildingController`는 자신이 그리드의 어느 셀을 차지하는지에 대한 정보를 전혀 들고 있지 않다(그 정보는 `PlacementSystem`의 지역 변수 `gridPos`에만 잠깐 존재했다가 사라짐). 리프트 시 자신의 셀을 해제하려면 건물이 자기 그리드 좌표를 알아야 하므로, 완공 시점(`BaseStructure.CompleteConstruction`)에 그 값을 건물에 넘겨주는 경로를 새로 추가해야 한다.
- 씬에 처음부터 배치돼 있는 시작 건물(커맨드센터 등)은 `PlacementSystem`을 거치지 않고 에디터에서 직접 배치된 것이라 애초에 `StructureData`에 등록돼 있지 않다 — 이런 건물은 그리드 좌표를 모르는 채로 시작하므로, "모르면 해제도 하지 않는다"로 자연스럽게 처리하고 최초 착륙 시점부터 정상적으로 그리드에 등록되게 한다.
- 건물 선택 시 커맨드 패널은 건물 종류별로 `RTSUnitController.UpdateUI()`가 `uIController.ShowMainBasePanel(...)` / `ShowBarracksPanel(...)` / `ShowFactoryPanel(...)` / `ShowAirportPanel(...)`을 호출해 채운다(각각 1~2개 슬롯만 사용). `SupplyDepot`/`Lab`은 아직 전용 패널이 없어 `ClearPanel()`만 호출된다. 리프트/착륙 버튼은 이 호출들 "다음에" 마지막 슬롯 하나만 따로 채워 넣는 방식으로 붙이면, 기존 패널 코드를 건드리지 않고도 모든 건물 종류에 공통으로 적용할 수 있다.
- 버튼 단축키는 `ProductionSlot`이 자기 슬롯에 할당된 `KeyCode`를 스스로 감지해서 클릭을 재현하므로(이미 활성화된 슬롯에서만 동작), 별도의 입력 처리 코드 없이 `ButtonAction`에 단축키만 넘기면 된다. 리프트/착륙엔 현재 건물 패널들에서 쓰이지 않는 `G`를 사용한다.

## 설계안

### 1. `Assets/Scripts/BuildSystem/GridData.cs` — 변경 없음
그대로 재사용(`RemoveObjectAt` / `AddObjectAt` / `CanPlaceObejctAt`). 새 호출은 항상 "실제로 등록됐던 셀"에 대해서만 이뤄지므로 안전하다(아래 `hasGridPosition` 가드 참고).

### 2. `Assets/Scripts/BuildSystem/PlacementSystem.cs`

**필드 추가**:
```csharp
// 기존 코드
    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹
```
```csharp
// 변경 코드
    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹

    // ===== 건물 리프트 이동(착륙 위치 선택) =====
    private BuildingController relocatingBuilding; // 현재 착륙 위치를 고르는 중인 건물(없으면 null)
```

**`StartConstruction()`에서 `Initialize` 호출에 그리드 좌표 전달**:
```csharp
// 기존 코드
        BaseStructure structure = obj.GetComponent<BaseStructure>();
        // 플레이어가 직접 건설을 취소할 때(CancelConstruction) 그리드 예약을 풀어줄 콜백도 함께 넘긴다.
        structure.Initialize(data.ID, data.productionTime, groundPos, () => CancelReservedConstruction(gridPos, null));
```
```csharp
// 변경 코드
        BaseStructure structure = obj.GetComponent<BaseStructure>();
        // 플레이어가 직접 건설을 취소할 때(CancelConstruction) 그리드 예약을 풀어줄 콜백도 함께 넘긴다.
        structure.Initialize(data.ID, data.productionTime, groundPos, gridPos, () => CancelReservedConstruction(gridPos, null));
```

**새 메서드 추가** (`CancelReservedConstruction` 아래):
```csharp
    // ===== 건물 리프트 이동 =====

    // 리프트 이륙한 건물이 자기 자리를 비울 때 호출 (BuildingController.LiftOff). 자원/일꾼과 무관.
    public void ReleaseBuildingGrid(Vector3Int gridPosition)
    {
        StructureData.RemoveObjectAt(gridPosition);
    }

    // "착륙" 버튼(BuildingController.BeginLanding)에서 호출: 착륙 위치를 고르는 프리뷰 모드로 진입한다.
    // 일반 건설모드(StartPlacement)와 달리 자원 소모/일꾼이 필요 없다.
    public void StartBuildingRelocation(BuildingController building)
    {
        StopPlacement();

        selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == building.GetBuildingID());
        if (selectedObjectIndex < 0)
            return;

        relocatingBuilding = building;

        gridVisualization.SetActive(true);

        preview.StartShowingPlacementPreview(
            database.buildingData[selectedObjectIndex].Prefab,
            database.buildingData[selectedObjectIndex].Size);

        inputManager.OnClicked += PlaceRelocatedBuilding;
        inputManager.OnExit += StopPlacement; // ESC = 착륙 위치 선택만 취소(건물은 계속 공중에 남음)
    }

    // OnClicked 핸들러: 클릭한 자리가 유효하면 그리드를 즉시 예약하고, 클릭 자리에 고정 고스트를 남긴 채
    // 건물을 그 위치로 비행시킨다(도착 후 실제로 착륙 처리는 BuildingController가 담당).
    private void PlaceRelocatedBuilding()
    {
        if (relocatingBuilding == null) { StopPlacement(); return; }
        if (inputManager.IsPointerOverUI()) return;

        Vector3 mousePos = inputManager.GetSelectedMapPosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        var data = database.buildingData[selectedObjectIndex];

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size)) return;
        if (IsBlocked(mousePos, data.Size)) return;
        if (IsTooCloseToResource(data.ID, gridPos, data.Size)) return;

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size);
        Vector3 landingPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab); // 착륙 완료 시 최종 정착 위치

        // 다른 곳에 겹쳐 짓지 못하도록 클릭 즉시 그리드를 예약 (건설 시스템과 동일한 패턴)
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;
        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        // 클릭한 자리에 건물이 도착할 때까지 남아있을 고정 고스트
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, landingPos);

        BuildingController building = relocatingBuilding;
        building.BeginRelocationFlight(
            gridPos,
            landingPos,
            onLanded: () => { if (ghost != null) Destroy(ghost); },
            onCancelled: () =>
            {
                StructureData.RemoveObjectAt(gridPos);
                if (ghost != null) Destroy(ghost);
            });

        StopPlacement();
    }
```

**`StopPlacement()`에 새 구독 해제 + `relocatingBuilding` 초기화 추가**:
```csharp
// 기존 코드
    public void StopPlacement()
    {
        selectedObjectIndex = -1;

        gridVisualization.SetActive(false);
        preview.StopShowingPreview();

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        lastDectectedPosition = Vector3Int.zero;
    }
```
```csharp
// 변경 코드
    public void StopPlacement()
    {
        selectedObjectIndex = -1;
        relocatingBuilding = null;

        gridVisualization.SetActive(false);
        preview.StopShowingPreview();

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnClicked -= PlaceRelocatedBuilding;
        inputManager.OnExit -= StopPlacement;

        lastDectectedPosition = Vector3Int.zero;
    }
```
(마우스 이동 시 프리뷰 위치/색을 갱신하는 기존 `Update()`는 `selectedObjectIndex` 기준으로 이미 동작하므로 착륙 위치 선택 모드에서도 그대로 재사용되어 수정이 필요 없다.)

### 3. `Assets/Scripts/Building/BaseStructure.cs`

**필드 추가 + `Initialize` 시그니처 변경**:
```csharp
// 기존 코드
    private int buildingID;
    private float remainingBuildTime;
    private Vector3 groundPosition; // 완공 시 실제 건물을 다시 배치할 지면 좌표(오프셋 없는 순수 지면 위치)
```
```csharp
// 변경 코드
    private int buildingID;
    private float remainingBuildTime;
    private Vector3 groundPosition; // 완공 시 실제 건물을 다시 배치할 지면 좌표(오프셋 없는 순수 지면 위치)
    private Vector3Int gridPosition; // 완공될 건물에 그대로 넘겨줄 그리드 좌표 (리프트 이동 시 자기 자리 해제용)
```
```csharp
// 기존 코드
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition, System.Action onCancelledByPlayer)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        this.onCancelledByPlayer = onCancelledByPlayer;
```
```csharp
// 변경 코드
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition, Vector3Int gridPosition, System.Action onCancelledByPlayer)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        this.gridPosition = gridPosition;
        this.onCancelledByPlayer = onCancelledByPlayer;
```

**`CompleteConstruction()`에서 완공된 건물에 그리드 좌표 전달**:
```csharp
// 기존 코드
            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            rtsController?.AddMaxPopulation(data.maxpopulationamount); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
```
```csharp
// 변경 코드
            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            if (obj.TryGetComponent<BuildingController>(out var builtController))
                builtController.SetGridInfo(gridPosition); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록 전달

            rtsController?.AddMaxPopulation(data.maxpopulationamount); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
```

### 4. `Assets/Scripts/Building/BuildingController.cs`

**using 추가**:
```csharp
using UnityEngine.AI;
```

**필드 추가**:
```csharp
// 추가 (기존 필드들 아래)
    [Header("건물 이동(리프트)")]
    [SerializeField] private bool canLift = true; // 이 건물이 리프트 이륙 가능한지 (건물별로 인스펙터에서 끌 수 있음)
    [SerializeField] private float liftHeight = 8f;   // 이륙 시 상승할 높이
    [SerializeField] private float liftMoveSpeed = 5f; // 상승/수평이동/하강 공통 속도

    private NavMeshObstacle navMeshObstacle;
    private PlacementSystem placementSystem;

    private bool isLifted;             // 현재 공중에 떠 있는지(상승 중/대기 중/이동 중/하강 중 모두 포함)
    private bool isAscending;          // 이륙 중(상승 중)
    private bool isFlyingToDestination;// 착륙 위치로 수평 이동 중
    private bool isDescending;         // 착륙 위치 위에서 하강 중

    private Vector3 verticalTarget;    // 상승 목표(현재 위치 기준 + liftHeight)
    private Vector3 flightDestination; // 착륙 목표(최종 정착 월드 좌표, 수평이동+하강 공통 목표)

    private Vector3Int gridPosition;   // 현재 자신이 점유 중인 그리드 셀 좌표
    private bool hasGridPosition;      // gridPosition이 유효한지 (에디터에 미리 배치된 시작 건물은 처음엔 false)

    private Vector3Int pendingGridPosition;   // 착륙 예정 위치의 그리드 좌표 (비행 중에만 유효)
    private System.Action onRelocationLanded;    // 착륙 완료 시(BuildingController.Land) 호출 - PlacementSystem이 고스트 제거
    private System.Action onRelocationCancelled;  // 착륙 전에 파괴되는 등 비행이 중단될 때 호출 - 그리드 예약 해제 + 고스트 제거
```

**`Start()`에 캐싱 추가**:
```csharp
// 기존 코드
        rtsController = FindFirstObjectByType<RTSUnitController>();

        rtsController.BuildingList.Add(this);

        UnitSpawner = GetComponentInChildren<UnitSpawner>();
```
```csharp
// 변경 코드
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();

        rtsController.BuildingList.Add(this);

        navMeshObstacle = GetComponent<NavMeshObstacle>();

        UnitSpawner = GetComponentInChildren<UnitSpawner>();
```

**`Update()`에 비행 처리 추가**:
```csharp
// 기존 코드
    void Update()
    {

    }
```
```csharp
// 변경 코드
    void Update()
    {
        if (isLifted)
            UpdateLiftedMovement();
    }

    // 이륙(수직 상승) → [대기: 착륙 위치 선택 전] → 수평 이동 → 착륙(수직 하강) 순서로 진행한다.
    // 공중유닛(UnitController)과 동일하게 MoveTowards 기반 직접 좌표 보간을 사용한다.
    private void UpdateLiftedMovement()
    {
        if (isAscending)
        {
            transform.position = Vector3.MoveTowards(transform.position, verticalTarget, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, verticalTarget) < 0.05f)
                isAscending = false; // 목표 고도 도달 - 착륙 위치를 정할 때까지 공중에서 대기

            return;
        }

        if (isFlyingToDestination)
        {
            Vector3 flatTarget = new Vector3(flightDestination.x, transform.position.y, flightDestination.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, liftMoveSpeed * Time.deltaTime);

            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatDest = new Vector3(flightDestination.x, 0f, flightDestination.z);

            if (Vector3.Distance(flatPos, flatDest) < 0.05f)
            {
                isFlyingToDestination = false;
                isDescending = true;
            }

            return;
        }

        if (isDescending)
        {
            transform.position = Vector3.MoveTowards(transform.position, flightDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, flightDestination) < 0.05f)
            {
                transform.position = flightDestination;
                Land();
            }
        }
    }
```

**새 공개 메서드 추가** (`Die()` 위 어딘가, 예: `GetProductionQueue()` 근처):
```csharp
    // ===== 건물 이동(리프트) =====

    public bool CanLift() => canLift;
    public bool IsLifted() => isLifted;

    // 완공 시(BaseStructure.CompleteConstruction) 또는 착륙 완료 시(Land) 자신이 점유한 그리드 좌표를 기록한다.
    public void SetGridInfo(Vector3Int gridPos)
    {
        gridPosition = gridPos;
        hasGridPosition = true;
    }

    // "리프트" 버튼: 공중으로 떠오르며 그리드에서 자신의 위치를 지운다(공중 유닛처럼 그리드/NavMesh 영향을 받지 않음).
    public void LiftOff()
    {
        if (!canLift || isLifted)
            return;

        if (hasGridPosition)
        {
            placementSystem?.ReleaseBuildingGrid(gridPosition);
            hasGridPosition = false;
        }

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = false;

        isLifted = true;
        isAscending = true;
        verticalTarget = transform.position + Vector3.up * liftHeight;
    }

    // "착륙" 버튼: 완전히 떠 있는 상태(상승/이동/하강 중이 아님)에서만 착륙 위치 선택 모드로 진입한다.
    public void BeginLanding()
    {
        if (!isLifted || isAscending || isFlyingToDestination || isDescending)
            return;

        placementSystem?.StartBuildingRelocation(this);
    }

    // PlacementSystem이 착륙 위치 클릭을 확정했을 때 호출: 그 위치로 수평 이동을 시작한다.
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        flightDestination = destination;
        isFlyingToDestination = true;
    }

    // 착륙 완료: 그리드에 새 위치를 등록하고 지상 상태로 복귀한다.
    private void Land()
    {
        isLifted = false;

        gridPosition = pendingGridPosition;
        hasGridPosition = true;

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = true;

        System.Action landed = onRelocationLanded;
        onRelocationLanded = null;
        onRelocationCancelled = null;

        landed?.Invoke();
    }
```

**`Die()`에 "비행 중 파괴" 정리 추가**:
```csharp
// 기존 코드
    public void Die()
    {
        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록
        rtsController?.RemoveMaxPopulationForBuilding(buildingID); // 이 건물이 제공하던 인구수 한도를 반환

        Destroy(gameObject);
    }
```
```csharp
// 변경 코드
    public void Die()
    {
        // 착륙 위치로 비행 중(또는 착륙 직전)에 파괴되면, 예약해둔 그리드 셀/고스트가 영원히 남지 않도록 정리한다.
        if (onRelocationCancelled != null)
        {
            onRelocationCancelled.Invoke();
            onRelocationCancelled = null;
            onRelocationLanded = null;
        }

        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록
        rtsController?.RemoveMaxPopulationForBuilding(buildingID); // 이 건물이 제공하던 인구수 한도를 반환

        Destroy(gameObject);
    }
```

### 5. `Assets/Scripts/UI/UIController.cs`

**아이콘 필드 추가**:
```csharp
// 기존 코드
    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;
```
```csharp
// 변경 코드
    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;
    [SerializeField] private Sprite liftOffIcon; // 리프트 버튼(지상 상태일 때)
    [SerializeField] private Sprite landIcon;    // 착륙 버튼(공중 상태일 때)
```

**새 메서드 추가** (`ShowAirportPanel` 아래):
```csharp
    // 건물 커맨드 패널의 마지막 슬롯에 리프트/착륙 버튼을 추가로 표시한다.
    // ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel 호출 "이후"에, 혹은
    // 전용 패널이 없는 건물(SupplyDepot/Lab 등) 선택 시 단독으로 호출된다.
    public void ShowBuildingLiftCommand(bool isLifted, ButtonAction onLiftOrLand)
    {
        if (slots.Length == 0)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        Sprite icon = isLifted ? landIcon : liftOffIcon;
        slots[slots.Length - 1].SetData(new CommandButtonData(icon, onLiftOrLand));
    }
```

### 6. `Assets/Scripts/System/RTSUnitController.cs`

**새 메서드 추가** (`#region Building선택 관련` 안, `SetRallySelectBuilding` 아래):
```csharp
    // "리프트" 버튼: 선택된 건물(단일 취급)을 공중으로 띄운다.
    public void LiftSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].LiftOff();
    }

    // "착륙" 버튼: 선택된 건물(단일 취급)의 착륙 위치 선택 모드로 진입한다.
    public void BeginLandingSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].BeginLanding();
    }
```

**`UpdateUI()`의 `SelectState.BuildingSelect` 케이스 끝에 리프트 버튼 표시 추가**:
```csharp
// 기존 코드
                switch (BuildingSelectState)
                {
                    case BuildingState.MainBaseSelect:
                        ...
                    case BuildingState.SupplyDepot:
                    case BuildingState.Lab:
                    case BuildingState.None:
                        uIController.ClearPanel();
                        uIController.HideProductionUI();
                        break;
                }
                break;
```
```csharp
// 변경 코드
                switch (BuildingSelectState)
                {
                    case BuildingState.MainBaseSelect:
                        ...
                    case BuildingState.SupplyDepot:
                    case BuildingState.Lab:
                    case BuildingState.None:
                        uIController.ClearPanel();
                        uIController.HideProductionUI();
                        break;
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 마지막 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (selectedBuildingList.Count > 0 && selectedBuildingList[0].CanLift())
                {
                    BuildingController building = selectedBuildingList[0];

                    uIController.ShowBuildingLiftCommand(
                        building.IsLifted(),
                        building.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>G</color>]", KeyCode.G)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>G</color>]", KeyCode.G));
                }
                break;
```
(실제 적용 시엔 `...` 부분 없이 기존 `case` 본문 전체를 그대로 두고 `switch` 블록이 끝난 직후에 위 블록만 추가합니다.)

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **리프트 가능 여부**: `BuildingController.canLift`(기본값 `true`) 인스펙터 토글로 건물별 on/off. 코드 수정 없이 프리팹별로 끌 수 있습니다(예: SupplyDepot/Lab을 리프트 불가로 하고 싶다면 그 프리팹에서 체크만 해제).
- **버튼 노출 범위**: 전용 커맨드 패널이 있는 MainBase/Barracks/Factory/Airport뿐 아니라, 전용 패널이 없는 SupplyDepot/Lab 선택 시에도 `canLift`가 켜져 있으면 패널을 새로 열고 리프트 버튼만 표시합니다(마지막 슬롯 1칸만 사용, 기존 슬롯들과 겹치지 않음).
- **자원/일꾼 불필요**: 착륙 위치 선택은 신규 건설이 아니므로 자원을 소모하지 않고, 일꾼도 필요 없습니다(건물 자신이 직접 이동).
- **착륙 위치 선택 중 ESC**: "착륙 위치를 고르는 모드"만 취소되고, 건물은 계속 공중에 떠 있는 상태로 남습니다(다시 착륙 버튼을 누르면 재시도 가능). 반대로 리프트 이륙 자체를 취소(다시 지상으로)하는 기능은 이번 범위에 포함하지 않습니다.
- **착륙 도착 시 재검증 없음**: 클릭 시점에 그리드를 즉시 예약하므로, 비행 도중 그 자리에 다른 건물이 끼어들 수는 없습니다. 다만 유닛이 마침 그 자리에 서 있는 경우의 착륙 시점 재검증은 하지 않습니다 — 기존 건설 시스템(일꾼이 건설 위치에 도착할 때도 재검증하지 않음)과 동일한 패턴을 유지한 것입니다.
- **공중에 뜬 동안 전투/피격**: 레이어·콜라이더는 그대로 두므로 계속 선택/우클릭/피격 대상이 됩니다(공중 무적 같은 처리는 하지 않음). 다만 물리적으로 위로 올라가 있어 거리 기반 사거리 판정에는 자연히 영향이 있을 수 있습니다. 별도 "공중 무적" 요청이 있으면 추가 작업으로 처리하겠습니다.
- **비행 중 파괴**: 착륙 위치로 이동하는 도중 건물이 파괴되면(`Die`) 예약해둔 그리드 셀과 남아있던 착륙 고스트를 정리합니다.
- **에디터에 미리 배치된 시작 건물**: `PlacementSystem`을 거치지 않아 그리드 좌표를 모르는 상태로 시작하므로, 최초 리프트 시엔 그리드 해제를 건너뛰고(원래 등록된 적이 없으므로) 첫 착륙부터 정상적으로 그리드에 등록됩니다.
- **단축키**: 리프트/착륙 버튼 모두 `G`를 사용합니다(현재 건물 관련 패널들에서 미사용 키).
- **⚠️ 인스펙터 연결 필요**: `UIController`에 새로 추가되는 `Lift Off Icon` / `Land Icon` 스프라이트 슬롯은 직접 채워주셔야 합니다(요청하신 대로 스프라이트 연결은 직접 하시는 부분).

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).

적용 후 인스펙터에서 직접 연결해주셔야 하는 것:
- `UIController`의 `Lift Off Icon` / `Land Icon` 스프라이트 슬롯.
- (필요 시) 건물 프리팹별 `Building Controller`의 `Can Lift` / `Lift Height` / `Lift Move Speed` 값 조정.
