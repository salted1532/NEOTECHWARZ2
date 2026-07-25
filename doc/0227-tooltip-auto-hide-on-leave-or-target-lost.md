# 0227 - 툴팁: UI 밖으로 마우스 이탈 시 / 호버 대상 소실 시 자동 숨김

**날짜:** 2026-07-24

## 요청 내용

> 현재 툴팁이 뜬 상황에서 클릭을 해서 선택이나 그런거로 넘어가면 UI 밖으로 마우스가 나가도 툴팁이 안사라지고 다시 UI에 다른곳을 호버해야 없어지네 만약 UI밖으로 마우스가 나가면 어떤 상황이든 툴팁은 안보이도록 해줘그리고 호버하고 있던 대상이 없어지면 그때도 안보이도록

정리하면 두 가지 요구사항:
1. 마우스가 UI(정확히는 현재 툴팁을 띄운 대상) 밖으로 나가면, 어떤 상황이든 예외 없이 툴팁이 즉시 사라져야 한다.
2. 호버 중이던 대상(버튼 등)이 사라지면(비활성화/파괴) 그때도 툴팁이 사라져야 한다.

## 조사 내용

`TooltipUI`는 `ProductionSlot.OnPointerEnter/OnPointerExit`, `UIController`의 EventTrigger(스탯 아이콘 호버) 두 곳에서 `Show()/Hide()`를 직접 호출하는 방식으로 동작한다 (`Assets/Scripts/UI/ProductionSlot.cs:124-139`, `Assets/Scripts/UI/UIController.cs:614,618`).

문제는 `Hide()` 호출이 전적으로 Unity 이벤트 시스템의 `OnPointerExit` 콜백에 의존한다는 점이다. 아래와 같은 경우 `OnPointerExit`이 발생하지 않거나 발생해도 이미 늦다:

- 버튼 클릭 콜백이 그 버튼 자신 혹은 슬롯 목록을 비활성화/재구성(`Clear()` → `gameObject.SetActive(false)`, 슬롯 재배치 등)하면, 해당 GameObject가 이미 계층에서 빠져버려 EventSystem이 `OnPointerExit`을 보낼 대상 자체가 없어진다. → 툴팁이 그대로 남음 (요청의 2번 케이스).
- 마우스가 화면/게임 창 밖으로 완전히 나가는 경우, Unity의 `StandaloneInputModule`이 그 프레임에 대해 안정적으로 `OnPointerExit`을 보장하지 않는 경우가 있다. → 툴팁이 그대로 남음 (요청의 1번 케이스).

또한 `TooltipUI.Update()`(`Assets/Scripts/UI/Tooltip/TooltipUI.cs:52-58`)는 `currentTarget == null`이면 그냥 `return`만 하고 `Hide()`를 호출하지 않아, 대상이 파괴돼도 툴팁이 마지막 위치에 그대로 떠 있는다.

## 계획한 코드 변경

`TooltipUI.cs`의 `Update()`에서 매 프레임 "지금 마우스가 실제로 `currentTarget` 사각형 위에 있는가"를 직접 검사하도록 바꾼다. `OnPointerEnter/Exit` 콜백에 의존하지 않는 자체 안전장치이므로, 위에서 설명한 두 가지 케이스(이벤트 유실, 대상 소실)를 모두 한 번에 해결한다. 기존 `OnPointerExit` 쪽 `Hide()` 호출은 즉각 반응을 위해 그대로 둔다(중복 호출은 문제 없음).

**기존 코드** (`Assets/Scripts/UI/Tooltip/TooltipUI.cs:52-58`)
```csharp
    private void Update()
    {
        if (!isVisible || currentTarget == null)
            return;

        PositionAboveTarget(currentTarget);
    }
```

**변경 코드**
```csharp
    private void Update()
    {
        if (!isVisible)
            return;

        // OnPointerExit 콜백에만 의존하지 않는 안전장치: 클릭으로 호버 대상이 파괴/비활성화되거나
        // (버튼 목록 재구성 등), 마우스가 화면/UI 밖으로 나가 OnPointerExit이 아예 발생하지 않는
        // 경우까지 포함해서, 매 프레임 "마우스가 실제로 대상 위에 있는가"를 직접 확인한다.
        if (!IsPointerOverTarget())
        {
            Hide();
            return;
        }

        PositionAboveTarget(currentTarget);
    }

    // currentTarget이 여전히 살아있고, 화면상 마우스 좌표가 그 사각형 범위 안에 있는지 확인한다.
    private bool IsPointerOverTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(currentTarget, Input.mousePosition, uiCamera);
    }
```

## 영향받는 파일

- `Assets/Scripts/UI/Tooltip/TooltipUI.cs` (수정 예정)

`ProductionSlot.cs`, `UIController.cs`는 변경 불필요 (기존 `OnPointerEnter`로 `Show()` 호출하는 흐름은 그대로 두고, `Hide()`가 걸리는 조건만 `TooltipUI` 내부에서 보강).

## 남은 작업

사용자 확인 후 위 계획대로 `Assets/Scripts/UI/Tooltip/TooltipUI.cs`에 실제 반영 완료. 추가 작업 없음.
