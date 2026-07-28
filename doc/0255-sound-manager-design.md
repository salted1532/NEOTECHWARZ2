# 0255 - 사운드 매니저(SoundManager) 설계

**날짜:** 2026-07-28

**아직 아무 것도 구현하지 않음 - 설계만 정리.** 실제 코드 반영은 아래 "열린 질문"에 대한 답을 받고
승인 후, Phase 단위로 나눠서 별도 진행.

## 요청 내용

> 사운드 매니저를 만들건데 - 배경음악(3종 랜덤 무한재생) / 효과음(공격,생산,사망,건설,파괴,채취,스킬 등
> 유닛/건물이 내는 모든 비-음성 소리) / 음성(선택/이동/공격명령 시 유닛별 랜덤 대사, 유닛 생성·사망
> 음성, 건물 음성, 공격받았다는 경고음, 자원/인구수 부족 경고음, 건설 실패 경고음, 업그레이드 완료
> 대사 등)을 다루고, 주음량/배경음악/효과음/음성 4개 볼륨을 각각 토글+슬라이더로 따로 조절할 수 있게
> 하고 싶다. 각 유닛별로 대사 클립이 다 다르므로 유닛별로 사운드를 적용/추가하기 쉬운 구조로 설계해달라.

## 조사 결과 (기존 코드 기준)

- 프로젝트에 오디오 관련 코드가 **전혀 없음** (`AudioSource`/`AudioClip`/`AudioMixer`/`SoundManager`
  전부 0건, `Assets/Scripts` 기준). 유일한 관련 코드는 `Assets/Scripts/Effects/EffectPlayer.cs`의
  주석 "AudioSource가 붙어있으면 같이 재생된다(프리팹에 미리 세팅)" — 즉 지금은 이펙트 프리팹에 우연히
  AudioSource가 박혀 있으면 파티클과 같이 재생되다 같이 파괴되는 정도이고, 볼륨 제어/믹서/풀링/BGM은
  전무함. **완전히 새로 만드는 시스템.**
- 이펙트(파티클) 쪽은 이미 "이벤트/명령형 호출 + 컴포넌트 조합" 패턴이 자리잡혀 있고, 사운드도 그대로
  이 패턴을 따라가면 기존 코드와 결이 맞는다:
  - `UnitEffects.cs`: `HealthManager.OnDamaged` / `OnDeath` **C# 이벤트 구독**(피격/사망), `PlayAttack()`
    / `StopAttackEffects()`는 `UnitController.Attack()` / `CancelAttackOrder()`에서 **직접 호출**.
    `GetComponent<UnitController>()`와 `GetComponent<EnemyUnitController>()`를 둘 다 null 체크해서
    **아군/적 유닛 프리팹에 공용으로 하나만 붙임** (두 컨트롤러가 완전히 별개 클래스라서 이런 dual-check
    가 필요함).
  - `ConstructionEffects.cs` (건설 중), `BuildingEffects.cs` (리프트/착륙)도 동일 패턴.
  - 애니메이션 이벤트(`AnimationEvent`)는 안 씀 — `UnitAnimatorDriver.cs`는 매 프레임 폴링해서
    Animator bool만 세팅하는 순수 폴링 방식. 즉 공격/사망 등 게임플레이 트리거는 전부 C# 상태 코드에서
    직접 호출되지, 애니메이션 클립에서 콜백이 오지 않음.
- `UnitDataSO.cs`/`BuildingDataSO.cs`는 전부 `[field: SerializeField] public T X { get; private set; }`
  자동 프로퍼티 패턴이고, `UnitTraitOption`처럼 `[System.Serializable]` 서브 클래스를 필드로 갖는 것도
  이미 있음(doc/0228) — 사운드 데이터도 같은 스타일로 넣을 수 있음.
- 매니저 참조는 싱글턴이 하나도 없음 — `RTSUnitController`가 `resourceManager` 등을 인스펙터
  직렬화 필드로 물고 있거나(`RTSUnitController` 자신), 유닛/건물 쪽이 `FindFirstObjectByType<RTSUnitController>()`
  로 찾는 두 가지 방식만 존재. 다만 사운드는 유닛/건물/UI/RTSUnitController 등 정말 많은 곳에서 호출해야
  해서, 매번 인스펙터로 참조를 꽂는 기존 방식은 배선 비용이 크다 → **이번 건은 예외적으로 정적 싱글턴
  (`SoundManager.Instance`)을 새로 도입**하는 걸 제안 (아래 "열린 질문" 참고).
