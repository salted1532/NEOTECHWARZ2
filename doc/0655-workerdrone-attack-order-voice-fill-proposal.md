# 0655 - 워커 드론 등 공격 명령 음성 누락 유닛에 order 클립 채우기 (적용 완료)

## 날짜
2026-08-21

## 요청 내용
"워커 드론에 경우 공격 사운드가 없는건데 모든 유닛들 사운드 뱅크에 공격 명령이 비어있는 유닛이 있으면 order 사운드 클립들을 넣어주면돼"

## 조사 내용

`UnitSoundBankSO.cs`(`Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`)의 필드 중 "공격 명령"에 해당하는 것은 `attackOrderVoice`(공격 명령 시 나오는 대사, 1~2개 권장)이다. `attackSFX`(공격 이펙트음)와는 별개 필드이며, 워커 드론은 `attackSFX`는 이미 채워져 있고 `attackOrderVoice`만 비어 있다.

전체 21개 유닛 사운드 뱅크(`Assets/Scripts/ScriptableObject/Sound/**/Unit/*.asset`)의 `attackOrderVoice`를 전수 확인:

| 진영 | 유닛 | attackOrderVoice | orderVoice(대체 후보) |
|---|---|---|---|
| NTA | Assault Trooper / Firehawk / Guardian Drone / Pulasr Tank / Ranger IFV / Scout Drone / Sharpshooter / SkyLancer | 있음 (1개) | 있음 |
| NTA | **WorkerDrone** | **비어있음** | **5개 있음** |
| OC | Brute Mech / Cyborg Soldier / Heavy Assault Tank / Ironhawk / Nanobot Repair / Railgunner / Raven / Strike Drone / Striker | 비어있음 | 비어있음 (Voice 계열 전체 미녹음) |
| Spore_Brood | Ripfang / Skitterwing / Spitter | 비어있음 | 비어있음 (Voice 계열 전체 미녹음) |

- NTA 진영은 8개 유닛 모두 `attackOrderVoice`가 이미 채워져 있고, **WorkerDrone만 비어있다.**
- OC/Spore_Brood 진영 12개 유닛은 `attackOrderVoice`뿐 아니라 `selectVoice`/`orderVoice`/`spawnVoice`/`deathVoice` 등 Voice 계열 전체가 통째로 비어있다 (아직 보이스 녹음/배정이 안 된 진영으로 보임). 즉 "order 사운드 클립"으로 채우려 해도 원본 `orderVoice` 자체가 비어있어 채울 데이터가 없다 — 이번 요청 범위에서 실질적으로 조치 가능한 대상이 아니다.
- 따라서 실제로 채울 수 있는 대상은 **WorkerDrone 1개뿐**이다.

## 계획된 코드 변경

`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/WorkerDrone Unit Sound Bank SO.asset`의 `attackOrderVoice.clips`를 비워두는 대신, 같은 애셋의 `orderVoice.clips` 5개(guid 동일하게 재사용, 새 오디오 파일 아님)를 그대로 채운다.

### 기존 코드
```yaml
  <attackOrderVoice>k__BackingField:
    <clips>k__BackingField: []
    <volumeScale>k__BackingField: 2
    <pitchVariance>k__BackingField: 0
```

### 변경 코드
```yaml
  <attackOrderVoice>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: fa333e86b8fe3b54ea76b3ffaabcf08c, type: 3}
    - {fileID: 8300000, guid: 44023e7d1f2adf747bd21ccddfe51776, type: 3}
    - {fileID: 8300000, guid: b83b428951daa924b904b581cf8e3beb, type: 3}
    - {fileID: 8300000, guid: f535294ef16bf764ab267eaf1efc3d5b, type: 3}
    - {fileID: 8300000, guid: 8ea9153d87c4e1a4a99dd846285c9e0b, type: 3}
    <volumeScale>k__BackingField: 2
    <pitchVariance>k__BackingField: 0
```

(`orderVoice` 필드는 그대로 유지 — 이동 명령용으로 계속 재생됨. 공격 명령 시에는 같은 5개 클립 중 랜덤으로 `attackOrderVoice`가 재생됨.)

## 요약/영향받는 파일
- OC/Spore_Brood 12개 유닛은 원본 order 음성 자체가 없어 이번 조치에서 제외 (별도 보이스 녹음/배정 작업 필요 — 이번 요청 범위 밖).
- 사용자 확인 후 위 변경을 실제 적용함: `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/WorkerDrone Unit Sound Bank SO.asset` (`attackOrderVoice` 필드만).
