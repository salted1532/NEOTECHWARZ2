# 0656 - 브리핑룸 씬 마우스 커서를 다른 씬과 동일하게 적용 (적용 완료)

## 날짜
2026-08-21

## 요청 내용
"브리핑 룸 씬에서 마우스 커서를 다른씬과 같이 적용시켜줘"

## 조사 내용

`MainScene`(`MainMenuController.cs`)과 `MissionSelect`(`MissionSelectManager.cs`)는 동일한 패턴으로 커서를 처리한다:
- `cursorTexture`/`cursorHoverTexture`/`cursorHotspot`/`uiCamera` 필드를 인스펙터로 노출
- `Awake()`에서 기본 커서로 `Cursor.SetCursor()` 1회 설정 — `UserControl`(미션 플레이) 등 다른 씬에서 바꾼 커서가 `Cursor.SetCursor`의 전역 상태 때문에 남아있는 문제(doc/0473)를 씬 진입 시 리셋
- `Update()`에서 클릭 가능한 버튼 위 호버 여부를 매 프레임 확인해 `cursorTexture`↔`cursorHoverTexture`로 전환 (`IsHoveringClickableButton()` — `RectTransformUtility.RectangleContainsScreenPoint` 사용, `TooltipUI`와 동일 방식)

반면 `Briefing_Room` 씬의 `BriefingRoomController.cs`에는 이 로직이 전혀 없다 — 커서 관련 필드/코드가 없어서, 브리핑룸에 들어오면 직전 씬(미션 플레이 등)에서 남은 커서가 그대로 유지되거나 OS 기본 화살표가 노출된다.

두 씬의 `.unity` 파일을 확인하니 커서 텍스처는 동일한 GUID를 재사용 중이고(`cursorTexture`=`9c2d77e2...`, `cursorHoverTexture`=`a4bfefaa...`), `cursorHotspot`은 `(0,0)`, Canvas가 둘 다 `Screen Space - Overlay`(`m_RenderMode: 0`)라 `uiCamera`는 비워둔다. `Briefing_Room` 씬의 Canvas도 동일하게 `Screen Space - Overlay`임을 확인했다.

## 계획된 코드 변경

`BriefingRoomController.cs`에 `MissionSelectManager.cs`(0616~)와 동일한 패턴을 그대로 이식한다. `goBackButton`/`startMissionButton`을 `hoverableButtons`로 등록.

### 필드 추가 (다른 `[Header]` 블록들 옆)
```csharp
[Header("마우스 커서 (다른 씬과 동일한 패턴)")]
[SerializeField] private Texture2D cursorTexture; // 비워두면 OS 기본 화살표 사용
[SerializeField] private Texture2D cursorHoverTexture; // 비워두면 호버해도 커서 안 바뀜
[SerializeField] private Vector2 cursorHotspot = Vector2.zero;
[SerializeField] private Camera uiCamera; // Canvas RenderMode가 Overlay면 비워둔다

private Button[] hoverableButtons;
private bool isHoveringButton;
```

### `Awake()` 끝에 추가
```csharp
hoverableButtons = new[] { goBackButton, startMissionButton };

if (cursorTexture != null)
    Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
```

### 새 메서드 추가 (`MainMenuController`/`MissionSelectManager`와 동일)
```csharp
private void Update()
{
    if (cursorHoverTexture == null)
        return;

    bool hovering = IsHoveringClickableButton();

    if (hovering == isHoveringButton)
        return;

    isHoveringButton = hovering;
    Cursor.SetCursor(hovering ? cursorHoverTexture : cursorTexture, cursorHotspot, CursorMode.Auto);
}

private bool IsHoveringClickableButton()
{
    foreach (Button button in hoverableButtons)
    {
        if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
            continue;

        RectTransform rect = button.transform as RectTransform;

        if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera))
            return true;
    }

    return false;
}
```

## 씬 파일 변경 (인스펙터 값)
`Briefing_Room.unity`의 `BriefingRoomController` 컴포넌트에 `MissionSelect.unity`와 동일한 값을 채운다:
- `cursorTexture`: guid `9c2d77e276d94de40bbf9f244dce00b8`
- `cursorHoverTexture`: guid `a4bfefaaa0a9380408d8bd5ddda6c7c1`
- `cursorHotspot`: `(0, 0)`
- `uiCamera`: 비움 (Overlay Canvas)

## 요약/영향받는 파일
- `Assets/Scripts/UI/BriefingRoomController.cs` — 필드/`Update()`/`IsHoveringClickableButton()` 추가, `Awake()`에 `hoverableButtons` 초기화 및 초기 `Cursor.SetCursor` 호출 추가
- `Assets/Scenes/Missions/Briefing_Room.unity` — 위 인스펙터 값 4개 채움

사용자 확인 후 위 변경을 실제 적용함. `npx uloop-cli compile`로 컴파일 성공 확인 (Success: true, 에러/경고 0).
