# 0124. 좌클릭 선택을 마우스를 놓는 시점으로 확정 (드래그 우선)

**날짜:** 2026-07-15

## 요청 내용

마우스 클릭을 놓았을 때(mouse up) 비로소 선택 과정을 확정하도록 로직을 수정해달라는 요청. 마우스를 놓는 시점에 그것이 단일 클릭이었다면 단일 선택을, 드래그 범위 안에 유닛이 들어와 있었다면 유닛(드래그) 선택을 적용하고, 마우스를 놓기 전까지는 어느 쪽도 확정된 선택으로 취급하지 않아야 한다.

## 조사 내용

`doc/0123-building-click-drag-unit-priority.md`에서 건물 클릭+드래그 조합만 임시(`clickSelectedBuildingThisDrag`)로 되돌리는 방식으로 패치했지만, 이번 요청은 그 패치를 일반화해서 애초에 마우스 다운 시점에 즉시 선택을 확정하지 않고, 마우스 업 시점에 한 번에 판단하도록 구조 자체를 바꾸는 것이다. 이렇게 하면 건물뿐 아니라 적/자원/BaseStructure 클릭에도 동일하게 존재하던 잠재적 문제(클릭 즉시 선택 → 드래그로 유닛이 추가되어 "적/자원 선택 + 유닛 선택"이 동시에 남는 문제)까지 한 번에 해결된다.

현재 구조(`Assets\Scripts\UserControl\UserControl.cs`):

- **마우스 다운** → `HandleLeftClick()`에서 유닛/적/건물/BaseStructure/자원 각각을 "클릭"한 즉시 `ClickSelectX(...)`를 호출해 선택을 확정한다(`DeselectAll()` 후 해당 대상만 선택).
- **마우스 업** → `CalculateDragRect()` 후 `SelectObject()`가 `dragRect` 안에 들어온 유닛들을 `DragSelectUnit()`으로 추가한다. 이 메서드는 항상 "추가만" 하고 절대 기존 선택을 지우지 않는다.

이 구조에서 "클릭 후 드래그로 다른 대상을 덮는" 모든 조합이 뒤섞인다: 예를 들어 적/자원/BaseStructure를 클릭한 뒤 유닛들 위로 드래그를 이어가면, 클릭으로 확정된 적/자원 선택 위에 드래그로 잡힌 유닛들이 그냥 더해져 버린다 (0123에서 건물에 대해서만 고친 것과 동일한 종류의 문제).

또한 평범한 드래그 박스 선택(빈 땅에서 시작해 유닛 여러 개를 감싸는 일반적인 RTS 드래그 선택)도 현재는 "다운 시점에 무조건 즉시 선택 확정 로직이 실행되고, 업 시점엔 추가만 하는" 구조라서, Shift 없이 새로 드래그해도 기존 선택 위에 유닛이 계속 쌓이는 문제가 있다(0047에서 의도한 "Shift 없으면 새로 교체, Shift면 추가" 원칙이 지금은 "업 시점에 결정"하는 로직이 없어서 지켜지지 않고 있음).

## 계획된 코드 변경

핵심 아이디어:
- 유닛/적/건물/BaseStructure/자원을 (Shift 없이) 클릭한 경우, 그 자리에서 바로 선택하지 않고 "마우스를 놓을 때 실행할 선택 동작"만 `pendingLeftClickSelect`에 저장해둔다.
- Shift+클릭(토글/추가)과 A모드 등 명령(공격/이동/순찰/랠리) 처리는 지금처럼 즉시 실행한다(이건 "선택"이 아니라 명시적 제스처/명령이므로 대상 그대로 유지).
- 마우스를 놓는 시점(`SelectObject()`)에서 드래그 범위 안에 유닛이 하나라도 있으면: 대기 중이던 단일 클릭 선택은 버리고, Shift가 아니면 기존 선택을 지운 뒤 드래그로 잡힌 유닛들만 선택하고(Shift면 기존 선택에 추가), 드래그 범위 안에 유닛이 하나도 없으면 그제서야 대기해둔 단일 클릭 선택을 실행한다.
- 기존에 0123에서 추가했던 건물 전용 임시 필드(`clickSelectedBuildingThisDrag`)는 이 일반화된 방식으로 대체되므로 제거한다.

**파일:** `Assets\Scripts\UserControl\UserControl.cs`

