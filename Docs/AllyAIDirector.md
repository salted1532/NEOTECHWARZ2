# AllyAIDirector

`Assets/Scripts/System/AllyAIDirector.cs`

## 개요

미션 씬에 아군 OC(구조 가능한 유닛) 생산 거점 하나당 하나씩 배치하는 "AI 관제소" —
[`EnemyAIDirector`](EnemyAIDirector.md)(doc/0532)의 아군판(doc/0543). 시간이 지날수록 점점 강해지는
공격 웨이브를 적대 세력(외계종족/적대 OC) 쪽으로 보내고, 죽은 유닛을 보충 생산한다.

`EnemyAIDirector`와의 차이:
- **점령지 탈환 별동대 / 기지 피격 시 병력 소집이 없다** — doc/0543에서 이번 요청 범위 밖으로 결정,
  필요해지면 `EnemyAIDirector`의 해당 로직(`RaidRoutine`/`HandleBaseAttacked`)을 그대로 옮겨오면 됨.
- **진영 분기(`EnemyFaction`)가 없다** — 아군 OC는 현재 하나의 진영만 존재하므로 `<OC>`/`<Spore Brood>`
  구분 없이 필드가 하나씩만 있음.
- **웨이브 구성이 갈수록 강해지는 고정 5단계**(무작위 3패턴이 아니라 웨이브당 구성 1개) — `EnemyAIDirector`가
  웨이브당 3패턴 중 무작위인 것과 다름.
- **스폰이 즉시 이뤄진다** — `EnemyAIDirector`처럼 생산 시간을 시뮬레이션하는 대기열이 없고, `unitID`로
  OC 유닛 로스터(`UnitData.AllyPrefab`)를 조회해 그 자리에서 바로 `Instantiate`한다. 여러 `spawnPoints`엔
  라운드로빈으로 고르게 분산.

## 공격 웨이브 구성 (`attackWaves`, 점점 강해지는 고정 5단계)

| 웨이브 | 구성 |
|---|---|
| 1차 | Cyborg Soldier×10 |
| 2차 | Cyborg Soldier×8 + Railgunner×3 |
| 3차 | Cyborg Soldier×8 + Striker×3 + Brute Mech×2 |
| 4차 | Cyborg Soldier×6 + Heavy Assault Tank×3 + Ironhawk×2 |
| 5차(이후 반복) | Heavy Assault Tank×3 + Raven×2 + Strike Drone×1 |

`waveTimes`의 시각 간격을 사이클별 대기 시간으로 쓰되(1차는 `waveTimes[0]`, 이후는 구간 간격, 리스트를
넘으면 마지막 두 시각의 간격을 반복), 이 대기는 미션 시작 시각이 아니라 **직전 웨이브가 전멸한 시점**
부터 다시 잰다(doc/0560, `EnemyAIDirector`와 동일한 패턴) — 대기 종료 후 구성이 완성될 때까지
기다렸다가(`WaitUntilReady`, doc/0560에서 신규 추가) 출발한 웨이브가 전멸해야만 다음 대기가 시작된다.
`waveIndex`가 `attackWaves.Count`를 넘어서면 5차 구성을 계속 반복한다. `assembleBeforeAttack`(기본
true)이 켜져 있으면 웨이브 병력이 `rallyPoint`에 다 모일 때까지 대기 후 한꺼번에 출발한다.

## 공격 목표 선정 (`PickAttackTarget`)

`EnemyBuildingController.ActiveBuildings`(전역 등록 리스트)에서 아군 OC 건물(`AllyBuildingController`)을
걸러낸 "진짜 적대 세력" 건물 중, **Hive Core(Spore Brood 건물 ID 7)가 살아있으면 항상 최우선 목표**로
삼는다. Hive Core가 없으면(파괴됐거나 이 미션에 없음) 등록 순서상 첫 번째 건물을 목표로 삼는다(doc/0543
— "Hive Core 먼저, 그 다음은 순서대로"). 이 리스트는 건물 등록/파괴 시에만 바뀌므로 매 웨이브 같은
순서를 유지하다가 앞에서부터 하나씩 사라진다.

## 보충 생산

`reinforceCheckInterval`(기본 20초)마다 다음 웨이브 구성 기준으로 부족분을 계산해 `SpawnUnit()`으로
즉시 스폰한다(생산 시간 없음). `unitID`로 OC 유닛 로스터(`RTSUnitController.GetEnemyUnitData`)의
`AllyPrefab`을 조회하므로, 미션 제작자가 `unitID`별 프리팹을 인스펙터에 직접 연결할 필요가 없다 — 스폰이
실패하면(`AllyPrefab`이 비어있는 등) 그 종류는 포기하고 다음 종류로 넘어간다(무한 루프 방지). 스폰된
유닛은 생산되자마자 `rallyPoint`(없으면 `DefaultRallyPosition()`)로 즉시 이동한다(`EnemyAIDirector`와
동일한 패턴, doc/0545, doc/0564).

## 배치형 방어 유닛 (`defenseUnits`)

