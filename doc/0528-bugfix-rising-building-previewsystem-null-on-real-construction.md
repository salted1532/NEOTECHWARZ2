# 0528 - 실제 건설 흐름에서 상승 애니메이션이 재생되지 않는 버그

## 결과
사용자 확인 후 제안대로 구현 완료. `BaseStructure.cs`에 위 "코드 변경" 섹션과 동일하게 반영됨.

실제 버그 재현 경로(`worker.GoBuild()` → `BuildTick()`(Update() 내부) → `onArrived` 콜백에서
`Instantiate`+`Initialize` 동기 호출)를 Play Mode에서 그대로 재현해 수정 전/후를 직접 검증:
- 수정 전에는 이 경로로 테스트하지 못했음(최초 검증 때는 에디터 스크립트에서 직접 호출해 우연히
  프레임 경계를 지나쳐 버그를 놓쳤음).
- 수정 후 같은 경로로 재현한 결과 `BaseStructure.risingBuilding` 필드가 건설 중(109/500, 22%
  진행)에 `null`이 아니라 실제 `SupplyDepot(Clone)` 오브젝트를 가리켰고, Y좌표(2.41)가 최종
  위치(4.53)보다 낮아 아직 떠오르는 중임을 확인. 스크린샷으로도 시각 확인.
- 완공까지 지켜본 결과 두 테스트 건물 모두 `health=500/500`, `layer=Building`으로 정상 완공되었고,
  `Indicators` 레이어에 남은 고스트 오브젝트(누수) 0개.

Unity 컴파일 확인 완료(에러 0, 기존에도 있던 무관한 경고만 존재).

## 날짜
2026-08-12

## 요청 내용
없음 (background fork로 돌린 `uloop-execute-dynamic-code` 자동 검증에서 발견). 0527에서 구현한
"건물이 땅속에서 떠오르는 애니메이션"을 실제 게임 흐름(일꾼이 건설 위치에 도착 → 자동으로 건설 시작)으로
재현했더니 애니메이션이 전혀 재생되지 않음을 발견.

## 조사 내용
- 직접 재현: `UnitController.Update()`(`Assets\Scripts\Unit\UnitController.cs:472`)가 매 프레임
  `BuildTick()`을 호출하고, 일꾼이 건설 위치에 도착하면 그 안에서 `GoBuild`의 `onArrived` 콜백인
  `PlacementSystem.StartConstruction()`이 실행됨. 이 메서드는 `Instantiate(baseStructurePrefab, ...)`
  직후 `structure.Initialize(...)`를 **같은 프레임, 같은 호출 스택 안에서 동기적으로** 호출한다
  (`Assets\Scripts\BuildSystem\PlacementSystem.cs:203-232`).
- `BaseStructure.Initialize()`는 그 안에서 곧바로 `SpawnRisingBuilding()`을 호출하는데
  (`Assets\Scripts\Building\BaseStructure.cs:83`), 이 메서드는 `Start()`에서만 채워지는
  `previewSystem` 필드에 의존한다 (`BaseStructure.cs:45-49`).
- Unity는 `Instantiate()`로 생성된 오브젝트의 `Awake()`는 즉시 실행하지만 `Start()`는 별도 배치로
  미룬다. `Initialize()`가 `Instantiate()` 직후 곧바로(=아직 `Start()`가 돌기 전) 호출되므로,
  `previewSystem`은 이 시점에 항상 `null`이다. `SpawnRisingBuilding()`의 가드(`if (previewSystem
  == null) return;`)가 조용히 아무 것도 하지 않고 리턴해버려서, `risingBuilding`/`risingTween`이
  아예 생성되지 않는다. `SpawnRisingBuilding()`은 `Initialize()`에서 딱 한 번만 호출되므로 이후에도
  다시 시도되지 않음 - 즉 실제 플레이에서는 매번 100% 실패한다.
