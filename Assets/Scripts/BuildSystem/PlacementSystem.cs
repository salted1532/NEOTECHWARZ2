using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

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

    private int selectedObjectIndex = -1;

    private GridData StructureData;
    private List<GameObject> placedGameObject = new();

    private Vector3Int lastDectectedPosition = Vector3Int.zero;

    private RTSUnitController rtsController;

    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹

    void Start()
    {
        StopPlacement();
        StructureData = new();
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    // ID에 해당하는 건물 데이터베이스 항목을 찾아 배치 모드를 시작한다 (프리뷰 표시 + 클릭/ESC 이벤트 구독).
    // ID가 0이면 선택 해제로 취급한다.
    public void StartPlacement(int ID)
    {
        StopPlacement();

        selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == ID);

        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No ID found {ID}");
            return;
        }

        if (ID == 0)
        {
            selectedObjectIndex = -1;
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

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
            return;

        // ⭐ 유닛 체크 추가
        if (IsBlocked(mousePos, data.Size))
            return;

        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        if (rtsController == null || !rtsController.TryConstructBuilding(data.ID))
            return; // 자원/인구가 부족하면 배치하지 않음 (여기서 자원이 실제로 차감됨)

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
            onArrived: () => StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));

        // 클릭 한 번으로 배치를 확정했으므로 건설모드는 여기서 종료한다 (기존 "취소" 버튼과 동일한 종료 방식)
        StopPlacement();
        rtsController?.ReturnState();
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 고스트를 지우고 BaseStructure(건물 기반)를 생성해 일꾼을 붙인다.
    // 실제 완성된 건물은 BaseStructure 자신이 건설시간이 다 되면 생성한다.
    private void StartConstruction(BuildingData data, Vector3 groundPos, Vector3Int gridPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        Vector3 structureSpawnPos = groundPos + Vector3.up * GetGroundOffsetY(baseStructurePrefab); // BaseStructure 자신의 높이 기준

        GameObject obj = Instantiate(baseStructurePrefab, structureSpawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        // 플레이어가 직접 건설을 취소할 때(CancelConstruction) 그리드 예약을 풀어줄 콜백도 함께 넘긴다.
        structure.Initialize(data.ID, data.productionTime, groundPos, () => CancelReservedConstruction(gridPos, null));

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
    // 건물이 들어설 영역에 유닛/장애물 등 blockingLayers에 속한 콜라이더가 있는지 물리 박스 검사로 확인한다.
    // 그리드 셀 점유 체크(StructureData)와 별개로, 실제 3D 공간상의 충돌까지 추가로 막기 위한 검사다.
    private bool IsBlocked(Vector3 worldPos, Vector2Int size)
    {
        Vector3 cellSize = grid.cellSize;

        Vector3 center = GetPlacementWorldPosition(grid.WorldToCell(worldPos), size);

        const float margin = 0.02f;

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

        foreach (Collider hit in hits)
        {
            Debug.Log(
                $"Blocked by : {hit.name} | Layer : {LayerMask.LayerToName(hit.gameObject.layer)}"
            );
        }

        return hits.Length > 0;
    }

    // 배치 모드를 종료하고 프리뷰/이벤트 구독을 정리한다. (취소 또는 배치 완료 후 재진입 대비)
    public void StopPlacement()
    {
        selectedObjectIndex = -1;

        gridVisualization.SetActive(false);
        preview.StopShowingPreview();

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        lastDectectedPosition = Vector3Int.zero;
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

            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size) && !IsBlocked(mousePos, data.Size);

            Vector3 previewPos = GetGroundPosition(gridPos, data.Size) + Vector3.up * GetGroundOffsetY(data.Prefab);

            preview.UpdatePosition(previewPos, valid);

            mouseIndicator.transform.position = mousePos;

            lastDectectedPosition = gridPos;
        }
    }
}