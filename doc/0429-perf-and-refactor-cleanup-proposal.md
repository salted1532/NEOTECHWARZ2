# 0429. 전체 코드 리팩토링/최적화 제안 (2차: 성능/불필요한 코드)

- 날짜: 2026-08-05
- 상태: **A그룹 적용 완료** (컴파일 확인: 에러 0개, 경고 34개는 전부 기존부터 있던 obsolete API 경고). B그룹은 사용자 확인 결과 둘 다 보류.

## 요청 내용

> 이제 코드 전체를 읽고 리팩토링을 해줘. 기능적으로 동일한 작동은 해야하고 비효율적으로 작동하거나
> 불필요한 코드는 있는지 최적화 해줘 해당하는 내용으로 변경안을 작성해줘 존재하는 모든 코드를 확인해줘

`doc/0428`(디버그 로그/데드 코드 정리)과는 별도의, 더 깊은 패스: 매 프레임 비효율, 죽은 코드,
중복 로직을 찾아 기능 변경 없이 정리한다. [[confirm-before-implementing-rule]]에 따라 적용 전
제안서로 먼저 정리.

## 조사 내용

`Assets/Scripts` 전체(80개 파일, 0428 정리 후 약 15,700줄)를 다시 전수 조사했다. 이번엔 ①매
프레임(Update/FixedUpdate) 루프에서 반복되는 불필요한 `GetComponent`/`FindObjectsByType`/할당
②안 쓰는 메서드/필드 ③거의 동일한 로직이 여러 곳에 중복된 부분에 집중했다. LINQ 사용은 프로젝트
전체에 전혀 없었고(0건), `AttackRange`/`EnemyAttackRange`/`CameraControl`/`ResourceNode` 등은
이미 캐싱·dirty-flag 패턴으로 잘 최적화돼 있어 추가로 손댈 곳이 없었다.

발견 사항은 확신도/영향도에 따라 두 그룹으로 나눴다:
- **A그룹**: 안전하고 명확한 개선 (기계적 변경, 동작 동일)
- **B그룹**: 구조적 변경이라 규모/방향에 대한 사용자 판단이 필요한 것, 또는 이득이 너무 작아
  건드리는 게 오히려 손해인 것

---

## A그룹: 적용 제안

### 1) `Stage0Objectives.cs` — 매 프레임 씬 전체 스캔

**문제**: `Update()`가 매 프레임 `FindObjectsByType<EnemyUnitController>(...)`로 씬 전체를
스캔해 배열을 새로 할당한다. 이 목표 텍스트는 튜토리얼 체크리스트 표시용이라 초당 갱신이 전혀
필요 없다(사람 눈에 보이는 텍스트일 뿐, 즉각적인 게임플레이 판정에 안 쓰임 - 주목표 3개
[거점점령/트루퍼10기/병영건설]만 승리 조건이고 이건 서브목표라 승리와 무관).

**기존 코드**:
```csharp
    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return; // 이미 승패가 갈렸으면 더 이상 갱신하지 않음

        int trooperCount = CountAliveUnits(AssaultTrooperUnitID);
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;

        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = trooperCount >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);
        bool enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        bool oreSecured = oreAmount >= RequiredOre;
```
**변경 코드**: 서브목표 스캔에 0.5초 간격 타이머만 추가 (그 외 로직/판정 결과는 완전히 동일):
```csharp
    private float enemyScanTimer;
    private bool enemiesCleared;

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return; // 이미 승패가 갈렸으면 더 이상 갱신하지 않음

        int trooperCount = CountAliveUnits(AssaultTrooperUnitID);
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;

        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = trooperCount >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);

        // 서브목표(승리 조건과 무관한 체크리스트 표시용)라 초당 갱신이 필요 없음 - 0.5초마다만 다시 스캔.
        enemyScanTimer -= Time.deltaTime;
        if (enemyScanTimer <= 0f)
        {
            enemyScanTimer = 0.5f;
            enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        }

        bool oreSecured = oreAmount >= RequiredOre;
```
(이 아래 `SetObjectiveText` 호출부/승리 조건 체크는 변경 없음)

