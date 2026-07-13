# 0092 - 건설모드 중 우클릭 이동 명령 허용 (제안)

**날짜:** 2026-07-13

## 요청 내용

> 일꾼이 건설모드로 들어갔을때 아무 명령이 안들어가는거에서 우클릭 이동명령은 들어갔으면 좋겠어

건설모드(Build 버튼을 눌러 건물 목록이 뜨거나, 건물 하나를 골라 배치 프리뷰가 마우스를 따라다니는 상태) 중에는 우클릭을 해도 아무 반응이 없다. 이동/추적/공격 명령만이라도 우클릭으로 들어가게 해달라는 요청.

## 조사 내용

- `UserControl.HandleRightClick()` (`Assets/Scripts/UserControl/UserControl.cs:371`)의 모든 분기(아군 따라가기, 적 공격, 땅 이동, 건물 클릭, 자원 채취)가 `rtsUnitController.IsUnitSelect()` 또는 `IsBuildingSelect()`를 조건으로 검사한다.
- `RTSUnitController.RTScurrentSate`가 `SelectState.BuildMode`인 동안은 `IsUnitSelect()`/`IsBuildingSelect()`가 모두 `false`이므로, 우클릭의 모든 분기가 그냥 통과되어 아무 일도 일어나지 않는다. 이게 "건설모드 중 아무 명령도 안 들어가는" 현상의 원인.
- `SelectUnit()` (`RTSUnitController.cs:172`)은 `IsBuildMode()`일 때 새로운 유닛 선택을 막기 때문에, 건설모드에 진입한 동안에도 `selectedUnitList`(건설을 맡을 일꾼)는 그대로 유지된다 — 즉 건설모드 중에 우클릭으로 이동 명령을 내려도 "누구를 이동시킬지"는 이미 selectedUnitList에 살아있다.
- 건설 패널의 "Cancel" 버튼(`RTSUnitController.cs:1132-1140`)은 `PlacementSystem.StopPlacement(); ReturnState();` 두 줄로 배치 프리뷰를 지우고 상태를 `SelectState.UnitSelect`로 되돌린다. ESC 키도 동일한 효과를 두 경로(InputManager.OnExit → PlacementSystem.StopPlacement, UserControl 키보드 처리 → ReturnState)로 낸다.

## 계획한 코드 변경

건설모드 중 우클릭이 들어오면, 기존 "Cancel" 버튼과 동일하게 배치 프리뷰를 취소하고 상태를 `UnitSelect`로 되돌린 다음, 이어서 원래 있던 우클릭 처리 로직(이동/추적/공격 등)을 그대로 태운다. 이렇게 하면 각 분기를 중복 작성할 필요 없이, 건설모드 중 우클릭이 "배치 취소 + 그 자리에서 원래 우클릭 명령 수행"으로 자연스럽게 동작한다.

### 1. `RTSUnitController.cs` - 건설모드 취소를 재사용 가능한 메서드로 추출

기존 코드 (`RTSUnitController.cs:1132-1140`):
```csharp
                    ButtonAction.Simple(
                        () =>
                        {
                            PlacementSystem.StopPlacement();
                            ReturnState();
                        },
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>T</color>]",
                        KeyCode.T));
```

변경 코드:
```csharp
                    ButtonAction.Simple(
                        CancelBuildMode,
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>T</color>]",
                        KeyCode.T));
```

그리고 `#region RTSController 상태 전환` (`RTSUnitController.cs:1158-1171`)에 메서드 추가:

기존 코드:
```csharp
    //건설모드 진입
    public void BuildModeOn()
    {
        RTScurrentSate = SelectState.BuildMode;
    }
    //상태 초기화
    public void ReturnState()
    {
        RTScurrentSate = SelectState.UnitSelect;
    }
```

변경 코드:
```csharp
    //건설모드 진입
    public void BuildModeOn()
    {
        RTScurrentSate = SelectState.BuildMode;
    }
    //상태 초기화
    public void ReturnState()
    {
        RTScurrentSate = SelectState.UnitSelect;
    }
    // 건설모드(배치 프리뷰 포함) 취소 - Cancel 버튼과 우클릭 명령 가로채기가 공유해서 쓴다.
    public void CancelBuildMode()
    {
        PlacementSystem.StopPlacement();
        ReturnState();
    }
```

### 2. `UserControl.cs` - 우클릭 처리 진입부에서 건설모드 취소

기존 코드 (`UserControl.cs:371-387`):
```csharp
    private void HandleRightClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit unitHit;
        RaycastHit groundHit;
        RaycastHit enemyHit;
        RaycastHit BuildingHit;
        RaycastHit OreHit;
        RaycastHit GasHit;

        bool clickedUnit = Physics.Raycast(ray, out unitHit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out groundHit, Mathf.Infinity, layerGround);
        bool clickedEnemy = Physics.Raycast(ray, out enemyHit, Mathf.Infinity, layerEnemy);
        bool clickedBuilding = Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding);
        bool clickedOre = Physics.Raycast(ray, out OreHit, Mathf.Infinity, layerOre);
        bool clickedGas = Physics.Raycast(ray, out GasHit, Mathf.Infinity, layerGas);
```

변경 코드:
```csharp
    private void HandleRightClick()
    {
        // 건설모드(배치 프리뷰 포함) 중 우클릭 = 배치 취소 + 그 자리에서 원래 우클릭 명령(이동/추적/공격 등) 수행.
        // ReturnState()로 UnitSelect로 되돌리면 아래 기존 분기들이 selectedUnitList(건설 맡던 일꾼)에 대해 그대로 동작한다.
        if (rtsUnitController.IsBuildMode())
            rtsUnitController.CancelBuildMode();

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit unitHit;
        RaycastHit groundHit;
        RaycastHit enemyHit;
        RaycastHit BuildingHit;
        RaycastHit OreHit;
        RaycastHit GasHit;

        bool clickedUnit = Physics.Raycast(ray, out unitHit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out groundHit, Mathf.Infinity, layerGround);
        bool clickedEnemy = Physics.Raycast(ray, out enemyHit, Mathf.Infinity, layerEnemy);
        bool clickedBuilding = Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding);
        bool clickedOre = Physics.Raycast(ray, out OreHit, Mathf.Infinity, layerOre);
        bool clickedGas = Physics.Raycast(ray, out GasHit, Mathf.Infinity, layerGas);
```

## 영향받는 파일

- `Assets/Scripts/System/RTSUnitController.cs` (Cancel 버튼 로직을 `CancelBuildMode()` 메서드로 추출)
- `Assets/Scripts/UserControl/UserControl.cs` (`HandleRightClick()` 진입부에서 건설모드면 취소 후 계속 진행)

## 참고

- 건물 목록 패널만 열려 있고(배치 프리뷰 시작 전) 우클릭해도 동일하게 취소 + 이동이 적용된다 (`PlacementSystem.StopPlacement()`는 `selectedObjectIndex < 0`이어도 안전하게 아무 것도 안 함).
- 왼쪽 클릭(배치 확정)이나 ESC(취소만) 동작은 이번 변경과 무관하게 그대로 유지.
- 아직 프로젝트 파일에는 반영하지 않음 — 승인 시 위 변경 그대로 적용 예정.