- 설정(Settings) UI는 전혀 없음 — 토글/슬라이더 붙일 옵션 패널도 이번에 새로 만들어야 함.
- `RTSUnitController.cs`에서 확인한, 사운드를 걸 기존 호출 지점(신규 로그가 아니라 실제 로직 분기점):
  - 자원부족: `TryProduceUnit` 1067줄 `Debug.Log("자원부족!")`, `TryResearch` 1098줄 동일 문구.
  - 인구수부족: `TryProduceUnit` 1069줄 `Debug.Log("인구수부족!")`.
  - 건설 실패: `TryConstructBuilding`은 현재 실패해도 로그조차 없음(조용히 `false` 리턴) — 새로 추가 필요.
  - 선택/이동/공격명령: `SelectUnit`(193줄, 클릭선택 전부가 여길 통과), `MoveSelectedUnits`(289줄),
    `AttackSelectedUnits`류(300~375줄) — 전부 "선택된 유닛 전체"에 한 번씩만 도는 진입점이라, 여기서
    "명령 1회당 무작위 유닛 1마리의 대사 1번" 재생을 걸면 12마리를 드래그 선택해도 목소리가 12번
    겹치지 않는다.

## 전체 아키텍처 개요

```
SoundManager (신규 싱글턴 MonoBehaviour, 씬에 1개)
 ├─ AudioMixer 참조 (Master/BGM/SFX/Voice 4개 그룹, 각 볼륨 파라미터 노출)
 ├─ BGM 전용 AudioSource 1개 (루프 재생, 곡 끝나면 자동으로 다음 랜덤곡)
 ├─ SFX용 AudioSource 풀 (동시 다발 재생 - 여러 유닛이 동시에 공격/사망해도 안 끊기게)
 ├─ Voice용 AudioSource 풀 + 카테고리별 재생 쿨다운(스팸 방지, 특히 "공격받았습니다" 경고음)
 ├─ 전역 나레이션 보이스 뱅크 참조 (GlobalVoiceBankSO 1개 - 자원/인구/공격경고/업그레이드완료)
 └─ 공개 API: PlaySFX / PlayVoice / PlayUnitSFX / PlayGlobalVoice / SetVolume / SetMute 등

UnitData / BuildingData (기존 SO) ── soundBank 필드 1개만 추가
 └─ UnitSoundBankSO / BuildingSoundBankSO (유닛/건물 종류별로 1개씩 만드는 신규 에셋)
      ├─ SFX 슬롯: attackSFX, spawnSFX(이륙/엔진 등), deathSFX, skillSFX ...
      └─ Voice 슬롯: selectVoice, moveVoice, attackOrderVoice, spawnVoice, deathVoice, ...
      (각 슬롯 = SoundClipSet: AudioClip 리스트 + 그중 하나를 랜덤으로 뽑는 헬퍼)

UnitAudio / BuildingAudio (신규 컴포넌트, UnitEffects/ConstructionEffects와 동일한 자리에 부착)
 ├─ HealthManager.OnDeath 구독 → soundBank의 deathSFX/deathVoice 재생
 ├─ PlayAttackSound() 같은 public 메서드 → UnitController.Attack()에서 직접 호출 (UnitEffects.PlayAttack()과 나란히)
 └─ 아군/적 컨트롤러 둘 다 GetComponent로 null 체크 (UnitEffects와 동일 패턴, 코드 중복 없이 공용)
```

핵심 설계 원칙 3가지:
1. **"유닛별로 쉽게 추가"** = 코드가 아니라 **에셋**으로 해결. 유닛 하나 늘 때 코드 수정 없이
   `UnitSoundBankSO` 에셋 하나 새로 만들어서 `UnitData.soundBank`에 드래그해 끼우기만 하면 끝
   (`UnitTraitOption`이 코드 수정 없이 유닛별 스킬을 붙이는 것과 동일한 아이디어).
2. **재생 트리거는 기존 이펙트 시스템과 동일한 자리**에서 건다 - 애니메이션 이벤트 새로 안 만들고,
   `UnitEffects`/`ConstructionEffects`/`BuildingEffects`가 이미 훅 걸어둔 지점(`Attack()`, `OnDeath`,
   `StartLoop()`/`StopLoopAndPlayComplete()`, `LiftOff()`/`Land()`) 옆에 사운드 호출을 나란히 추가.
