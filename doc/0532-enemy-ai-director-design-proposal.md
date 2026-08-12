# 0532 - Enemy AI Director 설계안 (검토 요청, 구현 전)

## 날짜
2026-08-12

## 요청 내용
"Enemy AI 구현(스크립트로 동작하는) Enemy Controller를 조종하는 스크립 제작"
- 시간에 맞춰서 공격병력을 모아서 공격 (ex: 5분/10분/15분 간격)
- 점령지에 별동대를 보내서 점령지 탈환
- 공격받았을 시 주변 적 유닛을 해당 지역으로 보내서 방어
- 적 유닛이 죽으면 다시 생산하여 추가 병력을 보내 죽은 유닛을 매꿈
- 적 OC / 외계종족(Spore Brood) 2가지 진영에 따라 다른 유닛(프리팹)이 생산되고 다른 방식으로 작동

사용자가 먼저 설계안 검토를 원함 → **이 문서는 제안일 뿐, 아직 코드 작성 안 함.**

## 기존 코드 조사

### 지금 "적"이 어떻게 동작하는가 (AI 없음)
`EnemyBuildingController.cs` 상단 주석에 명시: *"캠페인은 정해진 스크립트/트리거로 적 유닛을 직접
배치·스폰할 예정이라, 적 건물이 실제로 생산 큐/자원 소모/건설 그리드 같은 걸 가질 필요가 없다"* -
지금은 미션 제작자가 씬에 적 유닛/건물을 손으로 배치해두는 것이 전부이고, "AI 관제소" 같은 자동 판단
주체는 존재하지 않는다. 이번 요청은 그 빈 자리를 처음 채우는 것.

### 진영 2개가 이미 데이터 레벨에서 분리돼 있음
- **OC**(오메가 코퍼레이션, 인간형) - `Assets\prefabs\OC\Unit`, `Assets\prefabs\OC\Building`.
  `RTSUnitController.enemyUnitDatabase` / `enemyBuildingDatabase` (`EnemyUnitDataSO`/`EnemyBuildingDataSO`).
- **Spore Brood**(외계종족, 유기체) - `Assets\prefabs\Spore_Brood\Unit`(Ripfang/Skitterwing/Spitter),
  `Assets\prefabs\Spore_Brood\Building`(Hive Core/Spawning Pit/Bio-Reactor).
  `RTSUnitController.sporeBroodUnitDatabase` / `sporeBroodBuildingDatabase`, ID 대역이 OC와 겹치지 않게
  분리돼 있고(`RTSUnitController.cs:77-82`), `GetEnemyUnitData()`/`GetEnemyBuildingData()`가 OC 쪽에서
  못 찾으면 자동으로 Spore Brood 쪽을 조회한다(doc/0444).
- 두 진영 모두 **같은 컴포넌트**(`EnemyUnitController`/`EnemyBuildingController`)를 쓴다 - 차이는 어떤
  SO 데이터베이스의 ID를 참조하느냐뿐, 클래스가 분리돼 있지 않다.
- `UnitData`(`UnitDataSO.cs:133`)에 이미 `Prefab` 필드가 있어서, `enemyUnitID`만 있으면
  `rtsController.GetEnemyUnitData(id).Prefab`로 바로 Instantiate 가능 (`UnitSpawner.Spawn()`과 동일한 패턴).

### 재사용 가능한 기존 기능
- `EnemyUnitController.MoveTo(Vector3)` / `AttackMoveTo(Vector3)` - 이동 중 사거리 안에 들어오는 상대와
  자동 교전(`EnemyAttackRange`). 공격대/별동대 이동 명령은 이 둘로 충분하고, 새 이동 로직이 필요 없다.
- `EnemyUnitController.HandleAttacked()` (`EnemyUnitController.cs:133-142`) - 개별 유닛은 이미 "사거리
  밖에서 맞으면 공격자 쪽으로 반격하러 감"을 자체적으로 한다. 이번에 새로 필요한 건 유닛 단위가 아니라
  **건물(기지)이 공격받았을 때 주변 유닛을 소집**하는 상위 레벨 반응 - 건물은 `HealthManager`만 있고
  반응 로직이 없음.
- `HealthManager.OnDamaged(int damage, Vector3 attackerPosition, AttackEffectType type, bool isEnemyAttacker)` -
  `isEnemyAttacker == false`일 때만 "플레이어에게 맞았다"는 뜻(`EnemyUnitController.HandleAttacked`와
  동일한 판정 재사용).
