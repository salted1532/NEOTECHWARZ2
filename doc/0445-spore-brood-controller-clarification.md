# 0445. Spore Brood 종족 Controller는 어떻게 되는지 (질의응답)

**날짜:** 2026-08-07

## 요청 내용
> 그러면 Spore Brood종족에 Controller는 어떤식으로 할까?

## 답변

`doc/0444`에서 이미 결론 낸 것과 동일함 — **새 Controller 클래스는 안 만들고, OC가 쓰는 것과
완전히 같은 `EnemyUnitController` / `EnemyBuildingController`를 그대로 씀.**

- Spore Brood 프리팹(Ripfang/Spitter/Raven, Hive_Core/Spawning Pit/Bio_Reactor) 6개 전부 이미 이
  컴포넌트가 붙어 있음(0444에서 확인함).
- 이 클래스들은 애초에 "OC 전용"이 아니라 `enemyUnitID`/`enemyBuildingID` 하나로 데이터 SO를
  조회해서 스스로 스탯을 채우는 **범용** 클래스임 — 어디에도 "OC"라는 이름의 특수 분기가 없음.
- 이제 `RTSUnitController.GetEnemyUnitData`/`GetEnemyBuildingData`가 OC 데이터베이스에서 못 찾으면
  Spore Brood 데이터베이스(ID 10~12/7~9)를 이어서 조회하도록 돼 있으므로(0444), Spore Brood 프리팹의
  `Start()`가 자기 ID로 조회하면 자동으로 Spore Brood 쪽 스탯을 받아옴.
- 즉 Spore Brood 유닛/건물은 **OC와 똑같은 방식**(사거리 내 자동 교전, 이동, 공격-이동, 피격 시
  반격, 안개 연동, 선택 마커 등)으로 이미 작동함 — 이게 "OC의 EnemyController를 참고해서 동일하게
  작동"의 실제 결과임.

참고로 OC 쪽도 "전략적" 컨트롤러(무엇을 생산할지, 언제 공격을 보낼지 판단하는 AI)는 아직 없음
(`Docs/EnemyUnitController.md` 로드맵 참고 — 지금은 씬에 미리 배치되거나 미션 스크립트가 직접
스폰하는 "전술적" AI만 있음). Spore Brood도 지금 상태로는 동일하게, 씬에 배치해두면 사거리 안의
적과 자동 교전하는 정도까지만 동작함. 만약 "언제/어디서 스폰할지", "웨이브를 어떻게 짤지" 같은
진영 단위 전략 AI가 필요하면 그건 이번 범위 밖의 별도 작업(스테이지별 미션 스크립트나 새 스포너
클래스)이 될 것 — 필요해지면 그때 얘기해도 됨(YAGNI).

## 변경된 파일
- 없음 (질의응답, 코드/에셋 변경 없음)
