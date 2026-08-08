# 0470. MissionSelectManager 구현

**날짜:** 2026-08-08

**상태: 구현 완료** (아래 "확인 필요 사항"에 대한 답변 반영 후 씬 작업까지 전부 진행함)

## 확인 답변 및 최종 결정
1. 미션 이름: `Docs/Campaign.md`에서 가져옴 - Boot Camp(0) / Border Conflict(1) / Unknown Signal(2) /
   Invasion(3) / United Front(4) / Final Offensive(5).
2. 버튼→씬: `Mission0`~`Mission5` 1:1 매칭.
3. 씬 작업(Build Settings 등록, 버튼 연결, Tooltip 배선, Play 버튼 타겟 변경)까지 전부 진행함.
4. 툴팁은 새로 만들지 않고 **사용자가 이미 `MissionSelect.unity`의 Canvas 밑에 만들어둔 `ToolTip`
   오브젝트(배경 Image + `Text (TMP)` 하나)**를 그대로 재사용 - 기존 `TooltipUI.cs`를 그 오브젝트에
   붙이고, `titleText`만 그 `Text (TMP)`에 연결, `descriptionText`/`costRows`는 비워둠(둘 다 null-safe
   설계라 문제 없음). 미션 이름 + 번호를 `"{missionName}\nMission {missionNumber}"` 한 문자열로 합쳐서
   `titleText`에 넣고, `description`은 빈 문자열로 호출(=컴팩트 모드, 제목 크기에 맞춰서만 리사이즈).

## 요청 내용
> 이제 미션 선택 창을 만들껀데 씬을 하나 따로 쓰려고해 Play 버튼을 누르면 MissionSelect 창으로 가고
> 거기엔 각 맵으로 가는 버튼들이있는데 미션0 부터 5까지 일단 구현할거고 각 버튼마다 이동하는 씬을
> 지정할수 있는 Manager를 만들고 그리고 각 버튼 호버시 툴팁이 해당 버튼 위쪽에 나오도록 할건데
> ToolTip엔 미션이름, 미션 번호 정도만 나오도록 할거야 MissionSelectManager를 만들어줘

## 조사 결과 (기존 자산/패턴)

- **씬은 이미 존재함**: `Assets/Scenes/Missions/MissionSelect.unity`에 `Mission0`~`Mission5` 이름의
  Button 오브젝트가 이미 배치돼 있음(Button/Image만 있고 스크립트 연결은 안 돼 있음). 다만 아직
  **Build Settings에 등록 안 돼 있음**(`EditorBuildSettings.asset`엔 현재 `MainScene` +
  `Mission0`~`Mission5`만 등록됨) - `SceneManager.LoadScene("MissionSelect")`가 되려면 등록 필요.
- **Play 버튼**: `Assets/Scripts/UI/MainMenuController.cs`의 `OnPlayClicked()`이
  `SceneManager.LoadScene(testSceneName)`을 호출(`testSceneName` 필드 기본값 `"TestScene"`). 코드
  변경 없이 이 필드 값을 인스펙터에서 `"MissionSelect"`로 바꾸기만 하면 됨.
- **툴팁은 이미 있는 걸 그대로 재사용 가능**: `Assets/Scripts/UI/Tooltip/TooltipUI.cs`가 싱글턴
  (`static TooltipUI Instance`)으로 `Show(RectTransform target, string title, string description)`
  / `Hide()`를 제공, 버튼 위쪽에 자동 배치(`GetWorldCorners()` 기준 상단 중앙 + 마진). 만들 필요 없이
  그대로 호출만 하면 됨.
- **호버 연결 패턴도 이미 있음**: `UIController.AddStatHoverTooltip()`(EventTrigger를 코드로 추가해
  `PointerEnter`→`TooltipUI.Show`, `PointerExit`→`TooltipUI.Hide` 등록)이 동일한 목적의 기존
  구현이라 그대로 따라 하면 됨.
- **주의: `TooltipUI`는 현재 `GameManager.prefab`(`Mission0`~`Mission5` 씬에만 배치됨)에 붙어있고,
  `MainScene`/`MissionSelect`엔 없음.** `GameManager.prefab`엔 `RTSUnitController`/`ResourceManager`/
  `UpgradeManager` 등 RTS 게임플레이 전체가 딸려있어서 메뉴 씬에 통째로 넣는 건 낭비/부적절함 - 대신
  `MissionSelect.unity`에 `TooltipUI` 하나만 들어있는 가벼운 Canvas를 새로 만들어 넣어야 함.
- **미션 이름/번호를 담아둔 기존 데이터(ScriptableObject 등)는 없음** - 새 매니저 안에 직접
  들고 있어야 함.

