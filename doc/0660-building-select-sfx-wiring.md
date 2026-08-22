# 0660 - 건물 선택 SFX(BuildingSelect) 연결

## 요청
`Assets/Sound/NTA/Building/BuildingSelect.mp3` 클립을 건물 선택 시 재생되도록 해당 위치들에 연결.

## 현재 상태 확인
- 유닛 쪽은 이미 "선택 대사"(`selectVoice`, `PlaySelectVoice()`)와 "선택 확인음"(`selectSFX`, `PlaySelectSFX()`)이 분리돼 있음(`UnitSoundBankSO`/`UnitAudio.cs:120`) - 확인음은 `SoundManager.PlaySelectSFX()`로 2D 단일 채널 재생(거리 감쇠 없음, doc/0278/0285).
- 건물 쪽(`BuildingSoundBankSO`/`BuildingAudio.cs`)은 `selectVoice`만 있고 그 짝인 `selectSFX`가 없었음 - `RTSUnitController.SelectBuilding()`(`RTSUnitController.cs:667`)이 `PlaySelectVoice()`만 호출하고 있었음. `BuildingSelect.mp3`는 이 빠진 자리(유닛의 `selectSFX`에 대응하는 건물 선택 확인음)를 채우기 위한 클립으로 판단.
- 모든 NTA 건물(`Assets/Scripts/ScriptableObject/Data/NTA Building Data SO.asset`)이 사운드뱅크로 단일 에셋 `NTA Building Sound Bank SO.asset`(guid `9a67e2ddb708d194d80446462502eec1`)를 공유 - 건물별로 따로 안 걸어도 됨.

## 변경
1. `BuildingSoundBankSO.cs` - `selectSFX` 필드 추가 (`UnitSoundBankSO.selectSFX`와 동일 패턴).
2. `BuildingAudio.cs` - `PlaySelectSFX()` 추가, `SoundManager.PlaySelectSFX(bank.selectSFX)` 호출 (`UnitAudio.PlaySelectSFX()`와 동일 패턴).
3. `RTSUnitController.cs:669` - `SelectBuilding()`에서 기존 `PlaySelectVoice()` 바로 다음 줄에 `PlaySelectSFX()` 호출 추가.
4. `NTA Building Sound Bank SO.asset` - `selectSFX.clips`에 `BuildingSelect.mp3`(guid `ec8092f35eaf2de49b91b66d31683acb`) 연결.

## 범위 밖
- `OC Building Sound Bank SO.asset` - `selectSFX` 필드는 클래스에 추가됐지만(기본값 빈 배열) 클립을 안 걸어서 무음 - OC용 클립이 없어서 그대로 둠(기존 `placementSFX`도 OC 쪽엔 안 걸려있던 것과 동일 관례).
- Spore Brood(적) 건물 - 플레이어가 선택할 일이 없어 대상 아님.

## 결과
`uloop compile` 에러 0, 기존 경고만 유지 (변경으로 인한 신규 경고 없음).