- `CaptureSystem` / `TerritoryZone` / `TerritoryManager.Zones` - 점령지 소유 상태(`CaptureOwner`)를 이미
  들고 있고, 유닛이 트리거 콜라이더 안에 서 있기만 하면 자동으로 점령이 진행된다. 별동대는 그냥 그 위치로
  `AttackMoveTo()` 시키면 끝 - 새 점령 로직 불필요.
- `EnemyBuildingController.ActiveBuildings`(static list) - 참고용 패턴. 이번엔 director가 "자기가 만든
  유닛"만 추적하면 되므로 전역 리스트는 새로 안 만들고 director 인스턴스 안에 로컬 리스트로 둔다(아래 참고).

## 설계 개요

### 컴포넌트: `EnemyAIDirector` (신규, MonoBehaviour 1개)
**미션(씬)마다, 적 기지 하나당 1개** 배치하는 방식 - 지금 게임이 "미션별로 손으로 세팅"하는 캠페인
구조이므로, 이 director도 인스펙터에서 미션별로 값을 채워 넣는 스크립트형 트리거에 가깝다(완전 자동
범용 RTS AI 경제 시뮬레이터를 새로 만드는 게 아님).

```
EnemyAIDirector
├─ [진영] EnemyFaction faction  (OC | SporeBrood)
├─ [스폰] Transform spawnPoint, List<int> attackUnitIDs (이 진영이 생산할 enemyUnitID 목록)
├─ [기지 방어] EnemyBuildingController homeBuilding (공격받으면 방어 소집 트리거)
├─ [공격 웨이브] List<float> waveTimes (초, 미션 시작 후 경과 시각 - ex: 300/600/900),
│               int waveSize, Transform attackTarget (보통 플레이어 본진)
├─ [수비대 유지] int garrisonTarget, float reinforceCheckInterval
├─ [점령지 탈환] List<CaptureSystem> raidTargets, float raidInterval, int raidSquadSize
└─ [진영별 동작 차이] bool assembleBeforeAttack 등 튜닝값 (아래 "진영별 차이" 참고)
```

내부적으로 자기가 스폰한 유닛만 담는 로컬 리스트(`List<EnemyUnitController> garrison`)를 들고 관리한다
(전역 레지스트리 불필요 - director 하나가 자기 기지 병력만 책임지는 구조).

### 4가지 요청 동작 → 구현 매핑

1. **시간에 맞춰 공격 웨이브** (`AttackWaveRoutine`, 코루틴)
   `waveTimes` 리스트를 순서대로 대기 → 매 웨이브마다 `garrison`에서 `waveSize`만큼(모자라면 새로
   스폰) 뽑아 `attackTarget` 방향으로 `AttackMoveTo()`. 이동 중 자동교전 되므로 도중에 만나는 아군과도
   알아서 싸운다.

2. **점령지 탈환 별동대** (`RaidRoutine`, 코루틴)
   `raidInterval`마다 `raidTargets` 중 `CurrentOwner != Enemy`인 곳을 하나 골라(Ally가 뺏어간 곳을
   Neutral보다 우선), `garrison`에서 `raidSquadSize`만큼 떼어 그 지점으로 `AttackMoveTo()`. 도착해서
   트리거 콜라이더 안에 서 있기만 하면 `CaptureSystem`이 알아서 점령을 진행시킨다 - 별도 로직 불필요.

3. **공격받으면 주변 병력 소집** (`homeBuilding.GetHealthManager().OnDamaged += HandleBaseAttacked`)
   `isEnemyAttacker == false`(플레이어에게 맞음)일 때만 반응. `garrison` 중 현재 Idle인 유닛들을 공격
   받은 위치로 `AttackMoveTo()`. (참고: 개별 유닛 자체도 `EnemyUnitController.HandleAttacked`로 이미
   자기 방어를 하므로, 이건 그 위에 "건물이 맞았을 때 근처 병력을 부른다"는 상위 반응만 추가하는 것.)

