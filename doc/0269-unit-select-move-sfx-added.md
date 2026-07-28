# 0269 - 유닛 선택/이동 SFX(효과음) 카테고리 추가

**날짜:** 2026-07-28

## 요청 내용

> 이동, 선택 SFX도 추가해줘

지금까지는 선택/이동에 대사(Voice)만 있었는데, 대사와 별개로 같이 나는 효과음(예: 삑 소리 같은 UI성
효과음)도 추가해달라는 요청.

## 코드 변경

### 1. `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`

SFX 섹션에 `selectSFX`/`moveSFX` 필드 추가.

```csharp
[field: SerializeField]
public SoundClipSet selectSFX { get; private set; } // 선택 시 대사와 별개로 같이 나는 효과음(삑 소리 등)
[field: SerializeField]
public SoundClipSet moveSFX { get; private set; } // 이동명령 시 대사와 별개로 같이 나는 효과음
```

### 2. `Assets/Scripts/Audio/UnitAudio.cs`

`PlaySelectSFX()`/`PlayMoveSFX()` 추가. 대사(`PlaySelectVoice`/`PlayMoveVoice`)는 채널 1개짜리
`PlayOrderVoice`(끼어들기/코얼레싱 규칙 적용, doc/0262~0264)를 쓰지만, SFX는 일반 SFX 풀
(`SoundManager.PlaySFX`)로 재생해서 규칙과 무관하게 매번 독립적으로 재생된다(여러 개 겹쳐도 됨).

```csharp
public void PlaySelectSFX()
{
    if (bank != null)
        SoundManager.Instance?.PlaySFX(bank.selectSFX, transform.position);
}

public void PlayMoveSFX()
{
    if (bank != null)
        SoundManager.Instance?.PlaySFX(bank.moveSFX, transform.position);
}
```

### 3. 호출부 - 기존 `PlaySelectVoice()`/`PlayMoveVoice()` 호출 지점 전부에 나란히 추가

- `RTSUnitController.ClickSelectUnit` / `ShiftClickSelectUnit` / `DragSelectUnit`(드래그 첫 유닛):
  `PlaySelectVoice()` 옆에 `PlaySelectSFX()` 추가.
- `RTSUnitController.MoveSelectedUnits`(`PlayRepresentativeUnitVoice` 콜백):
  `PlayMoveVoice()` 옆에 `PlayMoveSFX()` 추가.
- `PlacementSystem.PlaceStructure()`(일꾼이 건설 위치로 이동 시작, doc/0265):
  `PlayMoveVoice()` 옆에 `PlayMoveSFX()` 추가.

건물(`BuildingSoundBankSO`)에는 이번에 추가하지 않았다 - 건물은 "이동" 개념이 없고, 선택 음성만
있는 상태를 그대로 유지.

## 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`: `selectSFX`/`moveSFX` 필드 추가.
- `Assets/Scripts/Audio/UnitAudio.cs`: `PlaySelectSFX()`/`PlayMoveSFX()` 추가.
- `Assets/Scripts/System/RTSUnitController.cs`: 선택 3곳 + 이동 1곳에 SFX 호출 추가.
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`: 건설 이동 시작 시 SFX 호출 추가.
- 새 `UnitSoundBankSO` 에셋을 만들 때 `selectSFX`/`moveSFX`도 채워야 소리가 나며, 비워두면 기존과
  동일하게 조용하다(다른 SFX 필드들과 동일한 안전한 null 처리).
