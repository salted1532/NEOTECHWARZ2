# 0444. 스포어 브루드 유닛/건물 스크립터블오브젝트 + 종족 Controller

**날짜:** 2026-08-07

## 요청 내용
> 0441 에 맞게 유닛,건물 스크립터블오브젝트 만들어주고 Spore Brood라는 종족의 Controller도 만들어줘
> 아직 조종하는 스크립트는 없으니깐 OC의 EnemyController를 참고해서 동일하게 작동하도록 만들어줘

## 조사 내용

### 1) Spore Brood 프리팹은 이미 존재하고, 이미 "OC의 EnemyController"가 붙어 있음

`Assets/prefabs/Spore_Brood/Unit/{Ripfang, Spitter, Raven}.prefab`,
`Assets/prefabs/Spore_Brood/Building/{Hive_Core, Spawning Pit, Bio_Reactor}.prefab` 6개 프리팹을 열어보니
전부 이미 `EnemyUnitController` / `EnemyBuildingController` 컴포넌트가 붙어 있음(자식의 `EnemyAttackRange`,
`HealthManager`, `UnitEffects`, `UnitAudio` 등도 동일). 즉 OC 프리팹을 복제해서 만든 것으로 보이고,
**"조종하는 스크립트"는 이미 OC와 완전히 동일한 것이 붙어 있는 상태**임.

`Docs/EnemyUnitController.md` / `doc/0231-enemycontroller-armor-attackdamage.md` 등을 보면 이 클래스는
애초에 "OC 전용"이 아니라 **`enemyUnitID`로 데이터 SO를 조회해 스스로 스탯을 채우는 범용 클래스**로
설계돼 있음(주석: "OC(오메가 코퍼레이션) 등 적 진영 유닛 데이터베이스" — "OC 등"이라고 이미 다른
진영도 같은 구조를 쓸 걸 전제함). 코드 어디에도 "OC"라는 문자열/특수 분기는 없고, 전부 ID → SO 조회
패턴뿐. 따라서 **새 Controller 클래스를 따로 만들 필요가 없고(이미 있는 것과 100% 동일하게 작동함),
새로 필요한 건 그 ID가 가리킬 "데이터"**임.

### 2) 진짜 문제 — ID가 OC 데이터와 충돌해서 엉뚱한 스탯을 불러옴

프리팹에 박혀 있는 현재 ID 값:

| 프리팹 | 필드 | 현재 값 | 문제 |
|---|---|---|---|
| Ripfang.prefab | enemyUnitID | 2 | Spitter와 충돌 |
| Spitter.prefab | enemyUnitID | 2 | Ripfang과 충돌 |
| Raven.prefab (스키터윙) | enemyUnitID | 8 | OC의 실제 유닛 "Raven"(ID 8, NTA Firehawk 대응)과 충돌 |
| Hive_Core.prefab | enemyBuildingID | 1 | OC "Omega Core"(ID 1)와 충돌 |
| Spawning Pit.prefab | enemyBuildingID | 3 | OC "Cyber Foundry"(ID 3)와 충돌 |
| Bio_Reactor.prefab | enemyBuildingID | 6 | OC "Neural Lab"(ID 6)와 충돌 |

그리고 `Assets/prefabs/Game/GameManager.prefab`(모든 미션 씬이 공유, 씬별 override 없음 확인함)의
`RTSUnitController`는 `enemyUnitDatabase` / `enemyBuildingDatabase` 필드가 **딱 하나**뿐이고 지금은
"OC Unit Data SO" / "OC Building Data SO"만 꽂혀 있음. 즉 지금 상태로 Spore Brood 프리팹을 씬에
배치하면 `EnemyUnitController.Start()`가 자기 `enemyUnitID`로 **OC의 데이터**를 그대로 불러와
버림(예: Ripfang이 OC의 "Cyborg Soldier" 스탯을 뒤집어씀) — "안 움직이는" 게 아니라 "엉뚱하게
움직이는" 상태가 될 것.

### 3) 데이터 저장 위치 — 새 SO 에셋 vs 기존 OC SO 에셋에 추가

`EnemyUnitDataSO`/`EnemyBuildingDataSO` 클래스 자체가 "OC 등 적 진영"을 위한 범용 컨테이너로 설계돼
있고, `RTSUnitController`는 씬당 이 데이터베이스를 **하나만** 참조하도록 돼 있음(`GameManager.prefab`
공유, 씬별 override 없음). 두 가지 방법이 있음:

- **A안(제안) — 기존 "OC Unit/Building Data SO" 에셋에 Spore Brood 항목 추가.** 새 SO 에셋도, 새
  C# 클래스도, `RTSUnitController.cs` 수정도 필요 없음. ID만 안 겹치게 새로 배정(유닛 10/11/12,
  건물 7/8/9)하면 끝. 클래스 설계(주석 "OC 등")가 애초에 이 용도를 염두에 뒀던 것과도 맞음.
