## 날짜
2026-07-28

## 요청 내용
move, attack, spawn 등 SFX는 언제 작동하는지 질문.

## 조사 내용
`Assets\Scripts\Audio\UnitAudio.cs`, `SoundManager.cs`, 그리고 실제 호출부(`RTSUnitController.cs`, `UnitController.cs`, `UnitSpawner.cs`, `PlacementSystem.cs`)를 확인.

- **Spawn (`PlaySpawnSound`)**: `UnitSpawner.Spawn()`에서 `Instantiate` 직후 매 유닛마다 1회 호출 (`UnitSpawner.cs:102`). `spawnSFX` + `spawnVoice` 동시 재생.
- **Move (`PlayMoveSFX`/`PlayMoveVoice`)**: 이동 "명령"을 내리는 시점에만 재생.
  - `RTSUnitController.MoveSelectedUnits()` (`RTSUnitController.cs:318-330`) — 선택된 유닛에게 이동 명령 시, 대표 유닛 1마리만 (다수선택 시 대사 겹침 방지, doc/0255).
  - `PlacementSystem`에서 워커가 건설 위치로 이동 시작할 때도 동일 호출 (`PlacementSystem.cs:186-187`).
- **Attack (`PlayAttackSFX`)**: 공격 "명령"이 아니라 실제 타격이 적용되는 순간마다 재생. `UnitController.Attack()` 내부에서 쿨다운(`alreadyAttacked`)과 지상/공중 도메인 체크를 통과해 데미지를 적용하는 지점에서 호출 (`UnitController.cs:886`). 교전 중이면 공격 주기마다 반복.
  - 별개로 공격 "명령" 음성(`PlayAttackOrderVoice`)은 `AttackSelectedUnits` 등 명령 진입점에서 대표 유닛 1마리만 재생 (`RTSUnitController.cs:342` 등).
- 공통: 클립은 유닛 종류별 `UnitSoundBankSO`(`UnitData.soundBank`)에서 조회하며, 슬롯이 비어있으면 무음.

## 요약/남은 작업
순수 Q&A, 코드 변경 없음.

## 변경된 파일
없음.
