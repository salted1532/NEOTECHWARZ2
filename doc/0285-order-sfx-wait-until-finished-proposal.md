## 날짜
2026-07-29

## 요청 내용
명령 확인음(`orderSFX` - 이동/공격/순찰 등 명령 시 대사와 별개로 나는 삑 소리, doc/0279)도 "재생이 끝난 뒤에 다음 명령의 사운드가 재생"되었으면 좋겠다는 요청 - 즉 지금 재생 중이면 다음 명령의 orderSFX 요청은 무시하고, 재생이 끝난 뒤에 들어오는 명령부터 다시 재생.

## 조사 내용
`UnitAudio.PlayOrderSFX()`는 `SoundManager.PlaySFX2D(bank.orderSFX)`를 호출하는데, 이 경로는 doc/0284에서 추가한 `limitSpam` 스팸 방지를 탄다 - 하지만 이건 "동시에 여러 유닛이 트리거"하는 상황(공격/사망 등)에 맞춘 것이라 **동시 재생 최대 4개까지 허용** + **50ms 최소 간격**이라, 짧은 시간 안에 명령을 연달아 내리면(예: 유닛 여러 종류를 번갈아 이동시키는 경우) 이전 삑 소리가 채 끝나기도 전에 다음 삑 소리가 겹쳐 재생될 수 있음. 사용자가 원하는 건 "동시 여러 개 허용"이 아니라 **"항상 최대 1개, 재생 중이면 다음 요청은 대기가 아니라 무시하고 끝난 뒤부터 재생"**.

이건 이미 `PlayOrderVoice`(선택/이동/공격명령 대사, doc/0262~0264)가 쓰는 패턴과 정확히 같음 - 전용 단일 `AudioSource`(`orderVoiceSource`) 하나만 두고, 재생 중이면 새 요청을 버리는 방식. `orderSFX`도 같은 패턴(전용 단일 채널)을 새로 만들어주는 게 가장 깔끔함 - `selectSFX`/`uiClickSFX`는 그대로 `PlaySFX2D`(doc/0284의 동시 4개 스팸방지)를 계속 쓰고, `orderSFX`만 분리.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**추가할 필드/초기화**
```csharp
private AudioSource orderVoiceSource;
private UnitSoundBankSO currentOrderVoiceUnitType;

// 명령 확인음(orderSFX) 전용 단일 채널 - orderVoiceSource와 동일한 패턴. 재생 중이면 다음 명령의
// orderSFX 요청은 무시하고, 재생이 끝난 뒤에 들어오는 명령부터 다시 재생한다.
private AudioSource orderSFXSource;
```
```csharp
orderVoiceSource = new GameObject("OrderVoiceSource").AddComponent<AudioSource>();
orderVoiceSource.transform.SetParent(transform);
orderVoiceSource.playOnAwake = false;
orderVoiceSource.spatialBlend = 0f;

orderSFXSource = new GameObject("OrderSFXSource").AddComponent<AudioSource>();
orderSFXSource.transform.SetParent(transform);
orderSFXSource.playOnAwake = false;
orderSFXSource.spatialBlend = 0f;
```

**새 재생 메서드 (PlayOrderVoice 근처에 추가)**
```csharp
// 명령 확인음(orderSFX) - 재생 중이면 새 요청을 버리고, 끝난 뒤에 들어오는 명령부터 다시 재생한다.
public void PlayOrderSFX(SoundClipSet set)
{
    if (set == null || !set.HasClips || orderSFXSource.isPlaying)
        return;

    AudioClip clip = set.GetRandomClip();
    if (clip == null)
        return;

    orderSFXSource.clip = clip;
    orderSFXSource.pitch = set.GetRandomPitch();
    orderSFXSource.volume = EffectiveVolume(sfxVolume, sfxMuted) * set.volumeScale;
    orderSFXSource.Play();
}
```

### Assets/Scripts/Audio/UnitAudio.cs

**기존 코드**
```csharp
public void PlayOrderSFX()
{
    if (bank != null)
        SoundManager.Instance?.PlaySFX2D(bank.orderSFX);
}
```

**변경 코드**
```csharp
public void PlayOrderSFX()
{
    if (bank != null)
        SoundManager.Instance?.PlayOrderSFX(bank.orderSFX);
}
```

(`PlaySelectSFX`/`PlayUIClick`은 그대로 `PlaySFX2D` 유지 - 이번 요청 범위 밖.)

## 확인 결과
사용자가 "orderSFX + selectSFX 둘 다" 선택. 선택과 명령은 서로 다른 이벤트라 한쪽이 재생 중이어도 다른 쪽을 막지 않도록 채널을 각각 따로 둠(`orderSFXSource`, `selectSFXSource`).

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs
- `orderSFXSource`/`selectSFXSource` 전용 단일 `AudioSource` 2개를 `Awake()`에서 생성(`orderVoiceSource`와 동일 패턴, `spatialBlend=0`).
- `PlaySelectSFX(SoundClipSet)`/`PlayOrderSFX(SoundClipSet)` 신규 메서드 추가 - 공통 로직은 `PlaySingleChannel(AudioSource, SoundClipSet)`으로 뽑음: 재생 중이면 새 요청 무시, 아니면 랜덤 클립 재생.
- 기존 `selectSFX`/`orderSFX`가 쓰던 `PlaySFX2D` 경로(doc/0284의 동시 4개 허용 스팸 방지)에서 완전히 분리됨. `uiClickSFX`(`PlayUIClick`)는 그대로 `PlaySFX2D` 유지.

### Assets/Scripts/Audio/UnitAudio.cs
- `PlaySelectSFX()`: `PlaySFX2D(bank.selectSFX)` → `PlaySelectSFX(bank.selectSFX)`
- `PlayOrderSFX()`: `PlaySFX2D(bank.orderSFX)` → `PlayOrderSFX(bank.orderSFX)`
- 관련 주석을 새 동작(전용 단일 채널, 재생 중이면 무시)에 맞게 갱신.

## 요약/남은 작업
적용 완료. 연속으로 빠르게 명령을 내리거나 유닛을 선택해서, 이전 확인음이 끝나기 전엔 다음 확인음이 재생되지 않고 끝난 뒤부터 재생되는지 실제 플레이로 확인 필요.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`
