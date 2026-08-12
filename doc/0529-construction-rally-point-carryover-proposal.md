# 0529 - 건설 중인 건물 우클릭 랠리 포인트 지정 + 완공 건물로 이관

## 결과
사용자 확인 후 제안대로 구현 완료. 아래 "코드 변경" 섹션과 동일하게 4개 파일에 반영됨:
`BuildingController.cs`, `BaseStructure.cs`, `RTSUnitController.cs`, `UserControl.cs`.

Unity 컴파일 확인 완료(에러 0, 경고 0).

## 날짜
2026-08-12

## 요청 내용
"건설중인 건물에도 우클릭이 가능하고 랠리 포인트를 저장하고 있다가 지어진 건물에도 랠리 포인트를 넘겨줬으면 좋겠어"

## 조사 내용
- 완공된 생산 건물(`BuildingController`)은 이미 "선택 후 땅 우클릭 = 랠리 포인트 지정"이 가능하다.
  `UserControl.IssueRightClickMoveAt()` (`Assets\Scripts\UserControl\UserControl.cs:819-837`)이
  `rtsUnitController.IsBuildingSelect()`일 때 `SetRallySelectBuilding(groundPoint)`를 호출해 처리한다.
  이 메서드는 이 화면 우클릭과 미니맵 우클릭이 공유한다.
- 반면 건설 중인 건물 기반(`BaseStructure`, `Assets\Scripts\Building\BaseStructure.cs`)이 선택된
  상태(`SelectState.BaseStructureSelect`)에서는 랠리 포인트 개념 자체가 없다. `IssueRightClickMoveAt()`는
  `IsUnitSelect()`/`IsBuildingSelect()`만 분기하므로, `BaseStructure` 선택 중 땅을 우클릭해도 아무 일도
  일어나지 않는다. UI 커맨드 패널(`RTSUnitController.cs:2186-2210`)에도 취소(`cmd.cancel.title`) 버튼만
  있고 랠리 버튼이 없다.
- 건설이 완료되면 `BaseStructure.CompleteConstruction()`(`BaseStructure.cs:212-249`)이 완공 건물 프리팹을
  `Instantiate`하고, 그 직후(=완공 건물 자신의 `Start()`가 돌기 전) `SetGridInfo(gridPosition)`을 호출해
  그리드 좌표를 넘겨준다. `BuildingController.Start()`(`BuildingController.cs:130-131`)는 그 뒤에 실행되며,
  현재는 `RallyPosition`을 항상 `transform.position + (0,0,-2)`로 덮어써 버린다. 즉 지금 구조에서
  `BaseStructure`에 랠리를 저장해 `SetGridInfo`처럼 완공 건물에 넘겨줘도, 뒤이어 실행되는 `Start()`가
  그 값을 곧바로 기본값으로 되돌려버린다 - 이 부분을 함께 고쳐야 이관이 실제로 유지된다.

## 설계 (제안)
기존 "완공 건물 선택 + 땅 우클릭 = 랠리 지정" 패턴을 `BaseStructure`에도 동일하게 적용하고, 저장된 값을
완공 시점에 넘겨준다.

1. `BaseStructure`에 `RallyPosition`/`SetRallyPosition()`/`GetRallyPos()`를 `BuildingController`와
   동일한 형태로 추가한다. 기본값은 동일하게 `transform.position + (0,0,-2)`. 사용자가 실제로 지정했는지
   여부를 `hasCustomRally` 플래그로 구분해, 지정한 적이 없으면 완공 건물이 원래 하던 대로 자체 기본값을
   쓰게 둔다(굳이 매번 이관할 필요 없음).
2. `RTSUnitController`에 `IsBaseStructureSelect()`(다른 `IsXSelect()`와 동일한 패턴)와
   `SetRallySelectedStructure(Vector3)`(`selectedBaseStructure?.SetRallyPosition(position)`)를 추가한다.
3. `UserControl.IssueRightClickMoveAt()`에 `IsBaseStructureSelect()` 분기를 추가해 땅 우클릭 시
   `SetRallySelectedStructure(groundPoint)`를 호출한다. 이 함수는 메인 화면/미니맵 우클릭이 공유하므로
   추가 배선 없이 둘 다 자동으로 지원된다.
4. `BaseStructure.CompleteConstruction()`에서 `hasCustomRally`가 true면, `Instantiate` 직후(=완공 건물의
   `Start()`가 돌기 전) `builtController.SetRallyPosition(RallyPosition)`을 호출해 이관한다.
