# 0557 - EnemyAIDirector/AllyAIDirector Docs 작성 + README 로드맵 동기화

## 날짜
2026-08-13

## 요청 내용
"현재 적 AI 스크립트에 대한 문서를 Docs폴더에 만들고 각 웨이브별 패턴등 내용을 정리해서 작성해줘
그리고 Readme파일도 갱신해줘 새로 생긴 스크립트에 대한 문서도 만들어주고 해당하는 링크로 연결도
해주고" + 사용자가 개인적으로 관리하던 대규모 로드맵/체크리스트 원문을 첨부하며 "로드맵도 보고
갱신해줘"

코드 변경 없는 순수 문서화 요청이라(`Docs/*.md`, `README.md`는 게임플레이 코드/에셋이 아님)
confirm-before-implementing 게이트 없이 바로 작성함.

## 조사 내용
- `Docs/` 폴더에 `EnemyAIDirector.md`/`AllyAIDirector.md`/`MeleeBodySlamAttack.md`가 없는 것을
  확인 — 이번 세션 이전 작업(doc/0532~0553)에서 새로 생긴 스크립트인데 아직 스크립트별 문서가 없었음.
- `EnemyAIDirector.cs`/`AllyAIDirector.cs`를 다시 읽어 최신 상태(웨이브 5단계×3패턴, 진영별 필드,
  집결 여부, 별동대, 기지방어, 배치형 방어유닛, 생산 대기열)를 확인.
- 웨이브 구성의 `unitID`를 실제 유닛명으로 치환하기 위해 `OC Unit Data SO.asset`에서 ID/이름 매핑
  확인(2=Cyborg Soldier, 3=Striker, 4=Railgunner, 5=Brute Mech, 6=Heavy Assault Tank, 7=Ironhawk,
  8=Raven, 9=Strike Drone), Spore Brood는 기존에 알고 있던 10=Ripfang/11=Spitter/12=Skitterwing 사용.
- 사용자가 체크(✓)로 표시한 "외계종족 공격 이펙트, 사망이펙트 따로 준비" 항목을 실제 코드로 검증 —
  `Ripfang.prefab`의 `hitEffects`(bulletHitPrefab/explosiveHitPrefab/laserHitPrefab/flameHitPrefab)
  GUID가 `Assault Trooper.prefab`(NTA)과 **완전히 동일**함을 확인, 즉 아직 기존 이펙트를 재사용 중이라
  실제로는 미구현 상태 — 사용자 체크리스트를 그대로 신뢰하지 않고 코드 대조 후 README 로드맵에 그대로
  유지(허위로 완료 처리하지 않음).
- README의 "로드맵 (미구현)" 섹션에 있던 "Enemy AI 구현" 항목이 실제로는 이미 완료된 상태(doc/0532~0552)임을
  확인 — 로드맵에서 제거하고 "구현 완료 기능 > 캠페인/미션" 섹션으로 이동.
- 사용자 체크리스트의 "UI창 크기, 위치 조절 기능" 항목은 README 어디에도 없던 신규 로드맵 항목 —
  로드맵에 추가.
- 체크리스트의 나머지 항목(서브 스테이지 구성/브리핑룸/건물 고유 스킬/건물 선택 사운드/UI 디자인
  개선/1대1 AI)은 이미 README "로드맵 (미구현)"에 전부 있었고, 체크(✓) 표시된 나머지 대부분의 항목도
  이미 README "구현 완료 기능"에 상세히 반영돼 있어 추가 변경 불필요.

## 적용한 변경

### 1. 신규 문서 생성
- `Docs/EnemyAIDirector.md` — 역할, 웨이브별 공격 패턴(OC/Spore Brood 각 5웨이브×3패턴 전체 표),
  점령지 별동대 구성, 기지 방어, 배치형 방어유닛, 생산 대기열, 필드/메소드 표
- `Docs/AllyAIDirector.md` — 역할, EnemyAIDirector와의 차이점, 고정 5단계 웨이브 구성표, Hive Core
  최우선 목표 로직, 필드/메소드 표
- `Docs/MeleeBodySlamAttack.md` — DOTween 몸통박치기 연출 컴포넌트 문서

### 2. `README.md` 갱신
- "핵심 스크립트" 표에 `MeleeBodySlamAttack`(TurretController 다음), `EnemyAIDirector`/`AllyAIDirector`
  (AllyAttackRange 다음) 행 추가, 각각 새 Docs 링크 연결
- "구현 완료 기능 > 캠페인/미션"에 Enemy AI/아군 OC AI 구현 완료 항목 추가(Docs 링크 포함)
- "로드맵 (미구현)"에서 "Enemy AI 구현" 항목 제거(완료로 이동), "UI 창 크기/위치 조절 기능" 항목 신규 추가
- "외계종족 전용 공격/사망 이펙트" 로드맵 항목은 실제 코드 대조 결과 여전히 미구현이라 그대로 유지(설명만 보강)

## 영향받는 파일
- 신규: `Docs/EnemyAIDirector.md`, `Docs/AllyAIDirector.md`, `Docs/MeleeBodySlamAttack.md`
- 변경: `README.md`
