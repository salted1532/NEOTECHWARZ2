# 0271 - 버그: 자원/인구 부족 등 나레이션이 한 번만 나오고 멈춤 (고정 쿨다운 문제)

**날짜:** 2026-07-28

## 요청 내용

> 자원 부족, 인구수 부족등의 음성이 한번만 나오고 더 안나온네 음성을 끊고 계속 나오는건 아니더라도
> 음성 재생이 끝나면 그리고 다음 명령때는 계속 나와야해 확인좀 해줘

## 원인

doc/0255 구현 당시 `SoundManager.PlayGlobalVoice`는 스팸 방지를 위해 **카테고리별 고정 6초 쿨다운**
(`globalVoiceCooldown = 6f`)을 뒀다:

```csharp
public void PlayGlobalVoice(SoundClipSet set)
{
    ...
    if (lastGlobalVoiceTime.TryGetValue(set, out float last) && Time.time - last < globalVoiceCooldown)
        return; // 재생 시각 기준 6초가 안 지났으면 무조건 무시

    lastGlobalVoiceTime[set] = Time.time;
    PlayVoice(set);
}
```

문제는 이 쿨다운이 "그 클립이 실제로 재생 중인지"가 아니라 **"마지막으로 재생을 시작한 시각으로부터
6초가 지났는지"**만 본다는 점이다. 클립 길이가 1~2초라도 그 뒤 4~5초 동안은 자원부족/인구부족을
다시 겪어도 조용히 무시됐다 - 사용자 입장에선 "한 번 나오고 다시는 안 나온다"처럼 느껴졌을 것.

## 코드 변경

시간 기반 쿨다운을 없애고, **"그 카테고리의 소리가 지금 재생 중인지"**로 판단하도록 바꿨다 -
재생 중이면 겹치지 않게 무시하고, 재생이 끝나면 곧바로 다음 요청부터 다시 재생된다.

### `Assets/Scripts/Audio/SoundManager.cs`

1. `PlayFromPool`이 사용한 `AudioSource`를 반환하도록 변경(기존엔 `void`) - `PlayGlobalVoice`가 나중에
   "이 소스가 아직 재생 중인지" 확인할 수 있어야 하기 때문. 반환값이 필요 없는 다른 호출부
   (`PlaySFX`/`PlaySFX2D`/`PlayVoice`)는 그냥 버리므로 영향 없음.

2. `globalVoiceCooldown`(float 필드) + `lastGlobalVoiceTime`(Dictionary<SoundClipSet, float>) 제거,
   `activeGlobalVoiceSources`(Dictionary<SoundClipSet, AudioSource>)로 교체.

Before:
```csharp
[SerializeField] private float globalVoiceCooldown = 6f;
...
private readonly Dictionary<SoundClipSet, float> lastGlobalVoiceTime = new Dictionary<SoundClipSet, float>();
...
public void PlayGlobalVoice(SoundClipSet set)
{
    if (set == null || !set.HasClips)
        return;

    if (lastGlobalVoiceTime.TryGetValue(set, out float last) && Time.time - last < globalVoiceCooldown)
        return;

    lastGlobalVoiceTime[set] = Time.time;
    PlayVoice(set);
}
```

After:
```csharp
private readonly Dictionary<SoundClipSet, AudioSource> activeGlobalVoiceSources = new Dictionary<SoundClipSet, AudioSource>();
...
public void PlayGlobalVoice(SoundClipSet set)
{
    if (set == null || !set.HasClips)
        return;

    if (activeGlobalVoiceSources.TryGetValue(set, out AudioSource activeSource)
        && activeSource != null && activeSource.isPlaying)
        return;

    AudioSource source = PlayFromPool(voicePool, set, voiceVolume, voiceMuted, spatialBlend: 0f, transform.position);
    if (source != null)
        activeGlobalVoiceSources[set] = source;
}
```

## 동작 정리

| 상황 | 결과 |
|---|---|
| 자원부족 대사가 재생되는 도중 자원부족 상황이 또 발생 | 무시(겹쳐 재생 안 함) - 기존과 동일 |
| 자원부족 대사가 끝난 뒤 자원부족 상황이 다시 발생 | 클립 길이와 무관하게 즉시 다시 랜덤 재생 (예전엔 최대 6초까지 씹혔음) |
| 자원부족 재생 중 인구수부족 상황 발생 | 서로 다른 카테고리(`SoundClipSet`)라 독립적으로 즉시 재생됨 - 원래도 그랬음 |

## 요약/영향받는 파일

`Assets/Scripts/Audio/SoundManager.cs` - `PlayFromPool` 반환형 변경, `PlayGlobalVoice`를 시간 기반
쿨다운에서 재생 상태 기반 판정으로 교체, 관련 필드 교체.
