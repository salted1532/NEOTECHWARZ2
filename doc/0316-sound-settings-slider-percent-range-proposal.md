# 0316. 사운드 설정 슬라이더를 0~100% 범위로 통일 (제안)

날짜: 2026-07-30

## 요청 내용

> 50%를 반영하는게 인게임 사운드들을 50%로 줄이는게 아니라 slider에 값들을 50으로 조절해야해

## 원인

`SoundSettingsPanel.cs`가 슬라이더는 `SoundManager`와 같은 **0~1** 값을 그대로 주고받고
(`SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume())` 등), 인풋필드만 표시/입력 시
`*100`/`/100`으로 변환해서 0~100%로 보여주고 있었음(`doc/0314`).

그런데 옵션 패널의 실제 슬라이더는 (인풋필드와 같은 숫자를 보여주기 위해) 인스펙터에서 Min=0/Max=100으로
만들어져 있는 것으로 보임. 그 상태에서 코드가 `SetValueWithoutNotify(0.5f)`를 호출하면, 슬라이더
입장에서는 "0~100 범위에서 0.5"라는 뜻이라 핸들이 사실상 맨 왼쪽 끝 근처에 위치하게 됨 - 반면
`SoundManager`에 저장되는 실제 볼륨값(0.5)은 정상이라 소리 자체는 정확히 50%로 줄어듦. 그 결과
"소리는 절반으로 줄었는데 슬라이더는 50 위치에 있지 않다"는 지금 증상이 생김.

## 해결

슬라이더도 인풋필드와 동일하게 **0~100 범위**로 값을 주고받도록 통일한다. `SoundManager`
(0~1)와의 변환은 `*100`/`/100`으로 슬라이더 쪽에서도 인풋필드와 똑같이 처리.

## 코드 변경

### `Assets/Scripts/UI/SoundSettingsPanel.cs`

**기존 코드**:
```csharp
    [Header("슬라이더 (0~1)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;
```
```csharp
        masterSlider?.SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume());
        bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume());
        sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.GetSFXVolume());
        voiceSlider?.SetValueWithoutNotify(SoundManager.Instance.GetVoiceVolume());
```
```csharp
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
```csharp
        percent = Mathf.Clamp(percent, 0f, 100f);
        float value = percent / 100f;
        slider?.SetValueWithoutNotify(value);
        setVolume?.Invoke(value);
        inputField?.SetTextWithoutNotify(percent.ToString("F0"));
```

**변경 코드**:
```csharp
    [Header("슬라이더 (0~100%, 인스펙터에서 Min=0/Max=100으로 설정 - 인풋필드와 동일한 범위)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider voiceSlider;
```
```csharp
        masterSlider?.SetValueWithoutNotify(SoundManager.Instance.GetMasterVolume() * 100f);
        bgmSlider?.SetValueWithoutNotify(SoundManager.Instance.GetBGMVolume() * 100f);
        sfxSlider?.SetValueWithoutNotify(SoundManager.Instance.GetSFXVolume() * 100f);
        voiceSlider?.SetValueWithoutNotify(SoundManager.Instance.GetVoiceVolume() * 100f);
```
```csharp
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
```
```csharp
        percent = Mathf.Clamp(percent, 0f, 100f);
        slider?.SetValueWithoutNotify(percent);
        setVolume?.Invoke(percent / 100f);
        inputField?.SetTextWithoutNotify(percent.ToString("F0"));
```

## 요약

- 슬라이더 ↔ 인풋필드가 이제 완전히 같은 0~100% 값을 주고받음. `SoundManager`(0~1)와의 변환은
  두 쪽 다 접점(슬라이더 이벤트, 인풋필드 이벤트, 새로고침)에서 동일하게 `*100`/`/100`으로 처리.
- **필수**: 옵션 패널의 슬라이더 4개(Master/BGM/SFX/Voice)의 인스펙터 `Min Value`/`Max Value`가
  실제로 `0`/`100`으로 되어 있는지 확인 부탁드립니다 - 코드가 이제 0~100 값을 넘기므로, 슬라이더 쪽
  Min/Max가 여전히 0~1이면 반대로 지금처럼 위치가 안 맞는 문제가 생깁니다.

## 영향받는 파일

- `Assets/Scripts/UI/SoundSettingsPanel.cs` (수정)

## 다음 단계

이대로 수정해도 될지 확인 부탁드립니다.
