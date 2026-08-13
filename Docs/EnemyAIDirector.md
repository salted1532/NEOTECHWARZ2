# EnemyAIDirector

`Assets/Scripts/System/EnemyAIDirector.cs`

## 개요

미션 씬에 적 기지 하나당 하나씩 배치하는 "AI 관제소" 컴포넌트(doc/0532 설계안). 스크립트로 실제 전략적
판단을 내리는 최초의 적 AI로, 씬에 미리 배치된 정적인 `EnemyUnitController`/`EnemyBuildingController`
(사거리 내 자동 교전만 하는 기초 AI, [`EnemyAttackRange`](EnemyAttackRange.md) 참고)와 별개로 다음
4가지를 자율적으로 수행한다.

1. **시간에 맞춘 공격 웨이브** — `waveTimes`에 지정한 시각(초)마다 정해진 구성의 병력을 모아 플레이어
   본진으로 보낸다.
2. **점령지 탈환 별동대** — 일정 주기로 별도 부대를 편성해 점령지(`CaptureSystem`)를 노린다.
3. **기지 방어** — 등록된 건물이 공격받으면 주변 유휴 병력을 공격 지점으로 소집한다.
4. **보충 생산** — 웨이브/별동대로 나가거나 전사해 줄어든 병력을 스폰 지점에서 자동으로 다시 생산한다.

인스펙터의 `faction`(OC/Spore Brood) 값 하나로 아래 5·6번 섹션의 `<OC>`/`<Spore Brood>` 전용 필드 중
어느 쪽을 쓸지 결정한다 — 실제 로직은 진영을 분기하지 않고, `AttackWaves`/`RaidSquadComposition`/
`AssembleBeforeAttack` 프로퍼티가 `faction`에 맞는 필드를 그때그때 골라주는 방식이라 나머지 코드는
진영을 몰라도 된다(doc/0540). 씬에 OC용 `EnemyAIDirector`와 Spore Brood용 `EnemyAIDirector`를 각각
하나씩 배치하는 구성을 상정한다.

아군판은 [`AllyAIDirector`](AllyAIDirector.md) 참고 — 점령지 탈환/기지 방어 소집이 빠진 축소판이다.

## 웨이브별 공격 패턴

`waveTimes`(예: 300/600/900초)의 시각 간격을 `waveIndex`(0부터 시작, 계속 누적)별 대기 시간으로 그대로
쓰되(1차는 `waveTimes[0]`, 이후는 구간 간격, 리스트를 넘으면 마지막 두 시각의 간격을 반복), 이 대기는
미션 시작 시각이 아니라 **직전 웨이브가 전멸한 시점**부터 다시 잰다(doc/0560) — 대기 종료 후 구성이
완성될 때까지 기다렸다가(`WaitUntilReady`) 출발한 웨이브가 전멸해야만 다음 대기가 시작된다. 각
웨이브는 3가지 구성 패턴(`WaveVariants.variants`)을 갖고, 실제 출발 시점에 그중 하나를 무작위로 골라
쓴다(doc/0551) — 매번 같은 조합만 나오지 않도록 하기 위함이며, `CurrentWaveComposition()`이 같은
`waveIndex` 동안은 결과를 캐싱하므로 생산 중이던 구성과 실제 발사 구성이 어긋나지 않는다. `waveIndex`가
`AttackWaves.Count`를 넘어서면 마지막 웨이브의 3가지 패턴에서 계속 무작위로 반복한다(doc/0539).

인스펙터에서 인원수를 조정할 땐 [`Docs/EnemyUnitAndBuildingStats.md`](EnemyUnitAndBuildingStats.md)
(OC)와 [`Docs/SporeBrood.md`](SporeBrood.md)(Spore Brood)의 유닛 ID/스탯을 함께 참고할 것.

### OC (`attackWavesOC`) — 집결 후 출발 (`assembleBeforeAttackOC = true`)

