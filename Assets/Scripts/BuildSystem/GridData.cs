using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 그리드 셀(Vector3Int) 단위로 어떤 오브젝트가 어느 칸을 점유하고 있는지 관리하는 순수 데이터 클래스.
// MonoBehaviour가 아니며, PlacementSystem이 배치 가능 여부 판정 및 점유/해제 처리에 사용한다.
public class GridData
{
    // 점유된 각 셀 좌표 -> 그 칸을 차지한 오브젝트 정보(PlacementData) 매핑
    Dictionary<Vector3Int, PlacementData> placedObjects = new();

    // 지정한 그리드 위치에 objectSize 크기의 오브젝트를 등록(점유 처리)한다.
    // 이미 점유된 셀과 겹치면 예외를 던진다 (호출 전에 CanPlaceObejctAt으로 먼저 확인해야 함).
    public void AddObjectAt(Vector3Int gridPosition,
                            Vector2Int objectSize,
                            int ID,
                            int placedObjectIndex)
    {
        List<Vector3Int> positionToOccupy = CalculatePositions(gridPosition, objectSize);
        PlacementData data = new PlacementData(positionToOccupy, ID, placedObjectIndex);
        foreach (var pos in positionToOccupy)
        {
            if (placedObjects.ContainsKey(pos))
                throw new Exception($"Dictionary already contains this cell positiojn {pos}");
            placedObjects[pos] = data;
        }
    }

    // 기준 좌표(gridPosition)에서 objectSize(x, y) 크기만큼의 모든 셀 좌표를 계산해 반환한다.
    private List<Vector3Int> CalculatePositions(Vector3Int gridPosition, Vector2Int objectSize)
    {
        List<Vector3Int> returnVal = new();
        for (int x = 0; x < objectSize.x; x++)
        {
            for (int y = 0; y < objectSize.y; y++)
            {
                returnVal.Add(gridPosition + new Vector3Int(x, 0, y));
            }
        }
        return returnVal;
    }

    // CalculatePositions의 외부 공개용 래퍼
    public List<Vector3Int> CalculatePositionsPublic(Vector3Int gridPosition, Vector2Int objectSize)
    {
        return CalculatePositions(gridPosition, objectSize);
    }

    // 해당 위치/크기에 오브젝트를 배치할 수 있는지(겹치는 칸이 없는지) 확인한다.
    public bool CanPlaceObejctAt(Vector3Int gridPosition, Vector2Int objectSize)
    {
        List<Vector3Int> positionToOccupy = CalculatePositions(gridPosition, objectSize);
        foreach (var pos in positionToOccupy)
        {
            if (placedObjects.ContainsKey(pos))
                return false;
        }
        return true;
    }

    // 해당 셀에 배치된 오브젝트의 인덱스(placedGameObject 리스트 상의 인덱스)를 반환. 없으면 -1
    internal int GetRepresentationIndex(Vector3Int gridPosition)
    {
        if (placedObjects.ContainsKey(gridPosition) == false)
            return -1;
        return placedObjects[gridPosition].PlacedObjectIndex;
    }

    // 해당 셀을 점유한 오브젝트가 차지하던 모든 셀을 일괄 해제(제거)한다.
    internal void RemoveObjectAt(Vector3Int gridPosition)
    {
        foreach (var pos in placedObjects[gridPosition].occupiedPositions)
        {
            placedObjects.Remove(pos);
        }
    }
}

// 그리드에 배치된 오브젝트 하나에 대한 점유 정보(어느 셀들을 차지했는지, ID, 리스트 인덱스)
public class PlacementData
{
    public List<Vector3Int> occupiedPositions;
    public int ID { get; private set; }
    public int PlacedObjectIndex { get; private set; }

    public PlacementData(List<Vector3Int> occupiedPositions, int iD, int placedObjectIndex)
    {
        this.occupiedPositions = occupiedPositions;
        ID = iD;
        PlacedObjectIndex = placedObjectIndex;
    }
}