3. **"선택/이동/공격명령 대사"는 유닛 단위가 아니라 명령 단위**로 재생 - `RTSUnitController`의
   선택/이동/공격 진입점(위 조사 결과 참고)에서 "이번에 영향받는 유닛 중 대표 1마리"의 대사만 골라
   재생. 그래야 다수 선택 시 대사가 겹쳐 시끄러워지는 문제를 원천적으로 막는다.

## 1) 사운드 카테고리 분류

요청 원문의 항목들을 아래처럼 4개 그룹(BGM/SFX/Voice/GlobalVoice)으로 정리했다. Voice와 GlobalVoice는
둘 다 "음성" 슬라이더로 묶여서 조절되지만(믹서 그룹은 하나), 데이터가 "유닛별이냐 아니냐"로 저장 위치가
다르다.

| 그룹 | 소속 볼륨 슬라이더 | 저장 위치 | 항목 |
|---|---|---|---|
| BGM | 배경음악 | `SoundManager` 인스펙터에 직접 (3곡 리스트) | 스테이지 시작 시 랜덤 1곡, 끝나면 다시 랜덤(직전 곡 연속 방지 옵션) 무한 반복 |
| SFX (유닛) | 효과음 | `UnitSoundBankSO` | 공격 소리, 생성 소리(이륙/엔진 등, **음성 제외**), 사망 소리, 스킬 사용 소리 |
| SFX (건물) | 효과음 | `BuildingSoundBankSO` | 건설 중 루프음, 건설 완료음, 파괴음 |
| SFX (기타) | 효과음 | `UnitSoundBankSO`(자원노드는 워커 유닛 것 재사용) / `SoundManager` 전역 | 자원 채취 소리(워커의 SFX), 인터페이스(버튼 클릭) 소리, 이동/회전(엔진·발소리 루프, 후순위) |
| Voice (유닛) | 음성 | `UnitSoundBankSO` | 선택 시 랜덤 대사(3~4개), 이동 시 랜덤 대사(3~4개), 공격명령 시 랜덤 대사(1~2개), 생성 음성, 사망 음성 |
| Voice (건물) | 음성 | `BuildingSoundBankSO` | 건물 음성(선택 시 등, 아래 열린 질문 참고) |
| Voice (워커 전용) | 음성 | Worker의 `UnitSoundBankSO` | 건설 실패 음성, 건설 완료 음성 (요청 원문에 "일꾼의"라고 명시됨 → 워커 유닛 보이스뱅크 소속) |
| GlobalVoice (나레이션) | 음성 | `GlobalVoiceBankSO` (유닛에 안 묶임, 1개 에셋) | 자원부족 경고, 인구수부족 경고, 화면 밖 피격 경고("공격받았습니다"), 업그레이드 완료 대사 |

## 2) 데이터 모델

### 2-1. 공용 `SoundClipSet` (신규, `Assets/Scripts/Audio/SoundClipSet.cs`)

`UnitTraitOption`과 같은 자리 감각으로, "랜덤 재생용 클립 묶음 하나"를 표현하는 최소 단위.

```csharp
[System.Serializable]
public class SoundClipSet
{
    [field: SerializeField]
    public List<AudioClip> clips { get; private set; } = new List<AudioClip>();

    [field: SerializeField, Range(0f, 1.5f)]
    public float volumeScale { get; private set; } = 1f; // 이 카테고리만 살짝 더 크게/작게 하고 싶을 때

    [field: SerializeField, Range(0f, 0.3f)]
    public float pitchVariance { get; private set; } = 0f; // 같은 클립이 반복돼도 덜 기계적으로 들리게(선택)

    // clips가 비어있으면 null 반환 (호출부는 null이면 그냥 재생 스킵)
    public AudioClip GetRandomClip() =>
        clips.Count == 0 ? null : clips[Random.Range(0, clips.Count)];
}
```

### 2-2. `UnitSoundBankSO` (신규, `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`)

유닛 "종류"별로 하나씩 만드는 에셋 (예: `Sharpshooter SoundBank.asset`). `UnitTraitOption`이 유닛
스킬을 코드 밖에서 관리하는 것과 동일하게, 유닛이 늘어나도 이 파일은 건드릴 필요 없음.

