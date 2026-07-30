# 0314. 사운드 설정 패널에 인풋필드(직접 수치 입력) 연결

날짜: 2026-07-30

## 결과

승인됨 - 단, 인풋필드 표시/입력 값은 0~1이 아니라 **0~100(%)**로 변경(사용자 선택). 내부 저장은
그대로 0~1 float(`SoundManager`)이고, 인풋필드에 보여주고 받을 때만 ×100/÷100으로 변환. 아래
"제안 코드"는 최초 초안(0~1 버전)이고, 실제 적용된 최종 코드는 이 문서 하단 "최종 적용 코드(0~100%)"
섹션 참고.

## 요청 내용

> 인풋필드로 직접 수치 조정할수 있도록 연결할수 있게 해줘

`Assets/Scripts/UI/SoundSettingsPanel.cs`(`doc/0255`)는 지금 슬라이더 4개(+토글 3개)만 지원함.
슬라이더 옆에 숫자를 직접 타이핑해서 조절할 수 있는 인풋필드를 추가로 연결해달라는 요청.

## 조사 내용 / 전제

- 프로젝트가 이미 `TooltipUI.cs`, `UIController.cs` 등에서 TextMeshPro(`TMPro`)를 쓰고 있어서,
  새 인풋필드도 **TMP_InputField**로 가정하고 작성함. 만약 실제로 만든 게 레거시
  `UnityEngine.UI.InputField`라면 알려주시면 그에 맞게 바꿔드립니다.
- 값의 범위는 기존 슬라이더와 동일하게 **0~1**로 맞춤(슬라이더가 0~1 범위이고 `SoundManager`도
  0~1 float로 저장하므로 - 0~100%로 바꾸려면 표시할 때만 변환하면 되니 필요하면 말씀해주세요).
- 슬라이더 ↔ 인풋필드 ↔ `SoundManager` 세 곳이 항상 같은 값을 보여주도록 서로 동기화:
  - 슬라이더를 드래그하면 인풋필드 텍스트도 같이 갱신됨.
  - 인풋필드에 숫자를 입력하고 엔터/포커스 아웃(`onEndEdit`)하면 슬라이더와 `SoundManager`도 같이 갱신됨.
  - 숫자가 아닌 값을 입력하면 무시하고 기존 값으로 되돌림. 범위를 벗어나면(예: `2.0`) 0~1로 clamp.
- 4개 필드가 거의 똑같은 처리(파싱→clamp→슬라이더/매니저/텍스트 갱신)를 반복하므로, 이번만 작은
  공용 헬퍼(`ApplyInputValue`) 하나를 둬서 중복을 줄임 - 기존 슬라이더/토글 핸들러는 이미 각각
  한 줄짜리라 그대로 유지, 인풋필드 쪽만 로직이 좀 더 복잡해서 헬퍼로 묶음.

## 코드 변경

### `Assets/Scripts/UI/SoundSettingsPanel.cs`