### 1) 필드 교체: 건물 전용 임시값 → 범용 pending 액션

기존 코드
```csharp
    // 이번 마우스다운~업 사이클에서 (Shift 없이) 단일 클릭으로 선택한 건물.
    // 마우스 업 시 드래그 범위 안에 유닛이 걸리면 이 건물 선택을 취소하고 유닛 선택을 우선한다.
    private BuildingController clickSelectedBuildingThisDrag;
```

변경 코드
```csharp
    // (Shift 없이) 클릭으로 확정하려던 단일 선택 동작. 실제 선택은 즉시 하지 않고 마우스를 놓을 때 실행한다.
    // 마우스 업 시 드래그 범위 안에 유닛이 하나라도 걸리면 이 값은 버려지고 드래그 유닛 선택이 우선한다.
    private Action pendingLeftClickSelect;
```

### 2) 마우스 다운 시작 시 초기화 대상 변경

기존 코드
```csharp
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();
            clickSelectedBuildingThisDrag = null;

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            HandleLeftClick();
        }
```

변경 코드
```csharp
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();
            pendingLeftClickSelect = null;

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            HandleLeftClick();
        }
```

### 3) 유닛 클릭 - 즉시 선택 대신 pending 등록

기존 코드
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                    rtsUnitController.ClickSelectUnit(unit);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 2. 적 클릭 = 선택 또는 공격 명령 (A 모드 중이면 해당 적을 추격 공격, 아니면 선택)
```

변경 코드
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                    pendingLeftClickSelect = () => { if (unit != null) rtsUnitController.ClickSelectUnit(unit); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 2. 적 클릭 = 선택 또는 공격 명령 (A 모드 중이면 해당 적을 추격 공격, 아니면 선택)
```

### 4) 적 클릭 - 즉시 선택 대신 pending 등록

기존 코드
```csharp
                rtsUnitController.ClickSelectEnemy(enemy);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 3. 건물 클릭 = 선택 또는 아군 건물 강제 공격 (A 모드 중이면 해당 건물을 강제로 공격, 아니면 선택)
```

변경 코드
```csharp
                pendingLeftClickSelect = () => { if (enemy != null) rtsUnitController.ClickSelectEnemy(enemy); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 3. 건물 클릭 = 선택 또는 아군 건물 강제 공격 (A 모드 중이면 해당 건물을 강제로 공격, 아니면 선택)
```

### 5) 건물 클릭 - 즉시 선택 대신 pending 등록 (0123의 임시 필드 대체)

기존 코드
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectBuilding(building);
                else
                {
                    rtsUnitController.ClickSelectBuilding(building);
                    clickSelectedBuildingThisDrag = building;
                }

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }

            // 건설 중인 BaseStructure 좌클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 강제로 공격, 아니면 선택)
```

변경 코드
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectBuilding(building);
                else
                    pendingLeftClickSelect = () => { if (building != null) rtsUnitController.ClickSelectBuilding(building); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }

            // 건설 중인 BaseStructure 좌클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 강제로 공격, 아니면 선택)
```

### 6) BaseStructure 클릭 - 즉시 선택 대신 pending 등록

기존 코드
```csharp
                rtsUnitController.ClickSelectStructure(baseStructure);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 4. 땅 클릭 = 명령 처리
```

변경 코드
```csharp
                pendingLeftClickSelect = () => { if (baseStructure != null) rtsUnitController.ClickSelectStructure(baseStructure); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 4. 땅 클릭 = 명령 처리
```

### 7) 광물 클릭 - 즉시 선택 대신 pending 등록

기존 코드
```csharp
        // 5. 광물 클릭 = 선택 처리
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                rtsUnitController.ClickSelectResource(node);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }
```

변경 코드
```csharp
        // 5. 광물 클릭 = 선택 처리
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                pendingLeftClickSelect = () => { if (node != null) rtsUnitController.ClickSelectResource(node); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }
```

### 8) 가스 클릭 - 즉시 선택 대신 pending 등록

기존 코드
```csharp
        // 5. 가스 클릭 = 선택 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                rtsUnitController.ClickSelectResource(node);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }
```

변경 코드
```csharp
        // 5. 가스 클릭 = 선택 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                pendingLeftClickSelect = () => { if (node != null) rtsUnitController.ClickSelectResource(node); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }
```

