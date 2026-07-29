## 날짜
2026-07-29

## 요청 내용
여러 유닛이 동시에 공격/사망할 때 같은 효과음이 겹쳐 재생되어 귀에 부담을 주는 문제. 사용자가 스타크래프트류 RTS에서 쓰는 오디오 관리 기법 8가지를 직접 조사해왔고("동일 사운드 동시 재생 제한", "최소 재생 간격", "우선순위", "채널 제한", "거리 감쇠", "랜덤 사운드 선택", "짧은 효과음", "그룹 관리"), 이 게임(`SoundManager.cs` 아키텍처)에 실제로 적용할 만한 방법을 찾아달라는 요청.

## 조사 내용 - 기법별 현재 적용 상태

| 기법 | 현재 상태 | 비고 |
|---|---|---|
| 랜덤 사운드 선택 | ✅ 이미 적용 | `SoundClipSet.GetRandomClip()` - 클립 여러 개 등록하면 자동 랜덤 (doc/0255) |
| 거리 감쇠 | ✅ 이미 적용 | 3D SFX `Linear` 롤오프, `minDistance=15`/`maxDistance=80` (doc/0277~0278) |
| 사운드 그룹 관리 | ✅ 이미 적용 (카테고리 단위) | BGM/SFX/Voice/Master 4개 카테고리별 볼륨·뮤트 (doc/0255) |
| 최소 재생 간격(Cooldown) | 🟡 부분 적용 | `PlayGlobalVoice`의 피격 경고음 등 "전역 나레이션"에만 있음(doc/0273). 공격/사망 등 일반 SFX(`PlaySFX`)엔 전혀 없음 - **여러 유닛이 같은 프레임에 공격하면 그대로 다 겹쳐 재생됨** |
| 동일 사운드 동시 재생 제한 | ❌ 미적용 | 없음. `attackSFX`는 유닛 "종류"별로 `SoundClipSet` 하나를 공유하므로(`UnitSoundBankSO`), 같은 종류 유닛 10마리가 동시에 쏘면 10번 다 재생 시도됨 |
| 채널(Channel) 제한 | 🟡 부분 적용 (전체 풀 단위) | `sfxPoolSize=16`(전체) - 카테고리/유닛 종류별 세부 제한은 없음. 16개 넘으면 가장 오래된 소스를 그냥 가로채서 재생(`GetAvailableSource`) - "제한"이 아니라 "빼앗기" 방식 |
| 사운드 우선순위(Priority) | ❌ 미적용 | 가로채기가 "가장 오래된 것"만 기준 - 중요도 개념 없음 |
| 짧은 효과음 사용 | 해당 없음(코드 밖 영역) | 오디오 클립 자체를 편집해야 하는 작업이라 스크립트로 제어 불가 - 원본 SFX 클립이 이미 짧으면 넘어가도 됨 |

## 이 게임에 적용할 만한 조합 (제안)

가장 직접적인 원인은 **"같은 SoundClipSet(예: 특정 유닛 종류의 attackSFX)이 짧은 시간 안에 몇 번이고 겹쳐 재생되는 것"**이므로, 사용자가 5점 만점을 준 두 기법 - **① 최소 재생 간격**과 **② 동일 사운드 동시 재생 제한** - 을 `SoundClipSet` 단위로 `PlayFromPool`에 추가하는 게 가장 효과적이라고 판단.

### ① 최소 재생 간격 (SoundClipSet별 retrigger 쿨다운)
같은 `SoundClipSet`이 마지막 재생 시작 후 N밀리초 이내에 다시 요청되면 이번 요청은 그냥 버림(재생 안 함). 같은 프레임/같은 틱에 여러 유닛이 동시에 공격해도 사실상 1~2번만 소리가 남. `PlayGlobalVoice`의 `minInterval` 패턴(doc/0273)과 동일한 방식을 `PlayFromPool`에 일반화.

### ② 동일 사운드 동시 재생 제한 (SoundClipSet별 동시 재생 개수 캡)
같은 `SoundClipSet`을 지금 재생 중인 소스 개수가 이미 N개(예: 3~4개)면, 새 요청은 재생하지 않고 버림. ①과 달리 "연속으로 몰아치는" 상황(예: 여러 유닛이 조금씩 시간차를 두고 계속 공격)에서도 항상 최대 볼륨 레이어 수를 일정하게 유지해준다. 구현은 `AudioSource -> 현재 재생 중인 SoundClipSet` 매핑을 하나 추가로 들고 있으면 됨(풀 소스가 가로채기로 재사용되어도 최신 매핑만 보면 정확).

