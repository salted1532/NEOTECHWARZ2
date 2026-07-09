# 0047. 유닛 선택 상태에서 Shift + 드래그로 추가 선택

**날짜:** 2026-07-09
**상태:** 제안 (구현 대기 - 사용자 확인 필요)

## 요청 내용
> 유닛이 선택된 상태에서 shift + 드래그 했을시 추가로 드래그 선택을 할수 있도록 수정해줘 현재는 유닛 선택 상태에서 shift + 드래그 시 바닥 클릭으로 입력되어 선택된 유닛이 리셋되고 그냥 드래그 선택이 되는데 shift 클릭 시 바닥 클릭을 무시하고 드래그 선택으로 추가 선택이 되도록 해줘

## 원인
`UserControl.HandleLeftClick()`은 `Input.GetMouseButtonDown(0)` 시점(드래그 시작 순간)에 즉시 호출된다. 드래그 시작 지점이 유닛/적/건물/자원 위가 아니라 빈 바닥이면 마지막 분기(6번, `Assets/Scripts/UserControl/UserControl.cs:328-329`)로 떨어져 **Shift 키 여부와 상관없이 무조건** `rtsUnitController.DeselectAll()`을 호출한다.

즉, Shift + 드래그를 시작하자마자(마우스를 떼기도 전에) 기존 선택이 초기화돼버리고, 이후 `MouseButtonUp`에서 `SelectObject()`가 드래그 박스 안의 유닛들을 추가하지만 이미 기존 선택은 사라진 뒤라 "그냥 새로 드래그 선택"한 것과 똑같이 동작한다.

반면 `RTSUnitController.DragSelectUnit()`(`Assets/Scripts/System/RTSUnitController.cs:149-156`)은 이미 "아직 선택 안 된 유닛만 추가"하는 방식이라 기존 선택을 지우지 않는다. 문제는 오직 드래그 시작 시점의 `DeselectAll()` 호출 하나뿐이다.

## 변경 내용 (제안)

`HandleLeftClick()`의 6번 분기("아무것도 아닌 곳 클릭 = 선택 해제")에서, Shift 키가 눌려있으면 `DeselectAll()`을 건너뛴다. 이렇게 하면:
- Shift 없이 빈 바닥을 드래그: 기존과 동일하게 시작 시점에 선택 초기화 → 새로 드래그 선택.
- Shift + 빈 바닥 드래그: 시작 시점에 선택 유지 → 드래그 종료 시 `SelectObject()`가 박스 안의 유닛들을 기존 선택에 추가.

### `Assets/Scripts/UserControl/UserControl.cs`

**기존 코드** (326~329행)
```csharp
        // 6. 아무것도 아닌 곳 클릭 = 선택 해제
        rtsUnitController.DeselectAll();
    }
```

**변경 코드**
```csharp
        // 6. 아무것도 아닌 곳 클릭 = 선택 해제
        // (Shift를 누른 채 빈 바닥에서 드래그를 시작한 경우엔, 곧이어 시작될 드래그 선택이
        //  기존 선택에 "추가"되어야 하므로 여기서 기존 선택을 지우지 않는다)
        if (!Input.GetKey(KeyCode.LeftShift))
            rtsUnitController.DeselectAll();
    }
```

## 영향받는 파일
- `Assets/Scripts/UserControl/UserControl.cs`

## 참고
- 유닛/건물 등 구체적인 대상을 클릭하는 다른 분기(1~5번)는 각 분기 안에서 `return`으로 끝나므로 이번 변경의 영향을 받지 않는다. 이번 변경은 "아무것도 히트되지 않았거나 명령 대기 상태가 아닌 빈 바닥을 클릭한" 6번 분기에만 해당된다.
- Shift + 드래그로 이미 선택된 유닛을 다시 박스로 감싸는 경우는 `DragSelectUnit()`이 중복 추가를 막아주므로 별도 처리가 필요 없다. (단, 이번 요청 범위는 "추가 선택"이며, 박스로 다시 감싼 기존 선택 유닛을 제외/토글하는 동작은 포함하지 않음 - 필요하면 별도 요청으로 처리)
