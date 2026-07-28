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
