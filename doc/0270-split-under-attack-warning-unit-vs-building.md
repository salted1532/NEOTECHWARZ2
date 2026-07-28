# 0270 - 화면 밖 피격 경고음을 유닛/건물로 분리

**날짜:** 2026-07-28

## 요청 내용

> 글로볼 보이드 뱅크에서 화면에 안보이는 공격을 당했을때 경고음을 유닛, 건물을 따로 뒀으면 좋겠어

`GlobalVoiceBankSO.underAttackWarning` 하나로 유닛/건물 피격 경고를 같이 쓰고 있었는데, 유닛이
공격받을 때와 건물이 공격받을 때 경고음을 다르게 재생할 수 있도록 분리해달라는 요청.

## 코드 변경

### `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`

Before:
```csharp
[field: SerializeField]
public SoundClipSet underAttackWarning { get; private set; }
```

After:
```csharp
[field: SerializeField]
public SoundClipSet unitUnderAttackWarning { get; private set; } // 화면 밖에서 아군 유닛이 공격받았을 때
[field: SerializeField]
public SoundClipSet buildingUnderAttackWarning { get; private set; } // 화면 밖에서 아군 건물이 공격받았을 때
```

### `Assets/Scripts/Audio/SoundManager.cs`

`PlayUnderAttackWarning()` 하나를 `PlayUnitUnderAttackWarning()` / `PlayBuildingUnderAttackWarning()`
둘로 분리 (각각 해당 `SoundClipSet`으로 기존 `PlayGlobalVoice` 쿨다운 로직을 그대로 재사용 - 쿨다운은
`SoundClipSet` 참조별로 독립 추적되므로 유닛 경고와 건물 경고가 서로의 쿨다운에 영향을 주지 않는다).

### 호출부

- `Assets/Scripts/Audio/UnitAudio.cs`의 `HandleDamaged` → `PlayUnitUnderAttackWarning()`
- `Assets/Scripts/Audio/BuildingAudio.cs`의 `HandleDamaged` → `PlayBuildingUnderAttackWarning()`

### `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`

기존 `underAttackWarning` 항목(비어있는 상태였음, 데이터 손실 없음)을 `unitUnderAttackWarning` +
`buildingUnderAttackWarning` 두 항목으로 교체.

## 요약/영향받는 파일

- `GlobalVoiceBankSO.cs`, `SoundManager.cs`, `UnitAudio.cs`, `BuildingAudio.cs`,
  `Global Voice Bank SO.asset` 전부 수정.
- 이제 `Global Voice Bank SO.asset` 인스펙터에서 유닛 피격 경고음/건물 피격 경고음 클립을 각각 따로
  채울 수 있다.
