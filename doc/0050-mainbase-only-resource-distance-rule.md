# 0050. 자원 최소 이격 거리(4칸) 규칙을 메인기지(CommandCenter)에만 적용 ([[0049-building-min-distance-from-resource|0049]] 범위 축소)

**날짜:** 2026-07-09

## 요청 내용
> 한가지 추가사항을 추가하자면 메인기지만 4칸 규칙에 적용받고 다른건물은 상관없이 지을수 있었으면 좋겠어

[[0049-building-min-distance-from-resource]]에서 모든 건물에 적용했던 "자원(광물/가스)으로부터 4칸 이격" 규칙을, **메인기지(커맨드센터)에만** 적용하도록 좁힌다. 다른 건물(SupplyDepot, Barracks, Factory, Spaceport, Lab)은 자원 옆에 붙여서 지어도 상관없게 된다.

## 메인기지 식별 방법
`RTSUnitController.cs`에 이미 있는 상수를 그대로 재사용한다(새 플래그 도입 불필요):
```csharp
public static class BuildingID
{
    public const int CommandCenter = 1;
    ...
}
```
`PlacementSystem`은 배치할 건물의 `BuildingData.ID`를 이미 들고 있으므로(`data.ID`), `data.ID == BuildingID.CommandCenter`로 판별한다.

## 설계안

**`PlacementSystem.cs`** — `IsTooCloseToResource`에 건물 ID를 인자로 받아, 메인기지가 아니면 즉시 통과시킨다:

```csharp
// 기존 코드
    private bool IsTooCloseToResource(Vector3Int gridPosition, Vector2Int size)
    {
        if (rtsController == null || rtsController.ResourceNodeList == null)
            return false;
```
```csharp
// 변경 코드
    private bool IsTooCloseToResource(int buildingID, Vector3Int gridPosition, Vector2Int size)
    {
        // ⭐ 메인기지(커맨드센터)만 이 규칙을 적용받는다. 다른 건물은 자원 옆에 지어도 무방.
        if (buildingID != RTSUnitController.BuildingID.CommandCenter)
            return false;

        if (rtsController == null || rtsController.ResourceNodeList == null)
            return false;
```

**호출부 2곳**에 건물 ID를 함께 전달하도록 수정:
```csharp
// 기존 코드 (PlaceStructure)
        if (IsTooCloseToResource(gridPos, data.Size))
            return;
```
```csharp
// 변경 코드 (PlaceStructure)
        if (IsTooCloseToResource(data.ID, gridPos, data.Size))
            return;
```
```csharp
// 기존 코드 (Update, 프리뷰 갱신)
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size)
                && !IsTooCloseToResource(gridPos, data.Size);
```
```csharp
// 변경 코드 (Update, 프리뷰 갱신)
            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
                && !IsBlocked(mousePos, data.Size)
                && !IsTooCloseToResource(data.ID, gridPos, data.Size);
```

## 참고
- `minDistanceFromResource`(4칸) 값과 거리 계산 방식(원형/유클리드)은 [[0049-building-min-distance-from-resource|0049]] 그대로 유지 — 적용 대상(모든 건물 → 메인기지만)만 좁힘.
- `BuildingID`는 `RTSUnitController` 내부의 `public static class`라 `PlacementSystem`에서 `RTSUnitController.BuildingID.CommandCenter`로 접근 가능(별도 using 불필요, 같은 네임스페이스).

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 설계안 그대로 `PlacementSystem.cs`에 반영함.