### 9) 마우스 업 시점의 선택 확정 로직 (SelectObject 재작성)

기존 코드
```csharp
    /// <summary>
    /// 드래그 범위 내 모든것 선택
    /// </summary>
    private void SelectObject()
    {
        //드래그 범위 안에 들어오는 유닛부터 먼저 계산
        List<UnitController> unitsInDrag = new List<UnitController>();

        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            if (dragRect.Contains(screenPos))
            {
                unitsInDrag.Add(unit);
            }
        }

        // 건물을 클릭(단일 선택)한 채로 드래그해서 그 범위 안에 유닛이 걸리면,
        // 건물 단일 클릭 선택 대신 드래그로 걸린 유닛 선택을 우선한다.
        if (unitsInDrag.Count > 0 && clickSelectedBuildingThisDrag != null)
        {
            rtsUnitController.DeselectAll();
            clickSelectedBuildingThisDrag = null;
        }

        foreach (UnitController unit in unitsInDrag)
        {
            rtsUnitController.DragSelectUnit(unit);
        }
    }
```

변경 코드
```csharp
    /// <summary>
    /// 마우스를 놓는 시점에 선택을 확정한다.
    /// 드래그 범위 안에 유닛이 있으면 유닛(드래그) 선택을 우선하고, 없으면 대기해둔 단일 클릭 선택을 실행한다.
    /// </summary>
    private void SelectObject()
    {
        //드래그 범위 안에 들어오는 유닛부터 먼저 계산
        List<UnitController> unitsInDrag = new List<UnitController>();

        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            if (dragRect.Contains(screenPos))
            {
                unitsInDrag.Add(unit);
            }
        }

        if (unitsInDrag.Count > 0)
        {
            // 드래그 범위 안에 유닛이 있으면 드래그 유닛 선택이 우선 - 대기 중이던 단일 클릭 선택은 취소한다.
            pendingLeftClickSelect = null;

            // Shift가 아니면 기존 선택을 지우고 드래그로 잡힌 유닛들만 새로 선택한다 (Shift면 기존 선택에 추가).
            if (!Input.GetKey(KeyCode.LeftShift))
                rtsUnitController.DeselectAll();

            foreach (UnitController unit in unitsInDrag)
            {
                rtsUnitController.DragSelectUnit(unit);
            }

            return;
        }

        // 드래그로 걸린 유닛이 없으면(제자리 클릭이거나 빈 범위로 드래그) 마우스를 놓는 시점에 단일 클릭 선택을 확정한다.
        pendingLeftClickSelect?.Invoke();
        pendingLeftClickSelect = null;
    }
```

## 요약/영향받는 파일

- 유닛/적/건물/BaseStructure/자원을 (Shift 없이) 클릭해도 그 즉시 선택되지 않고, 마우스를 놓는 순간 확정된다.
- 마우스를 놓을 때 드래그 범위 안에 유닛이 하나라도 있으면: 대기 중이던 단일 클릭 선택(건물/적/자원 등)은 무시되고, 드래그로 잡힌 유닛들이 선택된다 - Shift가 아니면 기존 선택을 지우고 교체, Shift면 기존 선택에 추가.
- 드래그 범위 안에 유닛이 없으면(제자리 클릭 또는 빈 공간 드래그): 대기해둔 단일 클릭 선택이 그대로 적용된다 (건물 클릭 → 건물 선택, 적 클릭 → 적 선택 등).
- 부가 효과: 기존에는 Shift 없이 빈 땅에서 새로 드래그해도 이전 선택 위에 유닛이 계속 "추가"되기만 했는데(교체가 안 됨), 이번 변경으로 Shift 없는 드래그는 이제 제대로 기존 선택을 지우고 새로 선택한 유닛들로 교체된다. Shift+드래그(0047)의 "추가" 동작은 그대로 유지된다.
- Shift+클릭(토글/추가 선택)과 A/M/P/Y 등 명령 대기 상태에서의 클릭(공격/이동/순찰/랠리 명령 실행)은 지금처럼 즉시 실행되며 이번 변경의 영향을 받지 않는다.
- 0123에서 추가했던 건물 전용 임시 필드(`clickSelectedBuildingThisDrag`)는 제거되고 범용 `pendingLeftClickSelect`로 대체된다.
- 영향받는 파일: `Assets\Scripts\UserControl\UserControl.cs`

이대로 구현할까요?