### 2) `RTSUnitController.cs`의 `UpdateUI()` — 선택된 대상의 `HealthManager`를 매 프레임 `GetComponent`로 재조회

**문제**: 무언가 선택돼 있는 동안 `UpdateUI()`가 매 프레임 호출되는데(1749/1770/1873/1892/1930번
줄), 5곳 모두 `selected대상.GetComponent<HealthManager>()`를 그 자리에서 매번 새로 조회한다.
`EnemyUnitController`/`BaseStructure`는 이미 자기 자신 안에서는 `healthManager` 필드로 캐싱해
쓰고 있지만 그 값을 밖에 꺼내주는 getter가 없었고, `UnitController`/`BuildingController`/
`EnemyBuildingController`는 애초에 캐싱 자체가 없었다.

**변경**: 다섯 컨트롤러 클래스 모두 `Awake()`/`Start()`에서 한 번만 `GetComponent`하고, `GetHealthManager()` getter로 노출 → `RTSUnitController`는 그 getter를 호출.

**`Unit/UnitController.cs`**
```csharp
// 기존 (178~180번 줄 필드 선언부)
    private AttackRange attackRange;         // 사거리 내 교전 대상 존재 여부 조회용 (자식 컴포넌트)
    private UnitEffects unitEffects;         // 공격/피격 이펙트 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private UnitAudio unitAudio;             // 공격/채취 SFX 재생용 (없을 수 있는 옵셔널 컴포넌트)
```
```csharp
// 변경 - HealthManager 필드 추가
    private AttackRange attackRange;         // 사거리 내 교전 대상 존재 여부 조회용 (자식 컴포넌트)
    private UnitEffects unitEffects;         // 공격/피격 이펙트 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private UnitAudio unitAudio;             // 공격/채취 SFX 재생용 (없을 수 있는 옵셔널 컴포넌트)
    private HealthManager healthManager;     // Info_panel 표시용 - Awake에서 한 번만 캐싱
```
```csharp
// 기존 (Awake() 253~261번 줄)
    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();
        turretController = GetComponentInChildren<TurretController>();
        unitEffects = GetComponent<UnitEffects>();
        unitAudio = GetComponent<UnitAudio>();
        laserBeamAttack = GetComponent<LaserBeamAttack>();
        TryGetComponent(out projectileAttack);
```
```csharp
// 변경
    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();
        turretController = GetComponentInChildren<TurretController>();
        unitEffects = GetComponent<UnitEffects>();
        unitAudio = GetComponent<UnitAudio>();
        laserBeamAttack = GetComponent<LaserBeamAttack>();
        healthManager = GetComponent<HealthManager>();
        TryGetComponent(out projectileAttack);
```
```csharp
// 기존 (1958번 줄, ApplyUnitData 안)
        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
```
```csharp
// 변경 - 캐싱된 필드 재사용
        healthManager?.InitializeHealth(data.hp);
    }

    public HealthManager GetHealthManager() => healthManager;
```

**`Building/BuildingController.cs`**
```csharp
// 기존 (32~33번 줄)
    private RTSUnitController rtsController;
    private Coroutine markerFlashRoutine;
```
```csharp
// 변경
    private RTSUnitController rtsController;
    private Coroutine markerFlashRoutine;
    private HealthManager healthManager; // Info_panel 표시용 - Start에서 한 번만 캐싱
```
```csharp
// 기존 (Start() 81~87번 줄)
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
```
```csharp
// 변경
    void Start()
    {
        buildingMarker.SetActive(false);
        healthManager = GetComponent<HealthManager>();

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
```
그리고 클래스 아무 곳에나(다른 `public Get...` 메서드들 옆) 추가:
```csharp
    public HealthManager GetHealthManager() => healthManager;
```

