# 0123. 건물 클릭 + 드래그 시 유닛 우선 선택

**날짜:** 2026-07-15

## 요청 내용

건물 위에서 좌클릭을 시작해 유닛까지 드래그하면 건물과 유닛이 동시에 선택된다. 클릭(단일 선택)이라도 드래그 범위까지 확인해서, 그 드래그 범위 안에 유닛이 하나도 없을 때만 "건물 단일 클릭 선택"으로 인식하도록 해달라는 요청. (드래그 범위 안에 유닛이 있으면 건물 선택은 취소되고 드래그로 걸린 유닛만 선택되어야 함)

## 조사 내용

`Assets\Scripts\UserControl\UserControl.cs`의 흐름:

1. **마우스 다운** → `HandleLeftClick()` 호출. 건물을 클릭한 경우(3번 분기, 296~270번 줄 부근) Shift가 아니면 `rtsUnitController.ClickSelectBuilding(building)`을 즉시 호출한다. 이 메서드는 `DeselectAll()` 후 해당 건물 하나만 선택 상태로 만든다.
2. **마우스 업** → `CalculateDragRect()`로 드래그 사각형을 계산하고, `SelectObject()`가 `rtsUnitController.UnitList`를 순회하며 화면상 드래그 사각형 안에 들어오는 유닛들에 대해 `DragSelectUnit(unit)`을 호출해 선택 목록에 **추가**한다.

문제는 1번에서 이미 건물이 `selectedBuildingList`에 들어간 상태이고, `DragSelectUnit` → `SelectUnit`은 유닛을 추가만 할 뿐 기존에 선택된 건물 목록(`selectedBuildingList`)을 지우지 않는다는 점이다. 그 결과 건물 위에서 클릭을 시작해 유닛들 위로 드래그하면 건물+유닛이 함께 선택된다.

드래그 범위는 마우스 다운 시점엔 알 수 없고 마우스 업 시점에야 확정되므로, "건물 단일 클릭"을 되돌릴지 여부도 마우스 업 시점에 판단해야 한다. 이를 위해:

- 이번 클릭 사이클에서 (Shift 없이) 건물을 단일 클릭 선택했다면 그 건물을 임시로 기억해둔다.
- 마우스 업 시 `SelectObject()`가 드래그 박스 안의 유닛들을 먼저 계산하고, 만약 유닛이 하나라도 걸렸고 임시로 기억해둔 건물이 있다면 `DeselectAll()`로 그 건물 선택을 취소한 뒤 드래그로 걸린 유닛들만 선택한다.
- 드래그 박스 안에 유닛이 없으면(즉 제자리 클릭이거나 빈 땅으로만 드래그한 경우) 건물 단일 선택은 그대로 유지된다.

Shift+클릭(`ShiftClickSelectBuilding`)은 사용자가 의도적으로 여러 대상을 더하는 제스처이므로 이 로직 대상에서 제외한다(기존 6번 분기의 Shift+드래그 추가선택 정책과 동일한 맥락).

이번 요청은 "건물 클릭 후 유닛까지 드래그"만 언급했으므로, 범위를 `BuildingController` 단일 클릭 케이스로 한정한다. 참고로 반대 방향(유닛 클릭 후 건물 위로 드래그)은 `SelectObject()`가 애초에 `UnitList`만 스캔하고 `BuildingList`는 스캔하지 않으므로 같은 버그가 발생하지 않는다 - 그대로 둔다.

## 계획된 코드 변경

**파일:** `Assets\Scripts\UserControl\UserControl.cs`

### 1) using 추가 (List 사용을 위해)

기존 코드
```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
```

변경 코드
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
```

### 2) 이번 클릭 사이클에서 단일 클릭 선택된 건물을 기억할 필드 추가

기존 코드
```csharp
    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;
```

변경 코드
```csharp
    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;

    // 이번 마우스다운~업 사이클에서 (Shift 없이) 단일 클릭으로 선택한 건물.
    // 마우스 업 시 드래그 범위 안에 유닛이 걸리면 이 건물 선택을 취소하고 유닛 선택을 우선한다.
    private BuildingController clickSelectedBuildingThisDrag;
```

### 3) 마우스 다운 시작 시 초기화

기존 코드
```csharp
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();

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
            clickSelectedBuildingThisDrag = null;

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            HandleLeftClick();
        }
```

### 4) 건물 단일 클릭 시 건물을 기억해두기

기존 코드
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectBuilding(building);
                else
                    rtsUnitController.ClickSelectBuilding(building);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }

            // 건설 중인 BaseStructure 좌클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 강제로 공격, 아니면 선택)
```

변경 코드
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

### 5) 마우스 업 시 드래그 범위 안 유닛 유무로 건물 선택 취소 여부 결정

기존 코드
```csharp
    /// <summary>
    /// 드래그 범위 내 모든것 선택
    /// </summary>
    private void SelectObject()
    {
        //유닛 선택
        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            if (dragRect.Contains(screenPos))
            {
                rtsUnitController.DragSelectUnit(unit);
            }
        }
    }
```

변경 코드
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

## 요약/영향받는 파일

- 건물을 클릭하고 그대로(움직임 없이) 두면 기존과 동일하게 건물이 단일 선택된다.
- 건물을 클릭한 채로 드래그했는데 그 드래그 범위 안에 유닛이 하나라도 있으면, 건물 선택은 취소되고 드래그 범위 안의 유닛들만 선택된다.
- Shift+클릭으로 건물을 선택한 경우는 영향받지 않는다(기존 추가 선택 정책 유지).
- 영향받는 파일: `Assets\Scripts\UserControl\UserControl.cs`

이대로 구현할까요?