| 웨이브 | 기준 인원 | 패턴 A | 패턴 B | 패턴 C |
|---|---|---|---|---|
| 1차 | 5 | Cyborg Soldier×5 | Cyborg Soldier×3 + Striker×2 | Cyborg Soldier×2 + Railgunner×2 + Striker×1 |
| 2차 | 6 | Cyborg Soldier×4 + Railgunner×2 | Cyborg Soldier×3 + Striker×3 | Cyborg Soldier×2 + Railgunner×1 + Brute Mech×1 + Striker×2 |
| 3차 | 7 | Cyborg Soldier×4 + Striker×2 + Brute Mech×1 | Cyborg Soldier×3 + Railgunner×2 + Brute Mech×2 | Striker×3 + Brute Mech×2 + Railgunner×2 |
| 4차 | 6 | Cyborg Soldier×3 + Heavy Assault Tank×2 + Ironhawk×1 | Brute Mech×2 + Heavy Assault Tank×2 + Railgunner×2 | Cyborg Soldier×2 + Ironhawk×2 + Heavy Assault Tank×1 + Striker×1 |
| 5차(이후 반복) | 4 | Heavy Assault Tank×2 + Raven×1 + Strike Drone×1 | Heavy Assault Tank×1 + Raven×2 + Ironhawk×1 | Strike Drone×1 + Heavy Assault Tank×1 + Brute Mech×2 |

### Spore Brood (`attackWavesSporeBrood`) — 집결 없이 즉시 개별 출발 (`assembleBeforeAttackSporeBrood = false`)

| 웨이브 | 기준 인원 | 패턴 A | 패턴 B | 패턴 C |
|---|---|---|---|---|
| 1차 | 7 | Ripfang×7 | Ripfang×5 + Spitter×2 | Ripfang×4 + Spitter×2 + Skitterwing×1 |
| 2차 | 8 | Ripfang×5 + Spitter×3 | Ripfang×6 + Skitterwing×2 | Spitter×4 + Skitterwing×2 + Ripfang×2 |
| 3차 | 6 | Spitter×4 + Skitterwing×2 | Ripfang×4 + Spitter×2 | Ripfang×2 + Spitter×2 + Skitterwing×2 |
| 4차 | 10 | Ripfang×6 + Spitter×4 | Ripfang×5 + Skitterwing×3 + Spitter×2 | Spitter×5 + Skitterwing×3 + Ripfang×2 |
| 5차(이후 반복) | 12 | Ripfang×5 + Spitter×4 + Skitterwing×3 | Ripfang×6 + Spitter×3 + Skitterwing×3 | Spitter×5 + Skitterwing×4 + Ripfang×3 |

두 진영 모두 웨이브가 반복 구간에 들어가도 물량이 계속 늘어나지 않고 마지막 웨이브 수준에서 유지된다
(doc/0550에서 원래 물량을 절반으로 조정, doc/0551에서 3패턴 무작위화 추가).

## 점령지 탈환 별동대

`raidInterval`(기본 45초)마다 별동대를 점검한다. 살아있는 별동대(`currentRaidSquad`)가 있으면 새로
편성하지 않고 그 부대를 그대로 재사용해 다음 목표로 보내며, 전멸했을 때만 `RaidSquadComposition`으로
새로 편성한다(doc/0549 — 하나의 부대가 여러 점령지를 순회하는 구조). 목표는 `PickRaidTarget()`이 자기
진영(Enemy) 소유가 아닌 점령지 중 우선순위 없이 무작위로 하나 고른다(doc/0561, 기존엔 Ally 우선 →
Neutral 순이었음, doc/0532).

| 진영 | 별동대 구성(`raidSquadComposition*`) |
|---|---|
| OC | Cyborg Soldier×2 + Striker×1 |
| Spore Brood | Ripfang×2 + Spitter×1 |

## 기지 방어 (`homeBuildings`)

