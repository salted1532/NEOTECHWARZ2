# 0337. MainScene에도 다른 씬과 동일한 커스텀 커서 적용

**날짜:** 2026-07-31

## 요청

> mainscene에도 마우스 호버 작동이 있을시 버튼 등에서 커서가 다른 씬처럼 변하도록 해줘

## 조사 내용

- TestScene/SampleScene은 `UserControl.cs`가 매 프레임 `UpdateCursor()`로 마우스 커서 텍스처를
  갱신한다. UI 위에 마우스가 있을 때(`EventSystem.current.IsPointerOverGameObject()`)는 항상
  `cursorDefaultTexture`(기본 커서, `GameManager.prefab`에 `Assets/images/Cursor/basic_01 GREEN.png`로
  연결돼 있음)로 고정한다 — 즉 버튼 종류에 따라 커서 모양이 달라지는 게 아니라, "UI 위에서는 항상
  게임 고유 기본 커서"라는 동작임.
- **MainScene은 `UserControl` 컴포넌트 자체가 없다**(RTS 게임플레이가 없는 순수 메뉴 화면) — 그래서
  `Cursor.SetCursor()`가 한 번도 호출되지 않아 OS 기본 화살표 커서가 그대로 남아있었음. 이게 "다른
  씬과 다르게 보이는" 원인.
- 메인 메뉴는 버튼 외에 "적/아군/중립 대상 호버"라는 개념 자체가 없으므로, 매 프레임 갱신하는
  `UpdateCursor()` 같은 로직은 필요 없고, 씬 시작 시 한 번만 같은 텍스처로 `Cursor.SetCursor`를
  호출하면 다른 씬과 동일한 결과(항상 게임 고유 기본 커서 표시)가 된다.

## 수정

`Assets/Scripts/UI/MainMenuController.cs`에 `cursorTexture`/`cursorHotspot` 필드 추가,
`Awake()`에서 `Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto)` 호출(비어있으면
기존처럼 OS 기본 화살표 유지, `UserControl.cursorDefaultTexture`와 동일한 "비워두면 OS 기본" 컨벤션).

`Assets/Scenes/MainScene.unity`의 `MainMenuController` 컴포넌트에 다른 씬과 **동일한 텍스처
에셋**(`Assets/images/Cursor/basic_01 GREEN.png`)과 핫스팟(0,0)을 연결.

`npx uloop-cli compile`/`get-logs`로 에러 0개 확인, 씬을 다시 열어 `Cursor Texture` →
`basic_01 GREEN`, `Cursor Hotspot` → `(0,0)`으로 연결된 것 재확인.

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scenes/MainScene.unity`
