# 0472. MissionSelectManager - 미션 잠금/해금 및 개발자용 전체 해금 버튼

**날짜:** 2026-08-08

## 요청 내용
> 이제 MissionSelectManager가 처음 미션0,1 빼고 나머지 미션의 버튼은 비활성화 하고 Normal Color를
> V값을 50으로 바꿔줘 이제 미션1을 클리어 시 다음스테이지가 열리는 방식으로 할려고해 그리고 내가
> 버튼하나를 추가했는데 Unlock ALl Mission버튼으로 이건 개발자용인데 누르면 모든 스테이지가
> 열리도록 해줘 추후에 정식버전에선 없앨거야

"미션1 클리어 시 다음 스테이지가 열리는 방식으로 할려고해"는 앞으로의 계획 설명이라 이번 작업
범위에는 "각 미션 씬에서 클리어를 감지해 실제로 해금을 갱신하는 연결"까지는 포함하지 않음 -
그 갱신에 쓸 수 있는 지속 저장 방식(아래)만 미리 만들어둠. 나중에 미션 씬 쪽(`StageManager.
OnVictory` 등)에서 이 값을 갱신하도록 이어붙이면 됨.

## 조사 - Normal Color 관련 주의사항

`Mission0`~`Mission5` 버튼은 `Button.transition = ColorTint`로 설정돼 있음 - 이 모드에서는
**`interactable = false`가 되면 실제로 화면에 표시되는 건 `Normal Color`가 아니라 `Disabled
Color`**임(Unity `Selectable`의 표준 동작). 그래서 요청대로 "비활성화 + Normal Color만
어둡게" 두 가지를 그대로 적용하면, 비활성화된 버튼은 Normal Color 변경과 무관하게 항상 기존
`Disabled Color`(연회색, 반투명)로만 보여서 실제로는 아무 효과가 없었을 것. 그래서 어둡게 하는
대상을 **Disabled Color 쪽**으로 바꿔 적용함(값 자체는 요청하신 대로 "Normal Color와 같은
색상에서 명도만 50%"로 계산). Normal Color 자체는 건드리지 않으므로 해금되면 원래 색 그대로
보임.

## 적용 (`MissionSelectManager.cs`)

- `HighestUnlockedMissionKey`(PlayerPrefs, 기본값 1) 추가 - "여기 저장된 번호 이하의 미션만
  해금" 규칙. 기본값 1이라 미션 0/1은 항상 열려있음(요청하신 초기 상태).
- `Awake()`에서 버튼/툴팁 연결 이후 `ApplyLockState()` 호출:
  - `entry.button.interactable = entry.missionNumber <= highestUnlocked`
  - `Color.RGBToHSV`로 그 버튼의 기존 `Normal Color`에서 H/S만 뽑아 V=0.5로 다시 조합한 값을
    `Disabled Color`에 대입.
- `[SerializeField] private Button unlockAllMissionButton;` 추가(헤더에 "개발자용 - 정식 버전
  출시 전 제거" 명시). 클릭 시 `PlayerPrefs`에 최대 미션 번호를 저장하고 `ApplyLockState()`를
  다시 호출해 전부 즉시 해금.
- 씬의 `UnlockMission` 버튼(사용자가 미리 만들어둔 오브젝트, 자식에 "Unlock All Mission" 텍스트)을
  `unlockAllMissionButton` 필드에 연결.

## 검증 (Play Mode)

- `PlayerPrefs` 키를 지운 "최초 실행" 상태로 확인: `Mission0`/`Mission1`은
  `interactable=True`, `Mission2`~`Mission5`는 `interactable=False`이고 전부
  `Disabled Color`의 V가 정확히 0.50으로 계산됨.
- `UnlockMission` 버튼의 `onClick`을 직접 호출해 확인: 6개 미션 버튼 전부
  `interactable=True`로 바뀌고, `PlayerPrefs["HighestUnlockedMission"]=5`로 저장됨(재실행해도
  유지됨 - 개발자가 한 번 눌러두면 계속 전부 열려있음).
- 확인 후 테스트로 남긴 `PlayerPrefs` 값은 다시 삭제해서 실제 초기 상태로 되돌려둠.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 물 메쉬 노이즈 없이 깨끗함.

## 남은 작업 (사용자 확인 필요)
- "미션1 클리어 시 다음 스테이지 해금"의 실제 연결(각 미션 씬의 `StageManager.OnVictory` 시점에
  `PlayerPrefs.SetInt("HighestUnlockedMission", ...)`를 갱신하는 지점 추가)은 이번에 포함 안 함 -
  준비되면 요청해줘.
- `UnlockMission` 버튼은 정식 버전 출시 전 씬에서 직접 제거(또는 `#if UNITY_EDITOR`로 감싸는 등)
  필요 - 지금은 요청대로 그대로 남겨둠.

## 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs`
- `Assets/Scenes/Missions/MissionSelect.unity` (`unlockAllMissionButton` 필드 연결)

## 후속 - 색 변경 제거, interactable만 유지 (같은 날 추가 요청)

> 그럼 Normal color 변경은 없애고 interactable의 경우는 유저프로필 데이터로 넣어서 다음번에
> 게임을 키더라도 진행상황에 맞게 플레이 할수 있게 해줘

`ApplyLockState()`에서 Disabled Color를 어둡게 계산해 넣던 부분을 전부 제거 - 이제 `interactable`
값만 설정하고 색은 전혀 건드리지 않음(잠긴 버튼은 Unity 기본 `Disabled Color`, 즉 원래 씬에
있던 연회색·반투명 그대로 보임).

"유저프로필 데이터로 넣어서 다음번에 게임을 키더라도 진행상황에 맞게"는 이미 `PlayerPrefs`
기반으로 구현돼 있던 부분이라(`PlayerPrefs.GetInt/SetInt`는 애초에 게임을 껐다 켜도 유지되는
사용자별 저장소) 추가로 바뀐 건 없음 - 색 로직만 빠졌음.

### 검증 (Play Mode)
- `PlayerPrefs` 키를 지운 최초 실행 상태로 다시 확인: `interactable`은 이전과 동일하게
  정상(`Mission0`/`1`=True, 나머지=False)이고, `normalColor`/`disabledColor` 둘 다 원래 씬
  기본값(`RGBA(1,1,1,1)` / `RGBA(0.784,0.784,0.784,0.502)`) 그대로 손대지 않음을 확인.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 이번엔 씬 파일 변경도 없음(색 로직이 Play Mode 테스트 중에만 적용됐다가 Stop 시
  되돌아가는 방식이라 애초에 씬에 저장된 적이 없었음) - `MissionSelectManager.cs`만 변경.

### 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs` (`ApplyLockState()`에서 색 관련 코드 제거)

## 후속2 - PlayerPrefabReset 버튼 연결 (같은 날 추가 요청)

> PlayerPrefabReset 버튼도 하나 만들었는데 PlayerPrefab을 리셋하는 기능을 연결해줘 이러면
> 스테이지 정보랑 사운드 값 정보도 리셋되도록

씬에 사용자가 미리 만들어둔 `PlayerPrefabReset` 버튼(`PlayerPrefs` 리셋 의도, 이름은 그대로 씬에
있는 오브젝트명을 따름)을 연결.

### 적용
- `[SerializeField] private Button playerPrefsResetButton;` 추가(`unlockAllMissionButton`과 같은
  "개발자용 - 정식 버전 출시 전 제거" 헤더 아래).
- `ResetPlayerPrefs()`: `PlayerPrefs.DeleteAll()` 한 번으로 처리 - 이 프로젝트에서 `PlayerPrefs`를
  쓰는 곳은 미션 해금 진행 상황(`HighestUnlockedMission`, doc/0472)과 `SoundManager`의 볼륨/뮤트
  설정(doc/0288) 딱 두 군데뿐이라, 개별 키를 나열하지 않고 전부 지워도 안전함. `SoundManager`는
  `MissionSelect` 씬엔 아예 없지만, 다음에 미션 씬에 들어가 `SoundManager.LoadVolumePrefs()`가
  실행될 때 "저장된 적 있는 키만 덮어쓴다" 로직 덕분에 지워진 키는 자동으로 인스펙터 기본값으로
  돌아감 - 별도 처리 불필요.
- `DeleteAll()` 뒤에 `PlayerPrefs.Save()`를 명시적으로 호출(디스크 반영을 즉시 보장 - 다른
  세터들과 달리 "전체 리셋"은 되돌리기 까다로운 동작이라 자동 저장 타이밍에 기대지 않음)하고,
  `ApplyLockState()`를 다시 호출해 미션 버튼들을 즉시 잠금 상태로 되돌림.

### 검증 (Play Mode)
- 리셋 전 `HighestUnlockedMission=5`, 임의의 `TestDummyKey`를 세팅해두고 `PlayerPrefabReset`
  버튼의 `onClick`을 직접 호출해 확인: 두 키 모두 `PlayerPrefs.HasKey()=False`로 삭제됨,
  미션 버튼은 `Mission0`/`Mission1`만 `interactable=True`로 즉시 되돌아감.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 물 메쉬 노이즈 없이 깨끗함.

### 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs` (`playerPrefsResetButton` 필드 + `ResetPlayerPrefs()`)
- `Assets/Scenes/Missions/MissionSelect.unity` (`playerPrefsResetButton` → `PlayerPrefabReset` 버튼 연결)

## 후속3 - PlayerPrefs 리셋을 MissionSelect에서 MainScene으로 이동 (다음날 추가 요청)

> MainScene에다가 PlayerPrefabReset를 만들었거든 메인화면에서 PlayerPrefs 초기화 할수 있도록
> 해줘 미션 선택에는 초기화하는 버튼 없애도 돼 메인화면에서 초기화 할수 있도록 하려고

### 적용
- `MissionSelectManager.cs`에서 `playerPrefsResetButton` 필드와 `ResetPlayerPrefs()`를 전부 제거.
- `MainMenuController.cs`(`MainScene`의 Play/Option/Exit 버튼을 담당하던 기존 스크립트)에 그대로
  옮겨옴: `playerPrefsResetButton` 필드 추가(다른 개발자용 버튼과 동일한 "정식 버전 출시 전 제거"
  헤더), `ResetPlayerPrefs()`(`PlayerPrefs.DeleteAll()` + `Save()`) 추가, `hoverableButtons`
  배열에도 포함시켜 다른 버튼들과 동일하게 호버 커서가 바뀌도록 함. `MissionSelect`와 달리
  `ApplyLockState()` 같은 즉시 갱신 로직은 필요 없음(메인 화면엔 미션 버튼이 없음).
- 사용자가 `MainScene`에 미리 만들어둔 `PlayerPrefabReset` 버튼을 `MainMenuController.
  playerPrefsResetButton`에 연결.
- `MissionSelect.unity`의 `PlayerPrefabReset` 버튼 GameObject를 씬에서 완전히 제거.

### 검증 (Play Mode)
- `MainScene`만 단독으로 로드한 상태에서: 리셋 전 `HighestUnlockedMission=5` +
  `TestDummyKey`를 세팅해두고 `PlayerPrefabReset` 버튼의 `onClick`을 직접 호출해 확인 - 두 키
  모두 삭제됨.
- `MissionSelect`만 단독으로 로드한 상태에서: `PlayerPrefabReset` 오브젝트가 더는 없음을 확인,
  `MissionSelectManager`와 `UnlockMission` 버튼은 정상 동작(미션0/1만 해금) 유지됨을 확인 - 필드
  제거가 다른 기능에 영향 없음.
- Unity 콘솔: 두 씬 모두에서 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`,
  `ErrorCount: 0`.
- `git status`: 물 메쉬 노이즈 없이 깨끗함.

### 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs` (`playerPrefsResetButton`/`ResetPlayerPrefs()` 제거)
- `Assets/Scripts/UI/MainMenuController.cs` (`playerPrefsResetButton`/`ResetPlayerPrefs()` 추가)
- `Assets/Scenes/MainScene/MainScene.unity` (`playerPrefsResetButton` → `PlayerPrefabReset` 버튼 연결)
- `Assets/Scenes/Missions/MissionSelect.unity` (`PlayerPrefabReset` 버튼 오브젝트 제거)

## 후속4 - 메인 메뉴로 돌아가는 Close 버튼 (같은 날 추가 요청)

> MissionSelectManager에다가 BacktoMainMenu 버튼을 추가해줘 내가 Close버튼을 하나 만들었는데
> 그걸로 돌아갈수있게

`SceneMenuController.cs`(게임플레이 씬의 "메인화면으로 나가기" 버튼)와 동일한 컨벤션 -
`mainSceneName`(문자열 필드, 기본값 `"MainScene"`) + `SceneManager.LoadScene(mainSceneName)`.

### 적용
- `MissionSelectManager.cs`에 `backToMainMenuButton`(씬의 `Close` 버튼) +
  `mainMenuSceneName`(기본값 `"MainScene"`) 필드 추가, `Awake()`에서
  `backToMainMenuButton?.onClick.AddListener(BackToMainMenu)`로 연결.
- `BackToMainMenu()`: `SceneManager.LoadScene(mainMenuSceneName)`.
- 씬의 `Close` 버튼을 `backToMainMenuButton` 필드에 연결.

### 검증 (Play Mode)
- `Close` 버튼의 `onClick`을 직접 호출한 뒤 확인: 활성 씬이 `MissionSelect` → `MainScene`으로
  정상 전환됨.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 물 메쉬 노이즈 없이 깨끗함.

### 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs` (`backToMainMenuButton`/`BackToMainMenu()` 추가)
- `Assets/Scenes/Missions/MissionSelect.unity` (`backToMainMenuButton` → `Close` 버튼 연결)
