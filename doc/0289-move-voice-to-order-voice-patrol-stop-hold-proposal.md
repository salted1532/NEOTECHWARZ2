## 날짜
2026-07-29

## 요청 내용
세 가지 요청:
1. `UnitSoundBankSO`의 `moveVoice`를 `orderVoice`로 이름 변경(구 `moveSFX`→`orderSFX`, doc/0279와 동일한 성격의 리네이밍).
2. 순찰(Patrol) 명령을 내렸을 때도 이 대사(음성)가 재생되도록.
3. `orderSFX`(명령 확인음, doc/0285에서 전용 단일 채널로 분리됨)는 정지(Stop)/홀드(Hold) 명령일 때는 재생되지 않도록.

## 조사 내용
`RTSUnitController.cs`에서 명령별 사운드 호출 현황:

| 명령 | 현재 Voice | 현재 SFX(orderSFX) |
|---|---|---|
| `MoveSelectedUnits`(이동) | `PlayMoveVoice()` | 재생 |
| `AttackSelectedUnits`류(공격, 6개 변형) | `PlayAttackOrderVoice()` | 재생 |
| `FollowSelectedUnits`(따라가기) | 없음 | 재생 |
| `StopSelectedUnits`(정지) | 없음 | 재생 → **제거 대상** |
| `HoldSelectedUnits`(홀드) | 없음 | 재생 → **제거 대상** |
| `EnterReturnMode`(자원반환) | 없음 | 재생 |
| `PatrolSelectedUnits`(순찰) | 없음 → **추가 대상** | 재생 |
| `GatherSelectedUnits`(채취) | 없음 | 재생 |
| `MoveToBuildingSelectedUnits`(건물 우클릭) | 없음 | 재생 |

`PlacementSystem.cs:186`에서도 일꾼이 건설 위치로 이동 시작할 때 `PlayMoveVoice()`를 호출함(건설 이동도 "이동"의 일종이므로 이 호출은 그대로 유지, 이름만 `PlayOrderVoice()`로 변경).

`UnitSoundBankSO.moveVoice`는 자동 프로퍼티(`[field: SerializeField]`)라 이름을 그냥 바꾸면 이미 9개 유닛 SoundBank 에셋에 채워둔 클립 데이터가 전부 날아간다 - doc/0279에서 `moveSFX`→`orderSFX`를 옮길 때 썼던 것과 동일하게 `FormerlySerializedAs`를 붙이면 기존 데이터가 자동으로 새 필드명으로 이어받아진다(에셋 파일을 직접 안 건드려도 됨, Unity가 다음 로드 시 자동 매핑).

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs
```csharp
[field: SerializeField]
public SoundClipSet moveVoice { get; private set; } // 3~4개 권장
```
→
```csharp
// 이동/순찰 명령 시 대사 (구 moveVoice, doc/0289 - 순찰 명령까지 범위 확대). FormerlySerializedAs로
// 기존에 채워둔 moveVoice 클립 데이터를 그대로 승계한다.
[field: SerializeField, FormerlySerializedAs("<moveVoice>k__BackingField")]
public SoundClipSet orderVoice { get; private set; } // 3~4개 권장
```

### Assets/Scripts/Audio/UnitAudio.cs
```csharp
public void PlayMoveVoice()
{
    if (bank != null)
        SoundManager.Instance?.PlayOrderVoice(bank.moveVoice, bank, "move");
}
```
→
```csharp
public void PlayOrderVoice()
{
    if (bank != null)
        SoundManager.Instance?.PlayOrderVoice(bank.orderVoice, bank, "order");
}
```

### Assets/Scripts/System/RTSUnitController.cs
- `MoveSelectedUnits`: `audio.PlayMoveVoice();` → `audio.PlayOrderVoice();`
- `PatrolSelectedUnits`:
```csharp
PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
```
→
```csharp
PlayRepresentativeUnitVoice(audio =>
{
    audio.PlayOrderVoice();
    audio.PlayOrderSFX();
});
```
- `StopSelectedUnits`: `PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());` 줄 삭제 (정지 명령은 소리 없음)
- `HoldSelectedUnits`: 동일하게 `PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());` 줄 삭제

### Assets/Scripts/BuildSystem/PlacementSystem.cs
- `worker.GetComponent<UnitAudio>()?.PlayMoveVoice();` → `worker.GetComponent<UnitAudio>()?.PlayOrderVoice();`

## 확인 결과
선택지가 사실상 하나뿐이라(순찰에 이동과 동일한 대사 풀 재사용) 별도 확인 없이 위 제안 그대로 진행.

## 코드 변경 (적용 완료)
위 "제안" 섹션 코드 그대로 적용:
- `UnitSoundBankSO.moveVoice` → `orderVoice`(`FormerlySerializedAs`로 기존 클립 데이터 승계)
- `UnitAudio.PlayMoveVoice()` → `PlayOrderVoice()`
- `RTSUnitController.MoveSelectedUnits`: `PlayMoveVoice()` → `PlayOrderVoice()`
- `RTSUnitController.PatrolSelectedUnits`: `PlayOrderVoice()` + `PlayOrderSFX()` 둘 다 재생하도록 추가
- `RTSUnitController.StopSelectedUnits`/`HoldSelectedUnits`: `PlayOrderSFX()` 호출 줄 삭제(정지/홀드는 무음)
- `PlacementSystem.cs`: 건설 이동 시작 시 `PlayMoveVoice()` → `PlayOrderVoice()`
- `SoundManager.PlayOrderVoice` 주석에 "순찰" 언급 추가

## 요약/남은 작업
적용 완료. Unity를 열어 각 유닛 SoundBank 에셋의 `moveVoice` 필드가 `Order Voice`로 자동 리네임되고 기존 클립이 그대로 남아있는지(FormerlySerializedAs 매핑) 확인 필요. 실제 플레이로 이동/순찰 시 대사가 나오고, 정지/홀드 시 확인음이 안 나는지 확인 필요.

## 변경된 파일
- `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Audio/SoundManager.cs` (주석)
