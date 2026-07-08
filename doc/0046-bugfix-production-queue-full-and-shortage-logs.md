# 0046. 버그수정: 대기열 가득참에도 자원 소모 + 부족 사유 로그 추가

**날짜:** 2026-07-09

## 요청 내용
> 현재 발견된 버그는 유닛생산시 대기열이 가득차있는데도 생산 명령이 들어가서 자원이랑 인구수가 사용되는데 대기열이 다 차있으면 해당 명령은 반환해주고 로그도로 debug.log()로 대기열 가득참! 이런식으로 나타내주고 자원부족, 인구수부족으로 명령이 반환되면 해당하는 로그문구가 남도록 해줘

## 원인
`RTSUnitController.TryProduceUnit()`이 대기열이 꽉 찼는지는 확인하지 않고 곧바로 `resourceManager.TrySpend(...)`부터 호출해 자원/인구수를 소모한 뒤 `SpawnUnit()`을 호출하는데, 정작 `UnitSpawner.Enqueue()`는 대기열이 5개 이상이면 조용히 무시하고 끝남 - 자원은 이미 빠져나갔는데 유닛은 큐에 추가되지 않는 상태가 됨.

## 변경 내용
- **`UnitSpawner.cs`**: 하드코딩돼있던 `5`를 `MaxQueueSize` 상수로 추출하고, `public bool IsQueueFull()` 추가.
- **`BuildingController.cs`**: `IsProductionQueueFull()` 추가(`UnitSpawner`에 위임, null이면 false).
- **`RTSUnitController.TryProduceUnit()`**: 자원을 소모하기 전에 먼저 `selectedBuildingList[0].IsProductionQueueFull()`을 확인 → 가득 찼으면 `Debug.Log("대기열 가득참!")` 후 그냥 반환(자원/인구 소모 없음). 그 다음 `TrySpend`가 실패하면 원인을 구분해서 `Debug.Log("자원부족!")` 또는 `Debug.Log("인구수부족!")`을 남기고 반환.

## 변경된 파일
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
