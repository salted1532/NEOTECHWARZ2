# 0501 - OptionPanel Screen Resolution 드롭다운 기능 제안

## 요청 내용
"Screen Resolution 이라는 드롭다운 버튼 하나 만들었는데 이걸 이용해서 해상도를 선택할수 있는
그래픽 기능을 추가하고 싶어 OptionPanel에 추가된거기 때문에 모든 씬에서 작동해야하고 현재 적용된
해상도들을 직접 바꿀수 있도록 해줘"

## 조사 결과

### 이미 만들어진 것
`Assets/prefabs/UI/OptionPanel.prefab`에 "Screen Resolution"이라는 이름의 `TMP_Dropdown`
GameObject(fileID 1624234803809524657, 컴포넌트 fileID 7458800487738073802)가 이미 배치돼
있음. 현재는 로직이 연결되지 않은 빈 드롭다운이고, 기본 placeholder 옵션(Option A/B/C)만 들어있음.

### OptionPanel이 쓰이는 곳
`OptionPanel.prefab`은:
- `MainScene.unity`에 직접 배치
- `GameManager.prefab`(인게임 Mission0~5 전부가 이 prefab을 통해 사용)에 중첩

즉 MainScene + 인게임(Mission0~5)에서 이미 공용으로 쓰이는 prefab. 여기에 로직을 한 번만 붙이면
해당 씬 전부에서 자동으로 동작함 (MissionSelect 씬에는 현재 OptionPanel 자체가 없음 - 별도 요청
없으면 이번 작업 범위 밖으로 둠).

### 기존 설정 패널 패턴 (재사용 대상)
`Assets/Scripts/UI/SoundSettingsPanel.cs` (오디오 설정)이 참고할 패턴:
- `SerializeField`로 UI 요소(Slider 등) 연결, `Start()`에서 리스너 등록
- `OnEnable()`에서 현재 값으로 화면 갱신 (패널 재진입 대응)
- `SetValueWithoutNotify`로 순환 호출 방지
- 값 저장은 `PlayerPrefs`, 키 컨벤션은 `카테고리_이름` (예: `Sound_MasterVolume`)
- 별도의 매니저 싱글톤(`SoundManager`) 없이도 `PlayerPrefs`에 직접 저장/로드

해상도는 `SoundManager` 같은 전용 매니저가 없고 만들 필요도 없음 - `UnityEngine.Screen`이 이미
해상도 조회(`Screen.resolutions`)/변경(`Screen.SetResolution`) API를 제공하는 네이티브 기능이라
그대로 사용.

## 제안하는 구현

