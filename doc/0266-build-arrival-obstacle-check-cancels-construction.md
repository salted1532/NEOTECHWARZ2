# 0266 - 일꾼 도착 시 장애물 재검사 → 건설 취소 + 환불 + 실패 음성

**날짜:** 2026-07-28

## 요청 내용

> 건설모드에서 건물위치도 괜찮아서 프리뷰를 설치했는데 일꾼이 건물을 지으러 갔는데 그 위치에
> 장애물(유닛이나 건물, 지형지물)이 있으면 건설 실패로 간주해서 건설 실패 음성이랑 건물 건설 명령이
> 취소되면서 환불이랑 프리뷰도 없어지게 해줘

## 조사 내용

- `PlacementSystem.PlaceStructure()`는 클릭하는 그 순간에만 `IsBlocked(mousePos, data.Size)`로
  장애물 검사를 한다(153~155번 줄). 검사를 통과하면 그리드를 예약하고 일꾼을
  `worker.GoBuild(spawnPos, onArrived: () => StartConstruction(...), onCancelled: ...)`으로 보낸다.
- `StartConstruction`(도착 콜백)은 지금까지 도착 시점에 **장애물을 다시 검사하지 않고** 무조건
  `BaseStructure`를 생성했다 - 즉 일꾼이 걸어가는 동안 그 자리에 다른 유닛이 멈춰서거나 다른 건물이
  들어서도 그냥 겹쳐서 건물이 생기는 버그가 있었다.
- `onCancelled` 콜백(다른 명령으로 이동이 취소된 경우)은 이미 `CancelReservedConstruction(gridPos, ghost)`
  (고스트 제거 + 그리드 예약 해제) + `rtsController.RefundBuilding(data.ID)`(환불)를 처리하고 있어서,
  같은 패턴을 "도착 시 장애물 발견" 케이스에도 그대로 재사용할 수 있었다.
- `UnitController.BuildTick()`을 보면 `onArrived` 콜백이 실행되기 *전에* 이미 `UnitcurrentState = UnitState.Idle`,
  `hasBuildOrder = false`로 정리돼 있어서, `StartConstruction`에서 실패 처리로 일찍 return해도 일꾼
  쪽에 별도 롤백이 필요 없다(`isConstructing`은 `BeginConstruction()`을 호출해야만 true가 되는데,
  실패 시엔 그 호출 자체를 안 함).

## 코드 변경

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

Before:
```csharp
    private void StartConstruction(BuildingData data, Vector3 groundPos, Vector3Int gridPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);
```

After:
```csharp
    private void StartConstruction(BuildingData data, Vector3 groundPos, Vector3Int gridPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        // 일꾼이 이동하는 동안(클릭 시점엔 비어있었지만) 그 자리에 유닛/건물/지형지물 같은 장애물이
        // 새로 생겼으면 건설 실패로 취급한다 - 그대로 겹쳐 짓지 않고 실패 음성 + 취소 + 환불 처리.
        if (IsBlocked(groundPos, data.Size))
        {
            worker.GetComponent<UnitAudio>()?.PlayBuildFailVoice();
            CancelReservedConstruction(gridPos, ghost);
            rtsController?.RefundBuilding(data.ID);
            return;
        }

        if (ghost != null)
            Destroy(ghost);
```

`IsBlocked`는 기존에 클릭 시점 검사용으로 쓰던 private 메서드를 그대로 재사용(물리 박스 검사 -
`blockingLayers`에 속하는 유닛/건물/지형 콜라이더가 있으면 true). `groundPos`/`data.Size`는
`StartConstruction`에 이미 파라미터로 들어와 있어 추가 계산 없이 그대로 넘겼다.

## 요약/영향받는 파일

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`: `StartConstruction()` 맨 앞에 도착 시점 장애물
  재검사 추가.
- 동작 변화: 일꾼이 건설 위치에 도착했을 때 그 자리가 막혀있으면 - ① 담당 일꾼의
  `PlayBuildFailVoice()`(doc/0261에서 이미 채워둔 워커 전용 건설 실패 음성) 재생, ② 고스트(프리뷰)
  제거 + 그리드 예약 해제, ③ 건물 가격 전액 환불, ④ `BaseStructure`(건설 중 표시)는 아예 생성되지
  않음. 일꾼은 자동으로 Idle 상태로 남는다(별도 처리 불필요).
