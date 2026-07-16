# 0144. 채취 중 영토 상실 시 즉시 정지로 정책 변경

## 날짜
2026-07-16

## 요청
`doc/0141`에서 "채취 중 영토 상실은 이번 왕복은 끝까지 허용"으로 결정했었는데, 실제로 디버그 조종(`doc/0143`)으로 거점 소유를 바꿔가며 테스트해보니 건물 생산은 잘 멈추지만 이미 채취 중인 일꾼은 계속 캐고 있어서, 영토를 잃으면 그 자리에서 바로 멈추도록 바꿔달라는 요청 — `doc/0141`의 해당 결정을 뒤집음.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs` (수정)

## 코드 변경

### 기존 코드 (`GatherTick()`, 노드 파괴 방어 로직 바로 앞)
```csharp
// 채취 도중(혹은 대기 중) 노드가 고갈되어 파괴된 경우(다른 유닛이 마저 캐간 경우 등) 방어
// 그냥 멈추지 않고, 자신 기준 10 거리 이내에 대체 자원이 있으면 그쪽으로 재이동한다
if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
    && gatherTargetNode == null)
{
    if (!TryRedirectToNearbyResource(null))
        CancelGathering();
```

### 변경 코드
```csharp
// 채취 중이던(이동/대기/채취 중) 노드가 영토를 잃으면(적에게 거점을 뺏기는 등) 왕복을 끝까지 두지 않고
// 그 자리에서 즉시 정지시킨다.
if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
    && gatherTargetNode != null && !TerritoryManager.IsInsideAlliedTerritory(gatherTargetNode.transform.position))
{
    StopUnit();
    return;
}

// 채취 도중(혹은 대기 중) 노드가 고갈되어 파괴된 경우(다른 유닛이 마저 캐간 경우 등) 방어
// 그냥 멈추지 않고, 자신 기준 10 거리 이내에 대체 자원이 있으면 그쪽으로 재이동한다
if ((gatherState == GatherState.MovingToResource || gatherState == GatherState.WaitingInQueue || gatherState == GatherState.Gathering)
    && gatherTargetNode == null)
{
    if (!TryRedirectToNearbyResource(null))
        CancelGathering();
```

`StopUnit()`은 이미 `CancelGatheringForNewCommand()`(대기열 자리 정리 + 반경 원상복구 + `gatherState = None`)를 내부적으로 호출하고, 추가로 이동 자체를 멈추고(`navMeshAgent.isStopped = true` 등) `UnitcurrentState`를 `Idle`로 전환한다 — 그래서 새 헬퍼 없이 기존 `StopUnit()` 호출 한 번으로 "그 자리에서 즉시 정지"가 그대로 구현된다.

## 요약
- `GatherTick()`이 매 프레임 현재 채취 대상 노드(`gatherTargetNode`)가 여전히 아군 영토 안인지 재확인한다.
- 이동 중/대기 중/채취 중 어느 단계든 상관없이, 노드가 영토 밖이 되는 즉시 `StopUnit()`으로 그 자리에서 멈추고 Idle로 전환된다(대체 자원으로 재이동하지 않음 — "그 자리에서 바로 멈췄으면 좋겠다"는 요청 그대로).
- 새로 채취 명령을 내리는 것(`Gather()`)과 자동 재배정(`FindNearestAvailableResourceNode`)의 영토 밖 차단(`doc/0142`)은 그대로 유지.

## 확인/테스트 필요
유니티에서 일꾼이 채취 중(이동/대기/채취 어느 단계든)일 때 `CaptureSystem`의 `Debug Owner`를 바꿔 그 자원이 속한 영토를 잃게 만들어보고, 일꾼이 즉시 멈추고 Idle이 되는지 확인 필요.

## 비고
[[confirm_before_implementing]] — 요청이 명확하고 되돌릴 수 있는 단순 정책 변경이라 별도 확인 질문 없이 바로 반영. `doc/0141`의 "이번 왕복은 끝까지" 결정을 이 문서가 대체한다.