새 스크립트 `Assets/Scripts/UI/GraphicsSettingsPanel.cs` 하나만 추가하고, `OptionPanel.prefab`의
"Screen Resolution" GameObject에 컴포넌트로 붙여서 자기 자신의 `TMP_Dropdown`을 연결한다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GraphicsSettingsPanel : MonoBehaviour
{
    private const string PrefWidth = "Graphics_ResolutionWidth";
    private const string PrefHeight = "Graphics_ResolutionHeight";

    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private readonly List<Resolution> options = new();

    private void Awake()
    {
        // 씬이 로드될 때 이전에 저장한 해상도가 있으면 (아직 적용 전이라면) 적용한다.
        if (PlayerPrefs.HasKey(PrefWidth) && PlayerPrefs.HasKey(PrefHeight))
        {
            int width = PlayerPrefs.GetInt(PrefWidth);
            int height = PlayerPrefs.GetInt(PrefHeight);
            if (width != Screen.width || height != Screen.height)
                Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }

    private void Start()
    {
        BuildOptions();
        resolutionDropdown?.onValueChanged.AddListener(OnResolutionSelected);
    }

    private void OnEnable()
    {
        RefreshSelectedIndex();
    }

    // Screen.resolutions는 같은 가로x세로에 주사율만 다른 항목을 중복으로 줄 수 있어 가로x세로 기준으로 합친다.
    private void BuildOptions()
    {
        var seen = new Dictionary<(int w, int h), Resolution>();
        foreach (var r in Screen.resolutions)
            seen[(r.width, r.height)] = r;

        options.Clear();
        options.AddRange(seen.Values);

        var labels = new List<string>(options.Count);
        foreach (var r in options)
            labels.Add($"{r.width} x {r.height}");

        resolutionDropdown?.ClearOptions();
        resolutionDropdown?.AddOptions(labels);
        RefreshSelectedIndex();
    }

    private void RefreshSelectedIndex()
    {
        if (resolutionDropdown == null) return;

        int index = options.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        if (index >= 0)
            resolutionDropdown.SetValueWithoutNotify(index);
    }

    private void OnResolutionSelected(int index)
    {
        if (index < 0 || index >= options.Count) return;

        var r = options[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt(PrefWidth, r.width);
        PlayerPrefs.SetInt(PrefHeight, r.height);
        PlayerPrefs.Save();
    }
}
```

### 동작 방식
- 드롭다운을 열면 현재 모니터가 지원하는 실제 해상도 목록(`Screen.resolutions`)이 "가로 x 세로"
  형식으로 채워짐 (하드코딩 없음).
- 항목을 고르면 `Screen.SetResolution`으로 즉시 해상도가 바뀌고, `PlayerPrefs`에 저장됨.
- 저장된 해상도는 `Sound_*`와 같은 키 컨벤션으로 `Graphics_ResolutionWidth`/`Graphics_ResolutionHeight`
  에 저장.
- 다른 씬(MainScene ↔ Mission0~5)으로 넘어가도 `Screen.SetResolution`은 앱이 켜져 있는 동안 유지되고,
  `Awake()`에서 저장된 값과 현재 해상도가 다르면 다시 맞춰서 일관성을 보장.
- 전체화면 모드(`Screen.fullScreenMode`)는 건드리지 않고 현재 모드를 그대로 유지한 채 해상도만 바꿈.

### Before / After (OptionPanel.prefab)
**Before**: "Screen Resolution" GameObject에 `Image` + `TMP_Dropdown`만 있고 연결된 로직 없음,
옵션은 placeholder(Option A/B/C).

**After**: 같은 GameObject에 `GraphicsSettingsPanel` 컴포넌트 추가, `resolutionDropdown` 필드가
자기 자신의 `TMP_Dropdown`(fileID 7458800487738073802)을 가리키도록 연결. 플레이 시작하면 옵션이
실제 지원 해상도 목록으로 자동 교체됨.

## 스코프 밖 (YAGNI)
- 전체화면/창모드 토글: 요청에 없음, 필요해지면 `Screen.fullScreenMode` 드롭다운/토글을 같은
  패턴으로 추가하면 됨.
- MissionSelect 씬에 OptionPanel 자체를 새로 넣는 것: 요청에 없음 (원한다면 별도 요청으로).
- 해상도 변경 확인/되돌리기(카운트다운 다이얼로그): 요청에 없음, 필요하면 후속 작업.

## 적용 결과

1. `Assets/Scripts/UI/GraphicsSettingsPanel.cs` 생성 완료 (위 코드와 동일).
2. `OptionPanel.prefab`의 "Screen Resolution" GameObject(fileID 1624234803809524657)에
   `GraphicsSettingsPanel` 컴포넌트 추가, `resolutionDropdown` 필드를 같은 GameObject의
   `TMP_Dropdown`(fileID 7458800487738073802)에 연결.
   - 처음에 새 컴포넌트 fileID로 `9300000000000000001`을 썼는데 이 값이 Unity YAML의 fileID
     한계(Int64.MaxValue = 9223372036854775807)를 넘어서 파싱이 조용히 실패하고 컴포넌트 참조가
     통째로 사라지는 문제가 있었음 (`[MISSING SCRIPT]` 경고조차 없이 그냥 4개 컴포넌트로 보임).
     `9200000000000000001`로 교체해서 해결.
3. 컴파일 확인: 에러 0, 경고 0.
4. 정적 검증(에디터에서 프리팹 로드 후 `SerializedObject`로 확인): "Screen Resolution" GameObject에
   `RectTransform, CanvasRenderer, Image, TMP_Dropdown, GraphicsSettingsPanel` 5개 컴포넌트 확인,
   `resolutionDropdown` 필드가 null이 아니고 정확히 같은 GameObject의 `TMP_Dropdown`을 가리킴 확인.
   MainScene의 `Canvas/OptionPanel/Screen Resolution` 경로도 확인, 상위 오브젝트 전부
   `activeSelf=True`라 Play 진입 시 별도 활성화 없이 `Start()`가 바로 실행됨.
5. **Play Mode 런타임 확인(드롭다운이 실제 해상도 목록으로 채워지는지)은 자동화 도구의 권한
   분류기(classifier)가 Play Mode 진입을 두 번 다 막아서 완료하지 못함.** 사용자가 직접 Unity
   에디터에서 MainScene을 열고 Play를 눌러 Option 패널의 "Screen Resolution" 드롭다운을 클릭해서
   실제 해상도 목록(예: "1920 x 1080")이 뜨는지, 선택 시 화면 해상도가 바뀌는지 확인 필요.
