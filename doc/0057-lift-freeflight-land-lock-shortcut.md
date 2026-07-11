# 0057. 리프트 중 우클릭 자유이동 + 공중 상태 커맨드 잠금 + 단축키 L/M

**날짜:** 2026-07-12

## 요청 내용
> 현재 이륙 → 착륙 위치 지정 → 위치 이동 → 착륙 방식인데, 이륙 시 공중 유닛처럼 우클릭으로 이동하고, 착륙 위치를 정했을 때는 기존처럼(수평 이동 후 착륙) 움직이도록 코드 수정. 그리고 공중에 있을 때는 모든 건물이 Land 버튼만 활성화되도록 해서 공중에서 유닛 생산이나 연구/업그레이드가 불가능하도록. 착륙/이륙 단축키는 `L`로 통일.
>
> (추가 요청) `M`키로 이동 가능한 "이동" 버튼도 넣어달라 — 유닛의 Move 버튼(M)처럼, 공중에 뜬 건물도 커맨드 패널에서 "이동" 버튼을 누르고(또는 M 단축키) 땅을 좌클릭하면 그 지점으로 이동하도록.

## 조사 결과 (현재 코드 상태)
- [[0054-building-lift-relocate|0054]]에서 만든 흐름은 "이륙(상승) → (착륙 버튼) → 착륙 위치 클릭 → 수평 이동 → 하강 → 착륙" 하나뿐이라, 이륙한 뒤엔 착륙 버튼을 눌러야만 움직일 수 있었다. 공중유닛(`UnitController`)처럼 우클릭으로 자유롭게 위치를 옮기는 기능은 없었다.
- `BuildingController.UpdateLiftedMovement()`는 `isAscending → isFlyingToDestination → isDescending` 순서를 상태 플래그로만 구분한다. `isFlyingToDestination`이 끝나면 무조건 `isDescending = true`로 넘어가 착륙까지 가버리므로, "그냥 그 자리로 이동만 하고 계속 공중에 떠 있기"(자유 비행)와 "착륙 위치로 이동 후 실제로 착륙하기"(공식 착륙 비행)를 구분할 방법이 없다 → 이번에 `pendingLanding` 플래그로 두 종류를 구분해야 한다.
- 착륙 관련 그리드 예약/고스트는 `PlacementSystem.PlaceRelocatedBuilding()` → `BuildingController.BeginRelocationFlight(newGridPos, destination, onLanded, onCancelled)`를 통해서만 이뤄진다. 우클릭 자유이동은 이 경로를 타지 않으므로 그리드/고스트와 무관하다 — **단, 이미 공식 착륙 비행이 진행 중(그리드 예약 + 고스트가 이미 걸려있는 상태)일 때 우클릭으로 자유이동을 명령하면, 그 예약/고스트를 정리하지 않고 그냥 덮어써버리면 그리드 셀과 고스트가 영원히 남는 버그가 생긴다.** 그래서 자유이동으로 전환할 때는 [[0056-bugfix-lift-relocate-stale-descending-flag|0056]]에서 만든 `onRelocationCancelled` 콜백(그리드 해제 + 고스트 파괴)을 먼저 실행해줘야 한다.
- 건물 커맨드 패널은 `RTSUnitController.UpdateUI()`의 `SelectState.BuildingSelect` 케이스에서 항상 `BuildingSelectState`에 따라 생산 패널(`ShowMainBasePanel` 등)을 먼저 그린 뒤, 그 아래에서 리프트/착륙 버튼을 마지막 슬롯에 덧붙이는 구조다. "공중일 때 Land 버튼만" 요구사항은 이 생산 패널 그리기 자체를 건너뛰면 된다(마지막에 붙이는 리프트/착륙 버튼 코드는 그대로 둬도 자동으로 Land 버튼만 남는다).
- 우클릭 이동은 `UserControl.HandleRightClick()`의 "땅 클릭 = 명령 처리" 블록에서 `rtsUnitController.IsBuildingSelect()`일 때 지금은 무조건 `SetRallySelectBuilding`(랠리 포인트 지정)을 호출한다. 여기서 선택된 건물이 공중에 떠 있는지 분기해서, 떠 있으면 랠리 대신 이동 명령을 보내야 한다.
- 단축키는 현재 `KeyCode.G`로 돼 있다(0054에서 임시로 정함). `KeyCode.L`은 `BuildMode` 패널의 "Lab" 버튼에서도 쓰이지만, `BuildMode`와 `BuildingSelect`는 서로 다른 `RTScurrentSate`라 동시에 화면에 뜨지 않으므로 충돌 없이 재사용 가능하다(이미 0054에서 확인한 내용과 동일한 이유).
- 유닛에는 이미 "Move 버튼 → M 단축키 → 좌클릭으로 목적지 지정" 패턴이 있다(`RTSUnitController.EnterMoveMode()` → `UserControl.SetOrderState("Move")` → `HandleLeftClick()`의 `UsercurrentState == OrderState.Move` 분기가 `rtsUnitController.MoveSelectedUnits(...)` 호출). 하지만 이 `Move` 상태를 건물에 그대로 재사용하면 `MoveSelectedUnits`가 `selectedUnitList`(항상 비어있음 - 건물 선택 중이므로)를 순회해 아무 일도 안 일어난다 → 건물 전용 `OrderState.BuildingMove`를 새로 만들고, `HandleLeftClick()`에 건물 전용 분기를 추가해야 한다.
- `UpdatePointer()`는 `OrderState.Move / Patrol / Rally`일 때만 매 프레임 `movePointer`를 마우스 위치로 따라다니게 한다(`Attack`은 별도로 `attackPointer` 처리). 새 `BuildingMove` 상태도 이 목록에 추가해야 마우스를 움직이는 동안 포인터가 따라다닌다.
- 건물 커맨드 패널 슬롯은 `UIController.SetCommands(params CommandButtonData[])`가 배열 인덱스 순서대로 `slots[]`에 채우고, `ShowBuildingLiftCommand()`가 그 위에 마지막 슬롯(`slots.Length - 1`)만 따로 덮어써 리프트/착륙 버튼을 붙이는 방식이다(0054). Move 버튼은 같은 방식으로 마지막에서 두 번째 슬롯(`slots.Length - 2`)에 붙이면 기존 코드를 건드리지 않고 추가할 수 있다. 아이콘은 이미 있는 `moveIcon`(유닛 Move 버튼과 동일한 아이콘)을 그대로 재사용한다.

