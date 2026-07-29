# 0298 - 적 유닛/건물 수치 정리 문서 작성

날짜: 2026-07-29

## 요청 내용

"적 유닛,건물에 대한 정보를 정리한 문서를 만들어줘 Docs폴더에다가 이전에 아군유닛으로 만든 유닛,건물 문서를 참고해서 현재 게임상 적용된 공격력,방어력,체력, 공격타입,크기 등 정보를 정리해서 만들어줘"

## 조사 내용

- `Docs/UnitAndBuildingStats.md`, `Docs/UnitBalanceReference.md`(아군 NTA 참고 포맷) 확인.
- 적 진영(OC) 데이터 출처 확인:
  - `Assets/Scripts/ScriptableObject/Data/OC Unit Data SO.asset` (`EnemyUnitDataSO`) — 유닛 9종의 체력/공격력/사거리/공격속도/장갑타입/크기타입/지상·공중 공격 가능 여부/비용/생산시간/티어/선행건물.
  - `Assets/Scripts/ScriptableObject/Data/OC Building Data SO.asset` (`EnemyBuildingDataSO`) — 건물 6종의 크기/비용/건설시간/체력/최대인구/선행건물.
  - SO에 없는 필드(방어력 `armor`, 공격 방식 `attackType`, 공중유닛 여부 `isAirUnit`, 고유 보너스 `bonusVersusArmorType/Percent`)는 각 유닛 프리팹(`Assets/prefabs/OC/Unit/**/*.prefab`)의 `EnemyUnitController` 직렬화 필드를 직접 읽어서 확인.
  - 건물 프리팹(`Assets/prefabs/OC/Building/Enemy_*.prefab`)의 `enemyBuildingID`/`maxHealth`가 SO 값과 일치하는지 확인 — 전부 일치(아군 쪽에 있던 unitID 불일치 버그가 적 쪽엔 없음).
  - `EnemyUnitController.cs`/`EnemyBuildingController.cs`/`EnemyAttackRange.cs` 코드를 읽어 `ApplyUnitData`/`ApplyBuildingData`가 실제로 SO 값을 스폰 시점에 덮어쓰는지, 어떤 필드가 프리팹 값 그대로 남는지 확인.
  - 특이사항 발견: 적(OC)은 아군(NTA)과 달리 `EnemyAttackRange.CanEngage()`가 지상/공중 공격 도메인 제약을 실제로 걸러낸다(아군 `AttackRange`는 이 제약이 코드에 없음) — 문서에 명시.
  - Ironhawk는 SO 설명상 "대공 전문 공중유닛"이지만 프리팹 `isAirUnit`이 0으로 남아 실제로는 지상 유닛처럼 이동(아군 SkyLancer와 동일한 미구현 패턴). 특성(트레이트) 데이터도 SO에는 있으나 `EnemyUnitController`가 조회하지 않아 미적용.
  - 레일거너(Railgunner)는 SO에 `requiredBuildingID: 6`(뉴럴 랩)이 명시되어 있으나, 실제 강제 여부는 코드상 미확인으로 문서에 별도 표기.

## 코드 변경

없음 — 순수 문서 작성 요청(프로젝트 코드/에셋 변경 없음)이라 `confirm-before-implementing` 게이트 대상이 아님.

## 요약

`Docs/EnemyUnitAndBuildingStats.md`를 새로 작성했다. 아군 문서와 동일한 포맷으로 적 유닛 9종(나노봇 리페어/사이보그 솔저/스트라이커/레일거너/브루트 메크/헤비 어썰트 탱크/아이언호크/레이븐/스트라이크 드론)과 적 건물 6종(오메가 코어/카고 사일로/사이버 파운드리/메크 야드/드론 행어/뉴럴 랩)의 체력/공격력/방어력/공격방식/장갑/크기/사거리/공격속도/비용/생산시간/선행조건을 정리하고, 적 진영 고유의 구현 차이(공격 도메인 제약이 실제로 작동함, Ironhawk 공중유닛 미구현, 트레이트 미구현 등)를 "참고/미구현 사항" 절에 기록했다.

## 변경된 파일

- `Docs/EnemyUnitAndBuildingStats.md` (신규)
- `doc/0298-enemy-unit-building-stats-doc.md` (이 파일, 신규)
