## 날짜
2026-07-29

## 요청 내용
"move sfx를 order sfx로 변경하고 공격명령, 이동명령, 순찰 등등 명령을 내릴때 작동하는식으로 바꿔줘." 범위 확인 결과 이동/공격/순찰뿐 아니라 정지/홀드/따라가기/채취/자원반환/건물이동까지 **선택된 유닛에게 내리는 모든 명령 계열**에 적용하기로 함.

## 조사 내용
`RTSUnitController.cs`에서 "선택된 유닛에게 내리는 명령"에 해당하는 메서드를 전부 확인:
- 이동: `MoveSelectedUnits`
- 공격: `AttackSelectedUnits`, `AttackGroundSelectedUnits`, `AttackFriendlySelectedUnits`, `AttackFriendlyBuildingSelectedUnits`, `AttackEnemyBuildingSelectedUnits`, `AttackFriendlyStructureSelectedUnits`
- 기타: `FollowSelectedUnits`, `StopSelectedUnits`, `HoldSelectedUnits`, `EnterReturnMode`, `PatrolSelectedUnits`, `GatherSelectedUnits`, `MoveToBuildingSelectedUnits`

이 중 공격 계열은 `PlayAttackOrderVoice()`만, 이동은 `PlayMoveVoice()`+`PlayMoveSFX()`를 호출했고, 나머지(Follow/Stop/Hold/Return/Patrol/Gather/MoveToBuilding)는 대사/효과음 호출이 아예 없었음.

`UnitSoundBankSO.moveSFX` 필드를 `orderSFX`로 리네임하면서, 이미 SkyLancer/Firehawk SoundBank 에셋에 채워둔 기존 클립 데이터가 유실되지 않도록 `[field: FormerlySerializedAs("<moveSFX>k__BackingField")]`를 붙여 승계.

## 코드 변경 (적용 완료)

### Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs

**기존 코드**
```csharp
using UnityEngine;
...
    [field: SerializeField]
    public SoundClipSet moveSFX { get; private set; } // 이동명령 시 대사와 별개로 같이 나는 효과음
```

**변경 코드**
```csharp
using UnityEngine;
using UnityEngine.Serialization;
...
    // 이동/공격/순찰 등 유닛에게 내리는 모든 명령 시 대사와 별개로 같이 나는 확인음 (구 moveSFX,
    // doc/0279 - 이동 전용에서 명령 전반으로 범위 확대). FormerlySerializedAs로 기존에 채워둔
    // moveSFX 클립 데이터를 그대로 승계한다.
    [field: SerializeField, FormerlySerializedAs("<moveSFX>k__BackingField")]
    public SoundClipSet orderSFX { get; private set; }
```

### Assets/Scripts/Audio/UnitAudio.cs

**기존 코드**
```csharp
    public void PlayMoveSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX2D(bank.moveSFX);
    }
```

**변경 코드**
```csharp
    // 이동/공격/순찰/정지/홀드/따라가기/채취/자원반환/건물이동 등 선택된 유닛에게 명령을 내리는
    // 모든 진입점에서 대표 유닛 1마리에 대해 호출된다 (doc/0279 - 이동 전용이던 moveSFX를 명령
    // 전반으로 확대). RTSUnitController의 각 명령 메서드가 대사(Voice) 재생과 나란히 호출한다.
    public void PlayOrderSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX2D(bank.orderSFX);
    }
```

### Assets/Scripts/System/RTSUnitController.cs
`MoveSelectedUnits`는 `PlayMoveSFX()` 호출을 `PlayOrderSFX()`로 교체. 공격 계열 6개 메서드는 기존 `PlayAttackOrderVoice()` 옆에 `PlayOrderSFX()`를 추가. 나머지(`FollowSelectedUnits`/`StopSelectedUnits`/`HoldSelectedUnits`/`EnterReturnMode`/`PatrolSelectedUnits`/`GatherSelectedUnits`/`MoveToBuildingSelectedUnits`)는 대사가 원래 없었으므로 `PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());` 한 줄씩만 새로 추가.

예 (공격 계열, 6곳 동일 패턴):
```csharp
// 기존
PlayRepresentativeUnitVoice(audio => audio.PlayAttackOrderVoice());

// 변경
PlayRepresentativeUnitVoice(audio =>
{
    audio.PlayAttackOrderVoice();
    audio.PlayOrderSFX();
});
```

예 (원래 대사 호출이 없던 명령, 7곳 동일 패턴 - `StopSelectedUnits` 예시):
```csharp
// 기존
public void StopSelectedUnits()
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].StopUnit();
    }
}

// 변경
public void StopSelectedUnits()
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].StopUnit();
    }

    PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
}
```

### Assets/Scripts/BuildSystem/PlacementSystem.cs
워커가 건설 위치로 이동을 시작할 때도 `PlayMoveSFX()` → `PlayOrderSFX()`로 함께 교체.

## 요약/남은 작업
적용 완료. 기존 에셋(`SkyLancer Unit Sound Bank SO.asset`, `Firehawk Unit Sound Bank SO.asset`)의 클립 데이터는 `FormerlySerializedAs`로 유지되지만, 유니티 에디터를 열어서 인스펙터에 `Order SFX` 필드로 정상 표시되는지 한 번 확인 권장(에디터가 열리면서 필드명이 갱신돼 저장되기 전까지는 .asset 파일의 YAML 키가 `<moveSFX>...`로 남아있을 수 있음 - 정상 동작에는 지장 없음).

## 변경된 파일
- `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