## 설계안

### 1. `Assets/Scripts/Building/BuildingController.cs`

**필드 추가** (`pendingGridPosition` 근처):
```csharp
// 기존 코드
    private Vector3Int pendingGridPosition;      // 착륙 예정 위치의 그리드 좌표 (비행 중에만 유효)
    private System.Action onRelocationLanded;    // 착륙 완료 시(Land) 호출 - PlacementSystem이 고스트 제거
    private System.Action onRelocationCancelled; // 착륙 전에 파괴되는 등 비행이 중단될 때 호출 - 그리드 예약 해제 + 고스트 제거
```
```csharp
// 변경 코드
    private Vector3Int pendingGridPosition;      // 착륙 예정 위치의 그리드 좌표 (비행 중에만 유효)
    private System.Action onRelocationLanded;    // 착륙 완료 시(Land) 호출 - PlacementSystem이 고스트 제거
    private System.Action onRelocationCancelled; // 착륙 전에 파괴되는 등 비행이 중단될 때 호출 - 그리드 예약 해제 + 고스트 제거

    private bool pendingLanding; // true면 현재 수평이동이 "공식 착륙 비행"(도착 시 하강→착륙까지 이어짐), false면 우클릭 자유이동(도착 시 그 자리에서 계속 공중 대기)
```

**`UpdateLiftedMovement()`의 수평이동 도착 처리 분기**:
```csharp
// 기존 코드
            if (Vector3.Distance(flatPos, flatDest) < 0.05f)
            {
                isFlyingToDestination = false;
                isDescending = true;
            }
```
```csharp
// 변경 코드
            if (Vector3.Distance(flatPos, flatDest) < 0.05f)
            {
                isFlyingToDestination = false;

                if (pendingLanding)
                    isDescending = true;
                // else: 우클릭 자유이동 도착 - 착륙하지 않고 그 자리에서 계속 공중에 떠 있는다
            }
```

**`BeginLanding()` 가드 완화** (자유 비행 중에도 착륙 위치 선택을 시작할 수 있도록. 단, 이미 착륙하러 가는 중이거나 상승/하강 중이면 여전히 막는다):
```csharp
// 기존 코드
    public void BeginLanding()
    {
        if (!isLifted || isAscending || isFlyingToDestination || isDescending)
            return;

        placementSystem?.StartBuildingRelocation(this);
    }
```
```csharp
// 변경 코드
    public void BeginLanding()
    {
        if (!isLifted || isAscending || isDescending || (isFlyingToDestination && pendingLanding))
            return;

        placementSystem?.StartBuildingRelocation(this);
    }
```