## 설계 제안

### `MissionSelectManager.cs` (신규, `Assets/Scripts/UI/`)

```csharp
[System.Serializable]
public class MissionSelectEntry
{
    public Button button;          // 인스펙터에서 Mission0~5 버튼 연결
    public int missionNumber;      // 툴팁에 표시할 번호
    public string missionName;     // 툴팁에 표시할 이름
    public string sceneName;       // 클릭 시 로드할 씬 이름 (예: "Mission0")
}

public class MissionSelectManager : MonoBehaviour
{
    [SerializeField] private List<MissionSelectEntry> missions = new();

    private void Start()
    {
        foreach (var entry in missions)
        {
            if (entry.button == null) continue;
            entry.button.onClick.AddListener(() => LoadMission(entry));
            SetupHoverTooltip(entry);
        }
    }

    private void LoadMission(MissionSelectEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.sceneName))
            SceneManager.LoadScene(entry.sceneName);
    }

    private void SetupHoverTooltip(MissionSelectEntry entry)
    {
        // UIController.AddStatHoverTooltip()과 동일한 EventTrigger 패턴
        // PointerEnter → TooltipUI.Instance?.Show(rect, entry.missionName, $"Mission {entry.missionNumber}")
        // PointerExit  → TooltipUI.Instance?.Hide()
    }
}
```

- 버튼-씬 매핑을 코드에 하드코딩하지 않고 인스펙터의 `List<MissionSelectEntry>`로 노출 - 요청하신
  "각 버튼마다 이동하는 씬을 지정할 수 있는" 요구사항에 맞고, 나중에 미션이 추가/순서가 바뀌어도
  코드 수정 없이 인스펙터에서만 처리 가능.
- 싱글턴 아님(`static Instance` 없음) - `MissionSelect.unity`에만 존재하는 화면 전용 매니저라
  다른 씬에서 참조할 일이 없음(`TooltipUI`는 싱글턴이지만 그건 여러 화면에서 공용으로 쓰기 때문 -
  이 매니저는 그런 이유가 없음).
- 툴팁 문구: `Show(target, entry.missionName, $"Mission {entry.missionNumber}")` - 제목에 미션 이름,
  설명 줄에 "Mission N" 한 줄만. (요청하신 "미션이름, 미션 번호 정도만"에 맞춤)

### 같이 필요한 비-코드 작업 (씬/설정)

1. **Build Settings에 `MissionSelect` 씬 추가** (`SceneManager.LoadScene`으로 이름 로드하려면 필수).
2. **`MissionSelect.unity`에 `MissionSelectManager`를 붙일 빈 GameObject 생성**, 인스펙터에서
   기존 `Mission0`~`Mission5` 버튼 6개를 각각 연결하고 `missionName`/`missionNumber`/`sceneName`
   값 채우기(이름은 제가 임의로 못 정하니 실제 미션 이름을 알려주시거나, 우선 "Mission 0"~"Mission 5"
   같은 placeholder로 채워두고 나중에 바꾸는 방식도 가능).
3. **`MissionSelect.unity`에 `TooltipUI` 전용 가벼운 Canvas 추가** (`GameManager.prefab` 전체를
   넣지 않고, `TooltipUI` + `TooltipContentFitter`만 있는 최소 UI만).
4. **`MainMenuController.testSceneName` 필드 값을 `"MissionSelect"`로 변경** (코드 수정 아님,
   인스펙터 값만 변경).

## 실제 적용 내역

- `Assets/Scripts/UI/MissionSelectManager.cs` 신규 생성 - 위 설계안 그대로, 툴팁 호출부만
  `TooltipUI.Instance?.Show(rect, $"{entry.missionName}\nMission {entry.missionNumber}", string.Empty)`
  로 확정(별도 description 슬롯이 없는 사용자 제작 `ToolTip`에 맞춤).
- `MissionSelect.unity`의 `ToolTip` 오브젝트에 `TooltipUI` 컴포넌트 추가 및 필드 연결
  (`root`=자기 자신, `canvasRect`=Canvas, `uiCamera`=비움(ScreenSpaceOverlay), `titleText`=
  `Text (TMP)`, `descriptionText`/`costRows`/`oreText`/`gasText`/`populationText`=비움).
- `MissionSelect.unity`에 `MissionSelectManager` GameObject 신규 생성, `Mission0`~`Mission5` 버튼
  6개 전부 연결 완료(이름/번호는 위 표 그대로, `sceneName`은 버튼명과 동일한 `Mission0`~`Mission5`).