- 콘솔 에러는 발생하지 않음(가드가 조용히 무시) - 그래서 첫 구현 확인 세션에서는 놓쳤음. 당시 검증은
  `execute-dynamic-code`로 별도 에디터 스크립트에서 `Instantiate`+`Initialize`를 호출했는데, 이 경로는
  실제 게임의 `Update()` 안 호출과 달리 라운드트립 사이에 프레임 경계가 여러 번 지나가 `Start()`가
  이미 실행된 뒤였다 - 그래서 그때는 우연히 `previewSystem`이 채워져 있어 정상 동작처럼 보였음.
- 건설 완료/체력 증가 등 나머지 로직은 이 버그와 무관하게 정상 동작한다(트윈이 없어도 `risingTween?.`
  널 조건부 연산자들이 모두 안전하게 무시됨) - 그래서 완공 자체는 문제없이 끝남.

## 설계 (제안)
`previewSystem`을 `Start()`가 아니라, 실제로 필요한 시점(`SpawnRisingBuilding()` 호출 시점)에 그 자리에서
`FindFirstObjectByType<PreviewSystem>()`으로 조회한다. `PreviewSystem`은 씬에 항상 미리 배치되어 있는
싱글턴 성격의 매니저라 `Awake()` 시점부터 이미 존재하므로(일꾼이 건설 시작하는 시점은 항상 게임 진행 중이라
씬 로드 초기 프레임보다 한참 뒤), 호출 순서 문제 없이 항상 찾을 수 있다. `previewSystem` 필드/`Start()`의
할당은 이제 아무 데도 안 쓰이므로 통째로 제거한다 (필드 하나 없애는 게 "Start()에도 넣고 지연조회도
넣는" 이중 관리보다 단순함).

## 코드 변경 (제안)

### Assets\Scripts\Building\BaseStructure.cs
기존 코드 (필드):
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private PreviewSystem previewSystem;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)
```

변경 코드:
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)
```

기존 코드 (`Start()`):
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        previewSystem = FindFirstObjectByType<PreviewSystem>();
    }
```

변경 코드:
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

기존 코드 (`SpawnRisingBuilding()`):
```csharp
    private void SpawnRisingBuilding(GameObject finishedPrefab, float buildTime)
    {
        if (previewSystem == null)
            return;

        Vector3 finalPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(finishedPrefab);
```

변경 코드:
```csharp
    // PlacementSystem이 일꾼 도착 시 이 오브젝트를 Instantiate한 직후 같은 프레임에 바로 Initialize()를
    // 호출하므로, 이 오브젝트 자신의 Start()는 아직 실행되지 않은 상태다. PreviewSystem은 씬에 항상 먼저
    // 존재하는 매니저이므로 Start()를 기다리지 않고 여기서 바로 조회한다 (doc/0528).
    private void SpawnRisingBuilding(GameObject finishedPrefab, float buildTime)
    {
        PreviewSystem previewSystem = FindFirstObjectByType<PreviewSystem>();
        if (previewSystem == null)
            return;

        Vector3 finalPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(finishedPrefab);
```

(이하 `SpawnRisingBuilding()` 나머지 본문은 그대로, `previewSystem` 지역 변수를 그대로 사용)

## 요약
- 원인: `Initialize()`가 `Instantiate()` 직후 동기 호출되어 자기 자신의 `Start()`보다 먼저 실행됨 →
  `Start()`에서만 채워지던 `previewSystem`이 항상 `null` → 상승 애니메이션이 실제 플레이에서 한 번도
  재생되지 않음(콘솔 에러 없이 조용히 실패).
- 수정: `previewSystem`을 필드/`Start()`에서 빼고, `SpawnRisingBuilding()`이 필요한 시점에 직접 조회.
- 영향받는 파일: `Assets\Scripts\Building\BaseStructure.cs` 1개뿐.

## 확인 필요
이 진단과 수정안대로 진행해도 될지 확인 부탁드립니다.
