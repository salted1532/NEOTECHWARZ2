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

    void Start()
    {
        StopPlacement();
        StructureData = new();
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

            Vector3 previewPos = GetPlacementWorldPosition(gridPos, data.Size);

            preview.UpdatePosition(previewPos, valid);

            mouseIndicator.transform.position = mousePos;

            lastDectectedPosition = gridPos;
        }
    }
}