- **B안 — "Spore Brood Unit/Building Data SO" 신규 에셋 + `RTSUnitController`에 필드 추가하고
  `GetEnemyUnitData`가 두 데이터베이스를 순서대로 조회하도록 수정.** 진영별로 에셋이 분리돼서
  이름은 깔끔하지만, 코드 수정이 새로 생기고 지금 구조(씬당 데이터베이스 1개)와 어긋남 — 나중에
  실제로 같은 씬에 OC와 Spore Brood가 **동시에** EnemyUnitController로 등장해야 하는 게 아니라면
  불필요한 확장.

A안으로 감(YAGNI) — 지금 5스테이지는 "OC 부재, 외계종족 단독"(`doc/0442`)이라 한 씬에 두 진영이
`EnemyUnitController`로 동시에 존재할 계획이 없음. "OC Unit Data SO"라는 이름이 이제 OC 전용이
아니게 되는 점은 있지만, 애초에 주석이 "OC 등 적 진영"이라 이름과 실제 용도가 어긋나는 것도 아님.

## 제안하는 변경

### 1) `OC Unit Data SO.asset`에 Spore Brood 유닛 3종 추가 (신규 ID 10/11/12)

`doc/0441` 표 그대로:

| ID | unitName | tier | armorType | sizeType | hp | attackDamge | attackRange | attackSpeed | canAttackGround | canAttackAir | attackDelivery | mineral | gas | population | productionTime | Prefab |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 10 | 립팽 (Ripfang) | 0 | Light | Small | 60 | 9 | 2 | 0.5 | ✔ | ✘ | Hitscan | 45 | 0 | 1 | 10 | Ripfang.prefab |
| 11 | 스피터 (Spitter) | 0 | Light | Medium | 50 | 11 | 13 | 1.1 | ✔ | ✔ | Projectile | 80 | 20 | 2 | 20 | Spitter.prefab |
| 12 | 스키터윙 (Skitterwing) | 0 | Light | Medium | 65 | 8 | 11 | 0.9 | ✔ | ✔ | Projectile | 95 | 35 | 2 | 26 | Raven.prefab |

Icon / soundBank는 아직 제작된 아이콘·사운드뱅크 에셋이 없어 비워둠(0441 "남은 작업" 항목, 비워두면
조용함/기본 문구만 표시 — 기존 패턴과 동일). requiredBuildingID는 0(생산 건물 티어 구분 없음 — 셋 다
산란구덩이 하나에서 생산).

### 2) `OC Building Data SO.asset`에 Spore Brood 건물 3종 추가 (신규 ID 7/8/9)

| ID | Name | Size | mineral | gas | maxpopulationamount | productionTime | hp | Prefab |
|---|---|---|---|---|---|---|---|---|
| 7 | 하이브 코어 (Hive Core) | 4x4 | 400 | 0 | 10 | 60 | 1600 | Hive_Core.prefab |
| 8 | 산란구덩이 (Spawning Pit) | 3x3 | 150 | 50 | 0 | 40 | 900 | Spawning Pit.prefab |
| 9 | 바이오리액터 (Bio-Reactor) | 2x2 | 150 | 0 | 8 | 30 | 700 | Bio_Reactor.prefab |

requiredBuildingID는 0(선행 건물 없음, 0441과 동일). Icon도 아직 없어 비워둠. `하이브 코어`의
"자연 재생"과 `바이오리액터` 파괴 페널티는 0441에서도 "신규 로직 필요"로 남겨둔 항목이라 이번
범위에는 포함하지 않음(스탯 데이터만).

### 3) 6개 Spore Brood 프리팹의 ID 필드 수정

| 프리팹 | 필드 | 변경 |
|---|---|---|
| Ripfang.prefab | enemyUnitID | 2 → **10** |
| Spitter.prefab | enemyUnitID | 2 → **11** |
| Raven.prefab | enemyUnitID | 8 → **12** |
| Hive_Core.prefab | enemyBuildingID | 1 → **7** |
| Spawning Pit.prefab | enemyBuildingID | 3 → **8** |
| Bio_Reactor.prefab | enemyBuildingID | 6 → **9** |

### 4) Controller 스크립트 — 변경 없음

`EnemyUnitController.cs` / `EnemyBuildingController.cs`는 그대로 둠. Spore Brood 프리팹이 이미 이
컴포넌트를 그대로 쓰고 있고, 위 1~3만 맞추면 OC 유닛/건물과 완전히 동일한 방식(자동 교전/이동/
공격-이동, 피격 반격, 안개 연동, 선택 마커 등)으로 작동함 — 그게 "OC의 EnemyController를 참고해서
동일하게 작동"의 실제 의미임.

## 확인 필요 사항 → 결정

