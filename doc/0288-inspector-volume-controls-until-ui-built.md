## 날짜
2026-07-29

## 요청 내용
"SFX/보이스/BGM 소리를 전체적으로 조절할 수 있게 해달라"는 요청. 조사해보니 `SoundManager.cs`에 볼륨 조절 API(`SetMasterVolume`/`SetBGMVolume`/`SetSFXVolume`/`SetVoiceVolume` 등)와 `SoundSettingsPanel.cs`(슬라이더/토글을 그 API에 연결하는 로직)는 이미 doc/0255에서 다 구현돼 있었지만, 실제 Canvas/슬라이더 UI가 에디터에서 한 번도 만들어진 적이 없어서 지금은 조절할 방법이 전혀 없는 상태였음. Canvas UI는 나중에 따로 만들 예정이라, 그 전까지 임시로 **인스펙터에서 직접 조절**할 수 있게 해달라는 것으로 범위 조정.

## 조사 내용
`masterVolume`/`bgmVolume`/`sfxVolume`/`voiceVolume`, `bgmMuted`/`sfxMuted`/`voiceMuted`가 전부 일반 `private` 필드라 인스펙터에 노출되지 않음. `Awake()`에서 `LoadVolumePrefs()`가 `PlayerPrefs.GetFloat(키, 1f)`로 항상 덮어쓰기 때문에, 단순히 `[SerializeField]`만 붙이면 인스펙터에서 값을 바꿔놔도 플레이 진입 시 다시 1f(또는 PlayerPrefs에 남은 값)로 되돌아가 버리는 문제가 있음 - 지금까지 저장된 UI가 없어서 PlayerPrefs 키 자체가 없는 상태이므로 `Mathf.Approximately` 대신 `PlayerPrefs.HasKey()`로 "키가 실제로 저장된 적 있을 때만" 덮어쓰도록 바꾸면, 인스펙터 기본값이 살아있게 됨(나중에 진짜 UI가 슬라이더를 조작해 저장하면 그때부터는 그 값이 우선함 - 정상 동작).

BGM은 `bgmSource.volume`이 `ApplyBGMVolume()` 호출 시점에만 갱신되는데, 지금은 `SetBGMVolume`/`SetBGMMuted`/트랙 전환 시에만 호출됨 - 인스펙터에서 직접 값을 바꾸면 이 경로를 안 타서 재생 중인 곡 볼륨이 바로 안 바뀜. `Update()`에서 매 프레임 `ApplyBGMVolume()`을 호출하도록 추가하면 인스펙터 수정이 실시간으로 반영됨(가벼운 대입 연산이라 비용 문제 없음). SFX/Voice는 재생 시점(`PlayFromPool`/`PlaySingleChannel`)마다 필드를 직접 읽어서 계산하므로 별도 처리 없이 인스펙터 값 변경이 다음 재생부터 즉시 반영됨.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
```csharp
    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private float voiceVolume = 1f;
    private bool bgmMuted;
    private bool sfxMuted;
    private bool voiceMuted;
```
```csharp
    private void LoadVolumePrefs()
    {
        masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume, 1f);
        bgmVolume = PlayerPrefs.GetFloat(PrefBGMVolume, 1f);
        sfxVolume = PlayerPrefs.GetFloat(PrefSFXVolume, 1f);
        voiceVolume = PlayerPrefs.GetFloat(PrefVoiceVolume, 1f);
        bgmMuted = PlayerPrefs.GetInt(PrefBGMMuted, 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(PrefSFXMuted, 0) == 1;
        voiceMuted = PlayerPrefs.GetInt(PrefVoiceMuted, 0) == 1;
    }
```
```csharp
    private void Update()
    {
        if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
            PlayRandomBGMTrack();
    }
```

**변경 코드 (제안)**
```csharp
    [Header("볼륨/뮤트 (임시 - 실제 설정 UI가 붙기 전까지 인스펙터에서 직접 조절/테스트용, doc/0288)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [SerializeField] private bool bgmMuted;
    [SerializeField] private bool sfxMuted;
    [SerializeField] private bool voiceMuted;
```
```csharp
    // PlayerPrefs에 실제로 저장된 적 있는 키만 덮어쓴다 - 아직 설정 UI가 없어서 저장된 적이 없다면
    // 위 인스펙터 기본값이 그대로 유지된다. 나중에 UI가 SetXxxVolume을 호출해 저장하기 시작하면
    // 그때부터는 저장된 값이 인스펙터 기본값보다 우선한다 (정상적인 영속화 동작).
    private void LoadVolumePrefs()
    {
        if (PlayerPrefs.HasKey(PrefMasterVolume)) masterVolume = PlayerPrefs.GetFloat(PrefMasterVolume);
        if (PlayerPrefs.HasKey(PrefBGMVolume)) bgmVolume = PlayerPrefs.GetFloat(PrefBGMVolume);
        if (PlayerPrefs.HasKey(PrefSFXVolume)) sfxVolume = PlayerPrefs.GetFloat(PrefSFXVolume);
        if (PlayerPrefs.HasKey(PrefVoiceVolume)) voiceVolume = PlayerPrefs.GetFloat(PrefVoiceVolume);
        if (PlayerPrefs.HasKey(PrefBGMMuted)) bgmMuted = PlayerPrefs.GetInt(PrefBGMMuted) == 1;
        if (PlayerPrefs.HasKey(PrefSFXMuted)) sfxMuted = PlayerPrefs.GetInt(PrefSFXMuted) == 1;
        if (PlayerPrefs.HasKey(PrefVoiceMuted)) voiceMuted = PlayerPrefs.GetInt(PrefVoiceMuted) == 1;
    }
```
```csharp
    private void Update()
    {
        ApplyBGMVolume(); // 인스펙터에서 master/bgmVolume/bgmMuted를 직접 바꿔도 재생 중인 BGM에 바로 반영되도록 (doc/0288)

        if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
            PlayRandomBGMTrack();
    }
```
(SFX/Voice는 재생 시점마다 필드를 직접 읽으므로 추가 코드 불필요 - 인스펙터 값을 바꾸면 바로 다음 재생부터 반영됨.)

## 확인 결과
사용자가 이전 질문("Canvas UI vs 인스펙터")에서 "인스펙터에서 조절"로 명시적으로 지시 - 위 제안 그대로 적용.

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs
위 "제안" 코드 그대로 적용: 볼륨 4개/뮤트 3개 필드에 `[SerializeField]`(볼륨은 `Range(0f,1f)`) 추가, `LoadVolumePrefs()`를 `PlayerPrefs.HasKey()` 체크로 변경, `Update()`에 `ApplyBGMVolume()` 매 프레임 호출 추가.

## 요약/남은 작업
적용 완료. Unity에서 `GameManager` 프리팹(또는 씬 인스턴스)의 `SoundManager` 컴포넌트를 선택하면 `Master/Bgm/Sfx/Voice Volume` 슬라이더와 `Bgm/Sfx/Voice Muted` 체크박스가 인스펙터에 바로 보여야 함 - 플레이 모드 중에 값을 바꾸면 즉시 반영되는지 확인 필요. 나중에 실제 Canvas/슬라이더 UI를 만들면(`SoundSettingsPanel` 배치), 그 UI가 `SetXxxVolume`을 호출해 PlayerPrefs에 저장하기 시작하는 순간부터 이 인스펙터 값들은 "최초 기본값" 역할만 하게 됨 - 별도 마이그레이션 불필요.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
