# 0049. 건물 배치 - 자원(광물/가스) 최소 이격 거리 4칸 적용

**날짜:** 2026-07-09

## 요청 내용
> 건물을 광물 기준으로 광물에서부터 4칸 떨어진곳에서 부터 지을수 있도록 코드수정해줘

## 확인한 세부 사항 (AskUserQuestion)
- **거리 계산 방식**: 원형(유클리드) 거리. 자원 노드의 그리드 셀 좌표와 건물이 차지할 각 셀 좌표 사이 `sqrt(dx² + dz²)` 를 계산해, 4칸 미만이면 배치 불가.
- **적용 대상**: 광물(Ore) + 가스(Gas) 자원 노드 둘 다 (요청 문구는 "광물"이었지만, 확인 결과 가스도 동일하게 적용하기로 함).

## 현재 배치 판정 흐름
`PlacementSystem.cs`는 클릭 시(`PlaceStructure`)와 마우스 이동 시(`Update`, 프리뷰 갱신) 두 곳에서 배치 가능 여부를 판정한다:
1. `StructureData.CanPlaceObejctAt(gridPos, data.Size)` - 다른 건물과 그리드 셀이 겹치는지
2. `IsBlocked(mousePos, data.Size)` - `blockingLayers`에 속한 콜라이더(유닛/장애물)와 물리적으로 겹치는지

여기에 3번째 조건으로 **자원 노드와의 최소 거리**를 추가한다. 자원 노드 목록은 `RTSUnitController.ResourceNodeList`(모든 `ResourceNode`가 `Start()`에서 자동 등록)를 그대로 사용한다.

## 설계안

**`PlacementSystem.cs`**

```csharp
// 기존 코드
    [SerializeField] 
    private float yOffset = 1f;

    private int selectedObjectIndex = -1;
```
```csharp
// 변경 코드
    [SerializeField] 
    private float yOffset = 1f;

    // ⭐ 자원(광물/가스) 노드로부터 최소 이격 거리 (그리드 칸 단위, 원형/유클리드 거리)
    [SerializeField]
    private float minDistanceFromResource = 4f;

    private int selectedObjectIndex = -1;
```

```csharp
// 기존 코드
        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
            return;

        // ⭐ 유닛 체크 추가
        if (IsBlocked(mousePos, data.Size))
            return;
```
```csharp
// 변경 코드
        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
            return;

        // ⭐ 유닛 체크 추가
        if (IsBlocked(mousePos, data.Size))
            return;

        // ⭐ 자원(광물/가스)과 너무 가까우면 배치 불가
        if (IsTooCloseToResource(gridPos, data.Size))
            return;
```

```csharp
// 기존 코드
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size) && !IsBlocked(mousePos, data.Size);
```
```csharp
// 변경 코드
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size)
                && !IsTooCloseToResource(gridPos, data.Size);
```

**신규 메서드** (`IsBlocked` 근처에 추가):
```csharp
    // 건물이 차지할 모든 셀 중 하나라도 자원(광물/가스) 노드와 minDistanceFromResource(칸, 원형 거리)보다
    // 가까우면 true를 반환한다. 자원 노드는 단일 대표 셀(자신의 위치가 속한 그리드 셀)로 취급한다.
    private bool IsTooCloseToResource(Vector3Int gridPosition, Vector2Int size)
    {
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
                float distance = Mathf.Sqrt(dx * dx + dz * dz);

                if (distance < minDistanceFromResource)
                    return true;
            }
        }

        return false;
    }
```

## 참고
- `GridData.CalculatePositionsPublic`이 이미 공개돼 있어(기존 `PlacementSystem`에서 쓰인 적은 없었지만) 그대로 재사용.
- `minDistanceFromResource`는 인스펙터에서 조정 가능한 `[SerializeField]`로 노출 (기획 조정 여지를 위해 4를 하드코딩하지 않음).
- 자원 노드가 채취로 인해 파괴되면 `ResourceNodeList`에서 자동으로 정리되므로(`RemoveAll(node => node == null)`), 별도 처리 불필요.

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 설계안 그대로 `PlacementSystem.cs`에 반영함.