4. **죽은 유닛 보충 생산** (`ReinforceRoutine`, 코루틴)
   `reinforceCheckInterval`마다 `garrison`에서 null(죽은 유닛) 정리 → 부족한 만큼
   (`garrisonTarget - garrison.Count`) `attackUnitIDs`에서 골라 `spawnPoint`에 Instantiate, `garrison`에
   추가. 웨이브/별동대로 나간 병력도 죽으면 자연히 이 루틴이 다시 채워준다(별도 분기 불필요 - "죽어서
   빈 자리"와 "원정 나가서 빈 자리"를 구분하지 않고 그냥 목표 인원수만 유지).

### 진영별 차이 (OC vs Spore Brood)
클래스를 두 개로 나누지 않고, `EnemyFaction` enum 하나로 값/분기를 나눈다 - 실제로 갈라지는 부분이
"판단 로직 몇 줄" 수준이라 상속 구조까지는 과함(나중에 진짜 알고리즘 자체가 갈라지면 그때 분리):

| 항목 | OC (인간형) | Spore Brood (외계, 무리형) |
|---|---|---|
| 유닛/건물 프리팹 | `attackUnitIDs`가 OC ID 대역 참조 | Spore Brood ID 대역 참조 (같은 필드, 값만 다름) |
| `assembleBeforeAttack` | true - 웨이브 인원이 다 모일 때까지 스폰 지점에서 대기 후 한꺼번에 출발 | false - 스폰되는 즉시 개별적으로 `AttackMoveTo` (물량으로 끊임없이 밀어붙이는 느낌) |
| `reinforceCheckInterval` | 상대적으로 느림(예: 20초) | 빠름(예: 8초) - 유충 번식 컨셉 |
| `raidSquadSize` | 소규모 정예(예: 2~3) | 다수(예: 4~6) |

이 표의 구체 수치는 전부 인스펙터 필드라 밸런싱은 기획 값 조정만으로 가능 - 코드에 하드코딩하지 않음.

## 스코프 밖 (안 하는 것)
- 자원 채집/실제 생산 큐/테크트리 기반 AI 경제 시뮬레이션 - `EnemyBuildingController` 자체가 "생산 큐가
  필요 없는 껍데기"로 설계돼 있고(doc 상단 주석), 이번 요청도 "정해진 시간에 병력을 모아 보낸다"는
  스크립트형 동작이라 여기 안 맞음.
- 여러 적 기지가 서로 협조하는 상위 AI(다중 director 간 통신) - 미션에 기지가 여럿이면 director를 여러
  개 배치하면 되고, 지금 단계에서 서로 알 필요는 없음.
- 진형(포메이션)/그룹 경로탐색 - `AttackMoveTo()`를 유닛별로 개별 호출하는 것으로 충분(기존 플레이어
  유닛 이동도 개별 NavMeshAgent 방식).
- 난이도 자동 스케일링(플레이어 병력 규모에 따라 웨이브 크기 조절 등) - 필요해지면 나중에 추가.

## 확인이 필요한 부분 (설계 결정 전 확인 요청)
1. **`attackTarget`(공격 웨이브 목적지)**: 매 미션마다 인스펙터에서 플레이어 본진 위치를 직접 지정하는
   방식으로 충분한지, 아니면 "현재 발견된 플레이어 건물 중 가장 가까운 곳"처럼 동적으로 찾아야 하는지?
2. **웨이브 반복 여부**: `waveTimes`(예: 300/600/900)를 다 쓰고 나면 웨이브가 끝나는 건지, 아니면 마지막
   간격을 계속 반복해야 하는지? (제안: `List<float> waveTimes` + 선택적 `loopIntervalAfterLast`(0이면
   반복 안 함) 필드로 둘 다 지원)
3. **한 미션에 두 진영이 동시에 존재할 수 있는지**(예: OC와 Spore Brood가 같은 스테이지에 같이 등장) -
   가능하다면 director를 진영별로 각각 배치하면 되므로 설계엔 영향 없지만 확인 차 여쭤봄.
4. **원정 나간 병력(웨이브/별동대)이 임무 실패 시 복귀하는지, 아니면 그 자리에서 다음 명령(자동교전 등)을
   기다리며 소멸하는지** - 제안 설계는 "보내고 끝"(복귀 로직 없음, 죽으면 보충 루틴이 채움)인데 이걸로
   충분한지?

## 영향받는 파일 (구현 단계에서, 아직 미착수)
- 신규: `Assets\Scripts\System\EnemyAIDirector.cs` (또는 더 적합한 폴더가 있다면 지정 요청)
- 변경 없음: `EnemyUnitController.cs`, `EnemyBuildingController.cs`, `RTSUnitController.cs`,
  `CaptureSystem.cs` - 전부 기존 public API로 충분해서 손댈 필요 없음(위 "재사용 가능한 기존 기능" 참고)