**기존 코드**:
```csharp
using UnityEngine;
using UnityEngine.UI;

// 볼륨 설정 패널 - 슬라이더 4개(주음량/배경음악/효과음/음성) + 토글 3개(배경음악/효과음/음성)를
// SoundManager의 볼륨/뮤트 API에 연결한다 (doc/0255 Phase 4). 주음량은 요청 원문에 토글 언급이 없어
// 슬라이더만 둔다.
//
// 이 스크립트는 로직만 담당한다. 실제 Canvas/슬라이더/토글 GameObject 배치(레이아웃)는 유니티
// 에디터에서 직접 만들고, 이 컴포넌트의 인스펙터 필드에 각 UI 요소를 연결해야 동작한다.
public class SoundSettingsPanel : MonoBehaviour
{
    [Header("슬라이더 (0~1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;

    [Header("토글 (켜짐 = 소리 남, 꺼짐 = 뮤트)")]
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle voiceToggle;

    private void Start()
    {
        masterSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
        voiceSlider?.onValueChanged.AddListener(OnVoiceVolumeChanged);

        bgmToggle?.onValueChanged.AddListener(OnBGMToggleChanged);
        sfxToggle?.onValueChanged.AddListener(OnSFXToggleChanged);
        voiceToggle?.onValueChanged.AddListener(OnVoiceToggleChanged);

        RefreshDisplayedValues();
    }

    // 패널이 다시 열릴 때마다(예: ESC 메뉴 재진입) SoundManager의 현재 값으로 다시 맞춘다.
    private void OnEnable()
    {
        RefreshDisplayedValues();
    }

    // SetValueWithoutNotify를 써서 여기서 값을 세팅해도 onValueChanged가 다시 SoundManager를
    // 호출하는 순환이 생기지 않는다.
    private void RefreshDisplayedValues()
    {
        if (SoundManager.Instance == null)
            return;

        masterSlider?.SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume());
        bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume());
        sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.GetSFXVolume());
        voiceSlider?.SetValueWithoutNotify(SoundManager.Instance.GetVoiceVolume());

        bgmToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsBGMMuted());
        sfxToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsSFXMuted());
        voiceToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsVoiceMuted());
    }

    private void OnMasterVolumeChanged(float value) => SoundManager.Instance?.SetMasterVolume(value);
    private void OnBGMVolumeChanged(float value) => SoundManager.Instance?.SetBGMVolume(value);
    private void OnSFXVolumeChanged(float value) => SoundManager.Instance?.SetSFXVolume(value);
    private void OnVoiceVolumeChanged(float value) => SoundManager.Instance?.SetVoiceVolume(value);

    // 토글 "켜짐" = 소리 남(뮤트 아님) 쪽이 더 직관적이라, SoundManager의 Muted와는 반전해서 넘긴다.
    private void OnBGMToggleChanged(bool isOn) => SoundManager.Instance?.SetBGMMuted(!isOn);
    private void OnSFXToggleChanged(bool isOn) => SoundManager.Instance?.SetSFXMuted(!isOn);
    private void OnVoiceToggleChanged(bool isOn) => SoundManager.Instance?.SetVoiceMuted(!isOn);
}
```

