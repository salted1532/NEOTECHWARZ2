# 0150. 건물 배치 Y좌표가 실제 지면이 아닌 그리드 셀 크기(2)로 양자화되는 버그

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 수정 제안**만 담고 있고
> `Assets/Scripts/BuildSystem/PlacementSystem.cs`는 아직 고치지 않았다. 확인해 주면 반영한다.

## 날짜
2026-07-16

## 요청
`TestScene`에서 `startPoint`로 스폰된 메인기지가 실제 지면이 아니라 Y축 "2"라는 값에 좌우되는 것 같다 — `startPoint` 기준으로 땅에 맞게 설치되게 해달라.

## 원인
`Grid` 컴포넌트(`Assets/Scenes/TestScene.unity`)의 `m_CellSize`가 `{x: 2, y: 2, z: 2}`로 돼 있다. 그런데 `PlacementSystem.GetGroundPosition()`이 **X/Z뿐 아니라 Y까지** 이 그리드 셀 크기로 계산한다:

```csharp
// PlacementSystem.cs:297
private Vector3 GetGroundPosition(Vector3Int gridPos, Vector2Int size)
{
    Vector3 basePos = grid.CellToWorld(gridPos); // Y도 grid.CellToWorld가 계산 (cellSize.y=2 기준)
    ...
    return basePos + centerOffset; // centerOffset.y는 항상 0
}
```

`SpawnStartingMainBase()`에서 이걸 호출하는 흐름:
```csharp
Vector3Int gridPos = grid.WorldToCell(startPoint.transform.position); // Y도 grid.WorldToCell이 cellSize.y=2로 나눠서 셀 좌표화
...
Vector3 groundPos = GetGroundPosition(gridPos, data.Size); // Y가 cellSize.y=2 배수로 다시 계산됨
Vector3 spawnPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab);
```

`startPoint`의 실제 Y는 `-1`인데, `grid.WorldToCell`이 `Y ÷ cellSize.y(2)`로 셀 좌표를 만들고 `grid.CellToWorld`가 다시 `셀좌표 × cellSize.y(2)`로 월드 좌표를 복원하면서, **원래 Y(-1)가 아니라 cellSize.y(2)의 배수로 양자화된 값(-2)**이 나와버린다. 즉 `GetGroundPosition()`은 실제 지면(터레인) 높이를 한 번도 샘플링하지 않고, 순전히 그리드 셀 격자 수학으로만 Y를 계산한다 — 원래 평평한 지형(Y≈0)에서는 이 오차가 안 보였지만, 이번에 경사로가 있는 `TestScene`에서 `startPoint`가 Y=-1인 위치에 있다 보니 오차가 그대로 드러난 것.

이 문제는 `SpawnStartingMainBase()`뿐 아니라 마우스로 직접 건물을 배치하는 `PlaceStructure()`, `PlaceRelocatedBuilding()`, 프리뷰(`Update()`), `IsBlocked()`까지 **`GetGroundPosition`/`GetPlacementWorldPosition`을 쓰는 모든 곳에 공통으로 적용되는 버그**다. 지금까지 다른 씬에서 안 보였던 건 지형이 평평해서 양자화 오차가 눈에 안 띄었을 뿐이다.

## 수정안
`GetGroundPosition`이 그리드에서 Y를 다시 계산하지 않고, **호출한 쪽이 이미 알고 있는 실제 지면 Y**(마우스 레이캐스트 결과, 또는 `startPoint.transform.position.y`)를 그대로 쓰도록 파라미터를 추가한다.

### 기존 코드
```csharp
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

private Vector3 GetPlacementWorldPosition(Vector3Int gridPos, Vector2Int size)
{
    return GetGroundPosition(gridPos, size) + Vector3.up * yOffset;
}
```

### 변경 코드
```csharp
// Y는 그리드 셀 양자화 대신 실제 지면 좌표(groundY)를 그대로 사용한다.
private Vector3 GetGroundPosition(Vector3Int gridPos, Vector2Int size, float groundY)
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

private Vector3 GetPlacementWorldPosition(Vector3Int gridPos, Vector2Int size, float groundY)
{
    return GetGroundPosition(gridPos, size, groundY) + Vector3.up * yOffset;
}
```

### 호출부 변경
- `SpawnStartingMainBase()`: `GetGroundPosition(gridPos, data.Size, startPoint.transform.position.y)`
- `PlaceStructure()`: `GetGroundPosition(gridPos, data.Size, mousePos.y)`
- `PlaceRelocatedBuilding()`: `GetGroundPosition(gridPos, data.Size, mousePos.y)`
- `Update()`(프리뷰): `GetGroundPosition(gridPos, data.Size, mousePos.y)`
- `IsBlocked(Vector3 worldPos, ...)`: `GetPlacementWorldPosition(grid.WorldToCell(worldPos), size, worldPos.y)`

`mousePos`/`worldPos`는 전부 `InputManager.GetSelectedMapPosition()`(레이캐스트로 실제 지면을 짚은 좌표)에서 온 값이라 Y가 이미 정확하다 — 그 값을 그리드 왕복 없이 그대로 흘려보내기만 하면 된다.

## 영향받는 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs` (수정 예정)

## 다음 단계
이대로 반영해도 될지 확인 부탁 — 반영되면 마우스 클릭 배치, 리프트 착륙, 시작 메인기지 스폰 전부 그리드 셀 크기와 무관하게 실제 지면 높이에 정확히 맞춰진다.

## 적용 완료
사용자 확인 후 `GetGroundPosition`/`GetPlacementWorldPosition`에 `groundY` 파라미터를 추가하고, 5개 호출부(`SpawnStartingMainBase`, `PlaceStructure`, `PlaceRelocatedBuilding`, `Update`(프리뷰), `IsBlocked`) 전부 실제 지면 Y(`startPoint.transform.position.y` 또는 `mousePos.y`/`worldPos.y`)를 넘기도록 수정 완료.

## 확인 필요 사항
유니티에서 `TestScene`을 다시 플레이해서 메인기지가 `startPoint`의 실제 지면 높이에 맞게 스폰되는지, 마우스로 건물을 배치할 때도 지형 높낮이를 잘 따라가는지 확인 부탁.
