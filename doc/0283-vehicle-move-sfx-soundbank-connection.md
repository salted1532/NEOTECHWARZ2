## 날짜
2026-07-29

## 요청 내용
"차량 이동 클립들 폴더"를 유닛별 SFX 폴더에 넣어뒀으니, 해당 파일이 있는 유닛에 한해 사운드뱅크의 이동 SFX(`orderSFX`, 구 `moveSFX` - doc/0279)에 연결해달라는 요청. 폴더에 이동 클립이 없는 유닛은 일부러 뺀 것이므로 건드리지 않아도 됨.

## 조사 내용
`Assets/Sound/NTA/Unit/*/SFX/` 폴더를 확인한 결과, 이동 클립이 있는 유닛과 없는 유닛이 명확히 갈림.

**이동 클립 있음**
- Pulasr Tank: `vehicle_move1.mp3`
- Ranger IFV: `vehicle_move1.mp3`
- Scout Drone: `vehicle_move1.mp3`
- SkyLancer: `vehicle_move1.mp3`
- Firehawk: `airplane_move.mp3`

**이동 클립 없음 (건드리지 않음)**
- Assault Trooper, Guardian Drone, Sharpshooter, WorkerDrone

각 SoundBank(`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/*.asset`)를 대조해보니:
- **SkyLancer, Firehawk**: `orderSFX`가 이미 각각 자기 폴더의 `vehicle_move1.mp3`/`airplane_move.mp3`와 정확히 같은 guid로 연결되어 있음(`volumeScale: 0.1`) - 이미 완료된 상태, 손댈 것 없음.
- **Pulasr Tank, Ranger IFV, Scout Drone**: `.asset` 파일 자체에 `orderSFX`(및 `selectSFX`) 필드가 아예 없음(구버전 상태로 저장된 뒤 갱신 안 됨). 게다가 이 3개 유닛의 `vehicle_move1.mp3`는 `.meta` 파일이 아직 없음 - Unity 에디터가 아직 한 번도 이 파일들을 임포트하지 않아 guid가 발급 안 된 상태(방금 폴더에 넣기만 한 파일로 보임).

## 코드/에셋 변경 (제안 - 아직 미적용)

Unity 에디터 없이 텍스트로 직접 작업해야 하므로, 아래처럼 처리할 예정:

1. **`.meta` 파일 새로 생성** (Pulasr Tank/Ranger IFV/Scout Drone의 `vehicle_move1.mp3` 각각) - 기존 SkyLancer `vehicle_move1.mp3.meta`와 동일한 `AudioImporter` 설정(3D: 1 등)을 그대로 쓰고, guid만 새로 발급해서 부여. (Unity가 다음에 이 프로젝트를 열 때 이 `.meta`를 그대로 인식하므로 나중에 자동 재생성되는 guid와 충돌할 일 없음.)
2. **각 SoundBank `.asset`에 `orderSFX` 필드 추가**, 새로 발급한 guid로 해당 유닛의 `vehicle_move1.mp3`를 연결. `volumeScale`은 기존에 이미 연결되어 있는 SkyLancer/Firehawk와 동일하게 **0.1**로 맞춤(엔진음이 확인음 대비 커서 낮춰둔 것으로 보여 일관성 유지), `pitchVariance: 0`.

예시 (Pulasr Tank, `gatherSFX` 다음 `selectVoice` 앞에 삽입):
```yaml
  <orderSFX>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: bdd29a03f52f4681bf73eb09fc5578da, type: 3}
    <volumeScale>k__BackingField: 0.1
    <pitchVariance>k__BackingField: 0
```
(Ranger IFV/Scout Drone도 각자 새로 발급한 guid로 동일하게)

## 확인 결과
사용자가 "meta 직접 생성 진행" + "volumeScale 0.1(기존과 동일)" 선택.

단, 실제로 meta 파일을 새로 만들려고 Write를 시도하자 Pulasr Tank/Ranger IFV/Scout Drone의 `vehicle_move1.mp3.meta`가 **이미 존재**하는 것으로 확인됨 - 처음 조사 때 썼던 Glob 패턴(`Assets\Sound\NTA\Unit\**\*.mp3.meta`)이 백슬래시 이스케이프 문제로 잘못 매칭되어 "없음"으로 잘못 판단했던 것. 실제 guid는 이미 Unity가 발급해둔 상태였음:
- Pulasr Tank: `0a036819633c0a1498c850190bd570a0`
- Ranger IFV: `01b33e1246c873e458921d0ce8b5ff7d`
- Scout Drone: `1c647670b297d5c45abf32ea9e76ede0`

새 meta를 만들지 않고 이 기존 guid를 그대로 SoundBank에 연결함.

## 코드/에셋 변경 (적용 완료)

### Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Pulasr Tank Unit Sound Bank SO.asset
`gatherSFX` 다음에 `orderSFX` 필드 추가:
```yaml
  <orderSFX>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: 0a036819633c0a1498c850190bd570a0, type: 3}
    <volumeScale>k__BackingField: 0.1
    <pitchVariance>k__BackingField: 0
```

### Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Ranger IFV Unit Sound Bank SO.asset
동일하게 `orderSFX` 추가 (guid: `01b33e1246c873e458921d0ce8b5ff7d`).

### Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Scout Drone Unit Sound Bank SO.asset
동일하게 `orderSFX` 추가 (guid: `1c647670b297d5c45abf32ea9e76ede0`).

SkyLancer/Firehawk는 이미 연결되어 있어 손대지 않음. 이동 클립이 없는 Assault Trooper/Guardian Drone/Sharpshooter/WorkerDrone도 요청대로 건드리지 않음.

## 요약/남은 작업
적용 완료. Unity 에디터를 열어 각 SoundBank 에셋의 `Order Sfx` 필드가 정상 표시되는지, 실제 재생 시 볼륨(0.1)이 적당한지 확인 필요.

## 변경된 파일
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Pulasr Tank Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Ranger IFV Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Scout Drone Unit Sound Bank SO.asset`