**변경 코드**:
```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 볼륨 설정 패널 - 슬라이더 4개(주음량/배경음악/효과음/음성) + 토글 3개(배경음악/효과음/음성) +
// 인풋필드 4개(슬라이더와 같은 값을 직접 숫자로 입력)를 SoundManager의 볼륨/뮤트 API에 연결한다
// (doc/0255 Phase 4, doc/0314). 주음량은 요청 원문에 토글 언급이 없어 슬라이더/인풋필드만 둔다.
//
// 이 스크립트는 로직만 담당한다. 실제 Canvas/슬라이더/토글/인풋필드 GameObject 배치(레이아웃)는
// 유니티 에디터에서 직접 만들고, 이 컴포넌트의 인스펙터 필드에 각 UI 요소를 연결해야 동작한다.
public class SoundSettingsPanel : MonoBehaviour
{
    [Header("슬라이더 (0~1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;

    [Header("토글 (켜짐 = 소리 남, 꺼짐 = 뮤트)")]
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle voiceToggle;

    [Header("인풋필드 (슬라이더와 같은 0~1 값 - 직접 숫자 입력용)")]
    [SerializeField] private TMP_InputField masterInputField;
    [SerializeField] private TMP_InputField bgmInputField;
    [SerializeField] private TMP_InputField sfxInputField;
    [SerializeField] private TMP_InputField voiceInputField;

    private void Start()
    {
        masterSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmSlider?.onValueChanged.AddListener(OnBGMVolumeChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
        voiceSlider?.onValueChanged.AddListener(OnVoiceVolumeChanged);

        bgmToggle?.onValueChanged.AddListener(OnBGMToggleChanged);
        sfxToggle?.onValueChanged.AddListener(OnSFXToggleChanged);
        voiceToggle?.onValueChanged.AddListener(OnVoiceToggleChanged);

        masterInputField?.onEndEdit.AddListener(OnMasterInputChanged);
        bgmInputField?.onEndEdit.AddListener(OnBGMInputChanged);
        sfxInputField?.onEndEdit.AddListener(OnSFXInputChanged);
        voiceInputField?.onEndEdit.AddListener(OnVoiceInputChanged);

        RefreshDisplayedValues();
    }

    // 패널이 다시 열릴 때마다(예: ESC 메뉴 재진입) SoundManager의 현재 값으로 다시 맞춘다.
    private void OnEnable()
    {
        RefreshDisplayedValues();
    }

    // SetValueWithoutNotify/SetTextWithoutNotify를 써서 여기서 값을 세팅해도 onValueChanged/onEndEdit가
    // 다시 SoundManager를 호출하는 순환이 생기지 않는다.
    private void RefreshDisplayedValues()
    {
        if (SoundManager.Instance == null)
            return;

        masterSlider?.SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume());
        bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume());
        sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.GetSFXVolume());
        voiceSlider?.SetValueWithoutNotify(SoundManager.Instance.GetVoiceVolume());

        bgmToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsBGMMuted());
        sfxToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsSFXMuted());
        voiceToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsVoiceMuted());

        masterInputField?.SetTextWithoutNotify(SoundManager.Instance.GetMasterVolume().ToString("F2"));
        bgmInputField?.SetTextWithoutNotify(SoundManager.Instance.GetBGMVolume().ToString("F2"));
        sfxInputField?.SetTextWithoutNotify(SoundManager.Instance.GetSFXVolume().ToString("F2"));
        voiceInputField?.SetTextWithoutNotify(SoundManager.Instance.GetVoiceVolume().ToString("F2"));
    }

    // 슬라이더를 움직이면 SoundManager뿐 아니라 옆 인풋필드 텍스트도 같이 갱신한다.
    private void OnMasterVolumeChanged(float value)
    {
        SoundManager.Instance?.SetMasterVolume(value);
        masterInputField?.SetTextWithoutNotify(value.ToString("F2"));
    }

    private void OnBGMVolumeChanged(float value)
    {
        SoundManager.Instance?.SetBGMVolume(value);
        bgmInputField?.SetTextWithoutNotify(value.ToString("F2"));
    }

    private void OnSFXVolumeChanged(float value)
    {
        SoundManager.Instance?.SetSFXVolume(value);
        sfxInputField?.SetTextWithoutNotify(value.ToString("F2"));
    }

    private void OnVoiceVolumeChanged(float value)
    {
        SoundManager.Instance?.SetVoiceVolume(value);
        voiceInputField?.SetTextWithoutNotify(value.ToString("F2"));
    }

    // 토글 "켜짐" = 소리 남(뮤트 아님) 쪽이 더 직관적이라, SoundManager의 Muted와는 반전해서 넘긴다.
    private void OnBGMToggleChanged(bool isOn) => SoundManager.Instance?.SetBGMMuted(!isOn);
    private void OnSFXToggleChanged(bool isOn) => SoundManager.Instance?.SetSFXMuted(!isOn);
    private void OnVoiceToggleChanged(bool isOn) => SoundManager.Instance?.SetVoiceMuted(!isOn);

    // 인풋필드에 숫자를 입력하고 엔터/포커스 아웃하면 슬라이더/SoundManager/텍스트를 모두 맞춘다.
    private void OnMasterInputChanged(string text) => ApplyInputValue(text, masterSlider, masterInputField, SoundManager.Instance?.SetMasterVolume);
    private void OnBGMInputChanged(string text) => ApplyInputValue(text, bgmSlider, bgmInputField, SoundManager.Instance?.SetBGMVolume);
    private void OnSFXInputChanged(string text) => ApplyInputValue(text, sfxSlider, sfxInputField, SoundManager.Instance?.SetSFXVolume);
    private void OnVoiceInputChanged(string text) => ApplyInputValue(text, voiceSlider, voiceInputField, SoundManager.Instance?.SetVoiceVolume);

    // 숫자가 아니면 무시하고 기존 값으로 되돌리고(RefreshDisplayedValues), 숫자면 0~1로 clamp한 뒤
    // 슬라이더/SoundManager/텍스트를 전부 그 값으로 맞춘다.
    private void ApplyInputValue(string text, Slider slider, TMP_InputField inputField, System.Action<float> setVolume)
    {
        if (!float.TryParse(text, out float value))
        {
            RefreshDisplayedValues();
            return;
        }

        value = Mathf.Clamp01(value);
        slider?.SetValueWithoutNotify(value);
        setVolume?.Invoke(value);
        inputField?.SetTextWithoutNotify(value.ToString("F2"));
    }
}
```