5. `BuildingController`가 이 이관 값을 자기 `Start()`에서 덮어쓰지 않도록, `rallyInitialized` 플래그를
   추가해 `SetRallyPosition()` 호출 여부를 기억하고, `Start()`는 `!rallyInitialized`일 때만 기본값을
   계산하게 바꾼다.

건설 중 일꾼이 죽어서 건설이 일시정지되거나 담당 일꾼이 교체되는 것과는 무관한 필드라 그 흐름은 건드리지
않는다. 리프트/착륙 등 완공 후 로직과도 독립적(랠리는 완공 시 한 번만 이관, 이후는 완공 건물이 스스로 관리).

## 코드 변경 (제안)

### Assets\Scripts\Building\BuildingController.cs
기존 코드:
```csharp
    // 생산된 유닛이 스폰 후 이동할 집결 지점
    private Vector3 RallyPosition;
```
변경 코드:
```csharp
    // 생산된 유닛이 스폰 후 이동할 집결 지점
    private Vector3 RallyPosition;
    private bool rallyInitialized; // true면 SetRallyPosition()으로 이미 값이 세팅됨(건설 중 BaseStructure에서
                                    // 이관된 값 포함) - Start()가 기본값으로 덮어쓰지 않도록 구분
```

기존 코드 (`Start()`):
```csharp
        // 기본 랠리 포인트는 건물 앞쪽(약간 -Z 방향)으로 설정
        RallyPosition = transform.position + new Vector3(0, 0, -2f);
```
변경 코드:
```csharp
        // 기본 랠리 포인트는 건물 앞쪽(약간 -Z 방향)으로 설정. 건설 중이던 BaseStructure에서 랠리 포인트를
        // 이관받은 경우(SetRallyPosition이 Start()보다 먼저 호출됨, doc/0529) 여기서 덮어쓰지 않는다.
        if (!rallyInitialized)
            RallyPosition = transform.position + new Vector3(0, 0, -2f);
```

기존 코드:
```csharp
    // 랠리 포인트(신규 생산 유닛의 집결지)를 지정 위치로 변경한다.
    public void SetRallyPosition(Vector3 position)
    {
        RallyPosition = position;
    }
```
변경 코드:
```csharp
    // 랠리 포인트(신규 생산 유닛의 집결지)를 지정 위치로 변경한다.
    public void SetRallyPosition(Vector3 position)
    {
        RallyPosition = position;
        rallyInitialized = true;
    }
```

### Assets\Scripts\Building\BaseStructure.cs
기존 코드 (필드):
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)
```
변경 코드:
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)

    // 생산된 유닛이 스폰 후 이동할 집결 지점 (건설 중에 우클릭으로 미리 지정 가능, 완공 시 완공 건물로 이관, doc/0529)
    private Vector3 RallyPosition;
    private bool hasCustomRally; // 플레이어가 실제로 랠리를 지정한 적이 있는지 (없으면 완공 건물이 자체 기본값을 쓰게 둠)
```

기존 코드 (`Start()`):
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```
변경 코드:
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        // 기본 랠리 포인트는 완공 건물과 동일한 공식(건물 앞쪽 -Z 방향) - 아직 지정 전 상태의 기본값일 뿐,
        // hasCustomRally가 true가 되기 전까지는 완공 시 이관하지 않는다.
        RallyPosition = transform.position + new Vector3(0, 0, -2f);
    }
```

기존 코드 (`CompleteConstruction()`):
```csharp
            if (obj.TryGetComponent<BuildingController>(out var builtController))
                builtController.SetGridInfo(gridPosition); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록 전달
```
변경 코드:
```csharp
            if (obj.TryGetComponent<BuildingController>(out var builtController))
            {
                builtController.SetGridInfo(gridPosition); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록 전달

                // 건설 중 우클릭으로 랠리 포인트를 지정해뒀다면 완공 건물에 그대로 이관한다 (doc/0529).
                // 완공 건물 자신의 Start()가 아직 돌기 전이라(Instantiate 직후 동기 호출), 여기서 세팅해두면
                // Start()의 기본값 계산이 이 값을 덮어쓰지 않는다(BuildingController.rallyInitialized 참고).
                if (hasCustomRally)
                    builtController.SetRallyPosition(RallyPosition);
            }
```

