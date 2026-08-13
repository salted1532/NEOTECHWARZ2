# 0540 - EnemyAIDirector 인스펙터 <공통>/<OC>/<Spore Brood> 구분 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"진영별 동작 차이를 넣기 위해서 스크립트 인스펙터에서 <공통> 부분과 <OC> <Spore Brood>로 나눠서 일단
만들어줄래 현재 작성된 인스펙터들은 다 공통 내용인거 같네"

→ 지금 인스펙터 필드는 전부 진영 무관 공용이고(`faction` 필드 자체는 있지만 아무것도 안 갈라짐),
`attackWaves`/`raidSquadComposition`(doc/0539)처럼 **내용 자체가 진영마다 완전히 다른** 데이터가
있는데도 리스트가 하나뿐이라 director 하나에 한쪽 진영 구성만 담을 수 있음. 이번 요청은 인스펙터를
<공통>/<OC>/<Spore Brood> 세 구역으로 나눠서, 나중에 진영별로 다른 값이 필요한 자리를 명확히 하려는
것. 이 문서는 제안일 뿐, 아직 코드 수정 안 함.

## 조사 - 지금 필드 중 뭐가 진짜 "진영마다 달라야 하는지"
| 필드 | 진영별로 달라야 하는가 | 이유 |
|---|---|---|
| `spawnPoint` | 아니오 (공통) | 씬 위치 - 진영과 무관 |
| `homeBuildings`/`defenseRadius` | 아니오 (공통) | 방어 트리거/반경 - 진영과 무관 |
| `waveTimes` | 아니오 (공통) | 웨이브가 "언제" 오는지는 진영과 무관 |
| `attackWaves`(doc/0539) | **예** | OC 유닛 ID와 Spore Brood 유닛 ID가 완전히 다름 |
| `rallyPoint`/`rallyRadius`/`rallyTimeout` | 아니오 (공통) | 집결 메커니즘 자체는 진영과 무관 |
| `reinforceCheckInterval` | 아니오 (공통) | 보충 체크 주기 |
| `raidTargets`/`raidInterval` | 아니오 (공통) | 점령지 목록/주기는 진영과 무관 |
| `raidSquadComposition`(doc/0539) | **예** | 위와 동일한 이유 |
| `assembleBeforeAttack` | **예(제안)** | doc/0532 "진영별 차이" 표에서 이미 OC=true/SporeBrood=false를
권장값으로 제시했었음 - 지금은 인스펙터에서 수동으로 맞춰야 하는데, 진영별 필드로 나누면 `faction`만
바꿔도 자동으로 맞는 값이 따라옴(실수로 안 맞추는 사고 방지) |

## 설계안

### 진영별로 값 두 개씩 두고, `faction`에 따라 런타임에 하나를 고르는 프로퍼티로 노출
```csharp
[Header("<공통> 스폰")]
[SerializeField] private Transform spawnPoint;

[Header("<공통> 기지 방어")]
[SerializeField] private List<EnemyBuildingController> homeBuildings;
[SerializeField] private float defenseRadius = 15f;

[Header("<공통> 공격 웨이브 타이밍")]
[SerializeField] private List<float> waveTimes;

[Header("<OC> 공격 웨이브 구성")]
[SerializeField] private List<WaveComposition> attackWavesOC;
[Header("<Spore Brood> 공격 웨이브 구성")]
[SerializeField] private List<WaveComposition> attackWavesSporeBrood;

[Header("<공통> 집결지 (assembleBeforeAttack일 때만 사용)")]
[SerializeField] private Transform rallyPoint;
[SerializeField] private float rallyRadius = 3f;
[SerializeField] private float rallyTimeout = 15f;

[Header("<공통> 수비대 유지")]
[SerializeField] private float reinforceCheckInterval = 20f;

[Header("<공통> 점령지 탈환 타이밍")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;

[Header("<OC> 별동대 구성")]
[SerializeField] private List<UnitGroup> raidSquadCompositionOC;
[Header("<Spore Brood> 별동대 구성")]
[SerializeField] private List<UnitGroup> raidSquadCompositionSporeBrood;

[Header("<OC> 웨이브 집결 여부")]
[SerializeField] private bool assembleBeforeAttackOC = true;
[Header("<Spore Brood> 웨이브 집결 여부")]
[SerializeField] private bool assembleBeforeAttackSporeBrood = false;
```

내부 로직(`CurrentWaveComposition()`, `RaidRoutine()`, `LaunchWave()`)은 필드를 직접 안 보고, `faction`에
따라 골라주는 읽기 전용 프로퍼티를 거친다:
```csharp
private List<WaveComposition> AttackWaves =>
    faction == EnemyFaction.OC ? attackWavesOC : attackWavesSporeBrood;

private List<UnitGroup> RaidSquadComposition =>
    faction == EnemyFaction.OC ? raidSquadCompositionOC : raidSquadCompositionSporeBrood;

private bool AssembleBeforeAttack =>
    faction == EnemyFaction.OC ? assembleBeforeAttackOC : assembleBeforeAttackSporeBrood;
```
`faction` 필드 하나만 바꾸면 웨이브/별동대 구성과 집결 여부가 전부 자동으로 맞는 쪽으로 전환됨 - 이제
`EnemyFaction` enum이 "라벨"에서 "실제로 값을 갈라주는 스위치"가 됨(단, 여전히 코드 분기 로직 자체가
늘어나는 게 아니라 "어느 데이터를 볼지"만 고르는 것 - doc/0532가 경계했던 "진짜 알고리즘이 갈라지는"
상황은 아님).

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **`assembleBeforeAttack`도 진영별로 분리**: `assembleBeforeAttackOC = true` / `assembleBeforeAttackSporeBrood = false`(doc/0532 권장값 그대로 기본값).
2. **doc/0539 콘텐츠 초안을 필드 기본값으로 그대로 옮김**: `attackWavesOC`/`attackWavesSporeBrood`,
   `raidSquadCompositionOC`/`raidSquadCompositionSporeBrood`에 doc/0539 표 내용을 C# 필드 초기값으로 직접 입력.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 기존 코드 (필드)
