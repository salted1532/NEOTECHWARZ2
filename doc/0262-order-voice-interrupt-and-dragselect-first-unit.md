# 0262 - 드래그 선택 첫 유닛 대사 + 명령 음성 끼어들기(interrupt) 처리

**날짜:** 2026-07-28

## 요청 내용

> 일단 확인된 변경사항은 드래그 선택시 리스트에서 제일 처음에 있는 유닛의 선택 음성이 나오도록
> 해주고 그리고 선택이나 명령, 공격들을 할때 다음 명령을 내리거나 하면 기존에 나오던 음성을 바로
> 정지하고 나오도록해줘 만약 같은 명령을 같은 유닛에게 내리는거면 그냥 음성을 끝까지 재생시켜줘
> 재생시키고 또 명령이 들어오면 그때 또 랜덤하게 재생하도록

두 가지 요청:
1. 드래그(박스) 선택 시 지금은 선택 음성이 아예 안 나오는데, 리스트에서 제일 처음 선택된 유닛의
   선택 음성이 나오도록.
2. 선택/이동/공격명령 음성이 재생되는 도중 다른 명령이 들어오면 기존 음성을 바로 끊고 새 음성을
   재생. 단, 같은 유닛에게 같은 종류의 명령이 연달아 들어온 경우엔 끊지 않고 끝까지 재생 - 그 다음에
   또 명령이 들어오면 그때 새로 랜덤 재생.

## 조사 내용

- `Assets/Scripts/UserControl/UserControl.cs`의 `SelectObject()`(787번 줄)를 확인해보니, 드래그 선택은
  매 프레임이 아니라 **마우스를 놓는 시점에 한 번만** 박스 안의 유닛 전체를 계산해 `DragSelectUnit`을
  순서대로 호출하는 구조였다(생각보다 단순함 - 매 프레임 스팸 우려는 기우였음). 더블클릭
  "같은 종류 전체 선택"(`SelectAllVisibleUnitsOfSameType`, 826번 줄)도 동일한 `DragSelectUnit` 루프
  패턴을 재사용한다.
- doc/0255에서 만든 `SoundManager.PlayVoice`는 매번 풀에서 "재생 중이 아닌 소스(없으면 가장 오래된
  소스)"를 가져다 쓰는 방식이라, "지금 재생 중인 게 어떤 명령의 목소리인지"를 추적할 수 없어서 이번
  끼어들기(interrupt) 요구사항을 구현할 수 없었다. 선택/이동/공격명령 전용으로 채널을 분리하고 상태를
  추적하는 새 경로가 필요했다.

## 코드 변경

### 1. `Assets/Scripts/Audio/SoundManager.cs` - 명령 음성 전용 채널 추가

일반 `voicePool`과 별개로 `orderVoiceSource` 하나만 두고, 마지막으로 재생한 (유닛, 명령종류)를
기억해서 같은 조합이면 끊지 않고, 다르면 즉시 끊고 새로 재생하는 `PlayOrderVoice` 추가.

```csharp
private AudioSource orderVoiceSource;
private UnitAudio currentOrderVoiceOwner;
private string currentOrderVoiceCategory;

// Awake()에서 풀 생성 이후 추가:
orderVoiceSource = new GameObject("OrderVoiceSource").AddComponent<AudioSource>();
orderVoiceSource.transform.SetParent(transform);
orderVoiceSource.playOnAwake = false;
orderVoiceSource.spatialBlend = 0f;

public void PlayOrderVoice(SoundClipSet set, UnitAudio owner, string category)
{
    if (set == null || !set.HasClips)
        return;

    bool sameCommandStillPlaying = currentOrderVoiceOwner == owner
        && currentOrderVoiceCategory == category
        && orderVoiceSource.isPlaying;

    if (sameCommandStillPlaying)
        return; // 같은 유닛 + 같은 명령이 다시 들어옴 - 끊지 않고 끝까지 재생

    AudioClip clip = set.GetRandomClip();
    if (clip == null)
        return;

    currentOrderVoiceOwner = owner;
    currentOrderVoiceCategory = category;

    orderVoiceSource.Stop(); // 다른 명령의 대사가 재생 중이었다면 즉시 끊는다
    orderVoiceSource.clip = clip;
    orderVoiceSource.pitch = set.GetRandomPitch();
    orderVoiceSource.volume = EffectiveVolume(voiceVolume, voiceMuted) * set.volumeScale;
    orderVoiceSource.Play();
}
```

### 2. `Assets/Scripts/Audio/UnitAudio.cs` - 선택/이동/공격명령 음성을 새 채널로 전환

`PlaySelectVoice`/`PlayMoveVoice`/`PlayAttackOrderVoice`가 `PlayVoice` 대신 `PlayOrderVoice(set, this, category)`를
호출하도록 변경(`this` = 이 유닛의 `UnitAudio` 인스턴스가 "누구의 명령인지"를 식별하는 키 역할).

### 3. `Assets/Scripts/System/RTSUnitController.cs` - 드래그 선택 첫 유닛만 재생

```csharp
public void DragSelectUnit(UnitController newUnit)
{
    if (!selectedUnitList.Contains(newUnit))
    {
        SelectUnit(newUnit);

        if (selectedUnitList.Count == 1)
            newUnit.GetComponent<UnitAudio>()?.PlaySelectVoice();
    }
}
```
`selectedUnitList`가 비어있다가 이번 호출로 1개가 된 순간(=이번 드래그에서 새로 선택된 첫 유닛)에만
재생한다. Shift+드래그로 기존 선택에 추가하는 경우(리스트가 이미 비어있지 않음)는 재생하지 않는다 -
"리스트의 제일 처음" 의미상 자연스러운 동작.

## 요약/영향받는 파일

- `Assets/Scripts/Audio/SoundManager.cs`: `orderVoiceSource` 전용 채널 + `PlayOrderVoice` 추가.
- `Assets/Scripts/Audio/UnitAudio.cs`: 선택/이동/공격명령 음성 재생을 `PlayOrderVoice`로 전환.
- `Assets/Scripts/System/RTSUnitController.cs`: `DragSelectUnit`이 드래그 배치의 첫 유닛만 선택
  음성을 재생하도록 변경.
- 동작 변화:
  - 드래그로 여러 마리를 한꺼번에 선택해도 첫 번째로 선택된 유닛의 대사 1개만 재생됨.
  - 선택/이동/공격 명령을 연달아 다르게 내리면(다른 유닛 대표로 바뀌거나 명령 종류가 바뀌면) 재생
    중이던 이전 대사가 즉시 끊기고 새 대사가 바로 재생됨.
  - 같은 유닛에게 같은 종류의 명령을 연달아 내리면(예: 같은 대표 유닛으로 이동 명령을 연속으로 내림)
    끊기지 않고 재생 중이던 대사를 끝까지 들려주고, 그 이후 명령부터 다시 새로 랜덤 재생됨.
