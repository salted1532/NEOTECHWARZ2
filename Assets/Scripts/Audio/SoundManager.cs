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


    [Header("동시다발 SFX/Voice 스팸 방지 (여러 유닛이 동시에 공격/사망 등으로 같은 사운드를 트리거할 때 귀에 부담 주는 것 방지)")]
    [SerializeField] private float sfxRetriggerInterval = 0.05f; // 같은 SoundClipSet은 이 시간(초) 이내 재요청 시 무시
    [SerializeField] private int sfxMaxConcurrentPerSet = 4; // 같은 SoundClipSet이 동시에 재생 중일 수 있는 최대 개수

    [Header("인터페이스(버튼 클릭) 소리 - 위치 없는 SFX")]
    [SerializeField] private SoundClipSet uiClickSFX;

    [Header("나레이션 (유닛/건물에 안 묶이는 전역 음성)")]
    [SerializeField] private GlobalVoiceBankSO globalVoiceBank;
    // 피격 경고음(유닛/건물)만 재생이 끝나도 곧바로 다시 울리지 않도록 별도 쿨다운을 둔다 - 전투 중
    // 계속 두들겨 맞으면 알림이 끊임없이 울려서 시끄럽기 때문(doc/0273). 자원/인구부족·업그레이드
    // 완료 등 나머지 나레이션은 doc/0271대로 "재생 중이 아니면 곧바로 재생" 규칙을 그대로 쓴다.
    [SerializeField] private float underAttackWarningCooldown = 10f;

    // 풀에서 관리하는 AudioSource 1개 + "언제 재생을 시작했는지" - 전부 재생 중일 때 가장 오래된 걸 가로채기 위함.
    private class PooledSource
    {
        public AudioSource Source;
        public float StartedAt = float.MinValue;
    }

    private readonly List<PooledSource> sfxPool = new List<PooledSource>();
    private readonly List<PooledSource> voicePool = new List<PooledSource>();

    // 나레이션 카테고리(SoundClipSet)별로 "지금 그 카테고리를 재생 중인 AudioSource"를 기억해둔다 -
    // 재생 중이면 같은 카테고리 재요청을 무시하고, 재생이 끝나면 다음 요청부터 다시 재생한다 (doc/0271).
    private readonly Dictionary<SoundClipSet, AudioSource> activeGlobalVoiceSources = new Dictionary<SoundClipSet, AudioSource>();

    // SFX/Voice 동시다발 스팸 방지용 - 같은 SoundClipSet이 마지막으로 재생을 시작한 시각(최소 재생 간격
    // 판정용)과, 각 AudioSource가 지금 어떤 SoundClipSet을 재생 중인지(동시 재생 개수 판정용)를 기억해둔다.
    // 풀 소스가 가로채기로 재사용돼도 sourceCurrentSet은 매번 갱신되므로 항상 최신 상태를 반영한다.
    private readonly Dictionary<SoundClipSet, float> lastSfxStartTime = new Dictionary<SoundClipSet, float>();
    private readonly Dictionary<AudioSource, SoundClipSet> sourceCurrentSet = new Dictionary<AudioSource, SoundClipSet>();

    // minInterval을 지정한 나레이션 카테고리(피격 경고음)에 한해서만 "마지막으로 재생을 시작한 시각"을
    // 추가로 추적한다 (doc/0273).
    private readonly Dictionary<SoundClipSet, float> lastGlobalVoiceStartTime = new Dictionary<SoundClipSet, float>();

    // 선택/이동/공격명령 음성 전용 채널 - 항상 이 소스 하나만 써서, "지금 어떤 유닛 종류의 대사가
    // 재생 중인지"를 정확히 추적할 수 있게 한다 (doc/0262~0264). 일반 voicePool과는 별개.
    // 유닛 "개체"가 아니라 "종류"를 키로 쓰기 위해 UnitSoundBankSO 참조 자체를 식별자로 사용한다 -
    // 같은 종류의 유닛은 항상 같은 SoundBank 에셋을 공유하므로, 드래그/단일 선택을 섞어도 같은 종류면
    // 같은 값으로 비교된다.
    private AudioSource orderVoiceSource;
    private UnitSoundBankSO currentOrderVoiceUnitType;

    // 명령 확인음(orderSFX)/선택 확인음(selectSFX) 전용 단일 채널 - orderVoiceSource와 동일한 패턴.
    // 재생 중이면 새 요청은 무시하고, 재생이 끝난 뒤에 들어오는 요청부터 다시 재생한다. 서로 다른
    // 이벤트(선택 vs 명령)라 한쪽이 재생 중이어도 다른 쪽을 막지 않도록 채널을 따로 둔다.
    private AudioSource orderSFXSource;
    private AudioSource selectSFXSource;

    private const string PrefMasterVolume = "Sound_MasterVolume";
    private const string PrefBGMVolume = "Sound_BGMVolume";
    private const string PrefSFXVolume = "Sound_SFXVolume";
    private const string PrefVoiceVolume = "Sound_VoiceVolume";
    private const string PrefBGMMuted = "Sound_BGMMuted";
    private const string PrefSFXMuted = "Sound_SFXMuted";
    private const string PrefVoiceMuted = "Sound_VoiceMuted";

    [Header("볼륨/뮤트 (임시 - 실제 설정 UI가 붙기 전까지 인스펙터에서 직접 조절/테스트용, doc/0288)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [SerializeField] private bool bgmMuted;
    [SerializeField] private bool sfxMuted;
    [SerializeField] private bool voiceMuted;

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
        BuildPool(sfxPool, sfxSourcePrefab, sfxPoolSize, "SFXSource", configureSpatialRolloff: true);
        BuildPool(voicePool, voiceSourcePrefab, voicePoolSize, "VoiceSource", configureSpatialRolloff: false); // voicePool은 항상 spatialBlend=0(2D)로만 재생되므로 롤오프 설정이 무의미하다

        orderVoiceSource = new GameObject("OrderVoiceSource").AddComponent<AudioSource>();
        orderVoiceSource.transform.SetParent(transform);
        orderVoiceSource.playOnAwake = false;
        orderVoiceSource.spatialBlend = 0f;

        orderSFXSource = new GameObject("OrderSFXSource").AddComponent<AudioSource>();
        orderSFXSource.transform.SetParent(transform);
        orderSFXSource.playOnAwake = false;
        orderSFXSource.spatialBlend = 0f;

        selectSFXSource = new GameObject("SelectSFXSource").AddComponent<AudioSource>();
        selectSFXSource.transform.SetParent(transform);
        selectSFXSource.playOnAwake = false;
        selectSFXSource.spatialBlend = 0f;
    }

    // BGM 곡이 끝나면(Loop 미사용) 매 프레임 감지해서 다시 랜덤으로 다음 곡을 재생 - "무한 랜덤 반복" 요구사항.
    private void Update()
    {
        ApplyBGMVolume(); // 인스펙터에서 master/bgmVolume/bgmMuted를 직접 바꿔도 재생 중인 BGM에 바로 반영되도록 (doc/0288)

        if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
            PlayRandomBGMTrack();
    }

    #region 풀 구성/획득

    private void BuildPool(List<PooledSource> pool, AudioSource prefab, int size, string namePrefix, bool configureSpatialRolloff)
    {
        for (int i = 0; i < size; ++i)
        {
            AudioSource source = prefab != null
                ? Instantiate(prefab, transform)
                : new GameObject($"{namePrefix}_{i}").AddComponent<AudioSource>();

            source.transform.SetParent(transform);
            source.playOnAwake = false;

            // SFX 풀만: 유니티 기본 롤오프(Logarithmic, minDistance=1)는 카메라 거리(대략 10~45유닛,
            // doc/0277)에서 너무 일찍 감쇠를 시작해 근접 전투에서도 거의 안 들렸다. 실제로 카메라를
            // 바짝 당겨야(가까이 있을 때만) 들리도록 거리값을 게임 카메라 스케일에 맞춘다.
            // minDistance/maxDistance를 15~80에서 10~45로 좁혀서 카메라 줌 범위 전체가 감쇠 구간에
            // 들어오게 함 - 줌 아웃할수록 더 꾸준히/가파르게 작아지도록 (doc/0286).
            if (configureSpatialRolloff)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 10f;
                source.maxDistance = 45f;
            }

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

    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리). 여러 유닛이 동시에
    // 트리거하는 경우가 흔해서 스팸 방지(limitSpam)를 켠다 (doc/0284).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos, limitSpam: true);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position, limitSpam: true);

    public void PlayUIClick() => PlaySFX2D(uiClickSFX);

    // 선택 확인음(selectSFX)/명령 확인음(orderSFX) - 재생 중이면 새 요청을 버리고, 끝난 뒤에 들어오는
    // 요청부터 다시 재생한다 (doc/0285). doc/0284의 동시 4개 허용 스팸 방지(limitSpam)와 달리 이 둘은
    // 항상 최대 1개만 재생되길 원해서 전용 단일 채널로 분리했다.
    public void PlaySelectSFX(SoundClipSet set) => PlaySingleChannel(selectSFXSource, set);

    public void PlayOrderSFX(SoundClipSet set) => PlaySingleChannel(orderSFXSource, set);

    private void PlaySingleChannel(AudioSource source, SoundClipSet set)
    {
        if (set == null || !set.HasClips || source.isPlaying)
            return;

        AudioClip clip = set.GetRandomClip();
        if (clip == null)
            return;

        source.clip = clip;
        source.pitch = set.GetRandomPitch();
        source.volume = EffectiveVolume(sfxVolume, sfxMuted) * set.volumeScale;
        source.Play();
    }

    // 유닛/건물 음성(스폰/사망 대사 등) - 스타1/2처럼 항상 또렷하게 들리도록 2D로 재생한다. 여러 유닛이
    // 동시에 죽거나 스폰될 때 대사가 겹쳐 시끄러워지지 않도록 스팸 방지도 함께 적용 (doc/0284).
    public void PlayVoice(SoundClipSet set) =>
        PlayFromPool(voicePool, set, voiceVolume, voiceMuted, spatialBlend: 0f, transform.position, limitSpam: true);

    // 선택/이동/공격명령 음성 전용 (doc/0263, doc/0264). "다른 종류의 유닛을 선택"(category=="select"
    // 이고 이전에 재생하던 종류와 다름)했을 때만 재생 중이던 대사를 즉시 끊고 새로 재생한다. 같은
    // 종류의 유닛이면(단일 선택/드래그 선택을 섞어써도) 끊지 않는다 - 드래그로 여러 마리를 잡다가
    // 같은 종류를 또 클릭해도 재생 중이던 대사가 계속 이어진다. 그 외의 모든 경우(같은 종류 재선택,
    // 이동/순찰/공격명령 전부)는 채널이 이미 재생 중이면 이번 요청을 그냥 버리고 재생 중이던 대사를 끝까지
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

    // 유닛/건물에 안 묶이는 나레이션 - 같은 카테고리(SoundClipSet)의 경고음이 아직 재생 중이면 겹쳐
    // 재생하지 않는다. 고정된 쿨다운 시간이 아니라 "재생이 끝났는지"로 판단하므로, 이전 재생이 끝난
    // 뒤에는 곧바로 다음 명령/상황에서 다시 새로 랜덤 재생된다 (doc/0271 - 예전엔 고정 6초 쿨다운이라
    // 그 안에 자원부족을 다시 겪어도 조용히 씹혔음).
    // minInterval(초) > 0이면 추가로 "마지막 재생 시작 시각으로부터 이 시간이 지나야" 다시 재생한다 -
    // 피격 경고음처럼 재생이 짧게 끝나도 계속 얻어맞으면 스팸되는 경우를 막기 위함(doc/0273).
    // 반환값: 실제로 이번 호출에서 재생을 새로 시작했는지(true) - 겹침 방지/쿨다운으로 조용히
    // 씹혔으면(false) 호출부가 "진짜 재생된 순간에만" 다른 연출(미니맵 마커 등)을 같이 트리거할 수 있게 함
    // (doc/0362).
    public bool PlayGlobalVoice(SoundClipSet set, float minInterval = 0f)
    {
        if (set == null || !set.HasClips)
            return false;

        if (activeGlobalVoiceSources.TryGetValue(set, out AudioSource activeSource)
            && activeSource != null && activeSource.isPlaying)
            return false;

        if (minInterval > 0f
            && lastGlobalVoiceStartTime.TryGetValue(set, out float lastStart)
            && Time.time - lastStart < minInterval)
            return false;

        AudioSource source = PlayFromPool(voicePool, set, voiceVolume, voiceMuted, spatialBlend: 0f, transform.position); // 이미 위에서 자체 겹침/간격 방지를 했으므로 limitSpam 불필요
        if (source == null)
            return false;

        activeGlobalVoiceSources[set] = source;
        lastGlobalVoiceStartTime[set] = Time.time;
        return true;
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

    // 피격 경고음은 유닛/건물 각각 독립적으로 underAttackWarningCooldown(기본 10초) 간격을 둔다.
    // 반환값: 이번 호출에서 실제로 경고음이 새로 재생됐는지 (doc/0362 - 미니맵 마커를 이 타이밍에만 맞추기 위함).
    public bool PlayUnitUnderAttackWarning()
    {
        return globalVoiceBank != null && PlayGlobalVoice(globalVoiceBank.unitUnderAttackWarning, underAttackWarningCooldown);
    }

    public bool PlayBuildingUnderAttackWarning()
    {
        return globalVoiceBank != null && PlayGlobalVoice(globalVoiceBank.buildingUnderAttackWarning, underAttackWarningCooldown);
    }

    public void PlayUpgradeCompleteVoice()
    {
        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.upgradeComplete);
    }

    // AudioSource를 반환하는 이유: PlayGlobalVoice가 "이 카테고리가 지금 재생 중인지"를 나중에
    // 확인할 수 있어야 하기 때문 (doc/0271). 반환값이 필요 없는 호출부(PlaySFX/PlayVoice 등)는 그냥 버린다.
    // limitSpam=true면 doc/0284의 두 가지 방지 규칙을 적용한다: 같은 SoundClipSet이 sfxRetriggerInterval
    // 이내에 재요청되면 무시(최소 재생 간격), 이미 sfxMaxConcurrentPerSet개만큼 동시 재생 중이면 무시
    // (동일 사운드 동시 재생 제한). 여러 유닛이 같은 프레임에 같은 종류의 공격/사망 사운드를 동시에
    // 트리거해도 소리가 무제한으로 겹쳐 쌓이지 않게 하기 위함.
    private AudioSource PlayFromPool(List<PooledSource> pool, SoundClipSet set, float categoryVolume, bool muted, float spatialBlend, Vector3 worldPos, bool limitSpam = false)
    {
        if (set == null || !set.HasClips)
            return null;

        if (limitSpam)
        {
            if (lastSfxStartTime.TryGetValue(set, out float lastStart) && Time.time - lastStart < sfxRetriggerInterval)
                return null;

            int concurrent = 0;
            foreach (PooledSource p in pool)
            {
                if (p.Source.isPlaying && sourceCurrentSet.TryGetValue(p.Source, out SoundClipSet playingSet) && playingSet == set)
                    ++concurrent;
            }
            if (concurrent >= sfxMaxConcurrentPerSet)
                return null;
        }

        AudioClip clip = set.GetRandomClip();
        if (clip == null)
            return null;

        PooledSource pooled = GetAvailableSource(pool);
        AudioSource source = pooled.Source;

        source.transform.position = worldPos;
        source.clip = clip;
        source.pitch = set.GetRandomPitch();
        source.spatialBlend = spatialBlend;
        source.volume = EffectiveVolume(categoryVolume, muted) * set.volumeScale;
        pooled.StartedAt = Time.time;
        source.Play();

        if (limitSpam)
        {
            lastSfxStartTime[set] = Time.time;
            sourceCurrentSet[source] = set;
        }

        return source;
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

    // PlayerPrefs에 실제로 저장된 적 있는 키만 덮어쓴다 - 아직 설정 UI가 없어서 저장된 적이 없다면 위
    // 인스펙터 기본값이 그대로 유지된다(doc/0288). 나중에 UI가 SetXxxVolume을 호출해 저장하기 시작하면
    // 그때부터는 저장된 값이 인스펙터 기본값보다 우선한다 - 정상적인 영속화 동작.
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
