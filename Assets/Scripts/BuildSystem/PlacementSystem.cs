using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 건물 배치 시스템의 핵심 컨트롤러.
// 배치 모드 시작/취소, 그리드 위치 계산, 배치 가능 여부(겹침 + 유닛/장애물 충돌) 판정, 실제 건물 생성을 담당한다.
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private LayerMask blockingLayers;

    [SerializeField] private GameObject mouseIndicator, cellIndicator;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private Grid grid;
    [SerializeField] private BuildingDataSO database;
    [SerializeField] private GameObject gridVisualization;
    [SerializeField] private PreviewSystem preview;

    // ⭐ 건물 높이 오프셋 (프리뷰 + 실제 공통 적용)
    [SerializeField]
    private float yOffset = 1f;

    [Header("메인기지(커맨드센터) 전용 - 자원 이격 거리")]
    // ⭐ 메인기지(커맨드센터)만 적용받는, 자원(광물/가스) 노드로부터의 최소 이격 거리 (그리드 칸 단위, 원형/유클리드 거리)
    [Tooltip("메인기지(커맨드센터)를 지을 때 광물/가스로부터 최소 이만큼(칸, 원형 거리) 떨어져야 함. 다른 건물에는 적용되지 않음.")]
    [SerializeField]
    private float minDistanceFromResource = 7f;

    private int selectedObjectIndex = -1;

    // 건설/착륙 배치 모드가 활성 상태인지 - UserControl이 배치 모드 중 유닛 선택(클릭/드래그)을
    // 막을 때 조회한다 (doc/0526). StartPlacement/StartBuildingRelocation이 켜고 StopPlacement가 끈다.
    public bool IsPlacementModeActive => selectedObjectIndex >= 0;

    private GridData StructureData;
    private List<GameObject> placedGameObject = new();

    private Vector3Int lastDectectedPosition = Vector3Int.zero;

    private RTSUnitController rtsController;

    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹

    [Header("시작 위치")]
    [Tooltip("게임 시작 시 메인기지(커맨드센터)를 그리드에 맞춰 즉시 생성할 위치. 빈 오브젝트를 씬에 배치해서 연결.")]
    [SerializeField] private GameObject startPoint;
    [Tooltip("게임 시작 시 startPoint 위치에 메인기지를 자동 스폰할지 여부. 유닛 조종만 있는 미션 등에서는 꺼둔다.")]
    [SerializeField] private bool spawnStartingMainBase = true;

    // ===== 건물 리프트 이동(착륙 위치 선택) =====
    private BuildingController relocatingBuilding; // 현재 착륙 위치를 고르는 중인 건물(없으면 null)

    // StructureData는 Awake()에서 만든다 - BuildingController.Start()가 씬에 미리 배치된 자기 자신을
    // 그리드에 등록할 때 이 PlacementSystem을 참조하는데, 서로 다른 GameObject의 Start() 호출 순서는
    // 유니티가 보장해주지 않는다. Awake()는 씬의 모든 Start()보다 항상 먼저 끝나므로 여기서 만들어야
    // BuildingController.Start()가 먼저 실행돼도 StructureData가 항상 준비돼 있다 (doc/0247).
    private void Awake()
    {
        StructureData = new();
    }

    void Start()
    {
        StopPlacement();
        rtsController = FindFirstObjectByType<RTSUnitController>();

        SpawnStartingMainBase();
    }

    // 게임 시작 시 startPoint 위치에 메인기지(커맨드센터)를 건설 과정 없이 완성된 상태로 즉시 생성하고,
    // 다른 배치와 동일하게 그리드에 등록한다(리프트 이동을 위한 gridPosition도 함께 설정됨).
    private void SpawnStartingMainBase()
    {
        if (!spawnStartingMainBase || startPoint == null)
            return;

        int index = database.buildingData.FindIndex(d => d.ID == RTSUnitController.BuildingID.CommandCenter);
        if (index < 0)
        {
            Debug.LogWarning("BuildingDataSO에 메인기지(CommandCenter) 데이터가 없습니다.");
            return;
        }

        BuildingData data = database.buildingData[index];
        Vector3Int gridPos = grid.WorldToCell(startPoint.transform.position);

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
        {
            Debug.LogWarning("시작 위치(startPoint)에 메인기지를 배치할 수 없습니다 (그리드 겹침).");
            return;
        }

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size, startPoint.transform.position.y);
        Vector3 spawnPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab);

        GameObject obj = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            obstacle.enabled = true;

        if (obj.TryGetComponent<BuildingController>(out var controller))
            controller.SetGridInfo(gridPos); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록

        placedGameObject.Add(obj);
        int placedIndex = placedGameObject.Count - 1;
        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        rtsController?.AddMaxPopulation(data.maxpopulationamount); // 완공 건물과 동일하게 인구수 한도 반영
    }

    // ID에 해당하는 건물 데이터베이스 항목을 찾아 배치 모드를 시작한다 (프리뷰 표시 + 클릭/ESC 이벤트 구독).
    // ID가 0이면 선택 해제로 취급한다.
    public void StartPlacement(int ID)
    {
        StopPlacement();

        if (ID == 0)
        {
            selectedObjectIndex = -1;
            return;
        }

        selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == ID);

        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No ID found {ID}");
            return;
        }

        gridVisualization.SetActive(true);

        preview.StartShowingPlacementPreview(
            database.buildingData[selectedObjectIndex].Prefab,
            database.buildingData[selectedObjectIndex].Size
        );

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    // OnClicked 이벤트 핸들러: 현재 마우스 위치가 배치 가능하면(그리드 겹침 없음 + 장애물 없음)
    // 실제 건물을 생성하고 그리드에 점유 정보를 등록한다.
    private void PlaceStructure()
    {
        if (selectedObjectIndex < 0) return;
        if (inputManager.IsPointerOverUI()) return;

        Vector3 mousePos = inputManager.GetSelectedMapPosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        var data = database.buildingData[selectedObjectIndex];

        // 현재 선택된 일꾼은 장애물 판정에서 제외 - 일꾼이 서 있는 자리에도 건물을 지을 수 있게
        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;

        if (!IsValidPlacement(mousePos, gridPos, data, worker != null ? worker.gameObject : null))
        {
            UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.constructionfail")); // 빨간 프리뷰 클릭(doc/0525)
            return;
        }

        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        // 자원부족 등 클릭 시점의 실패는 TryConstructBuilding 내부에서 전역 "자원부족" 나레이션을 재생한다
        // (doc/0272) - 일꾼의 건설 실패 음성(PlayBuildFailVoice)은 도착 시 장애물 발견 케이스 전용으로 남겨둔다.
        if (rtsController == null || !rtsController.TryConstructBuilding(data.ID))
            return; // 자원/인구가 부족하거나 선행 건물 조건을 못 채우면 배치하지 않음

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size, mousePos.y);
        Vector3 spawnPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab); // 완공될 건물 기준 높이 (고스트/일꾼 목적지용)

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);
        SoundManager.Instance?.PlaySFX(data.soundBank?.placementSFX, spawnPos); // 프리뷰가 이 자리에 고정되는 순간 (doc/0646)

        worker.GetComponent<UnitAudio>()?.PlayOrderVoice(); // 건설 위치로 이동을 시작하므로 이동 명령 음성 재생
        worker.GetComponent<UnitAudio>()?.PlayOrderSFX();

        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker),
            onCancelled: () =>
            {
                // 일꾼이 건설 위치에 도착하기 전(BaseStructure 생성 전)에 이동/공격 등 다른 명령으로
                // 건설 이동이 취소된 경우: 클릭 시 이미 차감된 건물 가격(광물/가스)을 전액 환불한다.
                CancelReservedConstruction(gridPos, ghost);
                rtsController?.RefundBuilding(data.ID);
            });

        // 클릭 한 번으로 배치를 확정했으므로 건설모드는 여기서 종료한다 (기존 "취소" 버튼과 동일한 종료 방식)
        StopPlacement();
        rtsController?.ReturnState();
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 고스트를 지우고 BaseStructure(건물 기반)를 생성해 일꾼을 붙인다.
    // 실제 완성된 건물은 BaseStructure 자신이 건설시간이 다 되면 생성한다.
    private void StartConstruction(BuildingData data, Vector3 groundPos, Vector3Int gridPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        // 일꾼이 이동하는 동안(클릭 시점엔 비어있었지만) 그 자리에 유닛/건물/지형지물 같은 장애물이
        // 새로 생겼으면 건설 실패로 취급한다 - 그대로 겹쳐 짓지 않고 실패 음성 + 취소 + 환불 처리.
        // 담당 일꾼 자신은 지금 막 그 자리에 도착해서 서 있는 상태라, 장애물 판정에서 제외한다.
        if (IsBlockedAtCenter(groundPos, data.Size, worker.gameObject))
        {
            worker.GetComponent<UnitAudio>()?.PlayBuildFailVoice();
            UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.constructionfail"));
            CancelReservedConstruction(gridPos, ghost);
            rtsController?.RefundBuilding(data.ID);
            return;
        }

        if (ghost != null)
            Destroy(ghost);

        Vector3 structureSpawnPos = groundPos + Vector3.up * GetGroundOffsetY(baseStructurePrefab); // BaseStructure 자신의 높이 기준

        GameObject obj = Instantiate(baseStructurePrefab, structureSpawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        // 플레이어가 직접 건설을 취소할 때(CancelConstruction) 그리드 예약을 풀어줄 콜백도 함께 넘긴다.
        // data.Size/grid.cellSize를 넘겨서, 3x3 기준으로 만들어진 BaseStructure 프리팹을 실제 건물 칸 수에 맞게 스케일한다.
        structure.Initialize(data.ID, data.productionTime, groundPos, gridPos, data.Size, grid.cellSize, () => CancelReservedConstruction(gridPos, null));

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }

    // 일꾼이 도착하기 전에 다른 명령으로 건설 이동이 취소됐을 때(GoBuild 콜백) 고스트를 지우고 예약해둔 그리드 셀을 비워준다.
    private void CancelReservedConstruction(Vector3Int gridPos, GameObject ghost)
    {
        if (ghost != null)
            Destroy(ghost);

        StructureData.RemoveObjectAt(gridPos);
    }

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

        if (!IsValidPlacement(mousePos, gridPos, data, null))
        {
            UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.landingblocked")); // 빨간 프리뷰 클릭(doc/0525)
            return;
        }

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size, mousePos.y);
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

    // 월드 좌표 → 그리드 셀 좌표. BuildingController/EnemyBuildingController가 씬에 미리 배치된 자신의
    // 위치로부터 자기 그리드 셀을 역산할 때 사용 (doc/0247).
    public Vector3Int WorldToGridCell(Vector3 worldPos) => grid.WorldToCell(worldPos);

    // 씬에 이미 배치돼 있는 건물(정상 건설 흐름을 거치지 않은 건물)이 게임 시작 시 자기 자신을 그리드
    // 점유 정보에 등록할 때 호출한다 (BuildingController/EnemyBuildingController.Start(), doc/0247).
    // 이미 그 칸이 점유돼 있으면(겹침) 등록하지 않고 false를 반환한다 - GridData.AddObjectAt은 겹치면
    // 예외를 던지므로 반드시 CanPlaceObejctAt으로 먼저 확인해야 한다.
    public bool RegisterBuildingGrid(GameObject buildingObject, Vector3Int gridPos, Vector2Int size, int id)
    {
        if (!StructureData.CanPlaceObejctAt(gridPos, size))
        {
            Debug.LogWarning($"{buildingObject.name}: 그리드 위치 {gridPos}가 이미 점유돼 있어 등록할 수 없습니다.");
            return false;
        }

        placedGameObject.Add(buildingObject);
        int placedIndex = placedGameObject.Count - 1;
        StructureData.AddObjectAt(gridPos, size, id, placedIndex);

        return true;
    }

    /// <summary>
    /// Grid → World 변환 + XZ 중앙정렬. Y는 그리드 셀 크기로 양자화하지 않고, 호출부가 이미 알고 있는
    /// 실제 지면 좌표(groundY, 마우스 레이캐스트나 startPoint.transform.position.y 등)를 그대로 쓴다.
    /// (그리드 셀 크기를 거쳐 Y를 되돌리면 cellSize.y 배수로 양자화돼 실제 지형 높이와 어긋난다 - doc/0150)
    /// </summary>
    public Vector3 GetGroundPosition(Vector3Int gridPos, Vector2Int size, float groundY)
    {
        Vector3 basePos = grid.CellToWorld(gridPos);
        Vector3 cellSize = grid.cellSize;

        Vector3 centerOffset = new Vector3(
            (size.x - 1) * cellSize.x * 0.5f,
            0,
            (size.y - 1) * cellSize.y * 0.5f
        );

        Vector3 pos = basePos + centerOffset;
        pos.y = groundY;
        return pos;
    }

    // 기존 IsBlocked()용 - 프리팹에 상관없이 대략적인 충돌 검사 박스 중심 높이만 필요하므로 고정 오프셋을 그대로 사용.
    private Vector3 GetPlacementWorldPosition(Vector3Int gridPos, Vector2Int size, float groundY)
    {
        return GetGroundPosition(gridPos, size, groundY) + Vector3.up * yOffset;
    }

    // 프리팹의 메쉬 바운드(로컬)와 스케일을 바탕으로, 피벗이 정확히 지면(바닥)에 닿도록 필요한 y 오프셋을 계산한다.
    // 메쉬가 없으면(콜라이더만 있는 경우 등) 안전한 기존 고정값(1)으로 대체한다.
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

    // 건물 프리팹의 전체 높이(로컬 메쉬 바운드 기준) - 건설 상승 애니메이션에서 건물을 땅속에 얼마나
    // 파묻어 시작할지 계산하는 데 쓰인다 (doc/0527). GetGroundOffsetY와 마찬가지로 루트의 MeshFilter만 본다.
    public static float GetBuildingHeight(GameObject prefab)
    {
        if (prefab == null)
            return 2f;

        if (!prefab.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return 2f;

        return meshFilter.sharedMesh.bounds.size.y * prefab.transform.localScale.y;
    }
    // 건물이 들어설 영역에 유닛/장애물 등 blockingLayers에 속한 콜라이더가 있는지 물리 박스 검사로 확인한다.
    // 그리드 셀 점유 체크(StructureData)와 별개로, 실제 3D 공간상의 충돌까지 추가로 막기 위한 검사다.
    // ignoreObject: 이 오브젝트(및 그 자식) 콜라이더는 장애물로 치지 않는다 - 건설 위치에 도착해서
    // 그 자리에 서 있는 담당 일꾼 자신이 장애물로 오인되지 않도록 하기 위함(doc/0268).
    // worldPos: 아직 그리드에 스냅되지 않은 원시 좌표(마우스 위치 등) - 내부에서 셀을 역산해 중심을 구한다.
    // 이미 GetGroundPosition() 등으로 중심정렬까지 끝낸 좌표는 여기 넣지 말고 IsBlockedAtCenter()를 써야 한다
    // (다시 WorldToCell로 역산하면 3x3 이상 건물에서 한 칸 옆으로 오판정되는 버그가 있었음 - doc/0350).
    private bool IsBlocked(Vector3 worldPos, Vector2Int size, GameObject ignoreObject = null)
    {
        Vector3 center = GetPlacementWorldPosition(grid.WorldToCell(worldPos), size, worldPos.y);
        return IsBlockedAtCenter(center, size, ignoreObject);
    }

    // groundCenter: 이미 건물 풋프린트의 기하학적 중심으로 계산된 좌표(GetGroundPosition의 반환값 등).
    // 다시 WorldToCell로 역산하지 않고 그대로 사용한다 (doc/0350).
    private bool IsBlockedAtCenter(Vector3 groundCenter, Vector2Int size, GameObject ignoreObject = null)
    {
        Vector3 cellSize = grid.cellSize;

        Vector3 center = groundCenter + Vector3.up * yOffset;

        // 인접 건물을 정확히 붙여 지을 때 지형 높이/그리드→월드 변환의 미세한 부동소수점 오차를 흡수할
        // 여유(예전 0.02는 이론상 계산으로는 붙여짓기가 통과해야 하는데도 실전에서 막히는 사례가 있었음).
        // 그리드 셀 점유 체크(StructureData)가 실제 겹침은 이미 정확히 막으므로, 이 여유를 넉넉히 키워도
        // 진짜 겹치는 배치가 통과할 위험은 없다.
        const float margin = 0.1f;

        Vector3 halfExtents = new Vector3(
            size.x * cellSize.x * 0.5f - margin,
            1f,
            size.y * cellSize.z * 0.5f - margin
        );

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            blockingLayers
        );

        bool blocked = false;

        foreach (Collider hit in hits)
        {
            if (ignoreObject != null && hit.transform.IsChildOf(ignoreObject.transform))
                continue; // 담당 일꾼 자신은 장애물로 치지 않음

            blocked = true;
        }

        return blocked;
    }

    // 건물이 차지할 모든 셀 중 하나라도 자원(광물/가스) 노드와 minDistanceFromResource(칸, 원형 거리)보다
    // 가까우면 true를 반환한다. 자원 노드는 단일 대표 셀(자신의 위치가 속한 그리드 셀)로 취급한다.
    private bool IsTooCloseToResource(int buildingID, Vector3Int gridPosition, Vector2Int size)
    {
        // ⭐ 메인기지(커맨드센터)만 이 규칙을 적용받는다. 다른 건물은 자원 옆에 지어도 무방.
        if (buildingID != RTSUnitController.BuildingID.CommandCenter)
            return false;

        if (rtsController == null || rtsController.ResourceNodeList == null)
            return false;

        List<Vector3Int> occupiedCells = StructureData.CalculatePositionsPublic(gridPosition, size);

        foreach (ResourceNode node in rtsController.ResourceNodeList)
        {
            if (node == null)
                continue;

            Vector3Int resourceCell = grid.WorldToCell(node.transform.position);

            foreach (Vector3Int cell in occupiedCells)
            {
                float dx = cell.x - resourceCell.x;
                float dz = cell.z - resourceCell.z;
                float sqrDistance = dx * dx + dz * dz;

                if (sqrDistance < minDistanceFromResource * minDistanceFromResource)
                    return true;
            }
        }

        return false;
    }

    // 건물이 차지할 모든 셀이 전부 아군 영토 안에 있는지 검사한다 (하나라도 밖이면 false).
    private bool IsInsideAlliedTerritory(Vector3Int gridPosition, Vector2Int size)
    {
        List<Vector3Int> occupiedCells = StructureData.CalculatePositionsPublic(gridPosition, size);

        foreach (Vector3Int cell in occupiedCells)
        {
            if (!TerritoryManager.IsInsideAlliedTerritory(grid.CellToWorld(cell)))
                return false;
        }
        return true;
    }

    [Header("지형 평탄도 검사 (절벽/벽면 건설 방지)")]
    [Tooltip("건물이 차지하는 풋프린트 전체를 이 간격(미터)으로 촘촘히 스캔해 지면 높이 최고-최저 차이를 " +
             "구한다. 값이 작을수록 절벽이 살짝만 걸쳐도 놓치지 않지만 레이캐스트 횟수가 늘어난다.")]
    [SerializeField] private float terrainSampleStep = 0.5f;

    [Tooltip("풋프린트 안에서 잰 지면 높이 중 최고-최저 차이가 이 값(미터)을 넘으면 배치 불가 처리한다. " +
             "영토 판정은 X/Z 평면만 보기 때문에(TerritoryZone.Contains), 영토가 지상과 언덕을 모두 포함해도 " +
             "이 검사가 절벽에 걸친 배치를 따로 막아준다.")]
    [SerializeField] private float maxFootprintHeightVariance = 1.5f;

    // 건물 풋프린트 전체를 terrainSampleStep 간격의 격자로 촘촘히 스캔해(칸 경계와 무관), 지면 높이
    // 최고-최저 차이가 maxFootprintHeightVariance를 넘거나(절벽/벽면) 표본 지점에 지형 자체가 없으면
    // (맵 바깥 등, doc/0378) 배치를 막는다. 칸 중앙 1점만 재던 이전 방식(doc/0376)은 절벽이 칸 중앙을
    // 피해가면 놓치는 문제가 있어 (doc/0377) 교체.
    private bool IsFootprintTerrainFlat(Vector3Int gridPosition, Vector2Int size)
    {
        Vector3 footprintOrigin = grid.CellToWorld(gridPosition);
        float footprintWidth = size.x * grid.cellSize.x;
        float footprintDepth = size.y * grid.cellSize.z;

        int stepsX = Mathf.Max(1, Mathf.CeilToInt(footprintWidth / terrainSampleStep));
        int stepsZ = Mathf.Max(1, Mathf.CeilToInt(footprintDepth / terrainSampleStep));

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int ix = 0; ix <= stepsX; ix++)
        {
            float sampleX = Mathf.Min(ix * terrainSampleStep, footprintWidth);
            for (int iz = 0; iz <= stepsZ; iz++)
            {
                float sampleZ = Mathf.Min(iz * terrainSampleStep, footprintDepth);
                Vector3 rayOrigin = footprintOrigin + new Vector3(sampleX, 1000f, sampleZ);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, inputManager.PlacementLayerMask))
                    return false; // 풋프린트 안에 지형이 없는 지점(맵 바깥으로 걸침 등)이 있으면 그 자체로 배치 불가 (doc/0378)

                min = Mathf.Min(min, hit.point.y);
                max = Mathf.Max(max, hit.point.y);
            }
        }

        return (max - min) <= maxFootprintHeightVariance;
    }

    [Header("경계 여백 검사 (맵 바깥 돌출 방지)")]
    [Tooltip("건물 풋프린트를 사방으로 이 칸 수만큼 넓힌 범위 전체에 지형이 있어야 배치 가능하다. " +
             "그 여백 안에 지형이 없는 칸이 하나라도 있으면(맵 바깥에 가깝다는 뜻) 배치를 막는다. " +
             "풋프린트 내부만 스캔하는 IsFootprintTerrainFlat과 달리 표본 간격에 기대지 않고 항상 " +
             "1칸만큼의 여유를 보장한다.")]
    [SerializeField] private int edgeMarginCells = 1;

    // 풋프린트를 사방으로 edgeMarginCells칸만큼 넓힌 범위의 모든 칸 중심을 레이캐스트로 확인해서,
    // 지형이 없는 칸이 하나라도 있으면 배치를 막는다 (doc/0380). IsFootprintTerrainFlat의 촘촘한
    // 표본은 풋프린트 "안쪽"만 훑어서 표본 간격보다 얇은 돌출은 놓칠 수 있는데, 이 검사는 풋프린트보다
    // 한 칸 넓게 "테두리 전체에 지형이 있어야 한다"는 더 강한 조건이라 표본 해상도와 무관하게 걸러진다.
    private bool HasTerrainMargin(Vector3Int gridPosition, Vector2Int size)
    {
        for (int x = -edgeMarginCells; x < size.x + edgeMarginCells; x++)
        {
            for (int z = -edgeMarginCells; z < size.y + edgeMarginCells; z++)
            {
                Vector3Int cell = gridPosition + new Vector3Int(x, 0, z);
                Vector3 cellCenter = grid.CellToWorld(cell) + new Vector3(grid.cellSize.x, 0, grid.cellSize.z) * 0.5f;

                if (!Physics.Raycast(cellCenter + Vector3.up * 1000f, Vector3.down, out _, 2000f, inputManager.PlacementLayerMask))
                    return false;
            }
        }
        return true;
    }

    // 배치 모드를 종료하고 프리뷰/이벤트 구독을 정리한다. (취소 또는 배치 완료 후 재진입 대비)
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

    // 배치 가능 여부 판정 (프리뷰 색상 갱신 + 클릭 검증 공용) - Update()와 클릭 핸들러가 항상 같은
    // 조건을 쓰도록 한 곳에 모은다 (doc/0525 - 예전엔 세 곳에 조건이 중복돼 있었음).
    private bool IsValidPlacement(Vector3 mousePos, Vector3Int gridPos, BuildingData data, GameObject ignoreObject)
    {
        return StructureData.CanPlaceObejctAt(gridPos, data.Size)
            && !IsBlocked(mousePos, data.Size, ignoreObject)
            && !IsTooCloseToResource(data.ID, gridPos, data.Size)
            && IsInsideAlliedTerritory(gridPos, data.Size)
            && IsFootprintTerrainFlat(gridPos, data.Size)
            && HasTerrainMargin(gridPos, data.Size);
    }

    // 배치 모드일 때만 동작: 마우스가 새 그리드 셀로 이동하면 유효성(valid)을 재계산해 프리뷰 색상/위치를 갱신한다.
    void Update()
    {
        if (selectedObjectIndex < 0) return;

        Vector3 mousePos = inputManager.GetSelectedMapPosition();
        Vector3Int gridPos = grid.WorldToCell(mousePos);

        if (lastDectectedPosition != gridPos)
        {
            var data = database.buildingData[selectedObjectIndex];

            UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;

            bool valid = IsValidPlacement(mousePos, gridPos, data, worker != null ? worker.gameObject : null);

            Vector3 groundPos = GetGroundPosition(gridPos, data.Size, mousePos.y);
            Vector3 previewPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab);

            preview.UpdatePosition(previewPos, groundPos, valid);

            mouseIndicator.transform.position = mousePos;

            lastDectectedPosition = gridPos;
        }
    }
}