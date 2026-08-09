# VictoryPanelController

`Assets/Scripts/UI/VictoryPanelController.cs`

## 개요

`StageManager.OnVictory`를 구독해서 일정 지연 후 승리 패널을 띄우고, 패널 안의 "메인화면으로"/"다음 스테이지로"/"게임으로 돌아가기" 버튼을 처리한다. 패널 레이아웃(배경/문구/연출)은 유니티 에디터에서 직접 만들고 이 스크립트에는 그 패널 GameObject와 버튼만 연결하면 된다 — `SceneMenuController`와 동일한 컨벤션(패널 표시/씬 전환만 담당).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `victoryPanel` | 승리 패널 (레이아웃은 직접 제작 후 연결) |
| `mainMenuButton` / `nextStageButton` / `returnToGameButton` | 패널 내 버튼 3종 |
| `mainSceneName` / `nextStageSceneName` | 각각 로드할 씬 이름 (다음 스테이지 씬이 아직 없어 일단 SampleScene으로 연결돼 있음) |
| `victoryDelay` | 승리 확정 후 패널을 띄우기까지의 지연 시간(연출용) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 버튼 리스너 연결, 패널 초기 비활성화 |
| `Start()` | `StageManager.Instance.OnVictory` 구독 |
| `OnDestroy()` | 구독 해제 |
| `HandleVictory()` (private) | `ShowVictoryPanelAfterDelay()` 코루틴 시작 |
| `ShowVictoryPanelAfterDelay()` (private) | `WaitForSecondsRealtime`으로 `victoryDelay`만큼 대기(시간 정지 상태에서도 진행됨) 후 패널 활성화 + `Time.timeScale = 0` + `UserControl.IsPaused = true` |
| `OnMainMenuClicked()` / `OnNextStageClicked()` (private) | 시간/일시정지 복구 후 해당 씬 로드 |
| `OnReturnToGameClicked()` (private) | 패널만 닫고 시간/일시정지 복구(씬 이동 없이 게임 계속) |

## 연관 컴포넌트

- **StageManager**: `OnVictory` 이벤트 발행처
- **SceneMenuController**: 패널 표시/씬 전환 컨벤션을 공유하는 대응 컴포넌트(옵션 패널용)
- **UserControl**: `IsPaused` 상태 공유
