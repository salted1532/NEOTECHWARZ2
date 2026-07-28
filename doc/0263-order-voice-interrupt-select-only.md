# 0263 - 명령 음성 끼어들기를 "다른 유닛 선택" 시에만 적용하도록 수정

**날짜:** 2026-07-28

## 요청 내용

> 명령 음성 끼어들기는 다른 유닛 선택시 선택 음성에서만 하고 만약 이동명령에서 공격명령을 하게
> 되면 이동명령 음성 끝까지 들려주고 그 다음 명령부터 다시 새로 재생하는식으로 해줘

doc/0262에서 만든 끼어들기(interrupt) 규칙이 "다른 유닛/다른 명령 종류"면 전부 끊고 새로 재생하는
방식이었는데, 이걸 다음처럼 좁혀달라는 요청:
- **다른 유닛을 선택**했을 때만 재생 중이던 대사를 즉시 끊고 새로 재생.
- 이동→공격처럼 **명령 종류가 바뀌어도**(선택이 아니면) 끊지 않고 재생 중이던 대사를 끝까지 들려주고,
  그 다음 명령부터 다시 새로 랜덤 재생.

## 코드 변경

### `Assets/Scripts/Audio/SoundManager.cs`

Before (doc/0262):
```csharp
public void PlayOrderVoice(SoundClipSet set, UnitAudio owner, string category)
{
    if (set == null || !set.HasClips)
        return;

    bool sameCommandStillPlaying = currentOrderVoiceOwner == owner
        && currentOrderVoiceCategory == category
        && orderVoiceSource.isPlaying;

    if (sameCommandStillPlaying)
        return;

    AudioClip clip = set.GetRandomClip();
    if (clip == null)
        return;

    currentOrderVoiceOwner = owner;
    currentOrderVoiceCategory = category;

    orderVoiceSource.Stop();
    orderVoiceSource.clip = clip;
    orderVoiceSource.pitch = set.GetRandomPitch();
    orderVoiceSource.volume = EffectiveVolume(voiceVolume, voiceMuted) * set.volumeScale;
    orderVoiceSource.Play();
}
```

After:
```csharp
public void PlayOrderVoice(SoundClipSet set, UnitAudio owner, string category)
{
    if (set == null || !set.HasClips)
        return;

    bool isNewUnitSelection = category == "select" && owner != currentOrderVoiceOwner;

    if (!isNewUnitSelection && orderVoiceSource.isPlaying)
        return; // 새 유닛 선택이 아니면 재생 중인 대사를 끊지 않는다 - 이번 요청은 버려짐

    AudioClip clip = set.GetRandomClip();
    if (clip == null)
        return;

    currentOrderVoiceOwner = owner;

    orderVoiceSource.Stop(); // isNewUnitSelection일 때만 실제로 뭔가 끊길 수 있음
    orderVoiceSource.clip = clip;
    orderVoiceSource.pitch = set.GetRandomPitch();
    orderVoiceSource.volume = EffectiveVolume(voiceVolume, voiceMuted) * set.volumeScale;
    orderVoiceSource.Play();
}
```

`category`별 비교가 필요 없어져서 `currentOrderVoiceCategory` 필드도 함께 제거했다(더 이상 아무
로직도 참조하지 않음).

## 동작 정리

| 상황 | 결과 |
|---|---|
| 다른 유닛을 새로 선택(`select`, owner 다름) | 재생 중이던 대사(그게 select든 move든 attack이든) 즉시 끊고 새 선택 대사 재생 |
| 같은 유닛을 다시 선택(`select`, owner 같음) | 채널이 재생 중이면 끊지 않고 무시(요청 버려짐), 비어있으면 재생 |
| 이동 → 공격처럼 명령 종류가 바뀜 | 채널이 재생 중이면 끊지 않고 무시, 비어있으면(=이전 대사가 이미 끝났으면) 재생 |
| 같은 유닛에게 같은 명령이 연달아 들어옴 | (위와 동일 규칙) 채널이 비어있을 때만 재생, 재생 중이면 무시 |

즉 "선택으로 다른 유닛을 새로 지정"하는 경우만 끼어들기가 발동하고, 그 외 모든 이동/공격명령
전환은 지금 재생 중인 대사가 끝날 때까지 새 요청이 조용히 버려진다(대기열에 쌓이지 않음 - 대사가
끝난 뒤 들어오는 명령부터 다시 재생 대상이 됨).

## 변경된 파일

`Assets/Scripts/Audio/SoundManager.cs` (`PlayOrderVoice` 로직 수정, `currentOrderVoiceCategory` 필드 제거).
`UnitAudio.cs`/`RTSUnitController.cs`는 이미 doc/0262에서 `PlayOrderVoice(set, this, category)` 형태로
호출하고 있어서 추가 수정 없음 - `category` 문자열이 여전히 "select"/"move"/"attack"으로 전달되지만
이제 `SoundManager` 내부에서 "select"인지 아닌지만 구분해서 사용한다.