```csharp
[CreateAssetMenu]
public class UnitSoundBankSO : ScriptableObject
{
    [Header("SFX (효과음 - 음성 제외)")]
    [field: SerializeField] public SoundClipSet attackSFX { get; private set; }
    [field: SerializeField] public SoundClipSet spawnSFX { get; private set; }  // 이륙음/엔진음 등, 유닛마다 다르게 채움
    [field: SerializeField] public SoundClipSet deathSFX { get; private set; }
    [field: SerializeField] public SoundClipSet skillSFX { get; private set; }  // 고급유닛 액티브 스킬용
    [field: SerializeField] public SoundClipSet gatherSFX { get; private set; } // 워커 전용, 나머지 유닛은 비워둠

    [Header("Voice (음성)")]
    [field: SerializeField] public SoundClipSet selectVoice { get; private set; }     // 3~4개 권장
    [field: SerializeField] public SoundClipSet moveVoice { get; private set; }       // 3~4개 권장
    [field: SerializeField] public SoundClipSet attackOrderVoice { get; private set; } // 1~2개 권장
    [field: SerializeField] public SoundClipSet spawnVoice { get; private set; }
    [field: SerializeField] public SoundClipSet deathVoice { get; private set; }

    [Header("Voice (워커 전용 - 다른 유닛은 비워둠)")]
    [field: SerializeField] public SoundClipSet buildCompleteVoice { get; private set; }
    [field: SerializeField] public SoundClipSet buildFailVoice { get; private set; }
}
```

`UnitData`(`UnitDataSO.cs`)에는 필드 1개만 추가:
```csharp
[field: SerializeField]
public UnitSoundBankSO soundBank { get; private set; } // 비워두면 그 유닛은 조용함 (null 체크로 안전)
```

### 2-3. `BuildingSoundBankSO` (신규) / `BuildingData` 추가 필드

```csharp
[CreateAssetMenu]
public class BuildingSoundBankSO : ScriptableObject
{
    [field: SerializeField] public SoundClipSet constructLoopSFX { get; private set; }
    [field: SerializeField] public SoundClipSet constructCompleteSFX { get; private set; }
    [field: SerializeField] public SoundClipSet destroySFX { get; private set; }
    [field: SerializeField] public SoundClipSet selectVoice { get; private set; } // "건물 음성" - 열린 질문 참고
}
```
`BuildingData`에 `soundBank` 필드 1개 추가 (동일 패턴).

### 2-4. `GlobalVoiceBankSO` (신규, 나레이션 - 유닛에 안 묶임)

```csharp
[CreateAssetMenu]
public class GlobalVoiceBankSO : ScriptableObject
{
    [field: SerializeField] public SoundClipSet insufficientResources { get; private set; }
    [field: SerializeField] public SoundClipSet insufficientPopulation { get; private set; }
    [field: SerializeField] public SoundClipSet underAttackWarning { get; private set; } // 화면 밖 피격
    [field: SerializeField] public SoundClipSet upgradeComplete { get; private set; }
}
```
`SoundManager` 인스펙터에 이 에셋 1개만 참조로 물린다 (진영별로 나레이터 목소리가 다르면 나중에
`GlobalVoiceBankSO`를 진영 수만큼 만들어서 `SoundManager`가 현재 플레이어 진영에 맞는 걸 선택하도록
확장 가능 - MVP는 1개로 시작).

## 3) `SoundManager` 본체 설계 (신규, `Assets/Scripts/Audio/SoundManager.cs`)

```csharp
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private AudioMixer mixer; // 그룹: Master / BGM / SFX / Voice

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource; // Loop=false, 곡마다 수동으로 다음 곡 스케줄
    [SerializeField] private List<AudioClip> bgmTracks; // 정확히 3개 권장, 개수 제한은 안 둠

    [Header("Pools")]
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4; // 음성은 SFX보다 훨씬 적어도 됨(동시에 여러 목소리 안 겹치게)

    [Header("Global Voice")]
    [SerializeField] private GlobalVoiceBankSO globalVoiceBank;

    [Header("Voice 스팸 방지")]
    [SerializeField] private float globalVoiceCooldown = 6f; // 카테고리별 최소 재재생 간격(초)

    void Awake() { Instance = this; LoadVolumePrefs(); /* 풀 생성 */ }

    // ── 재생 API ──────────────────────────────────────
    public void PlayUnitSFX(UnitSoundBankSO bank, SFXType type, Vector3 worldPos);
    public void PlayUnitVoice(UnitSoundBankSO bank, VoiceType type);           // 2D, 위치 없음
    public void PlayBuildingSFX(BuildingSoundBankSO bank, BuildingSFXType type, Vector3 worldPos);
    public void PlayBuildingVoice(BuildingSoundBankSO bank);
    public void PlayGlobalVoice(GlobalVoiceType type);                        // 내부에서 쿨다운 체크
    public void PlayUIClick();                                                // 인터페이스 소리(전역 1~n개)

    // ── 볼륨/뮤트 API (설정 UI가 호출) ──────────────────
    public void SetMasterVolume(float linear01);
    public void SetBGMVolume(float linear01);
    public void SetSFXVolume(float linear01);
    public void SetVoiceVolume(float linear01);
    public void SetBGMMuted(bool muted);
    public void SetSFXMuted(bool muted);
    public void SetVoiceMuted(bool muted);
    // (주음량은 요청 원문에 토글 언급이 없어 슬라이더만 - 열린 질문 참고)
}
```

