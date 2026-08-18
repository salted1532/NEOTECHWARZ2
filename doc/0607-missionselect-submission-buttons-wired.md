# 0607 — MissionSelectManager에 서브미션 1~4 버튼 연결

날짜: 2026-08-18

## 요청 내용

`MissionSelectManager`에 `Sub_Mission1`~`Sub_Mission4`를 추가하고 클릭 시 해당 씬으로 이동하도록
연결해달라는 요청.

## 조사 내용

- `MissionSelect.unity`를 열어 확인해보니 `Sub_Mission1`~`Sub_Mission4` 버튼(Image+Button)은 이미
  `Mission_Select_panel` 밑에 배치돼 있었음(사용자가 미리 만들어둠) — `MissionSelectManager.missions`
  리스트에만 아직 연결이 안 된 상태였음.
- `MissionSelectEntry.missionNumber`는 두 가지 역할을 겸함 — ① `ApplyLockState()`의
  `missionNumber <= highestUnlocked` 해금 판정, ② 로컬라이제이션 키(`missionselect.name.{번호}` 등)
  조회. 서브미션에 본편과 같은 번호를 그대로 쓰면 ①(같이 병행되는 본편 미션이 열리면 서브미션도 같이
  열린다는 자연스러운 규칙)은 맞지만, ②는 본편 이름 키와 충돌해서 서브미션에 본편 이름이 잘못 뜨는
  문제가 생김.
- 그래서 `MissionSelectEntry`에 `isSubMission` 플래그를 추가해 ①(해금 판정)은 그대로 본편과 번호를
  공유하되, ②(이름/툴팁 부제 키 조회)만 서브미션 전용 키(`missionselect.name.sub{번호}`,
  `missionselect.tooltip.subtitle.sub`)로 분기하도록 했다. 행성 이름(`missionselect.planet.{번호}`)은
  서브미션도 병행되는 본편과 같은 행성이라 그대로 공유해도 맞음 - 별도 키 불필요.
- `Sub_Mission1`~`4` 씬이 `EditorBuildSettings.scenes`(Build Settings)에 등록돼 있지 않아서, 이대로면
  `SceneManager.LoadScene("Sub_Mission1")`이 씬을 찾지 못해 실패하는 상태였음 - 같이 등록해야 함.

## 코드 변경

### `Assets/Scripts/UI/MissionSelectManager.cs`

기존 코드:
```csharp
[System.Serializable]
public class MissionSelectEntry
{
    public Button button;
    public int missionNumber;
    public string missionName;
    public string sceneName; // SceneManager.LoadScene에 넘길 씬 이름 (예: "Mission0")
}
```

변경 코드:
```csharp
[System.Serializable]
public class MissionSelectEntry
{
    public Button button;
    public int missionNumber; // 서브미션은 같이 병행되는 본편 미션 번호를 그대로 써서 해금 상태를 공유한다
    public string missionName;
    public string sceneName; // SceneManager.LoadScene에 넘길 씬 이름 (예: "Mission0")
    public bool isSubMission; // true면 이름/툴팁 부제 조회 시 "missionselect.name.sub{missionNumber}" 등 서브미션 전용 키를 쓴다
}
```

`SetupHoverTooltip()` 내부, 기존 코드:
```csharp
string missionName = LocalizationManager.GetTextOrFallback($"missionselect.name.{entry.missionNumber}", entry.missionName);
TooltipUI.Instance?.Show(rect, $"<{missionName}>", LocalizationManager.GetText("missionselect.tooltip.subtitle", entry.missionNumber));
```

변경 코드:
```csharp
string nameKey = entry.isSubMission ? $"missionselect.name.sub{entry.missionNumber}" : $"missionselect.name.{entry.missionNumber}";
string subtitleKey = entry.isSubMission ? "missionselect.tooltip.subtitle.sub" : "missionselect.tooltip.subtitle";
string missionName = LocalizationManager.GetTextOrFallback(nameKey, entry.missionName);
string subtitle = LocalizationManager.GetText(subtitleKey, entry.missionNumber);
string planetName = LocalizationManager.GetText($"missionselect.planet.{entry.missionNumber}");
TooltipUI.Instance?.Show(rect, $"<{missionName}>", $"{subtitle} · {planetName}");
```

