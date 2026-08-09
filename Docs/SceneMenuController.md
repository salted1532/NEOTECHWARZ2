# SceneMenuController

`Assets/Scripts/UI/SceneMenuController.cs`

## 개요

게임플레이 씬의 옵션 패널을 열고 닫고, "메인화면으로 나가기"/스테이지 이동을 처리한다. 사운드 슬라이더 연결은 `SoundSettingsPanel`이 이미 담당하므로 이 스크립트는 패널 표시/씬 전환만 담당한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `optionButton` / `optionCloseButton` | 옵션 패널 열기/닫기(X) 버튼 |
| `mainMenuButton` | "메인화면으로 나가기" 버튼 |
| `nextStageButton` / `previousStageButton` | 다음/이전 스테이지 이동 버튼 |
| `mainSceneName` / `nextStageSceneName` / `previousStageSceneName` | 각각 로드할 씬 이름 |
| `optionsPanel` | 옵션 패널 (레이아웃/사운드 슬라이더는 직접 제작 후 연결) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 버튼 리스너 연결, 옵션 패널 초기 비활성화 |
| `OpenOptionsPanel()` | 패널 활성화 + `Time.timeScale = 0` + `UserControl.IsPaused = true` (일시정지) |
| `CloseOptionsPanel()` | 패널 비활성화 + 시간/일시정지 상태 복구 |
| `OnMainMenuClicked()` / `OnNextStageClicked()` / `OnPreviousStageClicked()` (private) | 시간/일시정지 복구 후 해당 씬 로드 — 옵션(퍼즈) 상태로 나가면 다음 씬까지 멈춰있지 않도록 안전하게 복구 |

## 연관 컴포넌트

- **SoundSettingsPanel**: 옵션 패널 내 사운드 슬라이더 연결 담당
- **UserControl**: `IsPaused` 상태를 공유해 게임플레이 입력을 함께 제어