**`BeginRelocationFlight()` — 자유 비행 중이었어도 착륙 비행으로 깔끔히 전환**:
```csharp
// 기존 코드
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        flightDestination = destination;
        isFlyingToDestination = true;
    }
```
```csharp
// 변경 코드
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        isAscending = false;
        isDescending = false;
        pendingLanding = true;
        flightDestination = destination;
        isFlyingToDestination = true;
    }
```

**새 메서드 추가** (`BeginRelocationFlight` 아래, `Land()` 위): 우클릭 자유이동 + 착륙 예약 취소 헬퍼
```csharp
    // "우클릭 이동": 공중유닛처럼 그 지점 위 상공(현재 고도 유지)으로 수평 이동만 한다 - 착륙하지 않고 계속 공중에 떠 있는다.
    // 착륙 위치로 비행 중(공식 착륙 예약)이었다면 그 예약을 취소하고 자유 비행으로 전환한다.
    public void MoveWhileLifted(Vector3 groundDestination)
    {
        if (!isLifted)
            return;

        CancelPendingLandingFlight();

        isAscending = false;
        isDescending = false;
        pendingLanding = false;
        flightDestination = new Vector3(groundDestination.x, transform.position.y, groundDestination.z);
        isFlyingToDestination = true;
    }

    // 착륙 예약(공식 착륙 비행) 중이었다면 취소하고 예약해둔 그리드 셀/고스트를 정리한다.
    // (비행 중 파괴되거나, 우클릭으로 새 자유이동 명령을 받아 착륙 예약이 무효화될 때 호출)
    private void CancelPendingLandingFlight()
    {
        if (onRelocationCancelled == null)
            return;

        System.Action cancelled = onRelocationCancelled;
        onRelocationCancelled = null;
        onRelocationLanded = null;
        pendingLanding = false;

        cancelled.Invoke();
    }
```

**`Land()`에 `pendingLanding` 리셋 추가** (기존 [[0056-bugfix-lift-relocate-stale-descending-flag|0056]] 수정에 이어서):
```csharp
// 기존 코드
    private void Land()
    {
        isLifted = false;
        isAscending = false;
        isFlyingToDestination = false;
        isDescending = false; // ...(0056 주석 그대로)...
```
```csharp
// 변경 코드
    private void Land()
    {
        isLifted = false;
        isAscending = false;
        isFlyingToDestination = false;
        isDescending = false; // ...(0056 주석 그대로)...
        pendingLanding = false;
```

**`Die()` — 중복 로직을 `CancelPendingLandingFlight()`로 정리**:
```csharp
// 기존 코드
    public void Die()
    {
        // 착륙 위치로 비행 중(또는 착륙 직전)에 파괴되면, 예약해둔 그리드 셀/고스트가 영원히 남지 않도록 정리한다.
        if (onRelocationCancelled != null)
        {
            onRelocationCancelled.Invoke();
            onRelocationCancelled = null;
            onRelocationLanded = null;
        }

        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
```
```csharp
// 변경 코드
    public void Die()
    {
        CancelPendingLandingFlight(); // 착륙 위치로 비행 중(또는 착륙 직전)에 파괴되면 예약해둔 그리드 셀/고스트를 정리

        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
```

### 2. `Assets/Scripts/System/RTSUnitController.cs`

**새 메서드 추가** (`LiftSelectedBuilding`/`BeginLandingSelectedBuilding` 아래):
```csharp
    // 선택된 건물(단일 취급)이 현재 공중에 떠 있는지 (UserControl 우클릭 분기용)
    public bool IsSelectedBuildingLifted()
    {
        return selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();
    }

    // 공중에 뜬 건물을 공중유닛처럼 우클릭/Move버튼 지점으로 수평 이동시킨다 (착륙하지 않고 계속 공중에 떠 있음).
    public void MoveSelectedLiftedBuilding(Vector3 destination)
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].MoveWhileLifted(destination);
    }
```

**새 메서드 추가** (`#region UserControl 상태 전환`의 `EnterRallyMode()` 아래) — 공중 건물의 "이동" 버튼용:
```csharp
    public void EnterBuildingMoveMode()
    {
        userControl.SetOrderState("BuildingMove");
    }
```

