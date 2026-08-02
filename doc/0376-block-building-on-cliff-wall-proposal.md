# 0376 - 언덕 벽면(절벽)에 건물 건설 막기

**날짜:** 2026-08-03

**승인 후 구현 완료.** 높이차 검사 방식으로 확정 - 언덕 위/지상 각각 온전히 짓는 건 그대로 가능하고,
풋프린트가 지상↔언덕 경계(절벽)에 걸쳐 있는 경우만 막힘.

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 벽면에 건물 건설 막기
> ㄴ 점령지 영역이 언덕과 지상을 모두 포함할 경우 언덕의 벽을 뚫고 건물을 지을수 있는데
> 이를 해결해줘. 내가 생각한 방법은 지형의 끝쪽 모서리에 지을수 없는 영역의 범위를 지정하면
> Layer2의 언덕 지형에서도 벽면에는 건물을 지을수 없는 영역이 생겨 벽에다가 건물을 지을수 없을거 같아

## 조사 결과

- 건물 배치 유효성은 `PlacementSystem.cs`에서 세 곳(`PlaceStructure`, `PlaceRelocatedBuilding`,
  프리뷰 갱신용 `Update()`)이 전부 동일한 조합의 검사를 반복 호출한다: `CanPlaceObejctAt`(그리드 겹침) +
  `IsBlocked`(물리 충돌) + `IsTooCloseToResource` + `IsInsideAlliedTerritory`(영토 판정).
- `IsInsideAlliedTerritory` → `TerritoryZone.Contains()`(`TerritoryZone.cs:123`)는 핀들의 **X/Z만
  쓰는 2D 다각형 point-in-polygon 판정**이고 Y(높이)는 아예 무시함. 그래서 영토 다각형이 지상과 언덕
  꼭대기를 XZ 평면상 둘 다 포함하도록 그려지면, 그 사이 절벽/벽면 구간도 전부 "영토 안"으로 통과함.
- 건물의 실제 배치 높이는 `GetGroundPosition()`(`PlacementSystem.cs:347`)에서 클릭 시점의 단일 Y값
  (`mousePos.y`, 지면 레이캐스트 한 지점의 높이)을 건물 풋프린트 전체에 그대로 적용 - 즉 건물이 여러
  칸을 차지해도 바닥은 완전히 평평하다고 가정함. 풋프린트가 절벽에 걸치면(한쪽 칸은 지상, 다른 쪽 칸은
  언덕 꼭대기) 실제 지형은 그 사이에 수직/급경사 벽이 있는데 건물은 한 높이로 뻗어나가 벽을 뚫고 들어간
  것처럼 보이게 됨.
- 지형 단(段) 구분 자체는 이미 `Layer1`/`Layer2` 태그로 코드에서 참조된 전례가 있음
  (`CameraControl.SampleTerrainTier()`, `CameraControl.cs:189`) - 다만 이건 카메라 줌 전용이고 건설
  시스템과는 무관.
- `IsBlocked`(물리 OverlapBox, `blockingLayers` 대상)는 유닛/건물 등 "장애물" 콜라이더만 걸러내는
  용도라 절벽 벽면 지형 자체와는 무관 - 벽면 콜라이더가 `blockingLayers`에 없으면 여기서도 안 걸림.

## 검토한 방법 두 가지

1. **(요청하신 방법) 지형 타일 모서리에 "건설 불가 여백" 영역 지정** - Layer1/Layer2 각 지형 타일마다
   가장자리에 별도의 "건설 금지" 콜라이더나 좌표 범위를 새로 만들어 관리해야 함. 타일마다 모서리 모양이
   다르면(비정형 언덕) 일일이 수작업으로 여백을 그려 넣어야 하고, 나중에 지형을 수정하면 여백도 같이
   다시 손봐야 하는 유지보수 부담이 있음.
2. **(제안하는 방법) 건물 풋프린트가 차지하는 칸들의 실제 지면 높이를 재서, 칸들 사이 높이차가 너무 크면
   배치 불가** - 이미 있는 `IsTooCloseToResource`/`IsInsideAlliedTerritory`와 같은 자리에 검사 하나만
   추가하면 됨. 새 지형 데이터나 수작업 여백 표시가 전혀 필요 없고, "절벽에 걸쳐 있다"는 것 자체를
   직접 판정하므로 Layer1/Layer2뿐 아니라 어떤 절벽/경사 지형에도 똑같이 적용됨.

**2번(높이차 검사) 방식으로 구현할 것을 제안함** - 근본적으로 같은 문제(건물 풋프린트가 절벽에 걸침)를
더 적은 코드와 데이터 관리로 해결하고, 지형이 나중에 바뀌어도 자동으로 맞게 동작함.