- **풀링**: SFX/Voice 둘 다 "미리 만들어둔 AudioSource N개를 순환 재사용"하는 단순 오브젝트 풀.
  재생 시 유휴 소스가 없으면 가장 오래전에 재생 시작한 소스를 가로채 재사용(RTS 특성상 한 번에 수십
  개체가 동시에 공격하는 상황이 흔하므로 "재생 실패로 조용해지는 것"보다 "오래된 소리를 끊고 새 걸
  트는 것"이 자연스러움).
- **3D vs 2D**: 유닛/건물 SFX(공격/사망/건설/파괴/채취)는 `spatialBlend=1`(3D)로 재생 위치에 맞게
  풀에서 꺼낸 소스의 `transform.position`을 세팅 - 화면 밖에서 나는 전투는 작게 들리게. Voice(대사,
  나레이션)는 스타1/2처럼 `spatialBlend=0`(2D)으로 항상 또렷하게.
- **뮤트=슬라이더 값 보존**: 토글 OFF는 볼륨을 0으로 내리는 게 아니라 믹서 파라미터만 `-80dB`로
  보내고 슬라이더가 들고 있는 값(PlayerPrefs)은 그대로 유지 - 다시 토글 ON 하면 이전 슬라이더 위치로
  복귀. `SetBGMVolume(0.7f)` 후 `SetBGMMuted(true)`→`false`를 해도 0.7이 유지되는 식.
- **볼륨 저장**: `PlayerPrefs`에 `MasterVolume/BGMVolume/SFXVolume/VoiceVolume`(float, 0~1) +
  `BGMMuted/SFXMuted/VoiceMuted`(int, 0/1) 6~7개 키로 영속화, `Awake()`에서 로드해 즉시 믹서에 반영.
- **BGM 무한 랜덤 재생**: `bgmSource.loop = false`로 두고, `Update()`(또는 코루틴)에서
  `!bgmSource.isPlaying`을 감지하면 `bgmTracks`에서 직전 곡을 제외하고 랜덤으로 다음 곡을 골라
  재생 - "3곡 중 매번 랜덤, 끝나면 또 랜덤, 무한 반복" 요구사항 그대로.

## 4) 훅 지점 (기존 코드에서 사운드 호출을 추가할 자리)

기존 `UnitEffects`/`ConstructionEffects`/`BuildingEffects`와 **나란히** 동작하는 `UnitAudio`/
`BuildingAudio` 컴포넌트를 신설해서, 기존 파일(`UnitController.cs` 등)에는 `GetComponent<UnitAudio>()?.PlayXxx()`
한 줄씩만 추가하는 방식을 제안 (기존 이펙트 훅과 완전히 동일한 자리, 동일한 스타일).

| 이벤트 | 위치 | 방식 |
|---|---|---|
| 공격 소리/대사 | `UnitController.Attack()` (885줄 부근, `UnitEffects.PlayAttack()` 옆) | `UnitAudio.PlayAttackSFX()` 직접 호출 |
| 사망 소리/대사 | `HealthManager.OnDeath` 이벤트 | `UnitAudio`가 구독 (UnitEffects.HandleDeath와 동일 패턴) |
| 생성 소리/대사 | `UnitSpawner.Spawn(int unitID)` (Instantiate 직후) | 스폰된 유닛의 `UnitAudio.PlaySpawnSFX()`/`PlaySpawnVoice()` 호출 |
| 건설 루프/완료 소리 | `BaseStructure.Initialize()`/`CompleteConstruction()` (`ConstructionEffects` 호출 옆) | `BuildingAudio.PlayConstructLoop()`/`PlayConstructComplete()` |
| 파괴 소리 | `BuildingController.Die()` | `BuildingAudio.PlayDestroySFX()` |
| 채취 소리 | `UnitController.Gather()` 진행 중 루프 | 워커의 `UnitAudio.PlayGatherSFX()` |
| 선택 대사 | `RTSUnitController.SelectUnit()` (193줄, 단일 진입점) | 이번에 선택된 유닛 중 1마리만 뽑아 `PlayUnitVoice(select)` |
| 이동 대사 | `RTSUnitController.MoveSelectedUnits()` (289줄) | 대표 1마리만 |
| 공격명령 대사 | `AttackSelectedUnits()`류 (300~375줄) | 대표 1마리만 |
| 자원부족 경고 | `TryProduceUnit`/`TryResearch`의 `Debug.Log("자원부족!")` 옆 | `SoundManager.Instance.PlayGlobalVoice(InsufficientResources)` |
| 인구수부족 경고 | `TryProduceUnit`의 `Debug.Log("인구수부족!")` 옆 | `PlayGlobalVoice(InsufficientPopulation)` |
| 건설 실패 (워커 음성) | `PlacementSystem`의 조용한 `return` 지점 (신규 로그도 같이 추가 필요) | 마지막으로 명령한 워커의 `UnitAudio.PlayBuildFailVoice()` |
| 건설 완료 (워커 음성) | `BaseStructure.CompleteConstruction()` | 해당 건물을 지은 워커의 `UnitAudio.PlayBuildCompleteVoice()` |
| 화면 밖 피격 경고 | `HealthManager.OnDamaged` 중, 공격받은 유닛/건물이 카메라 뷰포트 밖일 때 | `PlayGlobalVoice(UnderAttackWarning)` (쿨다운 있음) |
| 업그레이드 완료 대사 | `UpgradeManager`의 연구 완료 처리 지점 | `PlayGlobalVoice(UpgradeComplete)` |

`UnitAudio`/`BuildingAudio`는 `UnitEffects`와 동일하게 `GetComponent<UnitController>()` /
`GetComponent<EnemyUnitController>()`를 둘 다 null 체크해서 아군/적 프리팹에 공용으로 하나만 부착
(적 쪽은 플레이어 커맨드가 없으므로 선택/이동/공격명령 대사는 자연히 해당 없음 - 사망/공격/생성
소리만 재생됨).

## 5) 설정(Settings) UI

기존 UI에 볼륨 설정 화면이 전혀 없어서 새로 만들어야 한다. `UIController.cs`의 기존 패널 패턴과는
독립적인 별도 오버레이(예: ESC 메뉴 안에 넣거나, 커맨드 패널에 톱니바퀴 버튼 추가)로 제안:

- `SoundSettingsPanel.cs` (신규): 슬라이더 4개(주음량/배경음악/효과음/음성) + 토글 3개
  (배경음악/효과음/음성 - 주음량은 요청 원문에 토글이 없어 슬라이더만, 아래 열린 질문 참고).
- 각 슬라이더 `OnValueChanged` → `SoundManager.Instance.SetXxxVolume(value)`, 각 토글 → `SetXxxMuted(!isOn)`.
- 실제 캔버스/버튼 레이아웃은 유니티 에디터에서 배치해야 하는 부분이라(프리팹/씬 GameObject 배선),
  스크립트만으로 끝나지 않고 에디터 작업이 필요함 - Phase 4로 분리 제안.

## 6) 구현 순서 제안 (Phase 분리)

1. **Phase 1 - 코어**: `SoundManager` + `AudioMixer` 에셋 생성 + 볼륨/뮤트 PlayerPrefs 영속화.
   (테스트는 인스펙터에서 값 조절해보는 수준, UI 없음)
2. **Phase 2 - 데이터**: `SoundClipSet`/`UnitSoundBankSO`/`BuildingSoundBankSO`/`GlobalVoiceBankSO`
   + `UnitData`/`BuildingData`에 `soundBank` 필드 추가.
3. **Phase 3 - 훅업**: 위 "4) 훅 지점" 표의 위치들에 실제 재생 호출 추가 (`UnitAudio`/`BuildingAudio`
   신설 포함). 이 시점부터 실제 클립을 채워 넣으면 소리가 들림.
4. **Phase 4 - 설정 UI**: 슬라이더/토글 패널 제작 + `SoundManager` API 연결.

각 Phase는 그 자체로 독립적으로 확인 가능해서, Phase 1이 끝나면 바로 다음 Phase로 넘어갈지 여부를
다시 확인받는 방식을 제안 (한 번에 전부 구현하지 않음).

## 열린 질문 (구현 시작 전 확인 필요)

1. **주음량(Master)에도 토글(뮤트)이 필요한가?** 요청 원문에는 배경음악/효과음/음성 3개만 "토글+
   슬라이더"라고 명시되어 있고 주음량은 설명이 없음. 슬라이더만 두는 걸로 가정했는데, 전체 음소거
   토글도 원하면 알려달라.
2. **"건물 음성"의 정확한 트리거가 뭔가?** 요청 원문에 "건물 음성"이 음성 목록에 있지만 구체적으로
   언제 재생되는지는 명시가 안 됨 (건물 선택 시 "커맨드 센터, 대기 중" 같은 대사? 아니면 다른 이벤트?)
   - 일단 "건물 선택 시 1회"로 가정해서 `BuildingSoundBankSO.selectVoice`를 넣었는데, 다른 의미면
     알려달라.
3. **"건설 실패 경고음"이 워커 음성인지 전역 나레이션인지?** 요청 원문 상단 목록엔 "건설 실패
   경고음"이 음성 카테고리 설명에, 사운드 매니저 섹션엔 "일꾼의 건설 실패음성"이라고 명시돼서, 이
   설계에서는 **워커 유닛의 `buildFailVoice`**로 정리했다(전역 나레이션이 아님). 이대로 괜찮은지?
4. **BGM 곡 전환 방식** - "매 스테이지마다 랜덤"이라고 했는데, 이 프로젝트는 스테이지 개념이 아직
   씬 분리로 명확히 안 보여서(캠페인 스토리는 doc/0229에 있지만 실제 스테이지 전환 코드는 미확인),
   일단 "씬(경기) 시작 시 랜덤 1곡 선택 + 곡이 끝날 때마다 다시 랜덤 재생을 무한 반복"으로 설계했다.
   여러 스테이지가 하나의 씬 로드/언로드 없이 이어지는 구조라면 "스테이지 전환 이벤트"를 어디서
   감지해야 할지 추가로 알려줘야 함.
5. **AudioMixer 에셋을 프로젝트에 새로 만들어도 되는가?** (`Assets/Audio/MainAudioMixer.mixer`) -
   Unity 에디터에서 믹서 그룹/노출 파라미터 설정이 필요한 부분이라, 코드만으로 100% 자동화하기보다
   에디터에서 직접 만드는 걸 권장 (원하면 최대한 스크립트/YAML로 대신 만들어볼 수도 있음, 다만
   위험도가 있어 권장하지 않음).
6. **효과음 세부 항목 중 "이동 & 회전"(엔진 루프/발소리) / "환경소리"** - 원-샷이 아니라 루프성
   사운드라 이번 Phase 3 훅 지점 표에는 포함하지 않았다(우선순위 낮음/후속 작업으로 미룸). 지금
   범위에 꼭 포함해야 하면 알려달라.

## 구현 결과 (2026-07-28, "그런식으로 구현해줘" 승인 후)

Phase 1~4를 한 번에 구현했다. 열린 질문 6개는 대부분 문서에 적어둔 가정대로 진행했고(질문 2/3/4는
"이대로 가정" 문구 그대로 채택), 아래 2개는 구현 시점에 추가로 판단해 결정했다:

- **열린 질문 5(AudioMixer)**: `.mixer` 에셋은 유니티 에디터 전용 복잡한 직렬화 포맷이라 텍스트 편집으로
  안전하게 만들 수 없어서, **AudioMixer를 아예 쓰지 않고** `SoundManager`가 카테고리별 볼륨(주음량×배경음악
  /효과음/음성)을 직접 곱해서 `AudioSource.volume`에 적용하는 방식으로 바꿨다. 기능(4개 볼륨 슬라이더 +
  3개 뮤트 토글, 뮤트해도 슬라이더 값 보존)은 설계와 동일하고, 에디터에서 믹서를 미리 구성해둬야 하는
  선행 작업이 없어졌다.
- **선택/이동/공격명령 대사의 정확한 트리거 지점**: 드래그 선택(`DragSelectUnit`)은 박스 안에 들어오는
  유닛마다 매 프레임 호출되는 구조라 여기에 훅을 걸면 대사가 스팸되므로, **`ClickSelectUnit`/
  `ShiftClickSelectUnit`(클릭 1회 = 호출 1회)에만 선택 대사를 걸고 드래그 선택은 무음으로 남겨뒀다.**
  이동/공격명령은 설계대로 `RTSUnitController`의 각 `~SelectedUnits` 진입점에 새로 추가한
  `PlayRepresentativeUnitVoice()` 헬퍼로 "선택된 유닛 중 첫 번째 유효한 유닛 1마리"만 재생한다.

### 실제로 만들어진/수정된 파일

- 신규: `Assets/Scripts/Audio/SoundClipSet.cs`, `SoundManager.cs`, `UnitAudio.cs`, `BuildingAudio.cs`,
  `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`, `BuildingSoundBankSO.cs`, `GlobalVoiceBankSO.cs`,
  `Assets/Scripts/UI/SoundSettingsPanel.cs`.
- 수정: `UnitDataSO.cs`/`BuildingDataSO.cs`(`soundBank` 필드 추가), `UnitController.cs`(`Attack()`에
  `PlayAttackSFX()`, `GatherTick()`의 `Gathering` 진입 시점에 `PlayGatherSFX()`), `UnitSpawner.cs`
  (`Spawn()`에서 Instantiate 직후 `PlaySpawnSound()`), `BaseStructure.cs`(`Initialize()`에
  `PlayConstructLoop()`, `CompleteConstruction()`에 `PlayConstructComplete()` + 담당 일꾼
  `PlayBuildCompleteVoice()`), `PlacementSystem.cs`(자원/인구 부족으로 `TryConstructBuilding` 실패 시
  일꾼의 `PlayBuildFailVoice()`), `RTSUnitController.cs`(`ClickSelectUnit`/`ShiftClickSelectUnit`에
  선택 대사, `PlayRepresentativeUnitVoice` 헬퍼 + 이동/공격 6개 메서드에 훅, `SelectBuilding`에 건물
  선택 음성, `TryProduceUnit`/`TryResearch`의 자원/인구 부족 로그 옆에 나레이션 경고, `AddGlobalBonus`에
  업그레이드 완료 대사).
- 사망 SFX/음성(`UnitAudio`/`BuildingAudio`의 `HandleDeath`/`HandleDestroyed`), 화면 밖 피격 경고
  (`HandleDamaged`)는 기존 `UnitEffects`/`BuildingEffects`와 동일하게 `HealthManager.OnDamaged`/
  `OnDeath` **이벤트 구독**만으로 동작해서, `UnitController.Die()`/`BuildingController.Die()` 자체는
  코드 수정 없이 그대로 둠 (프리팹에 `UnitAudio`/`BuildingAudio` 컴포넌트를 붙이기만 하면 됨).

### 아직 안 끝난 부분 (에디터에서 직접 해야 함)

1. **프리팹에 컴포넌트 부착**: 유닛 프리팹에 `UnitAudio`, 건물/`BaseStructure` 프리팹에 `BuildingAudio`를
   `HealthManager`/`UnitController`(또는 `EnemyUnitController`)와 나란히 추가해야 실제로 소리가 난다.
2. **사운드 뱅크 에셋 제작 + 연결**: 유닛/건물 종류별로 `UnitSoundBankSO`/`BuildingSoundBankSO` 에셋을
   만들고 클립을 채운 뒤, 각 `UnitData`/`BuildingData` 항목의 `soundBank` 필드에 연결. `GlobalVoiceBankSO`
   1개도 만들어 `SoundManager` 인스펙터에 연결.
3. **씬에 `SoundManager` 배치**: 빈 GameObject 하나에 `SoundManager` 컴포넌트를 붙이고, `bgmSource`
   (Loop 꺼진 AudioSource)와 `bgmTracks`(3곡) 연결.
4. **설정 UI 레이아웃**: `SoundSettingsPanel`은 로직만 있고, 실제 Canvas/슬라이더 4개/토글 3개 배치와
   인스펙터 연결은 유니티 에디터 작업 필요.

## 요약/영향받는 파일 (구현 승인 시)

- 신규 파일: `Assets/Scripts/Audio/SoundManager.cs`, `Assets/Scripts/Audio/SoundClipSet.cs`,
  `Assets/Scripts/Audio/UnitAudio.cs`, `Assets/Scripts/Audio/BuildingAudio.cs`,
  `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`,
  `Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs`,
  `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`, `Assets/Scripts/UI/SoundSettingsPanel.cs`,
  `Assets/Audio/MainAudioMixer.mixer` (에디터 작업).
- 수정 파일: `UnitDataSO.cs`/`BuildingDataSO.cs`(`soundBank` 필드 추가), `UnitController.cs`,
  `RTSUnitController.cs`, `UnitSpawner.cs`, `BaseStructure.cs`, `BuildingController.cs`,
  `PlacementSystem.cs`, `UpgradeManager.cs` (각각 위 "4) 훅 지점" 표 위치에 한두 줄 호출 추가).
- 지금은 **설계만** 정리된 상태이며 프로젝트 코드는 전혀 건드리지 않았다.
