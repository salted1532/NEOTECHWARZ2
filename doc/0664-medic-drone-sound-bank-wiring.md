# 0664 - 메딕 드론 사운드 뱅크 보이스 클립 연결 (적용 완료)

## 요청
> 메딕 드론 사운드 클립들 다 넣어뒀는데 해당하는곳에 연결해줘 공격음성은 2개인데 Order 사운드클립
> 4개도 같이 포함시켜

## 조사 내용
- 사용자가 `Assets/Sound/NTA/Unit/Medic Drone/Voice/`에 mp3를 미리 추가해둔 상태 (Select1-4,
  Order1-4, Attack1-2, Spawn2 - Death 클립은 아직 없음).
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Medic Drone Unit Sound Bank SO.asset`(신규,
  `UnitSoundBankSO` 타입)이 이미 존재하지만 모든 클립 필드가 빈 배열이었음.
- `NTA Unit Data SO.asset`(ID 10, Medic Drone)의 `soundBank` 필드는 이미 이 에셋(guid
  `19d8e42520f3fd114feaab2edb7d2d90`)을 참조하고 있어서, SO 에셋 필드만 채우면 별도 연결 작업 없이
  바로 재생됨.

## 변경 내용
`Medic Drone Unit Sound Bank SO.asset`의 Voice 카테고리에 guid로 클립 연결:

| 필드 | 채운 클립 |
|---|---|
| `selectVoice` | Select1, Select2, Select3, Select4 (4개) |
| `orderVoice` | Order1, Order2, Order3, Order4 (4개) |
| `attackOrderVoice` | Attack1, Attack2 + Order1~4 (요청대로 공격 전용 2개 + Order 4개 합쳐서 총 6개) |
| `spawnVoice` | Spawn2 (1개 - Spawn1은 없어서 있는 것만) |
| `deathVoice` | 미변경 (클립 없음) |

`attackOrderVoice`에 Order 클립을 같이 넣은 것은 doc/0655(WorkerDrone)에서 전용 공격 대사가 없을 때
`orderVoice` 클립을 재사용한 것과 같은 방식 - 이번엔 전용 Attack 클립 2개가 있으니 그걸 우선 두고
Order 4개를 뒤에 추가해 총 6개 중 랜덤 재생되도록 함(메딕 드론은 무공격 유닛이라 "전투 요원 아님"류
Attack 대사 2개만으로는 다양성이 부족하다고 판단, Order 대사도 섞이게).

## 남은 것
- Death 보이스 클립은 아직 준비 안 됨 - 추가되면 같은 방식으로 `deathVoice`에 연결하면 됨.
- SFX 계열(`attackSFX`/`spawnSFX`/`deathSFX`/`healTickSFX`/`selectSFX`/`orderSFX`)은 이번 요청
  범위 밖(보이스만 요청) - 전부 빈 상태로 남겨둠.

## 변경 파일
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Medic Drone Unit Sound Bank SO.asset`