`homeBuildings`에 등록한 건물이 플레이어에게 공격받으면(`isEnemyAttacker == false`), 공격받은 위치
기준 `defenseRadius`(기본 15) 안에 있는 살아있는 적 유닛 전체(`Physics.OverlapSphere` — 이 director가
스폰했는지와 무관하게 씬에 미리 배치해둔 유닛도 포함) 중, 아직 다른 임무로 나가지 않았고(`deployed`에
없음) 유휴 상태(`IsIdle()`)인 유닛만 골라 공격자 위치로 강제 이동시킨다(건물 앞이 아니라 공격자 쪽으로
반격, doc/0535).

## 배치형 방어 유닛 (`defenseUnits`)

씬에 미리 세워둔 고정 수비 유닛 목록 — `Start()` 시점의 위치/방향/종류를 기억해뒀다가, 그 유닛이 죽으면
`ReinforceRoutine` 주기로 같은 자리에 같은 종류를 다시 세운다(doc/0552). 단, 대체 생산은 슬롯당 1회뿐 —
그 대체 유닛까지 죽으면 더 이상 채우지 않는다(doc/0558). `garrison`/`raidGarrison`과 달리 생산 대기열을
거치지 않고 바로 `Instantiate`한다 — 건물이 파괴된 경우의 재건은 범위 밖.

## 보충 생산 (스폰 지점 / 생산 대기열)

