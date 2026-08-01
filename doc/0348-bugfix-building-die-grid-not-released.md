# 0348 — 버그 수정: 건물 파괴 시 그리드 점유가 안 풀림

## 요청

사용자 확인 요청: 건물이 파괴될 때 자신이 점유하던 위치를 그리드에서 제거하는 처리가 빠져있는 것 같다.

## 원인

`Assets/Scripts/Building/BuildingController.cs`의 `Die()`(HealthManager가 체력 0에서 호출하는 IDestructible 구현)가 `CancelPendingLandingFlight()`로 "착륙 비행 중 예약해둔" 그리드만 정리하고, 정작 건물이 지상에 서 있는 동안 점유해온 `gridPosition`은 한 번도 `PlacementSystem.ReleaseBuildingGrid()`로 해제하지 않았음.

같은 클래스의 `LiftOff()`는 이미 `hasGridPosition`이면 `ReleaseBuildingGrid(gridPosition)`을 호출해 자리를 비우는데, `Die()`에는 이 처리가 없어서 전투로 파괴된 건물의 그리드 셀이 영원히 "점유됨" 상태로 남아 그 자리에 다시 건물을 지을 수 없게 되는 버그였음.

## 변경 내용

`BuildingController.Die()`에 `LiftOff()`와 동일한 패턴으로 그리드 해제 처리 추가:

```csharp
if (hasGridPosition)
{
    placementSystem?.ReleaseBuildingGrid(gridPosition);
    hasGridPosition = false;
}
```

`CancelPendingLandingFlight()` 다음, 환불/리스트 정리보다 앞에 배치.

## 확인

`npx uloop-cli compile` — 에러 0, 경고 25개(전부 기존 경고, 이번 변경으로 인한 신규 경고 없음).

## 비고

`BaseStructure`(건설 중 파운데이션)의 `Die()`/`CancelConstruction()`은 이미 `onCancelledByPlayer` 콜백을 통해 예약 그리드를 정상적으로 해제하고 있어 별도 수정 불필요. 완공된 `BuildingController`만 이 버그가 있었음.
