# 0409 - 유닛의 NavMesh가 도달 가능 위치인지 구별할 수 있는가 (Q&A)

**날짜:** 2026-08-04

## 요청 내용

> 유닛의 navmesh가 도달 가능한 위치인지 아닌지를 구별할수 있어?

## 답변

가능하며, 이미 구현돼 있다. `IsPositionReachable()`(`UnitController.cs:729~734`,
`EnemyUnitController.cs` 동일 구조, [[0403]]에서 추가):

```csharp
private NavMeshPath reachabilityProbePath;
private bool IsPositionReachable(Vector3 pos)
{
    reachabilityProbePath ??= new NavMeshPath();
    return NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, reachabilityProbePath) &&
        reachabilityProbePath.status == NavMeshPathStatus.PathComplete;
}
```

`NavMesh.CalculatePath()`가 반환하는 `NavMeshPath.status`로 세 가지를 구별한다:
- `PathComplete` - 완전히 도달 가능
- `PathPartial` - 경사로 등으로 연결은 됐지만 끝까지는 도달 불가(갈 수 있는 데까지만)
- `PathInvalid` - 아예 경로가 없음(완전히 끊긴 영역)

`IsPositionReachable()`은 `PathComplete`일 때만 `true`. `NavMeshAgent.SetDestination()`과
달리 이 유닛의 실제 이동 경로/상태를 전혀 건드리지 않는 순수 조회라서, 지금 하고 있는 이동을
방해하지 않고 아무 때나(매 프레임 불러도) "이 위치까지 갈 수 있는가"만 확인할 수 있다.
[[0403]]/[[0406]]의 도달 불가 추격 판단이 이 함수로 게이트를 건다.

## 영향받는 파일

없음 (질의응답, 코드 변경 없음).