**`UpdateUI()`의 `SelectState.BuildingSelect` 케이스 — 공중일 때 생산 패널을 건너뛰고, 단축키를 `L`로 변경**:
```csharp
// 기존 코드
                switch (BuildingSelectState)
                {
                    case BuildingState.MainBaseSelect:
                        uIController.ShowMainBasePanel( ... );
                        uIController.ShowProductionUI( ... );
                        break;

                    case BuildingState.Tier1Select:
                        ...
                    case BuildingState.Tier2Select:
                        ...
                    case BuildingState.Tier3Select:
                        ...
                    case BuildingState.SupplyDepot:
                    case BuildingState.Lab:
                    case BuildingState.None:
                        uIController.ClearPanel();
                        uIController.HideProductionUI();
                        break;
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 마지막 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (selectedBuildingList.Count > 0 && selectedBuildingList[0].CanLift())
                {
                    BuildingController building = selectedBuildingList[0];

                    uIController.ShowBuildingLiftCommand(
                        building.IsLifted(),
                        building.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>G</color>]", KeyCode.G)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>G</color>]", KeyCode.G));
                }
                break;
```
```csharp
// 변경 코드
                // 공중에 뜬 건물은 생산/연구 등 모든 커맨드를 막고 아래에서 Land 버튼만 노출한다.
                bool selectedBuildingLifted = selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();

                if (selectedBuildingLifted)
                {
                    uIController.ClearPanel();
                    uIController.HideProductionUI();
                }
                else
                {
                    switch (BuildingSelectState)
                    {
                        case BuildingState.MainBaseSelect:
                            uIController.ShowMainBasePanel( ... );
                            uIController.ShowProductionUI( ... );
                            break;

                        case BuildingState.Tier1Select:
                            ...
                        case BuildingState.Tier2Select:
                            ...
                        case BuildingState.Tier3Select:
                            ...
                        case BuildingState.SupplyDepot:
                        case BuildingState.Lab:
                        case BuildingState.None:
                            uIController.ClearPanel();
                            uIController.HideProductionUI();
                            break;
                    }
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 마지막 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (selectedBuildingList.Count > 0 && selectedBuildingList[0].CanLift())
                {
                    BuildingController building = selectedBuildingList[0];

                    uIController.ShowBuildingLiftCommand(
                        building.IsLifted(),
                        building.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>L</color>]", KeyCode.L)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>L</color>]", KeyCode.L));

                    // 공중에 뜬 상태에서만 마지막에서 두 번째 슬롯에 "이동" 버튼을 추가로 노출한다.
                    if (building.IsLifted())
                    {
                        uIController.ShowBuildingMoveCommand(
                            ButtonAction.Simple(EnterBuildingMoveMode, "Move", "Move to a location while airborne. \nshortcut key [<color=yellow>M</color>]", KeyCode.M));
                    }
                }
                break;
```
(`...`으로 표시한 4개 케이스 본문은 기존 코드 그대로 `switch` 안으로 들여쓰기만 한 단계 더 들어갑니다. 로직 자체는 바뀌지 않습니다.)

### 3. `Assets/Scripts/UI/UIController.cs`

**새 메서드 추가** (`ShowBuildingLiftCommand` 아래) — 공중 상태에서만 쓰이는 "이동" 버튼, 마지막에서 두 번째 슬롯 전용:
```csharp
    // 공중에 뜬 건물 전용 "이동" 버튼 (마지막에서 두 번째 슬롯). ShowBuildingLiftCommand(Land 버튼, 마지막 슬롯)와 함께 사용.
    public void ShowBuildingMoveCommand(ButtonAction onMove)
    {
        if (slots.Length < 2)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        slots[slots.Length - 2].SetData(new CommandButtonData(moveIcon, onMove));
    }
```

### 4. `Assets/Scripts/UserControl/UserControl.cs`

**`OrderState`에 건물 전용 이동 상태 추가**:
```csharp
// 기존 코드
    private enum OrderState
    {
        None,
        Attack,
        Move,
        Patrol,
        Rally
    }
```
```csharp
// 변경 코드
    private enum OrderState
    {
        None,
        Attack,
        Move,
        Patrol,
        Rally,
        BuildingMove // 공중에 뜬 건물의 "이동" 버튼(M) 전용 - Move와 분리해야 HandleLeftClick에서 유닛용 MoveSelectedUnits와 섞이지 않는다
    }
```

**`UpdatePointer()` — 마우스를 움직이는 동안 `movePointer`가 따라다니는 상태 목록에 추가**:
```csharp
// 기존 코드
        else if (UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally)
```
```csharp
// 변경 코드
        else if (UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally || UsercurrentState == OrderState.BuildingMove)
```