- `ProjectSettings/EditorBuildSettings.asset`에 `MissionSelect` 씬 추가(`MainScene` 바로 다음 순서).
- `MainScene.unity`의 `MainMenuController.testSceneName` 값을 `"MissionSelect"`로 변경(기존 값은
  `"Mission0"`이었음 - Play 버튼이 이제 미션 선택 화면으로 감).

## 검증 (Play Mode)

- `MissionSelectManager`가 6개 버튼 전부를 정상적으로 찾아 연결했는지 확인(각 버튼에
  `EventTrigger`가 PointerEnter/PointerExit 2개 항목으로 붙어있음), `TooltipUI.Instance`도 정상.
- Mission2 버튼의 `PointerEnter` 콜백을 직접 호출해 확인: `ToolTip.activeSelf=True`,
  텍스트 `"Unknown Signal\nMission 2"`, 버튼 위쪽으로 정상 배치됨(`anchoredPosition`이 버튼 위치
  기준으로 위로 이동한 값).
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- `git status`: 물 메쉬 애셋 노이즈 있었음 → `git checkout --`로 되돌림. `Mission2.unity`/
  `Mission4.unity` 변경은 이 세션이 만든 게 아니라 기존에 있던 다른 작업 상태(건드리지 않음).
  `Cyborg Soldier `/`Railgunner`/`Striker`/`Brute Mech`/`Heavy Assault Tank`/`Ironhawk`/`Raven`
  프리팹 변경도 동시에 진행 중인 다른 세션(`doc/0460`)의 작업 - 건드리지 않음.

## 변경/생성된 파일

- `Assets/Scripts/UI/MissionSelectManager.cs` (신규)
- `Assets/Scenes/Missions/MissionSelect.unity` (`ToolTip`에 `TooltipUI` 연결, `MissionSelectManager`
  오브젝트 및 버튼 6개 연결)
- `Assets/Scenes/MainScene/MainScene.unity` (`MainMenuController.testSceneName` → `"MissionSelect"`)
- `ProjectSettings/EditorBuildSettings.asset` (`MissionSelect` 씬 추가)

## 후속 - 텍스트 2개 분리 + 배경 이미지 자동 리사이즈 (같은 날 추가 요청)

> tooltip의 텍스트를 1개 더 추가해서 각각 이름이랑 미션번호 넣어주면 되고 뒤에 배경 이미지를
> 텍스트 크기에 따라 맞춰서 변하도록 해줘