**`FogOfWar/Enemy/EnemyBuildingController.cs`**
```csharp
// 기존 (36~38번 줄)
    private RTSUnitController rtsController;
    private PlacementSystem placementSystem;
    private float groundOffset;
```
```csharp
// 변경
    private RTSUnitController rtsController;
    private PlacementSystem placementSystem;
    private float groundOffset;
    private HealthManager healthManager; // Info_panel 표시용 - Start에서 한 번만 캐싱
```
```csharp
// 기존 (Start() 43~50번 줄)
    private void Start()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
        fogWar = FindFirstObjectByType<csFogWar>();
```
```csharp
// 변경
    private void Start()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
        fogWar = FindFirstObjectByType<csFogWar>();
```
```csharp
// 기존 (128번 줄, ApplyBuildingData 안)
        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
```
```csharp
// 변경
        healthManager?.InitializeHealth(data.hp);
    }

    public HealthManager GetHealthManager() => healthManager;
```

**`FogOfWar/Enemy/EnemyUnitController.cs`** — 필드/캐싱은 이미 있음, getter만 추가:
```csharp
// 추가 (GetIcon()/GetEnemyName() 같은 다른 Get 메서드들 옆에)
    public HealthManager GetHealthManager() => healthManager;
```

**`Building/BaseStructure.cs`** — 필드/캐싱은 이미 있음, getter만 추가:
```csharp
// 추가 (다른 Get 메서드들 옆에)
    public HealthManager GetHealthManager() => healthManager;
```

**`System/RTSUnitController.cs`** — 5곳의 호출부 교체:
```csharp
// 1749번 줄 - 기존
                    uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor(),
// 변경
                    uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetAttackDamage(), unit.GetArmor(),

// 1770번 줄 - 기존
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetComponent<HealthManager>());
// 변경
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetHealthManager());

// 1873번 줄 - 기존
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetComponent<HealthManager>(), enemy.GetAttackDamage(), enemy.GetArmor(),
// 변경
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetHealthManager(), enemy.GetAttackDamage(), enemy.GetArmor(),

// 1892번 줄 - 기존
                    uIController.ShowInfoPanel(selectedEnemyBuilding.GetIcon(), selectedEnemyBuilding.GetBuildingName(), selectedEnemyBuilding.GetComponent<HealthManager>());
// 변경
                    uIController.ShowInfoPanel(selectedEnemyBuilding.GetIcon(), selectedEnemyBuilding.GetBuildingName(), selectedEnemyBuilding.GetHealthManager());

// 1930번 줄 - 기존
                        selectedBaseStructure.GetComponent<HealthManager>());
// 변경
                        selectedBaseStructure.GetHealthManager());
```

### 3) `Unit/UnitController.cs:352` — 죽은 디버그 로그 (0428에서 놓친 것)

**기존 코드**:
```csharp
                Debug.Log("공중유닛 도착 !");
```
**변경 코드**: 삭제. (공중 유닛이 이동 도착할 때마다 찍히는, 정보 가치 없는 leftover 프린트)

### 4) `System/RTSUnitController.cs:2045~2056` — 안 쓰는 빈 메서드

**확인**: `TestMethod`를 씬(`Assets/Scenes`)·프리팹(`Assets/prefabs`) 전체에서 검색해도 참조가
전혀 없음 (버튼 OnClick 등 인스펙터 바인딩도 없음) — 안전하게 삭제 가능.

**기존 코드**:
```csharp
    #region Test용

    /// <summary>
    /// 테스트용
    /// </summary>
    //UI 버튼 연결 테스트용
    public void TestMethod()
    {

    }

    #endregion
```
**변경 코드**: 통째로 삭제.

---

## B그룹: 판단이 필요함 (규모가 크거나, 이득 대비 리스크/디프가 안 맞음)

### 1) `UnitController.cs` ↔ `EnemyUnitController.cs` 중복 로직 (약 250~300줄) — 별도 결정 필요

