# 0264 - 명령 음성 끼어들기 기준을 유닛 "개체"가 아닌 "종류"로 변경

**날짜:** 2026-07-28

## 요청 내용

> 만약 드래그 선택이랑 뭐 단일선택이랑 섞어서 할때 유닛의 종류가 같은 종류의 유닛이면 선택시 음성이
> 끊기지 않도록 해줘

doc/0263까지는 "다른 유닛을 선택했을 때"를 `UnitAudio` 컴포넌트 인스턴스(개체) 기준으로 비교했다.
그래서 예를 들어 샤프슈터 A를 드래그로 선택한 뒤 이어서 샤프슈터 B를 단일 클릭하면, 둘 다 "샤프슈터"
라는 같은 종류인데도 서로 다른 개체라서 매번 대사가 끊기고 다시 재생되는 문제가 있었다. 이번 요청은
개체가 아니라 **유닛 종류**가 같으면 안 끊기게 해달라는 것.

## 코드 변경

"유닛 종류"를 구분하는 별도 ID/enum을 새로 만들지 않고, **이미 유닛 종류 1개당 1개씩 존재하는
`UnitSoundBankSO` 에셋 참조 자체를 종류 식별자로 재사용**했다 - 같은 종류의 유닛은 항상
`UnitData.soundBank`를 통해 동일한 에셋을 공유하므로, 이 참조를 비교하는 것만으로 "같은 종류인지"를
정확히 판별할 수 있다.

### `Assets/Scripts/Audio/SoundManager.cs`

Before:
```csharp
private UnitAudio currentOrderVoiceOwner;
...
public void PlayOrderVoice(SoundClipSet set, UnitAudio owner, string category)
{
    ...
    bool isNewUnitSelection = category == "select" && owner != currentOrderVoiceOwner;
    ...
    currentOrderVoiceOwner = owner;
    ...
}
```

After:
```csharp
private UnitSoundBankSO currentOrderVoiceUnitType;
...
public void PlayOrderVoice(SoundClipSet set, UnitSoundBankSO unitType, string category)
{
    ...
    bool isNewUnitTypeSelection = category == "select" && unitType != currentOrderVoiceUnitType;
    ...
    currentOrderVoiceUnitType = unitType;
    ...
}
```

### `Assets/Scripts/Audio/UnitAudio.cs`

`PlaySelectVoice`/`PlayMoveVoice`/`PlayAttackOrderVoice`가 `PlayOrderVoice`에 넘기던 두 번째 인자를
`this`(유닛 개체)에서 `bank`(그 유닛 종류의 SoundBank 에셋)로 변경.

## 동작 정리

| 상황 | 결과 |
|---|---|
| 샤프슈터 A 선택 중 대사 재생 도중 샤프슈터 B를 선택(같은 종류) | 끊지 않고 A의 대사를 계속 들려줌 |
| 샤프슈터 선택 중 대사 재생 도중 어설트 트루퍼 선택(다른 종류) | 즉시 끊고 어설트 트루퍼 대사 재생 |
| 드래그로 샤프슈터 여러 마리 선택 후, 이어서 단일 클릭으로 또 다른 샤프슈터 선택 | 같은 종류라 끊기지 않음 |

## 변경된 파일

`Assets/Scripts/Audio/SoundManager.cs`(`currentOrderVoiceUnitType` 필드로 교체, `PlayOrderVoice`
시그니처를 `UnitSoundBankSO` 기준으로 변경), `Assets/Scripts/Audio/UnitAudio.cs`(세 호출부가 `bank`를
식별자로 전달하도록 변경). `RTSUnitController.cs`는 `UnitAudio`의 공개 메서드만 호출하므로 추가 수정
없음.
