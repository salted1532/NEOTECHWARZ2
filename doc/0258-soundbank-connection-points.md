# 0258 - SoundBank 에셋을 어디에 연결해야 하는지

**날짜:** 2026-07-28

## 요청 내용

> 각 유닛별 사운드 뱅크를 만들면 이 스크립터블오브젝트는 어디랑 연결해야해?

## 답변 요약

연결 지점은 두 군데다 (하나만 하면 소리가 안 남):

1. **데이터 연결**: `New Unit Data SO.asset`(NTA) / `OC Unit Data SO.asset`(OC)을 열어 `unitData`
   리스트에서 해당 유닛 항목을 찾고, 그 항목의 `Sound Bank` 필드(`UnitData.soundBank`)에 만든
   `UnitSoundBankSO` 에셋을 드래그. 건물은 `New Building Data SO.asset`의 `BuildingData.soundBank`에
   동일하게 연결.
2. **프리팹 연결**: 실제로 재생을 트리거하는 건 `UnitAudio`/`BuildingAudio` 컴포넌트이므로, 유닛
   프리팹(`UnitController`/`EnemyUnitController` + `HealthManager`가 붙어있는 오브젝트)에 `UnitAudio`를,
   건물/`BaseStructure` 프리팹에 `BuildingAudio`를 `Add Component`로 붙여야 한다. 이 컴포넌트들은
   `Start()`에서 자기 unitID/buildingID로 `RTSUnitController.GetUnitData`/`GetBuildingData`를 조회해
   `soundBank`를 자동으로 찾아 쓰므로, 컴포넌트 쪽에는 SoundBank를 따로 인스펙터로 연결할 필요가 없다.

프로젝트 코드는 변경하지 않음 (순수 Q&A, doc/0255~0257 설계/구현 내용에 대한 사용법 재설명).

## 변경된 파일

없음.