새 메서드 추가 (`GetIcon()` 근처 등 다른 getter들과 함께):
```csharp
    // 랠리 포인트(신규 생산 유닛의 집결지)를 지정 위치로 변경한다 (건설 중 우클릭, doc/0529).
    public void SetRallyPosition(Vector3 position)
    {
        RallyPosition = position;
        hasCustomRally = true;
    }

    public Vector3 GetRallyPos() => RallyPosition;
```

### Assets\Scripts\System\RTSUnitController.cs
`SetRallySelectBuilding()` 근처에 추가:
```csharp
    public void SetRallySelectBuilding(Vector3 position)
    {
        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            selectedBuildingList[i].SetRallyPosition(position);
        }
    }

    // 건설 중인 건물(BaseStructure)이 선택된 상태에서 우클릭으로 랠리 포인트를 지정한다 (doc/0529).
    public void SetRallySelectedStructure(Vector3 position)
    {
        selectedBaseStructure?.SetRallyPosition(position);
    }
```

`IsBuildingSelect()` 근처에 추가:
```csharp
    public bool IsUnitSelect() => RTScurrentSate == SelectState.UnitSelect;
    public bool IsBuildingSelect() => RTScurrentSate == SelectState.BuildingSelect;
    public bool IsBaseStructureSelect() => RTScurrentSate == SelectState.BaseStructureSelect;
    public bool IsBuildMode() => RTScurrentSate == SelectState.BuildMode;
```

### Assets\Scripts\UserControl\UserControl.cs
기존 코드 (`IssueRightClickMoveAt()`):
```csharp
    public void IssueRightClickMoveAt(Vector3 groundPoint)
    {
        if (rtsUnitController.IsUnitSelect())
        {
            rtsUnitController.MoveSelectedUnits(groundPoint);
            ShowMovePointer(groundPoint);
        }

        if (rtsUnitController.IsBuildingSelect())
        {
            // 선택된 건물이 공중에 떠 있으면 공중유닛처럼 그 지점으로 이동시키고, 지상 건물이면 기존처럼 랠리 포인트를 지정한다.
            if (rtsUnitController.IsSelectedBuildingLifted())
                rtsUnitController.MoveSelectedLiftedBuilding(groundPoint);
            else
                rtsUnitController.SetRallySelectBuilding(groundPoint);

            ShowMovePointer(groundPoint);
        }
    }
```
변경 코드:
```csharp
    public void IssueRightClickMoveAt(Vector3 groundPoint)
    {
        if (rtsUnitController.IsUnitSelect())
        {
            rtsUnitController.MoveSelectedUnits(groundPoint);
            ShowMovePointer(groundPoint);
        }

        if (rtsUnitController.IsBuildingSelect())
        {
            // 선택된 건물이 공중에 떠 있으면 공중유닛처럼 그 지점으로 이동시키고, 지상 건물이면 기존처럼 랠리 포인트를 지정한다.
            if (rtsUnitController.IsSelectedBuildingLifted())
                rtsUnitController.MoveSelectedLiftedBuilding(groundPoint);
            else
                rtsUnitController.SetRallySelectBuilding(groundPoint);

            ShowMovePointer(groundPoint);
        }

        // 건설 중인 건물(BaseStructure)이 선택된 상태에서도 완공 건물과 동일하게 땅 우클릭으로 랠리
        // 포인트를 지정할 수 있게 한다 - 완공 시 그 값이 완공 건물로 그대로 이관된다 (doc/0529).
        if (rtsUnitController.IsBaseStructureSelect())
        {
            rtsUnitController.SetRallySelectedStructure(groundPoint);
            ShowMovePointer(groundPoint);
        }
    }
```

## 영향받는 파일
- `Assets\Scripts\Building\BuildingController.cs`
- `Assets\Scripts\Building\BaseStructure.cs`
- `Assets\Scripts\System\RTSUnitController.cs`
- `Assets\Scripts\UserControl\UserControl.cs`

## 스코프 밖(안 하는 것)
- 랠리 버튼(UI 커맨드 패널)을 `BaseStructure` 선택 시 추가하는 것은 하지 않음 - 완공 건물도 버튼 없이
  "선택 + 땅 우클릭"만으로 랠리가 되므로, 동일한 방식(우클릭만)으로 파리티를 맞추는 것이 요청 내용과
  기존 UX 패턴 둘 다에 부합. 별도로 원하시면 말씀해주세요.
- 건설 중인 건물을 다른 건물/유닛에 랠리(예: 자원 반납 지점처럼 특정 건물을 랠리 대상으로)하는 기능은
  완공 건물도 지원하지 않는 범위라 이번에도 추가하지 않음(순수 좌표 랠리만).

