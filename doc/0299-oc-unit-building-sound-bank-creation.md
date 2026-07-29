# 0299 - OC 유닛/건물 사운드 뱅크 생성

날짜: 2026-07-30

## 요청 내용

"스크립터블오브젝트 폴더 안에 OC안에도 유닛,건물에 사운드 뱅크좀 생성해줄래"

## 조사 내용

- `Assets/Scripts/ScriptableObject/Sound/NTA/` 아래에는 유닛별 `UnitSoundBankSO` 에셋 9개(`Assault Trooper`, `Firehawk`, `Guardian Drone`, `Pulasr Tank`, `Ranger IFV`, `Scout Drone`, `Sharpshooter`, `SkyLancer`, `WorkerDrone`)와 건물 전체가 공유하는 `BuildingSoundBankSO` 에셋 1개(`NTA Building Sound Bank SO`)가 있다.
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit`, `Assets/Scripts/ScriptableObject/Sound/OC/Building` 폴더는 이미 존재하지만 내부가 비어 있음(사운드 뱅크 에셋 없음).
- `UnitData.soundBank`(`UnitDataSO.cs:113`)와 `BuildingData.soundBank`(`BuildingDataSO.cs:55`)는 `EnemyUnitDataSO`/`EnemyBuildingDataSO`가 재사용하는 공용 필드라서 OC도 동일한 `UnitSoundBankSO`/`BuildingSoundBankSO` 타입을 그대로 쓸 수 있다(새 스크립트 불필요).
- `OC Unit Data SO.asset`의 유닛 9종(Nanobot Repair, Cyborg Soldier, Striker, Railgunner, Brute Mech, Heavy Assault Tank, Ironhawk, Raven, Strike Drone) 항목에는 `soundBank` 필드 자체가 아직 안 채워져 있음(= null) — NTA 쪽은 유닛마다 전용 뱅크가 연결되어 있는 것과 대조적.
- `OC Building Data SO.asset`도 마찬가지로 `soundBank` 미연결. 참고로 NTA 쪽도 `NTA Building Sound Bank SO` 에셋은 존재하지만 `NTA Building Data SO.asset`의 건물 6종 어디에도 아직 연결되어 있지 않음(기존부터 그런 상태 - 이번 요청과 무관하므로 손대지 않음).
- 사운드 재생 코드(`UnitAudio.cs`, `BuildingAudio.cs`)는 전부 `UnitData.soundBank` / `BuildingData.soundBank`를 통해 뱅크를 조회하므로, 에셋만 만들고 SO에 연결을 안 하면 실제로는 아무 효과가 없다.

## 확인 결과

- 유닛: "에셋 생성 + 연결까지" 선택 → 아래대로 적용.
- 건물: "에셋만 생성, 연결 안 함(NTA와 동일)" 선택 → 아래대로 적용.

## 코드 변경 (적용 완료)

### 1. 새 에셋 생성

OC 유닛 9종 각각에 대응하는 빈 `UnitSoundBankSO` 에셋을 `Assets/Scripts/ScriptableObject/Sound/OC/Unit/`에 생성 (클립은 전부 비워둠 - NTA도 처음엔 빈 상태로 만들고 나중에 사운드 디자이너가 채워 넣는 방식):

- `Nanobot Repair Unit Sound Bank SO.asset`
- `Cyborg Soldier Unit Sound Bank SO.asset`
- `Striker Unit Sound Bank SO.asset`
- `Railgunner Unit Sound Bank SO.asset`
- `Brute Mech Unit Sound Bank SO.asset`
- `Heavy Assault Tank Unit Sound Bank SO.asset`
- `Ironhawk Unit Sound Bank SO.asset`
- `Raven Unit Sound Bank SO.asset`
- `Strike Drone Unit Sound Bank SO.asset`

OC 건물 전체가 공유할 `BuildingSoundBankSO` 에셋 1개를 `Assets/Scripts/ScriptableObject/Sound/OC/Building/`에 생성 (NTA와 동일하게 건물은 종류별이 아니라 진영 전체 공용 1개):

- `OC Building Sound Bank SO.asset`

각 `.asset`은 NTA와 동일한 빈 스켈레톤 형태(예: `NTA Building Sound Bank SO.asset`과 동일하게 모든 `SoundClipSet`의 `clips`는 빈 배열, `volumeScale: 1`, `pitchVariance: 0`).

### 2. `OC Unit Data SO.asset` - 유닛 9개 항목에 soundBank 연결

기존 코드 (예: Nanobot Repair 항목, `Prefab` 다음 줄):

```yaml
    <Prefab>k__BackingField: {fileID: 4698038001367890631, guid: 611672d581903da42a55e939a7f7ffcf, type: 3}
    <shortcutKey>k__BackingField: 0
```

변경 코드:

```yaml
    <Prefab>k__BackingField: {fileID: 4698038001367890631, guid: 611672d581903da42a55e939a7f7ffcf, type: 3}
    <soundBank>k__BackingField: {fileID: 11400000, guid: ce4b906746514dbca8a33f13c7d3d233, type: 2}
    <shortcutKey>k__BackingField: 0
```

나머지 8개 유닛도 각자의 `Prefab` 줄 다음에 자신의 신규 사운드 뱅크 guid로 동일하게 `soundBank` 라인 추가 (NTA 쪽 필드 순서 - Icon → Prefab → soundBank → shortcutKey - 를 그대로 따름).

### 3. `OC Building Data SO.asset`

NTA도 아직 건물에 `soundBank`를 연결하지 않은 상태와 동일하게, `OC Building Sound Bank SO` 에셋만 만들고 `OC Building Data SO.asset`은 건드리지 않았다.

## 요약

- 유닛: 빈 `UnitSoundBankSO` 에셋 9개 생성 + `OC Unit Data SO.asset`의 유닛 9종에 각각 연결(soundBank 필드 채움) — NTA와 동일한 완성 상태로 맞춤.
- 건물: 빈 `BuildingSoundBankSO` 에셋 1개(`OC Building Sound Bank SO`) 생성. NTA 선례를 따라 `OC Building Data SO.asset`에는 연결하지 않음.
- 클립은 전부 비어 있는 상태로 생성됨 - 실제 효과음/음성 파일 연결은 이후 에디터에서 직접 채워야 함.

## 변경된 파일

- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/*.asset` + `.meta` (신규 9쌍)
- `Assets/Scripts/ScriptableObject/Sound/OC/Building/OC Building Sound Bank SO.asset` + `.meta` (신규 1쌍)
- `Assets/Scripts/ScriptableObject/Data/OC Unit Data SO.asset` (수정 - soundBank 9곳 추가)
- `doc/0299-oc-unit-building-sound-bank-creation.md` (이 파일, 신규)