처음엔 `ToolTip`에 텍스트가 하나뿐이라 이름+번호를 한 줄로 합쳐서 `titleText`에만 넣고
`descriptionText`는 비워뒀었음(`TooltipContentFitter`가 `descriptionText == null`이면 "컴팩트
모드"로 제목 크기에만 맞춤). 이번 요청으로 원래 `TooltipUI`가 설계된 대로(제목/설명 분리) 정상
사용하게 됨.

### 적용
- `ToolTip/Text (TMP)`를 복제해 `Text (TMP) (1)` 생성 - 이름 텍스트는 그대로 위쪽, 새로 만든 텍스트는
  아래쪽에 배치하고 폰트 크기를 살짝 줄여 "Mission N" 번호 줄로 사용(초기 위치일 뿐, 실제 배치는
  아래처럼 `TooltipContentFitter`가 매번 다시 계산함).
- `TooltipUI.titleText` = 이름 텍스트, `descriptionText` = 번호 텍스트로 재연결.
- `MissionSelectManager.SetupHoverTooltip()`의 `Show()` 호출을 합친 문자열 1개→ `Show(rect,
  entry.missionName, $"Mission {entry.missionNumber}")` 형태로 되돌림(원래 `TooltipUI`가 받게
  설계된 시그니처 그대로).
- 배경 `Image`(기존엔 `ToolTip`의 자식으로 고정 크기 100x100, 중앙 고정 앵커)의 RectTransform 앵커를
  `(0,0)~(1,1)` 스트레치로 변경 - `TooltipContentFitter.Fit()`이 매번 `root`(=`ToolTip`)의
  `sizeDelta`를 텍스트 분량에 맞춰 다시 계산하는데, `Image`가 이제 그 `root`를 그대로 꽉 채우도록
  따라가므로 텍스트가 길어지면 배경도 자동으로 늘어남(별도 코드 없이 앵커 설정만으로 해결).

### 검증 (Play Mode)
- Mission4 버튼 호버 시: `nameText='United Front'`, `numberText='Mission 4'` 둘 다 정상 표시.
- `ToolTip.sizeDelta`가 콘텐츠에 맞춰 `(100, 113.74)`로 재계산됐고, `Image.rect.size`가 정확히
  똑같은 `(100, 113.74)`로 함께 변함 - 배경이 텍스트 분량에 맞춰 자동으로 리사이즈되는 것 확인.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 물 메쉬 애셋 노이즈 재발 → `git checkout --`로 되돌림.

### 변경된 파일
- `Assets/Scripts/UI/MissionSelectManager.cs` (`Show()` 호출부 title/description 분리)
- `Assets/Scenes/Missions/MissionSelect.unity` (`Text (TMP) (1)` 추가, `TooltipUI` 필드 재연결,
  `Image` 앵커를 스트레치로 변경)

## 후속2 - 배경 Width도 자동 조절 (같은 날 추가 요청)

> Height는 조절이 잘되는데 Width도 조절되도록해줘

`TooltipContentFitter`가 지금까지 세로(Height)만 내용에 맞춰 재계산하고 있었음(`root.sizeDelta.x`는
항상 고정). 이 컴포넌트는 `MissionSelect`뿐 아니라 게임플레이 중 유닛 생산/공격력·방어력 스탯
툴팁에도 공용으로 쓰이는데, 그중 **비용(Ore/Gas/Population) 3줄이 있는 툴팁은 아이콘들이 root
기준 고정 좌표로 배치돼 있어서** 폭까지 자동으로 늘리면 그 배치가 어긋남 - 그래서 **비용이 없는
경우에만** 폭도 자동으로 늘어나게 했음(`MissionSelect`의 이름/번호 툴팁은 비용이 없으므로 해당됨).

### 적용 (`TooltipContentFitter.cs`)
- `horizontalPadding`(좌우 여백 합, 기본 20) 필드와 `defaultRootWidth`(최소 폭 하한, `Configure()`
  시점에 원래 배치된 `root.sizeDelta.x`를 그대로 기록) 추가.
- 텍스트의 `ContentSizeFitter.horizontalFit`을 `Configure()` 시점에 한 번만 고정하지 않고,
  `Fit(hasDescription, hasCost)`가 호출될 때마다 `autoWidth = !hasCost` 기준으로 매번
  `PreferredSize`(비용 없음)/`Unconstrained`(비용 있음)를 다시 지정 - 같은 툴팁이라도 호출부에
  따라(예: 비용 있는 생산 버튼 vs 비용 없는 명령 버튼) 상황이 바뀔 수 있어서.
- `Fit()`에서 `titleWidth`/`descriptionWidth`도 높이와 동일한 방식(`ForceRebuildLayoutImmediate` 후
  `.rect.width` 읽기)으로 측정, `autoWidth`일 때만 `totalWidth = max(defaultRootWidth,
  horizontalPadding + max(titleWidth, descriptionWidth))`로 `root.sizeDelta.x`도 같이 갱신.
  컴팩트 모드(제목만 있는 경우)에도 동일하게 적용.
- 폭이 바뀌면 텍스트가 원래 있던 x좌표에 그대로 남아 중앙에서 벗어나 보이므로, `autoWidth`일 때
  제목/설명 텍스트를 `x=0`(부모 중앙, 텍스트 피벗도 중앙이라 이렇게 하면 항상 가운데 정렬됨)으로
  재배치하는 `SetX()` 헬퍼 추가.
- 배경 `Image`는 이미(후속1에서) `root`를 꽉 채우는 스트레치 앵커라 별도 처리 없이 폭 변경에도
  자동으로 따라감.

### 검증 (Play Mode)
- Mission0(`Boot Camp`, 짧음)과 Mission2(`Unknown Signal`, 긺) 순서로 호버해서 비교: 폭이
  `201.26` → `280.38`로 실제 텍스트 길이에 맞춰 달라짐을 확인, `Image.rect.size`도 매번 정확히
  동일한 값으로 따라감.
- Unity 콘솔: 새로 발생한 Error 없음. `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.
- `git status`: 이번엔 물 메쉬 노이즈 없이 깨끗함.
- **참고**: 비용(Ore/Gas/Population) 있는 생산 버튼 툴팁(`autoWidth=false` 경로)은 이번 세션에서
  실제 게임플레이 씬으로 들어가 라이브 검증은 안 했음 - 코드상 `hasCost`일 때 폭 관련 로직 전체가
  기존과 동일하게(고정 폭, `Unconstrained`) 스킵되도록 가드돼 있어 기존 동작이 그대로 보존될
  것으로 판단했지만, 실제로 유닛 생산 버튼을 호버해서 비용 3줄 배치가 그대로인지 한 번 확인해보는
  걸 권장.

### 변경된 파일
- `Assets/Scripts/UI/Tooltip/TooltipContentFitter.cs`
