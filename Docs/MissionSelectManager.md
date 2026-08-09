# MissionSelectManager

`Assets/Scripts/UI/MissionSelectManager.cs`

## 개요

MissionSelect 씬의 미션 버튼들을 연결하는 씬 전용 매니저(싱글턴 아님). 버튼→씬 매핑은 코드에 하드코딩하지 않고 인스펙터의 `missions` 리스트로 노출해서, 미션이 추가되거나 순서가 바뀌어도 코드 수정 없이 처리할 수 있게 한다(doc/0470). 호버 시 툴팁은 기존 `TooltipUI` 싱글턴을 그대로 재사용한다. 미션 해금 상태는 `PlayerPrefs`의 "마지막으로 해금된 미션 번호"로 관리한다(doc/0472).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `MissionSelectEntry.button/missionNumber/missionName/sceneName` | 버튼 하나당 매핑 정보 — 미션 번호, 표시 이름, 로드할 씬 이름 |
| `missions` | 미션 버튼 항목 리스트 |
| `HighestUnlockedMissionKey` / `DefaultHighestUnlockedMission` | 해금 상태 저장 키 / 기본값(1, 즉 미션 0/1은 항상 열림) |
| `backToMainMenuButton` / `mainMenuSceneName` | 메인 메뉴로 돌아가기 버튼과 대상 씬 |
| `unlockAllMissionButton` | 개발자용 전체 해금 버튼 — 정식 출시 전 제거 예정 |
| `cursorTexture` / `cursorHoverTexture` / `cursorHotspot` / `uiCamera` | 커스텀 커서 관련 — `MainMenuController`와 동일한 패턴 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 각 미션 버튼에 클릭 리스너와 호버 툴팁 연결, `ApplyLockState()`로 잠금 상태 반영, 뒤로가기/전체해금 버튼 연결, 기본 커서 적용 |
| `Update()` | `MainMenuController`와 동일한 방식으로 버튼 호버 시 커서 전환 |
| `IsHoveringClickableButton()` (private) | 호버 대상 버튼 배열 중 마우스가 올라간 버튼이 있는지 판정 |
| `LoadMission(entry)` (private) | 해당 미션의 씬 로드 |
| `BackToMainMenu()` (private) | 메인 메뉴 씬 로드 |
| `ApplyLockState()` (private) | `PlayerPrefs`에 저장된 해금 번호 기준으로 각 버튼의 `interactable` 설정 |
| `UnlockAllMissions()` (private) | 개발자용 — 모든 미션을 즉시 해금. 정식 출시 전 버튼과 함께 제거 예정 |
| `SetupHoverTooltip(entry)` (private) | `UIController.AddStatHoverTooltip()`과 동일한 `EventTrigger` 패턴으로 호버 시 `TooltipUI.Show`/`Hide` 연결. 미션 이름은 로컬라이제이션 우선, 없으면 `entry.missionName` 사용 |

## 연관 컴포넌트

- **TooltipUI**: 미션 버튼 호버 시 툴팁 표시에 재사용
- **LocalizationManager**: 미션 이름/툴팁 부제목 텍스트 조회 (`missionselect.name.{번호}`, `missionselect.tooltip.subtitle`)
- **MainMenuController**: 커서 전환 로직 패턴을 공유

## 참고

`MissionSelectManager` 상단 주석에 "미션을 클리어하면 다음 미션이 열리는 연출은 각 미션 씬의 `StageManager.OnVictory` 등에서 이 키(`HighestUnlockedMissionKey`)를 갱신하도록 나중에 이어붙이면 됨 - 이번 작업에는 그 연결까진 포함하지 않음"이라고 명시돼 있다. 즉 현재 `StageManager`/`VictoryPanelController` 쪽에는 이 키를 갱신하는 코드가 없어 실제 미션 해금 연동은 아직 미완성 상태다.
