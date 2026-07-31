# 0338. MainMenuController - 클릭 가능한 버튼 위 마우스 호버 커서 변경

**날짜:** 2026-07-31

## 요청

> MainMenuController.cs에다가 UI 버튼 마우스 호버시 변경될수 있도록 해줄래 클릭 가능한 경우에
> 마우스가 변경되도록

## 설계

버튼마다 별도 컴포넌트(`IPointerEnterHandler` 등)를 붙이는 대신, `MainMenuController`가 이미 들고
있는 4개 버튼 참조(`playButton`/`optionButton`/`exitButton`/`optionCloseButton`)만으로 매 프레임
호버 여부를 확인하는 방식 채택 — `TooltipUI.IsPointerOverTarget()`([[0330]] 근처에서 이미 다룬
동일 클래스)이 쓰는 것과 똑같은 `RectTransformUtility.RectangleContainsScreenPoint` 패턴을 재사용.
새 씬 오브젝트/컴포넌트를 추가하지 않고 스크립트 하나로 끝남(요청대로 `MainMenuController.cs` 안에서
처리).

`button.interactable`도 같이 확인해서 "클릭 가능한 경우에만" 커서가 바뀌도록 함(꺼져 있거나
비활성화된 버튼은 호버해도 커서 그대로).

## 적용한 변경

`Assets/Scripts/UI/MainMenuController.cs`
- `cursorHoverTexture`(호버 시 텍스처, 비워두면 기능 자체가 꺼짐), `uiCamera`(Canvas가 Overlay면
  비워둠, `TooltipUI`와 동일 컨벤션) 필드 추가.
- `Awake()`에서 4개 버튼을 배열(`hoverableButtons`)로 캐싱.
- `Update()`에서 `IsHoveringClickableButton()`으로 매 프레임 확인 → 상태가 바뀔 때만
  `Cursor.SetCursor()` 재호출(변화 없으면 스킵, `TooltipUI`처럼 불필요한 재호출 방지).

`Assets/Scenes/MainScene.unity` — `MainMenuController` 컴포넌트에 연결:
- `cursorHoverTexture`: `Assets/images/Cursor/Bonus_50 GREEN.png` (기존에 RTS 씬에서
  `cursorSelectAllyTexture`로 이미 쓰이던 에셋을 그대로 재사용 - "클릭 가능/선택 가능" 의미에 가장
  가까운 기존 텍스처. 원하는 다른 아이콘이 있으면 인스펙터에서 이 필드만 바꾸면 됨)
- `uiCamera`: 비워둠(None) — MainScene Canvas가 Overlay 방식이라 정상

`npx uloop-cli compile`/`get-logs`로 에러 0개 확인, 씬을 다시 열어 `Cursor Hover Texture` →
`Bonus_50 GREEN` 연결 확인. Play Mode 진입 직후 콘솔 에러 0개도 확인.

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scenes/MainScene.unity`
