using System.Collections.Generic;
using UnityEngine;

// 사운드 전담 싱글턴. 주음량/배경음악/효과음/음성 4개 카테고리의 볼륨·뮤트를 관리하고,
// 실제 재생은 미리 만들어둔 AudioSource 풀을 순환 재사용해서 처리한다 (doc/0255).
// 유닛/건물 종류별 사운드는 UnitSoundBankSO/BuildingSoundBankSO에, 유닛에 안 묶이는 나레이션은
// GlobalVoiceBankSO에 들어있고, 이 매니저는 "재생/볼륨" 로직만 담당한다 - 어떤 클립을 언제 재생할지는
// UnitAudio/BuildingAudio 등 호출부가 결정해서 SoundClipSet을 그대로 넘겨준다.
//
// 다른 매니저들(ResourceManager 등)은 인스펙터 직렬화 필드로만 참조를 주고받지만, 사운드는 유닛/건물/UI/
// RTSUnitController 등 정말 많은 곳에서 호출해야 해서 이번만 예외적으로 정적 싱글턴(Instance)을 둔다.
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("BGM (3곡 권장 - 매 판마다 랜덤 1곡, 끝나면 다시 랜덤 무한 재생)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private List<AudioClip> bgmTracks = new List<AudioClip>();

    [Header("SFX/Voice 오디오소스 풀 (비워두면 코드에서 자동 생성)")]
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private AudioSource voiceSourcePrefab;
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4; // 음성은 SFX보다 훨씬 적어도 됨(동시에 여러 목소리가 겹치지 않는 편이 자연스러움)

    [Header("인터페이스(버튼 클릭) 소리 - 위치 없는 SFX")]
    [SerializeField] private SoundClipSet uiClickSFX;

    [Header("나레이션 (유닛/건물에 안 묶이는 전역 음성)")]
    [SerializeField] private GlobalVoiceBankSO globalVoiceBank;
    [SerializeField] private float globalVoiceCooldown = 6f; // 카테고리별 최소 재재생 간격(초) - "공격받았습니다" 스팸 방지

    // 풀에서 관리하는 AudioSource 1개 + "언제 재생을 시작했는지" - 전부 재생 중일 때 가장 오래된 걸 가로채기 위함.
    private class PooledSource
    {
        public AudioSource Source;
        public float StartedAt = float.MinValue;
    }

    private readonly List<PooledSource> sfxPool = new List<PooledSource>();
    private readonly List<PooledSource> voicePool = new List<PooledSource>();
    private readonly Dictionary<SoundClipSet, float> lastGlobalVoiceTime = new Dictionary<SoundClipSet, float>();

    // 선택/이동/공격명령 음성 전용 채널 - 항상 이 소스 하나만 써서, "지금 어떤 유닛 종류의 대사가
    // 재생 중인지"를 정확히 추적할 수 있게 한다 (doc/0262~0264). 일반 voicePool과는 별개.
    // 유닛 "개체"가 아니라 "종류"를 키로 쓰기 위해 UnitSoundBankSO 참조 자체를 식별자로 사용한다 -
    // 같은 종류의 유닛은 항상 같은 SoundBank 에셋을 공유하므로, 드래그/단일 선택을 섞어도 같은 종류면
    // 같은 값으로 비교된다.
    private AudioSource orderVoiceSource;
    private UnitSoundBankSO currentOrderVoiceUnitType;

    private const string PrefMasterVolume = "Sound_MasterVolume";
    private const string PrefBGMVolume = "Sound_BGMVolume";
    private const string PrefSFXVolume = "Sound_SFXVolume";
    private const string PrefVoiceVolume = "Sound_VoiceVolume";
    private const string PrefBGMMuted = "Sound_BGMMuted";
    private const string PrefSFXMuted = "Sound_SFXMuted";
    private const string PrefVoiceMuted = "Sound_VoiceMuted";

    private float masterVolume = 1f;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;
    private float voiceVolume = 1f;
    private bool bgmMuted;
    private bool sfxMuted;
    private bool voiceMuted;

    private int lastBGMTrackIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LoadVolumePrefs();
        BuildPool(sfxPool, sfxSourcePrefab, sfxPoolSize, "SFXSource");
        BuildPool(voicePool, voiceSourcePrefab, voicePoolSize, "VoiceSource");

        orderVoiceSource = new GameObject("OrderVoiceSource").AddComponent<AudioSource>();
        orderVoiceSource.transform.SetParent(transform);
        orderVoiceSource.playOnAwake = false;
        orderVoiceSource.spatialBlend = 0f;
    }

    // BGM 곡이 끝나면(Loop 미사용) 매 프레임 감지해서 다시 랜덤으로 다음 곡을 재생 - "무한 랜덤 반복" 요구사항.
    private void Update()
    {
        if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
            PlayRandomBGMTrack();
    }

    #region 풀 구성/획득

    private void BuildPool(List<PooledSource> pool, AudioSource prefab, int size, string namePrefix)
    {
        for (int i = 0; i < size; ++i)
        {
            AudioSource source = prefab != null
                ? Instantiate(prefab, transform)
                : new GameObject($"{namePrefix}_{i}").AddComponent<AudioSource>();

            source.transform.SetParent(transform);
            source.playOnAwake = false;
            pool.Add(new PooledSource { Source = source });
        }
    }

    // 재생 중이 아닌 소스를 우선 반환하고, 전부 재생 중이면 가장 오래전에 재생을 시작한 소스를 가로챈다
    // (RTS 특성상 여러 개체가 동시에 공격/사망하는 상황이 흔해서, "재생 실패로 조용해지는 것"보다 낫다).
    private PooledSource GetAvailableSource(List<PooledSource> pool)
    {
        PooledSource oldest = pool[0];

        foreach (PooledSource pooled in pool)
        {
            if (!pooled.Source.isPlaying)
                return pooled;

            if (pooled.StartedAt < oldest.StartedAt)
                oldest = pooled;
        }

        return oldest;
    }

    #endregion

    #region 재생 API

    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position);

    public void PlayUIClick() => PlaySFX2D(uiClickSFX);

    // 유닛/건물 음성 - 스타1/2처럼 항상 또렷하게 들리도록 2D로 재생한다.
    public void PlayVoice(SoundClipSet set) =>
        PlayFromPool(voicePool, set, voiceVolume, voiceMuted, spatialBlend: 0f, transform.position);

    // 선택/이동/공격명령 음성 전용 (doc/0263, doc/0264). "다른 종류의 유닛을 선택"(category=="select"
    // 이고 이전에 재생하던 종류와 다름)했을 때만 재생 중이던 대사를 즉시 끊고 새로 재생한다. 같은
    // 종류의 유닛이면(단일 선택/드래그 선택을 섞어써도) 끊지 않는다 - 드래그로 여러 마리를 잡다가
    // 같은 종류를 또 클릭해도 재생 중이던 대사가 계속 이어진다. 그 외의 모든 경우(같은 종류 재선택,
    // 이동/공격명령 전부)는 채널이 이미 재생 중이면 이번 요청을 그냥 버리고 재생 중이던 대사를 끝까지
    // 들려준다 - 그 다음에 들어오는 명령부터 다시 새로 랜덤 재생된다.
    // unitType: 이 유닛 "종류"를 식별하는 값으로 UnitSoundBankSO 참조를 그대로 쓴다(같은 종류는 항상
    // 같은 SoundBank 에셋을 공유하므로).
    public void PlayOrderVoice(SoundClipSet set, UnitSoundBankSO unitType, string category)
    {
        if (set == null || !set.HasClips)
            return;

        bool isNewUnitTypeSelection = category == "select" && unitType != currentOrderVoiceUnitType;

        if (!isNewUnitTypeSelection && orderVoiceSource.isPlaying)
            return; // 다른 종류의 유닛 선택이 아니면 재생 중인 대사를 끊지 않는다 - 이번 요청은 버려짐

        AudioClip clip = set.GetRandomClip();
        if (clip == null)
            return;

        currentOrderVoiceUnitType = unitType;

        orderVoiceSource.Stop(); // isNewUnitTypeSelection일 때만 실제로 뭔가 끊길 수 있음
        orderVoiceSource.clip = clip;
        orderVoiceSource.pitch = set.GetRandomPitch();
        orderVoiceSource.volume = EffectiveVolume(voiceVolume, voiceMuted) * set.volumeScale;
        orderVoiceSource.Play();
    }

    // 유닛/건물에 안 묶이는 나레이션 - 같은 SoundClipSet은 globalVoiceCooldown 동안 다시 재생하지 않는다(스팸 방지).
    public void PlayGlobalVoice(SoundClipSet set)
    {
        if (set == null || !set.HasClips)
            return;

        if (lastGlobalVoiceTime.TryGetValue(set, out float last) && Time.time - last < globalVoiceCooldown)
            return;

        lastGlobalVoiceTime[set] = Time.time;
        PlayVoice(set);
    }

    // 자주 쓰는 나레이션 카테고리는 호출부가 globalVoiceBank를 직접 null 체크하지 않도록 래핑해둔다.
    public void PlayInsufficientResourcesWarning()
    {
        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.insufficientResources);
    }

    public void PlayInsufficientPopulationWarning()
    {
        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.insufficientPopulation);
    }

    public void PlayUnderAttackWarning()
    {
        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.underAttackWarning);
    }

    public void PlayUpgradeCompleteVoice()
    {
        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.upgradeComplete);
    }

    private void PlayFromPool(List<PooledSource> pool, SoundClipSet set, float categoryVolume, bool muted, float spatialBlend, Vector3 worldPos)
    {
        if (set == null || !set.HasClips)
            return;

        AudioClip clip = set.GetRandomClip();
        if (clip == null)
            return;

        PooledSource pooled = GetAvailableSource(pool);
        AudioSource source = pooled.Source;

        source.transform.position = worldPos;
        source.clip = clip;
        source.pitch = set.GetRandomPitch();
        source.spatialBlend = spatialBlend;
        source.volume = EffectiveVolume(categoryVolume, muted) * set.volumeScale;
        pooled.StartedAt = Time.time;
        source.Play();
    }

    #endregion

    #region BGM

    private void PlayRandomBGMTrack()
    {
        int index = bgmTracks.Count == 1 ? 0 : Random.Range(0, bgmTracks.Count);

        // 곡이 2개 이상이면 직전과 같은 곡이 연속으로 나오지 않게 다시 뽑는다.
        while (bgmTracks.Count > 1 && index == lastBGMTrackIndex)
            index = Random.Range(0, bgmTracks.Count);

        lastBGMTrackIndex = index;
        bgmSource.clip = bgmTracks[index];
        ApplyBGMVolume();
        bgmSource.Play();
    }

    private void ApplyBGMVolume()
    {
        if (bgmSource != null)
            bgmSource.volume = EffectiveVolume(bgmVolume, bgmMuted);
    }

    #endregion

    #region 볼륨/뮤트 (설정 UI가 호출)

    private float EffectiveVolume(float categoryVolume, bool muted) => muted ? 0f : masterVolume * categoryVolume;

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

    // 뮤트 토글은 볼륨을 0으로 내리는 게 아니라 재생 시 곱해지는 배율만 0으로 만든다 - 슬라이더가 들고 있는
    // 값(PlayerPrefs)은 그대로 남아있어서, 다시 토글을 켜면 이전 위치로 복귀한다.
    public void SetMasterVolume(float linear01)
    {
        masterVolume = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(PrefMasterVolume, masterVolume);
        ApplyBGMVolume();
    }

    public void SetBGMVolume(float linear01)
    {
        bgmVolume = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(PrefBGMVolume, bgmVolume);
        ApplyBGMVolume();
    }

    public void SetSFXVolume(float linear01)
    {
        sfxVolume = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(PrefSFXVolume, sfxVolume);
    }

    public void SetVoiceVolume(float linear01)
    {
        voiceVolume = Mathf.Clamp01(linear01);
        PlayerPrefs.SetFloat(PrefVoiceVolume, voiceVolume);
    }

    public void SetBGMMuted(bool muted)
    {
        bgmMuted = muted;
        PlayerPrefs.SetInt(PrefBGMMuted, muted ? 1 : 0);
        ApplyBGMVolume();
    }

    public void SetSFXMuted(bool muted)
    {
        sfxMuted = muted;
        PlayerPrefs.SetInt(PrefSFXMuted, muted ? 1 : 0);
    }

    public void SetVoiceMuted(bool muted)
    {
        voiceMuted = muted;
        PlayerPrefs.SetInt(PrefVoiceMuted, muted ? 1 : 0);
    }

    public float GetMasterVolume() => masterVolume;
    public float GetBGMVolume() => bgmVolume;
    public float GetSFXVolume() => sfxVolume;
    public float GetVoiceVolume() => voiceVolume;
    public bool IsBGMMuted() => bgmMuted;
    public bool IsSFXMuted() => sfxMuted;
    public bool IsVoiceMuted() => voiceMuted;

    #endregion

    // 화면 밖(카메라 뷰포트 밖) 피격 경고에 사용 - 카메라를 못 찾으면 경고를 억제하지 않는 쪽(항상 화면 안으로 간주)이 안전하다.
    public static bool IsWorldPositionOnScreen(Vector3 worldPos)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return true;

        Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);
        return viewportPos.z > 0f && viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f;
    }
}
