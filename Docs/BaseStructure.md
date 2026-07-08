# BaseStructure

`Assets/Scripts/Building/BaseStructure.cs`

## 개요

건물 기반(파운데이션) 오브젝트. `PlacementSystem`이 일꾼이 건설 위치에 도착했을 때 이 프리팹을 생성하고 `Initialize()`로 지어질 건물 종류/건설시간을 넘겨준다. 담당 일꾼(`builder`)이 붙어있는 동안에만 건설시간이 줄어들고 체력이 차오르며, 담당 일꾼이 없으면(사망 등으로 Unity의 "가짜 null"이 되면) 건설이 자동으로 일시정지된다. 건설시간이 다 되면 실제 건물을 생성하고 자신은 파괴된다. 플레이어가 직접 취소하거나(환불 버튼/단축키) 전투로 파괴되는 경우도 동일한 정리 경로(`CancelConstruction`)를 탄다.

같은 게임오브젝트에 `HealthManager`가 함께 붙어있어야 하며(체력 표시/증가를 위임), `IDestructible`을 구현한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingDatabase` | `BuildingDataSO` (SerializeField) | 완공 시 생성할 건물 프리팹을 `buildingID`로 조회하기 위한 참조 |
| `buildingMarker`, `markerFlashInterval`, `markerFlashCount` | (SerializeField) | 선택/우클릭 피드백용 마커(Marker 자식 오브젝트) — 평소엔 꺼져있음, 선택 시 켜짐, 다른 일꾼 재배치 우클릭 시 3회 깜빡임 |
| `buildingID` | `int` | 지어질 건물의 ID |
| `remainingBuildTime` | `float` | 남은 건설 시간 (담당 일꾼이 있을 때만 감소) |
| `groundPosition` | `Vector3` | 완공 시 실제 건물을 다시 배치할 지면 좌표(오프셋 없는 순수 지면 위치) — `BaseStructure` 자신의 높이와 완성 건물의 높이가 달라도 항상 정확한 위치에 스폰하기 위해 별도로 기억해둠 |
| `healthPerSecond`, `healAccumulator` | `float` | 초당 채워지는 체력량(완공될 건물의 최대체력 ÷ 건설시간), `HealthManager.Heal()`이 int만 받아서 생기는 소수점 나머지 누적값 |
| `icon` | `Sprite` | 완공될 건물의 아이콘(Info_panel 표시용) — `Initialize()` 시점에 완공될 건물 프리팹의 `BuildingController.GetIcon()`에서 미리 읽어옴 |
| `builder` | `UnitController` | 현재 건설 담당 일꾼. `null`이면 건설 일시정지 |
| `healthManager` | `HealthManager` | 같은 오브젝트에 붙어있는 컴포넌트 (체력 표시/증가를 여기에 위임) |
| `rtsController` | `RTSUnitController` | 전역 컨트롤러 참조 |
| `onCancelledByPlayer` | `System.Action` | 플레이어가 직접 취소했을 때 `PlacementSystem`의 그리드 예약을 풀어주는 콜백 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 마커 비활성화, 같은 오브젝트의 `HealthManager` 캐싱 |
| `Start()` | `RTSUnitController` 참조 확보 |
| `Initialize(buildingID, buildTime, groundPosition, onCancelledByPlayer)` | `PlacementSystem`이 스폰 직후 호출. 완공될 건물 프리팹에서 최대체력/아이콘을 미리 읽어와 `healthPerSecond` 계산 + `HealthManager.SetMaxHealth`/`SetHealth(0)`으로 0부터 시작하도록 초기화 |
| `Update()` | 담당 일꾼이 없으면 그냥 반환(일시정지). 있으면 `remainingBuildTime` 감소 + `healAccumulator`에 `healthPerSecond * deltaTime`을 누적하다 1 이상이면 정수만큼 `HealthManager.Heal()` 호출. 시간이 다 되면 `CompleteConstruction()` |
| `AttachBuilder(worker)` | 일꾼이 도착해 건설을 시작(또는 재개)할 때 호출. 이미 다른 일꾼이 담당 중이었다면 그 일꾼의 `FinishConstruction()`을 먼저 호출해 풀어줌 |
| `GetBuildingID()` / `GetIcon()` | Info_panel 표시용 조회 |
| `SelectStructure()` / `DeselectStructure()` | 좌클릭 선택 시(`RTSUnitController`) 마커 on/off |
| `FlashMarker()` | 다른 일꾼을 이 건물 기반에 우클릭했을 때(재배치) 마커를 0.3초 간격 3회 깜빡임 |
| `CompleteConstruction()` (private) | `groundPosition` + `PlacementSystem.GetGroundOffsetY(완성 건물 프리팹)`로 정확한 위치를 계산해 실제 건물을 `Instantiate`, `NavMeshObstacle` 재활성화, `RTSUnitController.AddMaxPopulation`으로 인구수 한도 반영(완공 시점에만), 담당 일꾼 해제, 선택 상태 정리 후 자신 파괴 |
| `CancelConstruction()` | 플레이어가 Info_panel의 취소 버튼/단축키(T)로 직접 취소했을 때 호출. `RTSUnitController.RefundBuilding`으로 건물 가격 전액 환불 → 담당 일꾼 해제 → `onCancelledByPlayer` 콜백으로 그리드 예약 해제 → 선택 상태 정리 → 자신 파괴 |
| `Die()` (`IDestructible`) | `HealthManager`가 체력 0 이하로 판정하면 호출됨 — `CancelConstruction()`과 동일하게 처리(취소와 파괴를 같은 경로로 통일). 현재는 `BaseStructure`를 실제로 공격하는 경로가 없어 이론상의 대비 |

## 연관 컴포넌트

- **PlacementSystem**: `Instantiate` 후 `Initialize()` 호출, 완공/취소 시 쓰는 `GetGroundOffsetY(prefab)`를 static으로 제공, 취소 콜백으로 그리드 예약 해제
- **UnitController**: `GoBuild`로 이 위치까지 이동한 뒤 `BeginConstruction(this)` → `AttachBuilder(this)`로 붙음, 완공/취소/교체 시 `FinishConstruction()`으로 풀려남
- **HealthManager**: 건설 진행률에 따라 차오르는 체력 표시/증가를 위임(`SetMaxHealth`/`SetHealth`/`Heal`)
- **RTSUnitController**: 선택 상태(`selectedBaseStructure`), `AddMaxPopulation`, `RefundBuilding`, `ClearSelectedStructureIfMatches` 호출
- **UIController**: 선택 시 `ShowBaseStructureInfoPanel`(아이콘/이름/체력, 공격력·방어력 숨김), `ShowBaseStructureCommandPanel`(취소 버튼)로 표시됨