`EnemyAIDirector.defenseUnits`와 동일한 패턴(doc/0552) — 씬에 미리 세워둔 고정 수비 유닛이 죽으면 같은
자리에 같은 종류로 재생산. 단, 대체 생산은 슬롯당 1회뿐 — 그 대체 유닛까지 죽으면 더 이상 채우지
않는다(doc/0558).

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `spawnPoints` | `List<Transform>` | 스폰 위치(여러 곳, 라운드로빈으로 분산) |
| `defenseUnits` | `List<AllyController>` | 배치형 방어 유닛(죽으면 같은 자리에 1회 재생산) |
| `waveTimes` / `attackWaves` | `List<float>` / `List<AllyWaveComposition>` | 웨이브 출발 시각, 점점 강해지는 고정 5단계 구성 |
| `rallyPoint` / `rallyRadius` / `rallyTimeout` / `assembleBeforeAttack` | | 집결지 관련(EnemyAIDirector와 동일한 의미) |
| `reinforceCheckInterval` | `float` | 보충 생산 점검 주기(기본 20초) |
| `garrison` / `currentSquad` (디버그) | `List<AllyController>` | 현재 보유 병력 / 현재 파견된 별동대(Play 모드 인스펙터 확인용) |
| `allAllyUnits` / `allAllyBuildings` (디버그) | `List<...>` | 씬 전체 아군 OC 유닛/건물 스냅샷 |
| `nextWaveCountdown` (디버그) | `float` | 다음 웨이브까지 남은 시간 |
| `HiveCoreBuildingID` (private const) | `int` = 7 | Spore Brood Hive Core 건물 ID — 최우선 목표 판정용 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | `rtsController` 탐색, 방어 슬롯 구성, 초기 병력 채움, 웨이브/보충 생산 코루틴 시작 |
| `AttackWaveRoutine()` | (전멸 후부터 다시 잰) `WaveIntervalFor`만큼 대기 → 공격 대상 존재 대기 → 구성 완성 대기 → `LaunchWave()`가 전멸할 때까지 대기 → 다음 사이클(doc/0560/0565, 무한 반복) |
| `WaveIntervalFor(index)` | `waveTimes[index]` 기준 이번 사이클 대기 시간(리스트를 넘으면 마지막 두 시각 간격 반복, doc/0560) |
| `WaitUntilTargetExists()` | 공격할 적대 건물이 하나도 없으면 폴링 대기 - 그동안 병력을 차출하지 않아 `garrison`에 쌓인다(생산은 `ReinforceRoutine`이 계속, doc/0565 신규 추가) |
| `WaitUntilReady/IsComposeReady` | 요청한 구성이 `garrison`에 전부 갖춰질 때까지 폴링 대기(doc/0560 신규 추가, `EnemyAIDirector`와 동일한 패턴) |
| `LaunchWave()` | `TakeSquad`로 병력 차출 → (설정 시) 집결 대기 → `RunWaveSquad`가 전멸할 때까지 직접 대기(doc/0560, 구 fire-and-forget 방식 번복) |
| `CurrentWaveComposition()` | `waveIndex`에 맞는 고정 구성(리스트를 넘으면 마지막 구성 반복) |
| `RunWaveSquad(squad)` | 목표가 없으면 `PickAttackTarget()`으로 재탐색해 전원 재발령 - 재탐색해도 목표가 없으면 포기하지 않고 다음 프레임에 다시 찾는다(부대가 전멸하기 전까진 절대 끝나지 않음, doc/0565) |
| `PickAttackTarget()` | Hive Core 우선, 없으면 등록 순서상 첫 적대 건물 |
| `ReinforceRoutine()` | 주기적으로 풀 정리 + 다음 웨이브 구성 선제 생산 + 방어 유닛 재생산 + 디버그 스냅샷 갱신 |
| `RespawnDeadDefenseUnits()` | 죽은 배치형 방어 유닛 슬롯에 같은 종류를 재생산(슬롯당 1회, doc/0558) |
| `FillPool/SpawnUnit/NextSpawnPoint` | 부족분 계산 → OC 로스터의 `AllyPrefab` 즉시 스폰 → 스폰 지점 라운드로빈 순환 |
| `TakeSquad(composition)` | `garrison`에서 구성이 요구하는 유닛을 종류별로 차출해 `deployed`에 등록 |

## 연관 컴포넌트

- **[`EnemyAIDirector`](EnemyAIDirector.md)**: 같은 설계의 적 진영판(원본, 점령지/기지방어 포함)
- **[`AllyController`](AllyController.md)** / **[`AllyBuildingController`](AllyBuildingController.md)**: 실제로 스폰/관리되는 아군 OC 유닛·건물
- **[`EnemyBuildingController`](EnemyBuildingController.md)**: `ActiveBuildings` 전역 등록 리스트(공격 목표 탐색에 사용, `AllyBuildingController`가 상속)
- **[`RTSUnitController`](RTSUnitController.md)**: 유닛 데이터 조회(`GetEnemyUnitData`로 `AllyPrefab` 획득)