**A안(기존 OC SO 에셋에 항목 추가) vs B안(별도 SO 에셋 + `RTSUnitController` 확장)** 중 사용자가
**B안**을 선택함 — 별도의 "Spore Brood Unit/Building Data SO" 에셋을 새로 만들고,
`RTSUnitController`가 OC 데이터베이스에서 못 찾으면 스포어 브루드 데이터베이스를 이어서 조회하도록
확장함. (같은 씬에 OC와 스포어 브루드가 `EnemyUnitController`로 동시에 존재하게 될 가능성을
열어두기 위함으로 보임.) ID는 그래도 OC 쪽과 겹치지 않게 배정함(유닛 10/11/12, 건물 7/8/9) —
두 데이터베이스를 "OC 우선 조회 → 없으면 스포어 브루드 조회" 순서로 병합 검색하기 때문에, ID가
겹치면 OC 쪽 항목에 가려져서 스포어 브루드 데이터를 영영 못 찾는 문제가 생김.

기타 확인 사항:
- `Bio_Reactor.prefab`에 `ResearchQueue` 컴포넌트가 붙어있는 게 확인됨(아마 OC "Neural Lab"을
  복제한 흔적) — 적 건물 껍데기는 생산/연구 큐를 안 쓰므로(`EnemyBuildingController` 주석 참고)
  있어도 동작엔 영향 없어 이번 범위에서는 그대로 둠.
- Icon/soundBank는 자산이 없어 비워둠 — 나중에 아이콘/효과음 에셋이 준비되면 그때 채우면 됨.

## 구현 (승인 후 적용됨)

### `RTSUnitController.cs` — 스포어 브루드 데이터베이스 필드 + 병합 조회 추가

**Before:**
```csharp
    // OC(적 진영) 유닛 데이터베이스 - EnemyUnitController.Start()가 자기 enemyUnitID로 스탯을 조회할 때 사용 (doc/0232).
    [SerializeField]
    private EnemyUnitDataSO enemyUnitDatabase;
    // OC(적 진영) 건물 데이터베이스 - EnemyBuildingController.Start()가 자기 enemyBuildingID로 이름/체력을
    // 조회할 때 사용 (doc/0245).
    [SerializeField]
    private EnemyBuildingDataSO enemyBuildingDatabase;
    ...
    // enemyUnitID로 OC Unit Data SO(EnemyUnitDataSO)에서 UnitData를 조회한다 (EnemyUnitController가
    // 자기 자신의 스탯을 SO에서 가져올 때 사용, doc/0232).
    public UnitData GetEnemyUnitData(int enemyUnitID) =>
        enemyUnitDatabase != null ? enemyUnitDatabase.unitData.Find(d => d.ID == enemyUnitID) : null;

    // enemyBuildingID로 OC Building Data SO(EnemyBuildingDataSO)에서 BuildingData를 조회한다
    // (EnemyBuildingController가 자기 자신의 이름/체력을 SO에서 가져올 때 사용, doc/0245).
    public BuildingData GetEnemyBuildingData(int enemyBuildingID) =>
        enemyBuildingDatabase != null ? enemyBuildingDatabase.buildingData.Find(d => d.ID == enemyBuildingID) : null;
```

**After:**
```csharp
    // OC(적 진영) 유닛 데이터베이스 - EnemyUnitController.Start()가 자기 enemyUnitID로 스탯을 조회할 때 사용 (doc/0232).
    [SerializeField]
    private EnemyUnitDataSO enemyUnitDatabase;
    // OC(적 진영) 건물 데이터베이스 - EnemyBuildingController.Start()가 자기 enemyBuildingID로 이름/체력을
    // 조회할 때 사용 (doc/0245).
    [SerializeField]
    private EnemyBuildingDataSO enemyBuildingDatabase;
    // 스포어 브루드(외계종족) 유닛/건물 데이터베이스 - OC와는 별개 진영이라 SO 에셋도 분리(doc/0444).
    // enemyUnitID/enemyBuildingID는 OC 쪽과 겹치지 않게 배정돼 있으므로, OC 쪽에서 못 찾으면 이쪽에서 조회한다.
    [SerializeField]
    private EnemyUnitDataSO sporeBroodUnitDatabase;
    [SerializeField]
    private EnemyBuildingDataSO sporeBroodBuildingDatabase;
    ...
    // enemyUnitID로 OC Unit Data SO(EnemyUnitDataSO)에서 UnitData를 조회한다 (EnemyUnitController가
    // 자기 자신의 스탯을 SO에서 가져올 때 사용, doc/0232). OC 쪽에 없으면 스포어 브루드 쪽에서 조회한다(doc/0444).
    public UnitData GetEnemyUnitData(int enemyUnitID) =>
        enemyUnitDatabase?.unitData.Find(d => d.ID == enemyUnitID) ??
        sporeBroodUnitDatabase?.unitData.Find(d => d.ID == enemyUnitID);

    // enemyBuildingID로 OC Building Data SO(EnemyBuildingDataSO)에서 BuildingData를 조회한다
    // (EnemyBuildingController가 자기 자신의 이름/체력을 SO에서 가져올 때 사용, doc/0245). OC 쪽에 없으면
    // 스포어 브루드 쪽에서 조회한다(doc/0444).
    public BuildingData GetEnemyBuildingData(int enemyBuildingID) =>
        enemyBuildingDatabase?.buildingData.Find(d => d.ID == enemyBuildingID) ??
        sporeBroodBuildingDatabase?.buildingData.Find(d => d.ID == enemyBuildingID);
```

