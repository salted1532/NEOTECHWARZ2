# 0041. 다른 일꾼 우클릭 재배치 시 NavMeshObstacle 중심으로 못 가는 문제 수정

**날짜:** 2026-07-09

## 요청 내용
> 내가 보기엔 B가 이동해서 붙으면 건설에서 붙으면 이라는 점이 제대로 안작동하는거 같아 지금 BaseStructure가 navmesh장애물로 되어있어 그 중심점으로 갈수 없어 주변에 붙으면 건설(일꾼 건설재개)로 넘어가야 할거 같아

## 원인
`RTSUnitController.AssignBuilderToStructure()`가 새 일꾼을 `structure.transform.position`(오브젝트의 **중심점**)으로 그대로 보냄:
```csharp
worker.GoBuild(
    structure.transform.position,
    onArrived: () => worker.BeginConstruction(structure),
    onCancelled: null);
```
그런데 `BaseStructure`에는 `NavMeshObstacle`(박스, 로컬 extents 0.5 × 루트 스케일(3,1,3) = 월드 반경 약 1.5×0.5×1.5)이 붙어있어서, 그 중심점 자체는 오브젝트가 점유한 영역 한복판 — 일꾼이 실제로 도달할 수 없는 지점. 그래서 `UnitController.GoBuild`의 도착 판정(`BuildTick`, `Vector3.Distance(내위치, buildDestination) <= buildInteractRange`)이 계속 만족되지 않고, `onArrived`(=`BeginConstruction`)가 절대 호출되지 않음.

기존에 `UnitController.DistanceToTarget()`(자원 채취/반납 시 건물처럼 콜라이더가 큰 대상에 접근할 때 쓰는 헬퍼)이 이미 "피벗이 아니라 표면(가장 가까운 지점) 기준"으로 판정하는 동일한 문제를 해결해둔 전례가 있음 — 이번에도 같은 방식(`Collider.ClosestPoint`)으로 목적지 자체를 오브젝트 표면 위의 점으로 바꾸면 해결됨.

## 설계안

### `RTSUnitController.cs` — `AssignBuilderToStructure`가 콜라이더 표면의 가장 가까운 점으로 보내도록 수정

```csharp
// 기존 코드
    public void AssignBuilderToStructure(BaseStructure structure)
    {
        UnitController worker = GetSelectedWorker();
        if (worker == null)
            return;

        worker.GoBuild(
            structure.transform.position,
            onArrived: () => worker.BeginConstruction(structure),
            onCancelled: null);
    }
```
```csharp
// 변경 코드
    public void AssignBuilderToStructure(BaseStructure structure)
    {
        UnitController worker = GetSelectedWorker();
        if (worker == null)
            return;

        // NavMeshObstacle 때문에 중심점 자체엔 도달할 수 없으므로, 콜라이더 표면에서 일꾼과 가장 가까운
        // 지점을 목적지로 삼는다(자원/건물 접근 시 DistanceToTarget이 쓰는 것과 동일한 방식).
        Vector3 destination = structure.transform.position;
        if (structure.TryGetComponent<Collider>(out var collider))
            destination = collider.ClosestPoint(worker.transform.position);

        worker.GoBuild(
            destination,
            onArrived: () => worker.BeginConstruction(structure),
            onCancelled: null);
    }
```

`Collider.ClosestPoint()`는 볼록(convex) 콜라이더에서만 지원되는데, `BaseStructure`의 콜라이더는 `BoxCollider`라 항상 볼록이라 안전함. 이 표면 지점이 곧 `GoBuild`의 `buildDestination`이 되므로, `BuildTick()`의 도착 판정(`buildInteractRange = 2`)도 "표면까지의 거리"로 자연스럽게 맞아떨어짐(중심 기준으로 계산할 때 생기던 대각선 방향 오차 문제도 함께 해결됨).

## 참고
- 최초 건설(일꾼이 빈 자리로 이동해 `BaseStructure`가 그 자리에 막 생성되는 경우, `PlacementSystem.PlaceStructure`)는 이동 시작 시점에 아직 장애물이 존재하지 않으므로 영향 없음 - 이번 수정은 "이미 존재하는 BaseStructure에 다른 일꾼을 재배치"하는 경우에만 해당.

## 변경 예정 파일
- `Assets/Scripts/System/RTSUnitController.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