## 코드 변경 (제안)

### `Assets/Scripts/BuildSystem/InputManager.cs`

지면 레이캐스트에 쓰는 레이어 마스크(`placementLayermask` - 지상/언덕 모두 포함하도록 이미 설정돼
있음)를 `PlacementSystem`에서도 재사용할 수 있도록 공개 프로퍼티만 추가:

기존 코드:
```csharp
    [SerializeField]
    private LayerMask placementLayermask;
```

변경 코드:
```csharp
    [SerializeField]
    private LayerMask placementLayermask;

    // PlacementSystem이 건물 풋프린트의 칸별 지면 높이를 잴 때 동일한 지형 레이어를 재사용하기 위한 접근자.
    public LayerMask PlacementLayerMask => placementLayermask;
```

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

새 필드 + 검사 메서드 추가 (`IsInsideAlliedTerritory` 바로 아래):

```csharp
    [Header("지형 평탄도 검사 (절벽/벽면 건설 방지)")]
    [Tooltip("건물이 차지하는 모든 칸의 지면 높이 중 최고-최저 차이가 이 값(미터)을 넘으면 배치 불가 처리한다. " +
             "영토 판정은 X/Z 평면만 보기 때문에(TerritoryZone.Contains), 영토가 지상과 언덕을 모두 포함해도 " +
             "이 검사가 절벽에 걸친 배치를 따로 막아준다.")]
    [SerializeField] private float maxFootprintHeightVariance = 1.5f;

    // 건물이 차지할 모든 칸의 지면 높이를 각각 레이캐스트로 재서, 최고-최저 높이차가
    // maxFootprintHeightVariance를 넘으면 절벽/벽면에 걸쳐 있다고 보고 배치를 막는다.
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

호출부 3곳에 검사 추가 (기존 `IsInsideAlliedTerritory` 바로 뒤):

`PlaceStructure()` (163~165번째 줄) 기존:
```csharp
        // ⭐ 아군 영토 밖이면 배치 불가
        if (!IsInsideAlliedTerritory(gridPos, data.Size))
            return;
```
변경:
```csharp
        // ⭐ 아군 영토 밖이면 배치 불가
        if (!IsInsideAlliedTerritory(gridPos, data.Size))
            return;

        // ⭐ 절벽/벽면에 걸쳐 있으면 배치 불가 (doc/0376)
        if (!IsFootprintTerrainFlat(gridPos, data.Size))
            return;
```

`PlaceRelocatedBuilding()` (289~292번째 줄) 기존:
```csharp
        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size)) return;
        if (IsBlocked(mousePos, data.Size)) return;
        if (IsTooCloseToResource(data.ID, gridPos, data.Size)) return;
        if (!IsInsideAlliedTerritory(gridPos, data.Size)) return;
```
변경:
```csharp
        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size)) return;
        if (IsBlocked(mousePos, data.Size)) return;
        if (IsTooCloseToResource(data.ID, gridPos, data.Size)) return;
        if (!IsInsideAlliedTerritory(gridPos, data.Size)) return;
        if (!IsFootprintTerrainFlat(gridPos, data.Size)) return; // 절벽/벽면에 걸쳐 있으면 배치 불가 (doc/0376)
```

`Update()` 프리뷰 유효성 판정 (513~516번째 줄) 기존:
```csharp
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
                && !IsTooCloseToResource(data.ID, gridPos, data.Size)
                && IsInsideAlliedTerritory(gridPos, data.Size);
```
변경:
```csharp
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
                && !IsTooCloseToResource(data.ID, gridPos, data.Size)
                && IsInsideAlliedTerritory(gridPos, data.Size)
                && IsFootprintTerrainFlat(gridPos, data.Size);
```

## 열린 질문

- `maxFootprintHeightVariance` 기본값을 1.5m로 임의로 잡음 - 실제 지형 스케일(언덕 높이/칸 크기)을
  몰라서 추정치임. 너무 낮으면 정상적인 완만한 경사에도 건설이 막히고, 너무 높으면 절벽을 못 거를 수
  있어 - 에디터에서 값을 보며 조절 필요.
- 이 검사는 건물이 차지하는 칸의 "중심점" 높이만 재므로, 아주 좁은 칸(1칸짜리 건물이 절벽 정중앙에
  걸치는 극단적인 경우)은 못 거를 수 있음 - 이번 요청 범위(여러 칸짜리 건물이 지상/언덕에 걸쳐 지어지는
  일반적인 경우)에서는 문제 없다고 보고 추가 세분화(칸별로 더 촘촘히 샘플링)는 하지 않음.

## 영향받는 파일 (예정)

- `Assets/Scripts/BuildSystem/InputManager.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
