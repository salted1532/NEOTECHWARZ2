using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 볼륨 설정 패널 - 슬라이더 4개(주음량/배경음악/효과음/음성) + 토글 3개(배경음악/효과음/음성) +
// 인풋필드 4개(슬라이더와 같은 값을 0~100% 숫자로 직접 입력)를 SoundManager의 볼륨/뮤트 API에
// 연결한다 (doc/0255 Phase 4, doc/0314). 주음량은 요청 원문에 토글 언급이 없어 슬라이더/인풋필드만 둔다.
//
// 이 스크립트는 로직만 담당한다. 실제 Canvas/슬라이더/토글/인풋필드 GameObject 배치(레이아웃)는
// 유니티 에디터에서 직접 만들고, 이 컴포넌트의 인스펙터 필드에 각 UI 요소를 연결해야 동작한다.
public class SoundSettingsPanel : MonoBehaviour
{
    [Header("슬라이더 (0~100%, 인스펙터에서 Min=0/Max=100으로 설정 - 인풋필드와 동일한 범위)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;

    [Header("토글 (켜짐 = 소리 남, 꺼짐 = 뮤트)")]
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle voiceToggle;

    [Header("인풋필드 (0~100%, 내부적으로는 슬라이더와 같은 0~1로 변환)")]
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

        masterSlider?.SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume() * 100f);
        bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume() * 100f);
        sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.GetSFXVolume() * 100f);
        voiceSlider?.SetValueWithoutNotify(SoundManager.Instance.GetVoiceVolume() * 100f);

        bgmToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsBGMMuted());
        sfxToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsSFXMuted());
        voiceToggle?.SetIsOnWithoutNotify(!SoundManager.Instance.IsVoiceMuted());

        masterInputField?.SetTextWithoutNotify((SoundManager.Instance.GetMasterVolume() * 100f).ToString("F0"));
        bgmInputField?.SetTextWithoutNotify((SoundManager.Instance.GetBGMVolume() * 100f).ToString("F0"));
        sfxInputField?.SetTextWithoutNotify((SoundManager.Instance.GetSFXVolume() * 100f).ToString("F0"));
        voiceInputField?.SetTextWithoutNotify((SoundManager.Instance.GetVoiceVolume() * 100f).ToString("F0"));
    }

    // 슬라이더도 0~100%로 값을 주고받는다 - SoundManager에는 /100해서 0~1로 넘기고, 옆 인풋필드
    // 텍스트도 같이 갱신한다.
    private void OnMasterVolumeChanged(float percent)
    {
        SoundManager.Instance?.SetMasterVolume(percent / 100f);
        masterInputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }

    private void OnBGMVolumeChanged(float percent)
    {
        SoundManager.Instance?.SetBGMVolume(percent / 100f);
        bgmInputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }

    private void OnSFXVolumeChanged(float percent)
    {
        SoundManager.Instance?.SetSFXVolume(percent / 100f);
        sfxInputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }

    private void OnVoiceVolumeChanged(float percent)
    {
        SoundManager.Instance?.SetVoiceVolume(percent / 100f);
        voiceInputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }

    // 토글 "켜짐" = 소리 남(뮤트 아님) 쪽이 더 직관적이라, SoundManager의 Muted와는 반전해서 넘긴다.
    private void OnBGMToggleChanged(bool isOn) => SoundManager.Instance?.SetBGMMuted(!isOn);
    private void OnSFXToggleChanged(bool isOn) => SoundManager.Instance?.SetSFXMuted(!isOn);
    private void OnVoiceToggleChanged(bool isOn) => SoundManager.Instance?.SetVoiceMuted(!isOn);

    // 인풋필드에 숫자(0~100%)를 입력하고 엔터/포커스 아웃하면 슬라이더/SoundManager/텍스트를 모두 맞춘다.
    private void OnMasterInputChanged(string text) => ApplyInputValue(text, masterSlider, masterInputField, value => SoundManager.Instance?.SetMasterVolume(value));
    private void OnBGMInputChanged(string text) => ApplyInputValue(text, bgmSlider, bgmInputField, value => SoundManager.Instance?.SetBGMVolume(value));
    private void OnSFXInputChanged(string text) => ApplyInputValue(text, sfxSlider, sfxInputField, value => SoundManager.Instance?.SetSFXVolume(value));
    private void OnVoiceInputChanged(string text) => ApplyInputValue(text, voiceSlider, voiceInputField, value => SoundManager.Instance?.SetVoiceVolume(value));

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
        slider?.SetValueWithoutNotify(percent);
        setVolume?.Invoke(percent / 100f);
        inputField?.SetTextWithoutNotify(percent.ToString("F0"));
    }
}