```csharp
[Header("진영")]
[SerializeField] private EnemyFaction faction;

[Header("스폰")]
[SerializeField] private Transform spawnPoint;

[Header("기지 방어")]
[SerializeField] private List<EnemyBuildingController> homeBuildings;
[SerializeField] private float defenseRadius = 15f;

[Header("공격 웨이브")]
[SerializeField] private List<float> waveTimes;
[SerializeField] private List<WaveComposition> attackWaves;

[Header("집결지 (assembleBeforeAttack일 때만 사용)")]
[SerializeField] private Transform rallyPoint;
[SerializeField] private float rallyRadius = 3f;
[SerializeField] private float rallyTimeout = 15f;

[Header("수비대 유지")]
[SerializeField] private float reinforceCheckInterval = 20f;

[Header("점령지 탈환")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;
[SerializeField] private List<UnitGroup> raidSquadComposition;

[Header("진영별 동작 차이")]
[SerializeField] private bool assembleBeforeAttack = true;
```

### 변경 코드 (필드) - 전문은 `EnemyAIDirector.cs` 참고, 요지만
```csharp
[Header("진영 선택 - 아래 <OC>/<Spore Brood> 구역 중 어느 쪽을 쓸지 결정")]
[SerializeField] private EnemyFaction faction;

[Header("<공통> 스폰")]
[SerializeField] private Transform spawnPoint;

[Header("<공통> 기지 방어")]
[SerializeField] private List<EnemyBuildingController> homeBuildings;
[SerializeField] private float defenseRadius = 15f;

[Header("<공통> 공격 웨이브 타이밍")]
[SerializeField] private List<float> waveTimes;

[Header("<OC> 공격 웨이브 구성 (doc/0539 콘텐츠 초안)")]
[SerializeField] private List<WaveComposition> attackWavesOC = new List<WaveComposition> { /* 1~5차, doc/0539 표 그대로 */ };

[Header("<Spore Brood> 공격 웨이브 구성 (doc/0539 콘텐츠 초안)")]
[SerializeField] private List<WaveComposition> attackWavesSporeBrood = new List<WaveComposition> { /* 1~5차, doc/0539 표 그대로 */ };

[Header("<공통> 집결지 (AssembleBeforeAttack일 때만 사용)")]
[SerializeField] private Transform rallyPoint;
[SerializeField] private float rallyRadius = 3f;
[SerializeField] private float rallyTimeout = 15f;

[Header("<공통> 수비대 유지")]
[SerializeField] private float reinforceCheckInterval = 20f;

[Header("<공통> 점령지 탈환 타이밍")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;

[Header("<OC> 별동대 구성 (doc/0539 콘텐츠 초안)")]
[SerializeField] private List<UnitGroup> raidSquadCompositionOC = new List<UnitGroup>
{
    new UnitGroup { unitID = 2, count = 2 }, // Cyborg Soldier x2
    new UnitGroup { unitID = 3, count = 1 }, // Striker x1
};

[Header("<Spore Brood> 별동대 구성 (doc/0539 콘텐츠 초안)")]
[SerializeField] private List<UnitGroup> raidSquadCompositionSporeBrood = new List<UnitGroup>
{
    new UnitGroup { unitID = 10, count = 2 }, // Ripfang x2
    new UnitGroup { unitID = 11, count = 1 }, // Spitter x1
};

[Header("<OC> 웨이브 집결 여부")]
[SerializeField] private bool assembleBeforeAttackOC = true;

[Header("<Spore Brood> 웨이브 집결 여부")]
[SerializeField] private bool assembleBeforeAttackSporeBrood = false;

// faction에 따라 위 <OC>/<Spore Brood> 필드 중 하나를 골라주는 프로퍼티 - 이 셋을 거치면 나머지
// 로직은 진영을 몰라도 된다(doc/0540).
private List<WaveComposition> AttackWaves => faction == EnemyFaction.OC ? attackWavesOC : attackWavesSporeBrood;
private List<UnitGroup> RaidSquadComposition => faction == EnemyFaction.OC ? raidSquadCompositionOC : raidSquadCompositionSporeBrood;
private bool AssembleBeforeAttack => faction == EnemyFaction.OC ? assembleBeforeAttackOC : assembleBeforeAttackSporeBrood;
```

### 호출부 변경
`CurrentWaveComposition()`, `LaunchWave()`, `RaidRoutine()`, `Start()`, `ReinforceRoutine()`에서
`attackWaves`/`raidSquadComposition`/`assembleBeforeAttack`를 직접 참조하던 부분을 전부
`AttackWaves`/`RaidSquadComposition`/`AssembleBeforeAttack`(선택 프로퍼티)로 교체.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).

## 참고
필드 기본값(C# 객체 초기화 구문)으로 doc/0539 콘텐츠를 직접 넣어뒀기 때문에, 씬에 `EnemyAIDirector`를
새로 추가하면 인스펙터에 이 초안이 이미 채워진 채로 나타난다 - 미션 제작자는 그대로 쓰거나 필요한
항목만 조정하면 됨(빈 상태에서 처음부터 입력할 필요 없음).