## 요약

- 인풋필드 4개(마스터/BGM/SFX/보이스) 필드 추가, `TMP_InputField` 기준으로 작성.
- 슬라이더 ↔ 인풋필드 ↔ `SoundManager` 3자 동기화: 어느 쪽을 바꿔도 나머지 둘이 같이 갱신됨.
- 값 범위는 슬라이더와 동일한 0~1, 잘못된/범위 밖 입력은 clamp하거나 기존 값으로 되돌림.
- 토글 관련 로직은 변경 없음.

## 필요한 씬 작업 (코드 외)

- 옵션 패널에 만든 인풋필드 4개를 `SoundSettingsPanel`의 `Master/Bgm/Sfx/Voice Input Field` 필드에
  각각 연결.

## 영향받는 파일

- `Assets/Scripts/UI/SoundSettingsPanel.cs` (수정)

## 최종 적용 코드 (0~100%)

인풋필드 표시/입력만 0~100 정수 퍼센트로 바꾸고, `Slider`/`SoundManager`에는 여전히 0~1로 변환해서
넘긴다.

```csharp
    // 슬라이더를 움직이면 SoundManager뿐 아니라 옆 인풋필드 텍스트(0~100%)도 같이 갱신한다.
    private void OnMasterVolumeChanged(float value)
    {
        SoundManager.Instance?.SetMasterVolume(value);
        masterInputField?.SetTextWithoutNotify((value * 100f).ToString("F0"));
    }

    private void OnBGMVolumeChanged(float value)
    {
        SoundManager.Instance?.SetBGMVolume(value);
        bgmInputField?.SetTextWithoutNotify((value * 100f).ToString("F0"));
    }

    private void OnSFXVolumeChanged(float value)
    {
        SoundManager.Instance?.SetSFXVolume(value);
        sfxInputField?.SetTextWithoutNotify((value * 100f).ToString("F0"));
    }

    private void OnVoiceVolumeChanged(float value)
    {
        SoundManager.Instance?.SetVoiceVolume(value);
        voiceInputField?.SetTextWithoutNotify((value * 100f).ToString("F0"));
    }
```

`RefreshDisplayedValues()`의 인풋필드 4줄도 `* 100f`, `"F0"`으로:
```csharp
        masterInputField?.SetTextWithoutNotify((SoundManager.Instance.GetMasterVolume() * 100f).ToString("F0"));
        bgmInputField?.SetTextWithoutNotify((SoundManager.Instance.GetBGMVolume() * 100f).ToString("F0"));
        sfxInputField?.SetTextWithoutNotify((SoundManager.Instance.GetSFXVolume() * 100f).ToString("F0"));
        voiceInputField?.SetTextWithoutNotify((SoundManager.Instance.GetVoiceVolume() * 100f).ToString("F0"));
```

`ApplyInputValue`는 입력값을 0~100 퍼센트로 파싱/clamp한 뒤 0~1로 나눠서 슬라이더/SoundManager에 넘긴다:
```csharp
    // 인풋필드는 0~100(%)로 입력받는다 - 숫자가 아니면 무시하고 기존 값으로 되돌리고, 범위를 벗어나면
    // 0~100으로 clamp한 뒤 0~1로 변환해서 슬라이더/SoundManager/텍스트를 전부 그 값으로 맞춘다.
    private void ApplyInputValue(string text, Slider slider, TMP_InputField inputField, System.Action<float> setVolume)
    {
        if (!float.TryParse(text, out float percent))
        {
            RefreshDisplayedValues();
            return;
        }

        percent = Mathf.Clamp(percent, 0f, 100f);
        float value = percent / 100f;
        slider?.SetValueWithoutNotify(value);
        setVolume?.Invoke(value);
        inputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }
```

헤더 주석도 `[Header("인풋필드 (0~100%, 내부적으로는 0~1로 변환)")]`로 변경.

## 다음 단계

실제로 만든 인풋필드가 TMP_InputField가 맞는지만 확인 부탁드립니다(레거시 InputField면 알려주세요).
