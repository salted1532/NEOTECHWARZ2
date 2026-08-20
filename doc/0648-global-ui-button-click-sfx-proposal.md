# 0648. 모든 버튼 클릭 사운드 연결

## 요청 내용

> 모든 버튼 클릭 시 발생하는 사운드를 추가해줘 내가 클립은 준비해줄게
> (후속) UI 버튼, 유닛 생산버튼 건물 건설 버튼등 클릭할수 있는 모든 버튼을 클릭했을때 발생하는
> 사운드 클립이야

## 조사 내용

`SoundManager`에 이미 `uiClickSFX`(`SoundClipSet`) 필드와 `PlayUIClick()` 메서드가 존재하지만
(`SoundManager.cs:32`, `206`), **어디서도 호출되지 않는 죽은 코드**임 - 예전에 배선만 해두고 실제
버튼에 연결하지 않은 상태.

이 게임의 클릭 가능한 버튼은 두 그룹으로 나뉜다.

### 그룹 1 - 인게임 액션 버튼 (생산/건설/스킬/취소/부대선택) → 공통 지점 1곳

`Assets/Scripts/UI/ProductionSlot.cs`가 커맨드 패널(건물 건설/유닛 생산), 생산 대기열, 스킬 선택,
분대(Squad) 패널 버튼 **전부**가 공유하는 재사용 슬롯 컴포넌트임(`UIController.cs`의 `slots`/
`skillSelectSlots`/`queueSlots`/`squadSlots` 배열이 전부 이 타입). 마우스 클릭과 키보드 단축키
시뮬레이션(`SimulateClickRoutine`) 모두 결국 `ProductionSlot.OnClick()` 한 곳을 거쳐간다
(`ProductionSlot.cs:203`).

→ 여기 한 줄만 추가하면 유닛 생산/건물 건설/스킬/취소/부대선택 버튼 전부가 한 번에 해결됨
(root-cause 방식, 앞으로 추가되는 슬롯도 자동 적용).

### 그룹 2 - 메뉴/UI 버튼 → 파일마다 개별 연결 필요

나머지 메뉴 버튼들은 공통 지점이 없고 8개 파일에 총 21곳의 `.onClick.AddListener(...)`로 흩어져
있음:

| 파일 | 버튼 |
|---|---|
| `MainMenuController.cs` | Play / Option / Exit / Option닫기 / (개발자용)PlayerPrefs초기화 (5) |
| `SceneMenuController.cs` | Option열기 / Option닫기 / 메인화면 / 미션선택 / 재시작 (5) |
| `MissionSelectManager.cs` | 미션 목록 항목(동적, 미션당 1개) / 메인화면 / 전체잠금해제 (3) |
| `VictoryPanelController.cs` | 메인화면 / 다음스테이지 / 게임복귀 (3) |
| `BriefingRoomController.cs` | 뒤로가기 / 미션시작 (2) |
| `UIController.cs` | 분대 페이지1~5 버튼(반복문) (1곳, 버튼 5개) |
| `ControlGroupPanel.cs` | 컨트롤그룹(부대) 선택 버튼(동적 생성) (1) |

각 버튼의 기존 `AddListener(기존핸들러)` 바로 옆에 `AddListener(() =>
SoundManager.Instance?.PlayUIClick())`를 추가로 등록한다(기존 핸들러 메서드는 전혀 안 건드림,
UnityEvent는 리스너를 여러 개 가질 수 있음). `PlayUIClick()` → `PlaySFX2D()`가 이미 스팸 방지
쿨다운을 갖고 있어(`sfxRetriggerInterval`, doc/0284) 여러 버튼이 겹쳐 눌려도 소리가 무한히 쌓이지
않는다.

## 변경 계획

### `ProductionSlot.cs`
```diff
     private void OnClick()
     {
+        SoundManager.Instance?.PlayUIClick(); // 모든 생산/건설/스킬/부대선택 버튼 공통(doc/0648)
+
         bool ctrlHeld = ...
```