### 로컬라이제이션 키 추가 (`ko.json`/`en.json`)

```json
{ "key": "missionselect.name.sub1", "value": "측면 기습" },
{ "key": "missionselect.name.sub2", "value": "잔해 수색" },
{ "key": "missionselect.name.sub3", "value": "구조대 파견" },
{ "key": "missionselect.name.sub4", "value": "최후의 저지선" },
{ "key": "missionselect.tooltip.subtitle.sub", "value": "서브미션 (미션 {0} 병행)" },
```
(en.json은 대응하는 영어 값 — Flanking Strike / Wreckage Search / Search & Rescue Detachment /
Last Line of Defense / "Side Mission (parallel to Mission {0})")

## 씬/설정 변경

- `Assets/Scenes/Missions/MissionSelect.unity` — `MissionSelectManager.missions`에 4개 항목 추가
  (`execute-dynamic-code`로 `SerializedObject` 통해 씬에 이미 있던 `Sub_Mission1`~`4` 버튼을 연결):

  | button | missionNumber | missionName(폴백) | sceneName | isSubMission |
  |---|---|---|---|---|
  | Sub_Mission1 | 1 | Flanking Strike | Sub_Mission1 | true |
  | Sub_Mission2 | 2 | Wreckage Search | Sub_Mission2 | true |
  | Sub_Mission3 | 3 | Search & Rescue Detachment | Sub_Mission3 | true |
  | Sub_Mission4 | 4 | Last Line of Defense | Sub_Mission4 | true |

- `ProjectSettings/EditorBuildSettings.asset` — `Sub_Mission1.unity`~`Sub_Mission4.unity` 4개를
  Build Settings 씬 목록에 추가(안 하면 `SceneManager.LoadScene`이 씬을 못 찾음).

해금 규칙: `missionNumber`를 병행되는 본편 미션과 공유하므로, 예를 들어 Sub_Mission2는 `Mission2`가
열릴 때(=Mission1 클리어 시) 같이 열린다. Sub_Mission1은 `Mission1`과 같은 번호(1)라 기본값
(`DefaultHighestUnlockedMission = 1`)만으로 처음부터 열려 있다.

## 부작용 처리

`execute-dynamic-code`로 여러 씬(Mission1 등)을 여는 과정에서 TMP 폰트 SDF 아틀라스/Ocean 메쉬
에셋이 에디터에 의해 재직렬화되어 의도치 않게 변경 표시됨 - 이번 작업과 무관해서 `git checkout`으로
되돌림.

## 검증

`npx uloop-cli compile` — `ErrorCount: 0` (기존과 동일한 warning만). 씬 저장 후 `missions.arraySize`가
6→10으로 늘고 4개 항목이 올바르게 채워진 것을 다시 읽어 확인.

## 요약/남은 작업

MissionSelect 화면에서 서브미션 1~4 버튼을 클릭하면 해당 씬으로 이동하도록 연결 완료. 서브미션
씬들에는 아직 `SubStageNObjectives` 컴포넌트가 붙어있지 않고(doc/0606 참고) 실제 미션 오브젝트도
배치돼 있지 않아, 지금 버튼을 누르면 씬은 열리지만 임무 목표/승패 판정은 동작하지 않는 빈 맵 상태다 -
다음 단계로 남겨둠.

## 변경된 파일

- `Assets/Scripts/UI/MissionSelectManager.cs`
- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`
- `Assets/Scenes/Missions/MissionSelect.unity`
- `ProjectSettings/EditorBuildSettings.asset`
- `doc/0607-missionselect-submission-buttons-wired.md` (이 로그)