### 신규 SO 에셋

- `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` (`EnemyUnitDataSO`, 기존
  클래스 재사용) — ID 10=립팽/11=스피터/12=스키터윙, doc/0441 표의 스탯 그대로. Prefab 필드는 각각
  `Ripfang.prefab`/`Spitter.prefab`/`Raven.prefab` 참조. Icon/soundBank는 비워둠.
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Building Data SO.asset` (`EnemyBuildingDataSO`,
  기존 클래스 재사용) — ID 7=하이브 코어/8=산란구덩이/9=바이오리액터, doc/0441 표 그대로. Prefab
  필드는 각각 `Hive_Core.prefab`/`Spawning Pit.prefab`/`Bio_Reactor.prefab` 참조.

### `GameManager.prefab` — RTSUnitController에 새 SO 2개 연결

```yaml
  sporeBroodUnitDatabase: {fileID: 11400000, guid: 667a2ec9644cf5146bd3ab04db1bffb5, type: 2}
  sporeBroodBuildingDatabase: {fileID: 11400000, guid: ddd9c087e3234c218fdc12f8209cff57, type: 2}
```

`GameManager.prefab`은 모든 미션 씬이 공유하는 단일 프리팹(씬별 override 없음)이라, 여기 한 곳만
연결하면 모든 씬에서 스포어 브루드 유닛/건물이 자동으로 조회 가능해짐.

### 6개 프리팹 ID 수정 (충돌 해소)

| 프리팹 | 필드 | Before → After |
|---|---|---|
| Ripfang.prefab | enemyUnitID | 2 → **10** |
| Spitter.prefab | enemyUnitID | 2 → **11** |
| Raven.prefab | enemyUnitID | 8 → **12** |
| Hive_Core.prefab | enemyBuildingID | 1 → **7** |
| Spawning Pit.prefab | enemyBuildingID | 3 → **8** |
| Bio_Reactor.prefab | enemyBuildingID | 6 → **9** |

각 프리팹의 `enemyName`/`buildingName` 인스펙터 기본값도 OC 쪽 복제 흔적("Cyborg Soldier", "Raven"
등 — 실제로는 `ApplyUnitData`/`ApplyBuildingData`가 Start() 시점에 SO 값으로 덮어써서 게임 동작에는
영향 없었음)을 정리해서 실제 유닛/건물 이름으로 바꿔둠. Icon도 준비된 아이콘이 없어 비워둠(OC
아이콘이 잘못 남아있던 것 제거).

### Controller 스크립트 — 변경 없음

`EnemyUnitController.cs` / `EnemyBuildingController.cs`는 그대로 둠(제안 그대로).

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 0`.
- Unity 콘솔 Error 로그 0건 (새 `.asset` 파일 2개의 수동 작성 YAML이 정상 파싱됨).

## 변경된 파일

- `Assets/Scripts/System/RTSUnitController.cs` (필드 2개 추가, `GetEnemyUnitData`/`GetEnemyBuildingData` 병합 조회로 수정)
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` (신규)
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset.meta` (신규, Unity 자동 생성)
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Building Data SO.asset` (신규)
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Building Data SO.asset.meta` (신규)
- `Assets/prefabs/Game/GameManager.prefab` (새 SO 2개 필드 연결)
- `Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab` (enemyUnitID/enemyName 수정)
- `Assets/prefabs/Spore_Brood/Unit/Spitter.prefab` (enemyUnitID/enemyName 수정)
- `Assets/prefabs/Spore_Brood/Unit/Raven.prefab` (enemyUnitID/enemyName 수정)
- `Assets/prefabs/Spore_Brood/Building/Hive_Core.prefab` (enemyBuildingID/buildingName/icon 수정)
- `Assets/prefabs/Spore_Brood/Building/Spawning Pit.prefab` (enemyBuildingID/buildingName/icon 수정)
- `Assets/prefabs/Spore_Brood/Building/Bio_Reactor.prefab` (enemyBuildingID/buildingName/icon 수정)
- (신규 C# Controller 스크립트 없음 — 기존 `EnemyUnitController`/`EnemyBuildingController` 재사용)
