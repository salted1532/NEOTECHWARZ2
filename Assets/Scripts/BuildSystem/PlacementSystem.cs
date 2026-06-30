using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

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

    private void StopPlacement()
    {
        selectedObjectIndex = -1;

        gridVisualization.SetActive(false);
        preview.StopShowingPreview();

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        lastDectectedPosition = Vector3Int.zero;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            StartPlacement(1);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            StartPlacement(2);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            StartPlacement(3);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            StartPlacement(4);
        if (Input.GetKeyDown(KeyCode.Alpha5))
            StartPlacement(5);
        if (Input.GetKeyDown(KeyCode.Alpha6))
            StartPlacement(6);



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