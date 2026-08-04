# 0432. UI 위에서 시작한 드래그 선택 무시

- 날짜: 2026-08-05
- 상태: **적용 완료** (컴파일 확인: 에러 0개)

## 요청 내용

> UI위에서는 드래그 선택이 안되도록 할수 있할까? UI 위에서부터 드래그가 그려지네 마우스 좌클릭이
> 들어갈때부터 UI 위면 취소 되도록

## 조사 내용

`Assets/Scripts/UserControl/UserControl.cs`의 `HandleMouse()`에 버그가 있다.

```csharp
if (Input.GetMouseButtonDown(0))
{
    start = Input.mousePosition;   // ← UI 위인지 확인하기도 전에 이미 드래그 시작점을 기록해버림
    dragRect = new Rect();
    pendingLeftClickSelect = null;

    if (EventSystem.current.IsPointerOverGameObject())
        return;   // ← 여기서 리턴하지만 start는 이미 세팅된 뒤

    HandleLeftClick();
}

// 드래그 중
if (Input.GetMouseButton(0))
{
    end = Input.mousePosition;
    DrawDragRectangle();   // ← 이 블록은 "이번 클릭이 UI 위에서 시작했는지" 자체를 전혀 확인하지 않음
}

// 드래그 종료
if (Input.GetMouseButtonUp(0))
{
    CalculateDragRect();
    SelectObject();   // ← 여기도 마찬가지로 확인 없음 - 드래그 선택이 그대로 실행됨
    ...
}
```

`EventSystem.current.IsPointerOverGameObject()` 체크는 "좌클릭이 눌린 그 프레임"에만 확인하고,
확인 결과와 무관하게 `start`는 이미 기록돼 있다. 그리고 "드래그 중"/"드래그 종료" 블록은 애초에
이 클릭이 UI 위에서 시작했는지를 전혀 기억/확인하지 않고 매 프레임 무조건 실행된다. 그래서
버튼을 누른 채로 커서를 게임 화면 쪽으로 슬쩍만 움직여도 `start`(UI 위의 좌표)~`end`(현재 좌표)
사이로 드래그 박스가 그려지고, 마우스를 떼면 `SelectObject()`(드래그 범위 안 유닛 선택)까지
그대로 실행돼버린다.

## 코드 변경 (제안)

이번 좌클릭이 UI 위에서 시작됐는지를 기억해두는 필드 하나(`dragStartedOverUI`)를 추가하고, 드래그
중/드래그 종료 블록에서 이 값을 확인해서 UI에서 시작한 클릭이면 드래그박스도 그리지 않고 선택도
실행하지 않는다.

**기존 코드**:
```csharp
    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;
```
```csharp
    private void HandleMouse()
    {
        // 좌클릭 시
        // 드래그 시작
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();
            pendingLeftClickSelect = null;

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            HandleLeftClick();
        }

        // 드래그 중
        if (Input.GetMouseButton(0))
        {
            end = Input.mousePosition;
            DrawDragRectangle();
        }

        // 드래그 종료
        if (Input.GetMouseButtonUp(0))
        {
            CalculateDragRect();
            SelectObject();

            start = Vector2.zero;
            end = Vector2.zero;

            DrawDragRectangle();
        }
```

**변경 코드**:
```csharp
    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;

    // 이번 좌클릭(눌림~뗌)이 UI 위에서 시작됐는지. true면 드래그박스도 그리지 않고 드래그 선택도 실행하지 않는다.
    private bool dragStartedOverUI;
```
```csharp
    private void HandleMouse()
    {
        // 좌클릭 시
        // 드래그 시작
        if (Input.GetMouseButtonDown(0))
        {
            // 누른 시점에 UI 위였으면 이번 좌클릭 자체를 완전히 무시한다 - 선택/드래그박스 모두 시작하지 않음.
            dragStartedOverUI = EventSystem.current.IsPointerOverGameObject();

            if (dragStartedOverUI)
                return;

            start = Input.mousePosition;
            dragRect = new Rect();
            pendingLeftClickSelect = null;

            HandleLeftClick();
        }

        // 드래그 중
        if (Input.GetMouseButton(0) && !dragStartedOverUI)
        {
            end = Input.mousePosition;
            DrawDragRectangle();
        }

        // 드래그 종료
        if (Input.GetMouseButtonUp(0))
        {
            if (!dragStartedOverUI)
            {
                CalculateDragRect();
                SelectObject();

                start = Vector2.zero;
                end = Vector2.zero;

                DrawDragRectangle();
            }

            dragStartedOverUI = false;
        }
```

우클릭 블록은 원래부터 `IsPointerOverGameObject()`를 그 자리에서 바로 확인하고 상태를 남기지
않으므로(드래그 개념이 없음) 이번 문제와 무관 - 손대지 않는다.

## 요약 / 영향받는 파일

- 좌클릭이 UI(버튼/패널 등) 위에서 시작되면 그 클릭은 완전히 무시된다 - 드래그박스도 그려지지
  않고, 마우스를 뗄 때 드래그 선택도 실행되지 않는다.
- UI 밖(게임 화면)에서 시작한 클릭/드래그는 기존과 완전히 동일하게 동작한다.
- 영향받는 파일: `Assets/Scripts/UserControl/UserControl.cs` (코드 변경만)
- 아직 프로젝트 파일에는 적용하지 않음 (제안 단계).