**`HandleLeftClick()`의 "땅 클릭 = 명령 처리" 블록에 건물 전용 분기 추가** (`OrderState.Rally` 처리 바로 아래):
```csharp
// 기존 코드
            if (UsercurrentState == OrderState.Rally)
            {
                rtsUnitController.SetRallySelectBuilding(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }
        }
```
```csharp
// 변경 코드
            if (UsercurrentState == OrderState.Rally)
            {
                rtsUnitController.SetRallySelectBuilding(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }

            if (UsercurrentState == OrderState.BuildingMove)
            {
                rtsUnitController.MoveSelectedLiftedBuilding(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }
        }
```

**`HandleRightClick()`의 "땅 클릭 = 명령 처리" 블록**:
```csharp
// 기존 코드
            if (rtsUnitController.IsBuildingSelect())
            {
                rtsUnitController.SetRallySelectBuilding(groundHit.point);

                UsercurrentState = OrderState.Rally;
                UpdatePointer();
                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

            }
```
```csharp
// 변경 코드
            if (rtsUnitController.IsBuildingSelect())
            {
                // 선택된 건물이 공중에 떠 있으면 공중유닛처럼 그 지점으로 이동시키고, 지상 건물이면 기존처럼 랠리 포인트를 지정한다.
                if (rtsUnitController.IsSelectedBuildingLifted())
                    rtsUnitController.MoveSelectedLiftedBuilding(groundHit.point);
                else
                    rtsUnitController.SetRallySelectBuilding(groundHit.point);

                UsercurrentState = OrderState.Rally;
                UpdatePointer();
                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

            }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **자유 비행 중 고도**: 우클릭 이동은 현재 고도를 그대로 유지한 채 수평으로만 이동합니다(공중유닛과 동일하게 상승/하강 없이 그 높이에서 계속 떠 있음).
- **자유 비행 중 Land 버튼**: 자유 비행 중에도 Land 버튼을 눌러 착륙 위치 선택 모드로 들어갈 수 있습니다(클릭하는 순간 자유 비행 목표가 착륙 목표로 교체됨). 다만 이미 "착륙 위치가 확정되어 그리로 비행/하강 중"인 상태에서는 Land 버튼이 막힙니다(재입력 시 먼저 예약된 그리드/고스트를 정리해야 하는데, 그 경로는 버튼이 아니라 우클릭 자유이동에서만 정리하도록 설계했습니다).
- **착륙行 도중 우클릭으로 딴 곳 이동**: 착륙 위치로 비행/하강하던 도중에 우클릭으로 다른 곳을 이동 명령하면, 원래 예약돼 있던 그리드 셀 해제 + 남아있던 착륙 고스트 파괴까지 함께 처리한 뒤 자유 비행으로 전환합니다([[0054-building-lift-relocate|0054]]에서 만든 `onRelocationCancelled` 콜백 재사용) — 그리드/고스트가 영구히 남는 사고를 방지.
- **공중 상태 커맨드 잠금 범위**: `MainBase/Barracks/Factory/Airport`의 생산 버튼 및 생산 대기열 UI를 전부 숨기고 Land 버튼만 남깁니다. 아직 연구/업그레이드 시스템 자체가 코드에 없어서 "연구/업그레이드 불가"는 이번엔 별도 처리할 대상이 없고, 향후 추가되더라도 이번에 만든 "공중이면 생산 패널 자체를 그리지 않는다" 구조를 그대로 따르면 자동으로 막힙니다.
- **단축키 L 충돌**: `BuildMode` 패널의 "Lab" 버튼도 `L`을 쓰지만, `BuildMode`와 `BuildingSelect`는 동시에 표시되지 않는 서로 다른 상태라 충돌하지 않습니다.
- **Move 버튼(M)과 우클릭 자유이동의 관계**: 완전히 동일한 동작(`MoveWhileLifted`)을 서로 다른 두 경로(버튼+좌클릭 / 즉시 우클릭)로 트리거할 뿐입니다 — 우클릭은 지금처럼 언제든 즉시 이동하고, Move 버튼은 유닛과 동일하게 "버튼(또는 M) 누르고 → 좌클릭으로 목적지 지정" 방식입니다. 두 경로 모두 착륙 예약 중이었다면 그리드/고스트를 정리한 뒤 자유 비행으로 전환하는 동일한 안전장치(`CancelPendingLandingFlight`)를 거칩니다.
- **Move 버튼 노출 조건**: 공중에 뜬 상태(`IsLifted() == true`)에서만 마지막에서 두 번째 슬롯에 나타납니다. 지상 상태에서는 기존처럼 마지막 슬롯의 "Lift Off" 버튼만 있습니다(생산 패널이 그 앞 슬롯들을 채움).

## 변경 예정 파일
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