`spawnPoints`(여러 곳 지정 가능)마다 독립된 FIFO 생산 대기열을 갖는다. 각 스폰 지점에 선택적으로
`productionBuilding`을 지정할 수 있는데, 지정했다면 그 건물이 파괴되는 순간 해당 지점의 대기 중인
주문이 전부 취소된다(`SpawnQueue.IsAvailable`) — 다음 `FillPool` 체크 때 살아있는 다른 지점에서 자동
재주문되므로 별도 재배치 로직은 없다. 새 주문은 남은 생산 시간의 합이 가장 적은 지점에 자동 분산
(`LeastLoadedQueue`)된다. `reinforceCheckInterval`(기본 20초)마다 `ReinforceRoutine`이 다음 웨이브/
별동대 구성 기준으로 부족분을 계산해 미리 주문해둔다(웨이브 발사 시점에야 생산을 시작하면 시간이 안
맞으므로 항상 선제적으로 채움).

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `faction` | `EnemyFaction`(OC/SporeBrood) | 아래 진영별 필드 중 어느 쪽을 쓸지 결정하는 라벨 |
| `spawnPoints` | `List<EnemySpawnPoint>` | 스폰 위치 + (선택) 연동 생산 건물 |
| `homeBuildings` / `defenseRadius` | `List<EnemyBuildingController>` / `float` | 기지 방어 트리거 건물, 소집 반경 |
| `defenseUnits` | `List<EnemyUnitController>` | 배치형 방어 유닛(죽으면 같은 자리에 1회 재생산) |
| `waveTimes` | `List<float>` | 웨이브 출발 예정 시각(초, 오름차순) |
| `attackWavesOC` / `attackWavesSporeBrood` | `List<WaveVariants>` | 진영별 웨이브 구성(웨이브별 3패턴) |
| `rallyPoint` / `rallyRadius` / `rallyTimeout` | `Transform` / `float` / `float` | 집결지, 도착 판정 반경, 집결 최대 대기시간 |
| `reinforceCheckInterval` | `float` | 보충 생산 점검 주기(기본 20초) |
| `raidTargets` / `raidInterval` | `List<CaptureSystem>` / `float` | 별동대 후보 점령지, 파견 주기(기본 45초) |
| `raidSquadCompositionOC` / `raidSquadCompositionSporeBrood` | `List<UnitGroup>` | 진영별 별동대 구성 |
| `assembleBeforeAttackOC` / `assembleBeforeAttackSporeBrood` | `bool` | 웨이브 집결 대기 여부 |
| `garrison` / `raidGarrison` (디버그) | `List<EnemyUnitController>` | 현재 보유 중인 웨이브용/별동대용 병력 풀(Play 모드 인스펙터에서 확인 가능) |
| `nextWaveCountdown` / `nextRaidCountdown` (디버그) | `float` | 다음 웨이브/별동대까지 남은 시간 |
| `allEnemyUnits` / `allEnemyBuildings` (디버그) | `List<...>` | director 풀과 무관하게 씬 전체 적 유닛/건물 스냅샷 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnEnable/OnDisable` | `homeBuildings`의 `HealthManager.OnDamaged`를 건물별 캡처 델리게이트로 구독/해지 |
| `Start()` | `rtsController` 탐색, 스폰 대기열/방어 슬롯 구성, 초기 병력 채움, 3개 코루틴 시작(웨이브·별동대는 대상이 있을 때만) |
| `Update()` | 스폰 지점별 생산 대기열을 매 프레임 진행, 완성되면 스폰 후 집결지로 `MoveTo` |
| `AttackWaveRoutine()` | (전멸 후부터 다시 잰) `WaveIntervalFor`만큼 대기 → 플레이어 전멸 확인 → 구성 완성 대기 → `LaunchWave()`가 전멸할 때까지 대기 → 다음 사이클(doc/0560, 무한 반복) |
| `WaveIntervalFor(index)` | `waveTimes[index]` 기준 이번 사이클 대기 시간(리스트를 넘으면 마지막 두 시각 간격 반복, doc/0560) |
| `IsPlayerDefeated()` | 플레이어 건물이 하나도 안 남았는지 |
| `WaitUntilReady/IsComposeReady` | 요청한 구성이 pool에 전부 갖춰질 때까지 폴링 대기 |
| `LaunchWave()` | `TakeSquad`로 병력 차출 → (OC만) 집결 대기 → `RunWaveSquad`가 전멸할 때까지 직접 대기(doc/0560, 구 fire-and-forget 방식 번복) |
| `CurrentWaveComposition()` | `waveIndex`에 맞는 웨이브의 3패턴 중 무작위 하나(같은 `waveIndex` 동안 캐싱) |
| `RunWaveSquad(squad)` | 목표(플레이어 MainBase 우선)가 없으면 재탐색해 전원 재발령, 전멸 시 종료 |
| `PickAttackTarget()` | 플레이어 MainBase 중 무작위 하나(없으면 아무 건물이나) |
| `RaidRoutine()` | `raidInterval`만큼 대기 → 별동대 생존 확인/재편성 → `PickRaidTarget()`으로 이동 |
| `PickRaidTarget()` | 자기 진영 소유가 아닌 점령지 중 무작위로 하나 선택(doc/0561) |
| `HandleBaseAttacked` | 공격받은 건물 주변 유휴 유닛을 공격자 위치로 소집 |
| `ReinforceRoutine()` | 주기적으로 풀 정리 + 다음 웨이브/별동대 구성 선제 생산 + 방어 유닛 재생산 + 디버그 스냅샷 갱신 |
| `RespawnDeadDefenseUnits()` | 죽은 배치형 방어 유닛 슬롯에 같은 종류를 재생산(슬롯당 1회, doc/0558) |
| `FillPool/EnqueueProduction/LeastLoadedQueue` | 부족분 계산 → 생산 대기열에 주문 → 가장 한가한 스폰 지점에 분산 |
| `TakeSquad(pool, composition)` | pool에서 구성이 요구하는 유닛을 종류별로 차출해 `deployed`에 등록 |

## 연관 컴포넌트

- **[`AllyAIDirector`](AllyAIDirector.md)**: 같은 설계를 아군 OC 쪽에 적용한 축소판(점령지/기지방어 없음)
- **[`EnemyUnitController`](EnemyUnitController.md)** / **[`EnemyBuildingController`](EnemyBuildingController.md)**: 실제로 스폰/관리되는 유닛·건물
- **[`CaptureSystem`](CaptureSystem.md)**: 별동대 파견 대상(점령지)
- **[`RTSUnitController`](RTSUnitController.md)**: 플레이어 건물 목록 조회(`IsPlayerDefeated`/`PickAttackTarget`), 유닛 데이터 조회(`GetEnemyUnitData`)