두 파일은 "플레이어 유닛"과 "적 유닛"에 대해 사실상 같은 로직을 각자 따로 구현하고 있다:
- 공중 유닛 이동 처리 (`Update()` 내 약 45줄)
- 추격 도달-불가 상태 머신 (`MoveAgentTo`/`IsPositionReachable`/`ChaseTarget`, 약 90줄) — 방금
  0428/0429에서 로그를 정리한 그 코드
- 공격 판정 (`Attack`/`CalculateFinalDamage`/`GetTargetArmor`/`GetTargetSizeType`/
  `GetTargetArmorType`/`IsAirborne`, 약 120줄)
- 공중 목표 위치/지면 높이 샘플링/Y축만 회전 (`AirTargetPosition`/`SampleGroundHeight`/
  `RotateYOnly`, 약 30줄)
- 공격 대상 지정 마커 깜빡임 (`FlashMarker`/`FlashMarkerRoutine`, 약 30줄)

공유 베이스 클래스나 static 헬퍼(예: `CombatMath`, `MovementAgentHelper`)로 뽑아내면 중복을
제거할 수 있지만, 두 클래스의 상속/구성 구조 자체를 건드리는 **구조적 변경**이라 "기계적으로
안전한 리팩토링"의 범위를 넘는다. 실수로 한쪽만 고치고 다른 쪽을 놓치는 등 향후 유지보수
리스크와도 관련 있는 부분이라, 진행 여부와 방식(베이스 클래스로 합칠지 / static 헬퍼로만 뽑을지)
을 먼저 정하고 싶다면 알려달라 — 별도 제안서로 설계부터 다시 잡는 게 안전하다.

### 2) `UnitController.cs`의 `depositTargetTransform.GetComponent<BuildingController>()` 매 프레임 재조회 (1748번 줄)

일꾼이 자원을 들고 반납 건물로 걸어가는 동안(`GatherTick`의 `MovingToBase` 케이스) 매 프레임
`GetComponent`를 호출한다. `depositTargetTransform`이 바뀌는 지점 5곳(1486/1588/1621/1736/1756
번 줄) 모두에서 `BuildingController`를 같이 캐싱해두면 없앨 수 있지만, 손대는 지점이 다섯 곳으로
퍼져 있는 데 비해 절감되는 비용은 "일꾼 1마리가 반납 이동 중일 때 GetComponent 1회/프레임" 정도로
작다. 이득 대비 디프가 크다고 판단해 기본 제안에서는 뺐다 — 원하면 포함해서 처리 가능.

### 3) 그 외 검토했지만 그대로 두는 게 나은 것들

- **`RTSUnitController.cs` 1798/1806/1814/1822번 줄**: `GetProductionQueue()` 호출 시 내부에서
  `GetRepresentativeBuilding()`을 다시 계산한다(1760번 줄에서 이미 한 번 구했음에도). 하지만
  `GetRepresentativeBuilding()` 자체가 선택된 건물 1~수 개를 우선순위로 훑는 가벼운 연산이라
  체감 이득이 사실상 없음 - 건드리지 않는 걸 권장.
- **`UIController.cs` 460/540번 줄**: 생산/연구 대기열 슬롯(최대 5개)마다 취소 버튼 클로저를 매
  갱신마다 새로 만든다. 슬롯 수가 5개로 고정 상한이라 GC 부담이 무시할 수준 - 건드리지 않는 걸
  권장.

---

## 요약 / 영향받는 파일

**A그룹 적용 시** (기능 동일, 매 프레임 비용/죽은 코드만 감소):
- `Assets/Scripts/System/Stage0Objectives.cs`
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/System/RTSUnitController.cs`

**B그룹**: 기본적으로 미적용. 1번(중복 로직 통합)은 진행 여부/방식을 먼저 정해야 하고, 2번(반납
대상 캐싱)은 원하면 같이 처리 가능.

아직 프로젝트 파일에는 아무것도 적용하지 않음 (제안 단계).
