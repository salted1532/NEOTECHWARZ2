# 0380 - 촘촘한 스캔 대신 맵 가장자리 1칸 여백으로 돌출 방지

**날짜:** 2026-08-03

**승인 후 구현 완료.** `terrainSampleStep` 0.5m로 원복, `HasTerrainMargin`(기본 1칸) 추가, 3곳 호출부 연결.

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 다시 0.5m로 되돌려주고 그리드 자체를 조금 고쳐볼까 하는데 그리드가 맵바깝과 맵 사이 그 1줄의
> 그리드만 건설 불가로 지정하는건 어때?

## 조사 결과

- [[0379]]에서 `terrainSampleStep`을 0.2m까지 낮췄지만, 이 방식은 "얼마나 촘촘히 스캔하느냐"의
  문제라서 근본적으로 한계가 있음 - 지형 경계가 표본 간격보다 더 얇게 걸치면 여전히 놓침, 반대로
  더 촘촘히 하면 레이캐스트 비용만 계속 늘어남 (풋프린트 "안쪽"만 스캔하는 방식의 구조적 한계).
- 사용자가 제안한 방식(풋프린트를 감싸는 1칸 여백 전체에 지형이 있어야 배치 가능)은 다른 종류의
  보장을 준다: 표본 해상도에 기대는 대신, **풋프린트 자체가 아니라 그 바깥 한 칸 테두리까지도 전부
  지형이 있어야 한다**는 더 강한 조건을 걸기 때문에, 얼마나 얇은 돌출이든(맵 경계가 정확히 어디를
  지나가든) 여백 한 칸만큼 여유가 생겨서 표본 해상도와 무관하게 걸러짐. 코드도 더 단순해짐(칸 단위
  레이캐스트만 하면 되고 세밀한 미터 단위 스텝 계산이 필요 없음).
- [[0377]]/[[0379]]에서 다룬 "절벽/언덕 벽" 감지(칸 내부 높이차 검사, `IsFootprintTerrainFlat`)는
  이번 요청과 별개 문제라 그대로 둠 - 이번엔 `terrainSampleStep`만 0.5m로 되돌리고, 맵 경계 돌출은
  새로 추가하는 여백 검사가 전담하게 함.

## 코드 변경 (제안)

### 1. `terrainSampleStep`을 0.5m로 원복

기존 코드 (`PlacementSystem.cs:491`):
```csharp
    [SerializeField] private float terrainSampleStep = 0.2f;
```
변경 코드:
```csharp
    [SerializeField] private float terrainSampleStep = 0.5f;
```

### 2. 풋프린트를 감싸는 1칸 여백 지형 검사 추가

`IsFootprintTerrainFlat` 아래에 새 검사 메서드 추가:

```csharp
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
```

### 3. 호출부 3곳에 검사 추가 (기존 `IsFootprintTerrainFlat` 바로 뒤)

`PlaceStructure()`:
```csharp
        if (!IsFootprintTerrainFlat(gridPos, data.Size))
            return;

        if (!HasTerrainMargin(gridPos, data.Size)) // 맵 가장자리 1칸 여백 검사 (doc/0380)
            return;
```

`PlaceRelocatedBuilding()`:
```csharp
        if (!IsFootprintTerrainFlat(gridPos, data.Size)) return;
        if (!HasTerrainMargin(gridPos, data.Size)) return; // 맵 가장자리 1칸 여백 검사 (doc/0380)
```

`Update()` 프리뷰 유효성 판정:
```csharp
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
                && !IsTooCloseToResource(data.ID, gridPos, data.Size)
                && IsInsideAlliedTerritory(gridPos, data.Size)
                && IsFootprintTerrainFlat(gridPos, data.Size)
                && HasTerrainMargin(gridPos, data.Size);
```

## 열린 질문

- `edgeMarginCells` 기본값 1칸 - 그래도 놓치는 경우가 있으면 2칸으로 늘릴 수 있음(레이캐스트 비용은
  선형적으로만 늘어나서 부담 적음).
- 이 검사는 `IsFootprintTerrainFlat`을 대체하는 게 아니라 추가되는 것 - 절벽/언덕 벽(칸 내부 높이차)
  감지는 그대로 유지되고, 맵 경계 돌출만 이 새 검사가 전담함.

## 영향받는 파일 (예정)

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
