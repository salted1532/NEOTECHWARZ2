# 스포어 브루드 (Spore Brood) — 외계 종족

> 최종 갱신: 2026-08-09
> 설계 배경(요청/조사 과정)은 [`doc/0441`](../doc/0441-alien-monster-faction-design-proposal.md) 참고.
> 스토리상 위치는 [`Campaign.md`](Campaign.md) 2~5막에서 NTA/OC(오메가 코퍼레이션) 양측을 공격하는
> "외계종족"이 바로 이 종족이다. 값 출처는
> `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` /
> `Spore Brood Building Data SO.asset`(`EnemyUnitDataSO`/`EnemyBuildingDataSO` 재사용, 유닛ID
> 10~12·건물ID 7~9로 기존 OC(오메가 코퍼레이션) 유닛ID 1~9·건물ID 1~6 뒤에 이어붙임) — 로컬라이제이션
> 키도 같은 이유로 `unit.oc.10~12`/`building.oc.7~9` 네임스페이스를 그대로 공유한다.

## 컨셉

지능형 문명이 아니라 포자로 뒤덮인 행성에서 자생한 **집단 본능(하이브 마인드)** 생물군. 개체는
단순하지만 무리 지어 압박하며, 건물조차 금속이 아닌 살아있는 생체 조직으로 이루어져 있다(성장/재생
이미지). 인간형 실루엣을 피하고 곤충·기생생물·파충류가 뒤섞인 형태로 디자인. 티어(생산 건물 계층)
없이 건물 1개(산란구덩이)가 근접/원거리/공중을 전부 생산하는 대신, 방어 타입을 3종 모두
경장갑(Light)으로 통일해 "물량과 기동성은 강하지만 관통 공격에 취약한 괴물 무리"라는 정체성을
스탯으로 뒷받침한다.

---

## 유닛 (3종, 전부 산란구덩이에서 생산)

### 립팽 (Ripfang) — 근접
- 적유닛ID: 10
- 컨셉: 날카로운 턱으로 물어뜯는 저비용 돌격 개체. 무리로 찍어 눌러야 하는 전위
- 공격 가능 대상: 지상만
- 공격 방식: Hitscan (물어뜯기)
- 장갑: 경장갑 / 크기: 소형
- 가격&인구수: 45 / 0 / 1 (광물/가스/인구)
- 생산시간: 10
- 체력: 60 / 공격력: 9 / 사거리: 2 / 공격속도: 0.5초(빠름)
- 프리팹: `Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab`

### 스피터 (Spitter) — 원거리
- 적유닛ID: 11
- 컨셉: 부식성 체액을 뱉어내는 원거리 저격 개체 — 세 유닛 중 가장 낮은 체력의 글래스캐논, 대지/대공을
  모두 커버하는 이 종족의 주력 딜러
- 공격 가능 대상: 지상, 공중
- 공격 방식: Projectile (산성 침)
- 장갑: 경장갑 / 크기: 중형
- 가격&인구수: 80 / 20 / 2
- 생산시간: 20
- 체력: 50 / 공격력: 11 / 사거리: 13 / 공격속도: 1.1초
- 프리팹: `Assets/prefabs/Spore_Brood/Unit/Spitter.prefab`

### 스키터윙 (Skitterwing) — 공중
- 적유닛ID: 12
- 컨셉: 하늘을 뒤덮는 기생 비행체. 치고 빠지는 견제형 — 이 종족의 유일한 공중 유닛이자, 별도
  방공 유닛이 없는 종족 특성상 대공 방어의 핵심
- 공격 가능 대상: 지상, 공중
- 공격 방식: Projectile (독침)
- 장갑: 경장갑 / 크기: 중형
- 가격&인구수: 95 / 35 / 2
- 생산시간: 26
- 체력: 65 / 공격력: 8 / 사거리: 11 / 공격속도: 0.9초
- 프리팹: `Assets/prefabs/Spore_Brood/Unit/Skitterwing.prefab`

---

## 건물 (3종)

### 하이브 코어 (Hive Core) — 메인기지
- 적건물ID: 7 / 크기: 4×4
- 컨셉: 종족의 심장부. 살아있는 구조물로 서서히 자연 재생(바이오 리제너레이션)
- 가격: 400 / 0 (시작 건물) / 건설시간: 60 / 체력: 1600 / 최대인구 증가: +10
- 생산 유닛: 립팽·스피터·스키터윙 전부 (티어 구분 없음)
- 프리팹: `Assets/prefabs/Spore_Brood/Building/Hive_Core.prefab`
- ⚠️ **"자연 재생" 컨셉은 미구현.** `HealthManager`에 시간 경과 자동 회복 기능이 없어 SO/로컬라이제이션
  설명에만 존재하는 텍스트 설정이다 (doc/0441 "남은 작업" 항목).

### 산란구덩이 (Spawning Pit) — 유닛 생산
- 적건물ID: 8 / 크기: 3×3
- 컨셉: NTA/OC가 병영·공장·우주공항으로 나눈 생산 계층을 이 종족은 건물 하나로 압축 — 파괴되면
  종족 전체의 유닛 생산이 완전히 멈추는 리스크를 짊어짐
- 가격: 150 / 50 / 건설시간: 40 / 체력: 900 / 최대인구 증가: +0
- 프리팹: `Assets/prefabs/Spore_Brood/Building/Spawning Pit.prefab`

### 바이오리액터 (Bio-Reactor) — 에너지 코어
- 적건물ID: 9 / 크기: 2×2
- 컨셉: 이름상 "에너지 코어"지만 실제로는 보급(인구) 공급 역할
- 가격: 150 / 0 / 건설시간: 30 / 체력: 700 / 최대인구 증가: +8
- 프리팹: `Assets/prefabs/Spore_Brood/Building/Bio_Reactor.prefab`
- ⚠️ 파괴 시 페널티(생산 속도 저하 등)는 미구현 — 현재는 순수 인구 공급 건물로만 동작한다 (doc/0441
  "남은 작업" 항목).

---

## 구현 상태 참고

- 유닛/건물 스탯, 아이콘, 프리팹, 로컬라이제이션(EN/KR) 텍스트까지 doc/0441 제안값 그대로 전부
  구현되어 있다 (SO 에셋 실측 확인 완료).
- **하이브 코어 자연 재생**, **바이오리액터 파괴 페널티**는 애초에 신규 로직이 필요하다고 doc/0441에
  명시됐던 항목으로, 아직 코드에 없다.
- **캠페인 스테이지 배치 여부는 스크립트에서 확인되지 않았다** — `Assets/Scripts/System/Stage*.cs`
  어디에도 이 종족 관련 참조가 없어, 씬에 직접 배치돼 있는지(다른 OC 적처럼 `EnemyBuildingController`/
  `EnemyUnitController` 껍데기로 씬에 수동 배치) 별도 확인이 필요하다.
- 데미지 시스템(장갑/크기/공격방식 배율, 최종 데미지 계산식)은 OC(오메가 코퍼레이션)와 완전히
  동일 — [`EnemyUnitAndBuildingStats.md`](EnemyUnitAndBuildingStats.md)의 "데미지 시스템" 절 참고.

## 관련 문서
- [`doc/0441`](../doc/0441-alien-monster-faction-design-proposal.md) — 설계 제안 원본(요청/조사 과정)
- [`Campaign.md`](Campaign.md) — 스토리상 "외계종족"의 등장 흐름
- [`EnemyUnitAndBuildingStats.md`](EnemyUnitAndBuildingStats.md) — OC(오메가 코퍼레이션) 유닛/건물 스탯,
  동일 SO/데미지 시스템을 공유하는 대응 진영
