# 0056. 버그수정: 건물 리프트 2번째 이륙 시 KeyNotFoundException

**날짜:** 2026-07-12

## 요청 내용
> 건물 이동에서 버그 발견: 처음 이륙/착륙은 잘 되는데, 2번째 이륙 시 바로 착륙해버리고, 그 상태에서 다시 이륙하려고 하면 `KeyNotFoundException: The given key '(2, 0, -21)' was not present in the dictionary.`가 `GridData.RemoveObjectAt` → `PlacementSystem.ReleaseBuildingGrid` → `BuildingController.LiftOff`에서 발생함.

## 원인 분석
`BuildingController.Land()`가 착륙을 마무리하면서 `isLifted`만 `false`로 되돌리고, 비행 단계를 나타내는 `isAscending` / `isFlyingToDestination` / `isDescending` 세 플래그는 리셋하지 않았다.

`Land()`는 항상 `isDescending == true`인 상태에서만 호출되는데(`UpdateLiftedMovement()`의 하강 분기에서만 호출됨), 착륙 후에도 `isDescending`이 `true`로 남아있는 게 문제의 시작점이다.

### 재현 순서
1. **1차 이륙 → 착륙**: 정상 동작. 하지만 착륙 직후 `isDescending = true`가 리셋되지 않고 그대로 남는다(지상 상태에선 `Update()`의 `if (isLifted)` 가드에 걸려 당장은 티가 안 남).
2. **2차 이륙** (`LiftOff()`): `isAscending = true`로 상승을 시작한다.
3. 목표 고도에 도달해 `isAscending = false`가 되면, `UpdateLiftedMovement()`는 `isFlyingToDestination`(false)을 건너뛰고 **1번에서 리셋되지 않은 `isDescending`(true) 분기로 바로 진입**한다.
4. 그 결과 플레이어가 착륙 위치를 고르지 않았는데도 옛날 `flightDestination`(=1차 착륙 지점, 즉 방금 이륙한 바로 그 자리)으로 곧장 "하강"해 `Land()`가 다시 호출된다 — 이것이 사용자가 본 "2번째 이륙 시 바로 착륙"이다.
5. 이 자동 재착륙은 `PlacementSystem.PlaceRelocatedBuilding()`을 거치지 않으므로 `StructureData.AddObjectAt()`으로 그리드에 실제로 재등록되지 않는다. 그런데 `Land()`는 무조건 `hasGridPosition = true`로 표시해버려서, **실제로는 등록 안 된 셀인데 등록됐다고 착각하는 상태**가 된다.
6. 이 상태에서 3번째로 `LiftOff()`를 시도하면 `hasGridPosition == true`라서 `placementSystem.ReleaseBuildingGrid(gridPosition)`을 호출하는데, 그 셀은 5번 단계에서 실제로는 `StructureData` 딕셔너리에 없는 키라서 `GridData.RemoveObjectAt()`의 `placedObjects[gridPosition]` 조회가 `KeyNotFoundException`을 던진다.

## 수정 내용

### `Assets/Scripts/Building/BuildingController.cs` — `Land()`
```csharp
// 기존 코드
    private void Land()
    {
        isLifted = false;

        gridPosition = pendingGridPosition;
        hasGridPosition = true;

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = true;

        System.Action landed = onRelocationLanded;
        onRelocationLanded = null;
        onRelocationCancelled = null;

        landed?.Invoke();
    }
```
```csharp
// 변경 코드
    private void Land()
    {
        isLifted = false;
        isAscending = false;
        isFlyingToDestination = false;
        isDescending = false; // 착륙 후에도 이 플래그가 남아있으면 다음 이륙 시 목표 고도 도달과 동시에
                               // 바로 이 하강 분기로 다시 진입해버려서(옛 flightDestination으로 즉시 재착륙),
                               // 실제로는 그리드에 등록되지 않은 자리를 등록된 것처럼 표시하는 버그가 생긴다.

        gridPosition = pendingGridPosition;
        hasGridPosition = true;

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = true;

        System.Action landed = onRelocationLanded;
        onRelocationLanded = null;
        onRelocationCancelled = null;

        landed?.Invoke();
    }
```

## 상태
**적용 완료.**