### 적용 범위
- `PlaySFX`/`PlaySFX2D`(전투/명령 SFX 전반)에 공통 적용 - 공격/사망/스킬/건설 등 "여러 개체가 동시에 트리거"되는 상황이 흔한 쪽.
- `PlayVoice`(스폰/사망 대사)에도 적용 가능하지만, 이건 사용자가 언급한 문제(공격 SFX 스팸)의 범위 밖이라 일단 제외하고 필요하면 별도 확인.
- `PlayOrderVoice`/`PlayGlobalVoice`는 이미 자체적인 겹침 방지 로직이 있으므로 손대지 않음.

### 적용 안 하는 것 (근거)
- **우선순위(Priority)**: 지금 규모(풀 16개, 유닛 종류별 개별 캡 도입 시)에서는 ①②만으로 "귀 아픔" 문제가 충분히 해결될 것으로 보이고, "가로채기 시 중요한 소리 보호" 같은 추가 복잡도는 지금 시급하지 않음 - 나중에 실제로 폭발/스킬음이 잘려나가는 게 체감되면 그때 추가.
- **짧은 효과음 재제작**: 코드 영역 밖(오디오 에셋 편집) - 스킵.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**추가할 필드**
```csharp
[Header("동시다발 SFX 스팸 방지 (여러 유닛이 동시에 공격/사망 등으로 같은 사운드를 트리거할 때 귀에 부담 주는 것 방지)")]
[SerializeField] private float sfxRetriggerInterval = 0.05f; // 같은 SoundClipSet은 이 시간(초) 이내 재요청 시 무시
[SerializeField] private int sfxMaxConcurrentPerSet = 4; // 같은 SoundClipSet이 동시에 재생 중일 수 있는 최대 개수

private readonly Dictionary<SoundClipSet, float> lastSfxStartTime = new Dictionary<SoundClipSet, float>();
private readonly Dictionary<AudioSource, SoundClipSet> sourceCurrentSet = new Dictionary<AudioSource, SoundClipSet>();
```

**PlayFromPool 수정 (sfxPool에 한해 스팸 방지 체크 추가)**
```csharp
private AudioSource PlayFromPool(List<PooledSource> pool, SoundClipSet set, float categoryVolume, bool muted, float spatialBlend, Vector3 worldPos, bool limitSpam = false)
{
    if (set == null || !set.HasClips)
        return null;

    if (limitSpam)
    {
        if (lastSfxStartTime.TryGetValue(set, out float lastStart) && Time.time - lastStart < sfxRetriggerInterval)
            return null; // 최소 재생 간격 - 같은 프레임/틱에 몰린 중복 트리거 억제

        int concurrent = 0;
        foreach (PooledSource p in pool)
        {
            if (p.Source.isPlaying && sourceCurrentSet.TryGetValue(p.Source, out SoundClipSet playingSet) && playingSet == set)
                ++concurrent;
        }
        if (concurrent >= sfxMaxConcurrentPerSet)
            return null; // 동일 사운드 동시 재생 개수 제한
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
```

**호출부 수정**
```csharp
public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
    PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos, limitSpam: true);

public void PlaySFX2D(SoundClipSet set) =>
    PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position, limitSpam: true);
```
(`PlayVoice`/`PlayGlobalVoice` 호출부는 `limitSpam` 기본값(false)을 그대로 써서 지금과 동일하게 동작 - 회귀 없음.)

## 확인 결과
사용자가 "PlayVoice(스폰/사망 대사)까지 포함" + "50ms/4개(제안값)"로 선택.

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs
- `sfxRetriggerInterval`(0.05f)/`sfxMaxConcurrentPerSet`(4) 인스펙터 필드 추가.
- `lastSfxStartTime`(`Dictionary<SoundClipSet, float>`), `sourceCurrentSet`(`Dictionary<AudioSource, SoundClipSet>`) 추가.
- `PlayFromPool`에 `limitSpam` 매개변수(기본 `false`) 추가 - `true`면 최소 재생 간격 체크 후, 동시 재생 개수 체크를 통과해야만 실제로 재생하고 두 딕셔너리를 갱신한다.
- `PlaySFX`/`PlaySFX2D`/`PlayVoice`는 `limitSpam: true`로 호출하도록 변경.
- `PlayGlobalVoice` 내부의 `PlayFromPool` 호출은 이미 자체적으로 겹침/최소간격을 처리하고 있어(doc/0271, doc/0273) `limitSpam`을 켜지 않음(기본값 `false` 그대로).
- `PlayOrderVoice`는 `PlayFromPool`을 거치지 않는 전용 채널(`orderVoiceSource`)이라 영향 없음.

## 요약/남은 작업
적용 완료. 실제로 유닛 여러 마리를 동시에 공격/사망시켜서 소리가 겹치는 정도가 적당한지 확인 필요. 너무 자주 씹히면 `sfxRetriggerInterval`을 줄이거나 `sfxMaxConcurrentPerSet`을 늘리고, 여전히 시끄러우면 반대로 조정.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
