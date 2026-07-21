# 0184 - ESC로 생산 대기열을 뒤(마지막 슬롯)부터 취소 - 수정 제안

## 요청

"유닛 대기열을 esc를 누르면 뒤에서부터 [4]인덱스부터 뒤에서부터 대기열 취소 되도록 해줘" — 생산
건물을 선택한 상태에서 ESC를 누르면, 대기열의 맨 앞이 아니라 **맨 뒤(가장 최근에 추가된 항목,
꽉 찼을 때 인덱스 4)**부터 하나씩 취소되도록 해달라는 요청. ESC를 여러 번 누르면 4 → 3 → 2 → ...
순서로 역순 취소되어야 함.

## 조사 내용

- `Assets/Scripts/System/RTSUnitController.cs`
  - `GetProductionQueue()` (748번 줄)와 `CancelProduction(int index)` (766번 줄 부근, 방금
    [[multi-building-production-resource-dupe-bugfix-proposal]](0183)에서 로직을 다룬 파일)는 항상
    `selectedBuildingList[0]`(현재 화면에 표시 중인 건물) 기준으로 동작 — 큐 UI 클릭 취소도 이 건물
    기준.
  - `CancelProduction(index)`는 `UnitSpawner.Cancel(index)`로 위임 → 해당 인덱스 항목을 큐에서
    제거하고 반환된 `unitID`만큼 `RefundUnit()`으로 환불한다. 인덱스만 올바르게 넘기면 "맨 뒤부터
    취소"는 이미 있는 이 경로 그대로 재사용 가능 — 큐 관련 로직을 새로 만들 필요 없음.
  - 현재 ESC 키는 생산 대기열과 아무 연결이 없다.
- `Assets/Scripts/UserControl/UserControl.cs`
  - `HandlekeyBoard()` (568번 줄)의 ESC 처리는 두 곳:
    - 582~588번 줄: 건설모드(`IsBuildMode()`)일 때 ESC → `ReturnState()` (건설모드 취소).
    - 592~598번 줄: `UsercurrentState != OrderState.None`일 때 ESC → 공격/이동/순찰/랠리 등 위치·대상
      지정 대기 상태 취소.
  - 건물을 선택한 상태(`IsBuildingSelect()`)에서 ESC를 누르는 경우는 아직 아무 처리도 없어서, 새
    분기를 추가해도 기존 두 ESC 처리와 겹치지 않는다(건설모드/주문 대기 상태와 건물 선택은 서로
    배타적인 상황).

## 계획된 코드 변경

### 1. `Assets/Scripts/System/RTSUnitController.cs`

`CancelProduction(int index)` 바로 아래에 대기열 맨 뒤 항목을 취소하는 헬퍼를 추가한다 (기존 메서드는
그대로 두고, ESC 전용 진입점만 새로 만든다).

Before:
```csharp
    //대기열 취소 (취소된 유닛 가격만큼 환불)
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        int canceledUnitID = selectedBuildingList[0].CancelProduction(index);
        RefundUnit(canceledUnitID);
    }
```

After:
```csharp
    //대기열 취소 (취소된 유닛 가격만큼 환불)
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        int canceledUnitID = selectedBuildingList[0].CancelProduction(index);
        RefundUnit(canceledUnitID);
    }

    // ESC 단축키 전용: 대기열의 "맨 뒤" 항목(가장 최근에 추가된 항목)부터 하나씩 취소한다.
    // 큐가 꽉 찼으면 인덱스 4 → 3 → 2 → ... 순서로, ESC를 누를 때마다 한 칸씩 역순 취소된다.
    public void CancelLastQueuedProduction()
    {
        IReadOnlyList<ProductionData> queue = GetProductionQueue();

        if (queue == null || queue.Count == 0)
            return;

        CancelProduction(queue.Count - 1);
    }
```

### 2. `Assets/Scripts/UserControl/UserControl.cs`

`HandlekeyBoard()`에서 건설모드 ESC 처리 바로 아래에, 건물이 선택된 상태의 ESC 처리를 추가한다.

Before:
```csharp
        if (rtsUnitController.IsBuildMode())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                rtsUnitController.ReturnState();
            }
        }

        // ESC: 공격(A)/이동(M)/순찰(P)/랠리(Y)/건물이동(M) 등 위치·대상 지정을 기다리는 대기 상태를 취소한다.
        // HandleControlGroupInput의 그룹 선택 취소 로직과 동일한 패턴(상태를 None으로 되돌리고 포인터를 끔).
        if (Input.GetKeyDown(KeyCode.Escape) && UsercurrentState != OrderState.None)
        {
            UsercurrentState = OrderState.None;

            attackPointer.SetActive(false);
            movePointer.SetActive(false);
        }
```

After:
```csharp
        if (rtsUnitController.IsBuildMode())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                rtsUnitController.ReturnState();
            }
        }

        // ESC: 생산 건물을 선택 중이면 대기열의 맨 뒤 항목부터 하나씩 취소한다 (인덱스 4 → 3 → ... 순서).
        if (rtsUnitController.IsBuildingSelect() && Input.GetKeyDown(KeyCode.Escape))
        {
            rtsUnitController.CancelLastQueuedProduction();
        }

        // ESC: 공격(A)/이동(M)/순찰(P)/랠리(Y)/건물이동(M) 등 위치·대상 지정을 기다리는 대기 상태를 취소한다.
        // HandleControlGroupInput의 그룹 선택 취소 로직과 동일한 패턴(상태를 None으로 되돌리고 포인터를 끔).
        if (Input.GetKeyDown(KeyCode.Escape) && UsercurrentState != OrderState.None)
        {
            UsercurrentState = OrderState.None;

            attackPointer.SetActive(false);
            movePointer.SetActive(false);
        }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/System/RTSUnitController.cs` (`CancelLastQueuedProduction()` 신규 추가),
  `Assets/Scripts/UserControl/UserControl.cs` (`HandlekeyBoard()`에 ESC 분기 추가).
- 기존 큐 슬롯 클릭 취소(`UpdateQueue`의 `onCancel(queueIndex)`, 특정 인덱스 직접 취소)는 그대로
  유지 — ESC는 별도의 "맨 뒤부터" 취소 경로만 추가.
- 큐가 비어있을 때 ESC를 누르면 아무 일도 일어나지 않음(`CancelLastQueuedProduction`이 조용히
  반환).
- [[multi-building-production-resource-dupe-bugfix-proposal]](0183)에서 고친 건물별 1:1 자원
  차감/환불 로직과는 무관 — `CancelProduction`은 여전히 `selectedBuildingList[0]` 하나만 대상으로
  하므로 환불 금액도 그 건물이 낸 만큼만 정확히 돌아간다.

## 확인 필요

이대로 진행해도 될까요?