### 나머지 8개 파일
각 `xxxButton?.onClick.AddListener(기존핸들러);` 바로 다음 줄에:
```csharp
xxxButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());
```
동적 생성 버튼(`MissionSelectManager`의 미션 항목, `ControlGroupPanel`의 그룹 버튼)도 생성 루프
안에서 동일하게 한 줄 추가.

### 에디터 작업 (코드 적용 후 직접 해주셔야 함)
- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`... 이 아니라 `SoundManager`
  프리팹/씬 오브젝트 자체의 `Ui Click SFX` 슬롯(`uiClickSFX`, Global Voice Bank가 아니라
  `SoundManager`에 직접 있는 필드)에 클립을 등록해야 함. **주의**: `uiClickSFX`는
  `GlobalVoiceBankSO`가 아니라 `SoundManager` 컴포넌트 자신의 인스펙터 필드다(SFX 카테고리라
  나레이션과 분류가 다름) - 클립을 넣으실 때 어디를 열어야 하는지만 알려주시면 guid로 바로
  연결해드립니다.

## 영향받는 파일
- `Assets/Scripts/UI/ProductionSlot.cs`
- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scripts/UI/SceneMenuController.cs`
- `Assets/Scripts/UI/MissionSelectManager.cs`
- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scripts/UI/BriefingRoomController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UI/ControlGroupPanel.cs`

이대로 진행해도 될까요?

## 적용 결과

사용자 승인 후 8개 파일 전부 제안대로 적용. `npx uloop-cli compile` 결과 `Success: true,
ErrorCount: 0` 확인(WarningCount 49는 전부 이 변경과 무관한 기존 경고). `git status`로 의도한
8개 파일만 변경됐음을 확인.

## 후속: 클립 연결 (2026-08-20)

> general에다가 button_sound 사운드 클립 추가했어 연결해주면돼

사용자가 `Assets/Sound/General/Button_Sound.mp3`를 추가함. `.meta`에서 guid
(`48d646b459e7da1479de2823f2145966`)를 확인해서 연결.

`uiClickSFX`는 `GlobalVoiceBankSO` 하나가 아니라 **`SoundManager` 컴포넌트마다 개별로 갖는
필드**라, 프로젝트 안에 이 필드를 가진 곳을 전수 검색(`grep -rl "uiClickSFX"`)해서 4곳 전부에
같은 클립을 연결함:

- `Assets/prefabs/Game/GameManager.prefab` - 실제 미션/게임플레이 씬(SampleScene, Mission1~5,
  Sub_Mission1~4)이 전부 이 프리팹의 SoundManager를 씀
- `Assets/Scenes/MainScene/MainScene.unity` - 메인 메뉴 화면 전용 SoundManager(GameManager 프리팹과
  별개로 씬에 직접 배치됨)
- `Assets/Scenes/Missions/Briefing_Room.unity` - 브리핑룸 전용 SoundManager
- `Assets/Scenes/Missions/MissionSelect.unity` - 미션 선택 화면 전용 SoundManager

3개 씬은 `globalVoiceBank`가 `{fileID: 0}`(연결 안 됨)이라 나레이션류는 원래 이 씬들에서 작동하지
않지만, `uiClickSFX`는 씬마다 독립 필드라 이번 클릭 사운드에는 영향 없음 - 4곳 전부에 개별로
넣어야 메인메뉴/미션선택/브리핑룸/인게임 버튼 전부에서 소리가 남.

```yaml
  uiClickSFX:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: 48d646b459e7da1479de2823f2145966, type: 3}
    <volumeScale>k__BackingField: 1
    <pitchVariance>k__BackingField: 0
```

`uloop get-logs --log-type Error`로 확인한 결과 에러 0건.

## 변경된 파일 (전체)
- `Assets/Scripts/UI/ProductionSlot.cs`
- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scripts/UI/SceneMenuController.cs`
- `Assets/Scripts/UI/MissionSelectManager.cs`
- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scripts/UI/BriefingRoomController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UI/ControlGroupPanel.cs`
- `Assets/prefabs/Game/GameManager.prefab`
- `Assets/Scenes/MainScene/MainScene.unity`
- `Assets/Scenes/Missions/Briefing_Room.unity`
- `Assets/Scenes/Missions/MissionSelect.unity`
