# 0377 - 절벽 감지(doc/0376) 표본 해상도 부족으로 놓치는 경우 수정

**날짜:** 2026-08-03

**승인 후 구현 완료.** `terrainSampleStep` 0.5m 기본값으로 적용.

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 언덕의 위치에 따라서 정상적으로 벽면 설치가 막히는 부분도 있고 언덕이 그리드의 살짝만 닿는 경우에는
> 벽면 설치가 가능한 부분도 생기네 높이차를 감지하지 못하는 예외 부분이 생기는거 같아

## 조사 결과

- [[0376]]에서 만든 `IsFootprintTerrainFlat()`(`PlacementSystem.cs:496`)은 건물이 차지하는 **각 그리드
  칸당 딱 한 점(칸의 정중앙)만** 레이캐스트로 높이를 잰다 (`grid.CellToWorld(cell) + cellCenterOffset`).
- 절벽 경계선이 어떤 칸을 "살짝만" 스치듯 지나가는 경우 - 즉 그 칸의 정중앙까지는 안 닿고 모서리/변
  근처에만 걸치는 경우 - 그 칸의 중앙점은 여전히 낮은 지대(또는 높은 지대) 한쪽에만 있는 것으로
  측정되어, 실제로는 그 칸 안에 절벽이 지나가는데도 높이차가 감지되지 않고 통과해버림. 사용자가 관찰한
  "언덕이 그리드에 살짝만 닿는 경우 벽면 설치가 가능해지는" 현상과 정확히 일치.
- 즉 표본 해상도(칸당 1점)가 절벽 경계의 위치에 따라 절벽을 완전히 놓칠 수 있는 구조적 문제 - 절벽이
  마침 어느 칸의 중앙점들을 피해서 지나가면 감지가 안 됨.

## 코드 변경 (제안)

칸 중앙 1점이 아니라, 건물 풋프린트 전체를 **일정한 간격(격자 크기와 무관한 고정 스텝)으로 촘촘하게
스캔**하도록 바꾼다. 그리드 칸 경계와 무관하게 촘촘히 훑으므로, 절벽이 정확히 어느 위치를 지나가든
칸 중앙을 피해가는 것과 상관없이 걸리게 된다.

기존 코드 (`PlacementSystem.cs:488~514`):
```csharp
    [Header("지형 평탄도 검사 (절벽/벽면 건설 방지)")]
    [Tooltip("건물이 차지하는 모든 칸의 지면 높이 중 최고-최저 차이가 이 값(미터)을 넘으면 배치 불가 처리한다. " +
             "영토 판정은 X/Z 평면만 보기 때문에(TerritoryZone.Contains), 영토가 지상과 언덕을 모두 포함해도 " +
             "이 검사가 절벽에 걸친 배치를 따로 막아준다.")]
    [SerializeField] private float maxFootprintHeightVariance = 1.5f;

    // 건물이 차지할 모든 칸의 지면 높이를 각각 레이캐스트로 재서, 최고-최저 높이차가
    // maxFootprintHeightVariance를 넘으면 절벽/벽면에 걸쳐 있다고 보고 배치를 막는다 (doc/0376).
    private bool IsFootprintTerrainFlat(Vector3Int gridPosition, Vector2Int size)
    {
        List<Vector3Int> occupiedCells = StructureData.CalculatePositionsPublic(gridPosition, size);
        Vector3 cellCenterOffset = new Vector3(grid.cellSize.x, 0, grid.cellSize.z) * 0.5f;

        float min = float.MaxValue;
        float max = float.MinValue;

        foreach (Vector3Int cell in occupiedCells)
        {
            Vector3 rayOrigin = grid.CellToWorld(cell) + cellCenterOffset + Vector3.up * 1000f;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, inputManager.PlacementLayerMask))
                continue; // 지형이 없는 칸(맵 밖 등)은 다른 검사에서 이미 걸러짐

            min = Mathf.Min(min, hit.point.y);
            max = Mathf.Max(max, hit.point.y);
        }

        return (max - min) <= maxFootprintHeightVariance;
    }
```

변경 코드:
```csharp
    [Header("지형 평탄도 검사 (절벽/벽면 건설 방지)")]
    [Tooltip("건물이 차지하는 풋프린트 전체를 이 간격(미터)으로 촘촘히 스캔해 지면 높이 최고-최저 차이를 " +
             "구한다. 값이 작을수록 절벽이 살짝만 걸쳐도 놓치지 않지만 레이캐스트 횟수가 늘어난다.")]
    [SerializeField] private float terrainSampleStep = 0.5f;

    [Tooltip("풋프린트 안에서 잰 지면 높이 중 최고-최저 차이가 이 값(미터)을 넘으면 배치 불가 처리한다. " +
             "영토 판정은 X/Z 평면만 보기 때문에(TerritoryZone.Contains), 영토가 지상과 언덕을 모두 포함해도 " +
             "이 검사가 절벽에 걸친 배치를 따로 막아준다.")]
    [SerializeField] private float maxFootprintHeightVariance = 1.5f;

    // 건물 풋프린트 전체를 terrainSampleStep 간격의 격자로 촘촘히 스캔해(칸 경계와 무관), 지면 높이
    // 최고-최저 차이가 maxFootprintHeightVariance를 넘으면 절벽/벽면에 걸쳐 있다고 보고 배치를 막는다.
    // 칸 중앙 1점만 재던 이전 방식(doc/0376)은 절벽이 칸 중앙을 피해가면 놓치는 문제가 있어 (doc/0377) 교체.
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
                    continue; // 지형이 없는 지점(맵 밖 등)은 다른 검사에서 이미 걸러짐

                min = Mathf.Min(min, hit.point.y);
                max = Mathf.Max(max, hit.point.y);
            }
        }

        return (max - min) <= maxFootprintHeightVariance;
    }
```

## 열린 질문

- `terrainSampleStep` 기본값 0.5m는 임의 추정치 - 절벽이 아주 얇게(0.5m보다 좁게) 스치는 극단적인
  경우는 여전히 놓칠 수 있음. 필요하면 에디터에서 더 작게 조절 가능(레이캐스트 비용은 늘어남).
- 프리뷰(`Update()`)는 마우스가 새 그리드 칸으로 옮길 때만(`lastDectectedPosition != gridPos`)
  재계산하므로 매 프레임 촘촘한 레이캐스트가 도는 건 아님 - 성능 영향은 미미할 것으로 보임.

## 영향받는 파일 (예정)

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
