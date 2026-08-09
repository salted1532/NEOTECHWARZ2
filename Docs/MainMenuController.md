# MainMenuController

`Assets/Scripts/UI/MainMenuController.cs`

## 개요

메인 메뉴 화면(MainScene)의 Play/Option/Exit 버튼을 연결한다. 옵션 패널 자체(레이아웃)는 `SoundSettingsPanel`과 동일한 컨벤션으로 유니티 에디터에서 직접 만들고, 이 스크립트는 그 패널 GameObject를 인스펙터로 연결받아 켜고 끄는 로직만 담당한다. 버튼 호버 시 커서를 바꾸는 로직도 포함한다 — MainScene은 RTS 게임플레이가 없어 `UserControl`(다른 씬에서 매 프레임 커서를 되돌리는 컴포넌트)이 존재하지 않으므로 이 스크립트가 직접 처리한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `playButton` / `optionButton` / `exitButton` / `optionCloseButton` | 메인 메뉴 버튼 4종 |
| `playerPrefsResetButton` | 개발자용 PlayerPrefs 초기화 버튼 — 정식 출시 전 제거 예정 |
| `testSceneName` | Play 클릭 시 로드할 씬 이름 |
| `mainMenuPanel` / `optionsPanel` | 메인 메뉴 패널(비워두면 자기 자신)과 옵션 패널 |
| `cursorTexture` / `cursorHoverTexture` / `cursorHotspot` / `uiCamera` | 커스텀 커서 텍스처 및 핫스팟, UI 카메라(Overlay 캔버스면 비워둠) — `TooltipUI`와 동일한 컨벤션 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 버튼 클릭 리스너 연결, 패널 초기 상태 설정(꺼진 채로 저장돼있어도 항상 켜짐), 호버 대상 버튼 배열 구성, 기본 커서 적용 |
| `Update()` | 매 프레임 클릭 가능한 버튼 위에 마우스가 있는지 확인해 커서를 전환(`TooltipUI.IsPointerOverTarget()`과 동일한 `RectTransformUtility` 방식) |
| `IsHoveringClickableButton()` (private) | `hoverableButtons` 중 interactable하고 활성화된 버튼 위에 마우스가 있는지 판정 |
| `OnPlayClicked()` / `OnOptionClicked()` (private) | 씬 이동 / 옵션 패널 열기 |
| `CloseOptionsPanel()` | 옵션 패널의 X 버튼에 연결 |
| `OnExitClicked()` (private) | 에디터에서는 `EditorApplication.isPlaying = false`, 빌드에서는 `Application.Quit()` |
| `ResetPlayerPrefs()` (private) | 개발자용 — 미션 해금 진행 상황(doc/0472), 사운드 설정(doc/0288) 등 저장된 값을 전부 삭제. 정식 출시 전 버튼과 함께 제거 예정 |

## 연관 컴포넌트

- **SoundSettingsPanel**: 옵션 패널 내 사운드 슬라이더 연결 담당(레이아웃 컨벤션 공유)
- **TooltipUI**: `IsPointerOverTarget()`과 동일한 방식으로 마우스 호버 판정
