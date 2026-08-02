# 0366. 씬에 직접 배치한 보급고 등 건물도 인구수 최대치에 반영

**날짜:** 2026-08-02

## 요청 내용
> 게임 시작시 보급고 프리팹이 설치되어있으면 인구수 최대치가 그에 맞게 늘어나도록 해줘

## 원인

인구수 한도(`ResourceManager.rawMaxPopulation`)는 오직 `BaseStructure.CompleteConstruction()`이
`rtsController.AddMaxPopulation(data.maxpopulationamount)`를 호출할 때만 증가한다
(`Assets/Scripts/Building/BaseStructure.cs:197`). 즉 정상적인 건설 흐름(일꾼이 지어서 완공)을 거친
건물만 인구수 한도에 반영됨.

씬(Hierarchy)에 완성된 건물 프리팹(`SupplyDepot.prefab` 등)을 직접 배치해둔 경우엔 이 건설 흐름을
아예 거치지 않으므로 `AddMaxPopulation`이 한 번도 호출되지 않아 인구수 한도가 늘어나지 않음.
[[0333-scene-placed-unit-population-accounting]]에서 유닛에 대해 이미 동일한 문제를 고친 적이
있는데, 그때는 유닛(`currentPopulation` 소모)이었고 이번은 건물(`rawMaxPopulation` 한도)이라는 점만
다르고 원인 구조는 동일함.

참고로 `BuildingController.Die()`는 씬 배치 여부와 상관없이 항상
`rtsController.RemoveMaxPopulationForBuilding(buildingID)`를 호출한다
(`Assets/Scripts/Building/BuildingController.cs:507`) — 즉 씬에 배치된 보급고가 파괴되면, 애초에
추가된 적 없는 인구수 한도를 깎아버려서 다른 건물이 정당하게 늘려둔 한도까지 잠식하는 부작용도
이미 존재함. 이번 수정으로 "추가"와 "제거"가 대칭을 이루게 되어 이 부작용도 함께 해결됨.

## 씬 배치 여부 판별 방법 (이미 존재하는 신호 재사용)

`BuildingController`에는 이미 정확히 이 용도로 쓰이는 `hasGridPosition` 플래그가 있음:
- 정상 건설된 건물: `BaseStructure.CompleteConstruction()`이 `Instantiate()` 직후(= 그 건물의
  `Start()`가 돌기 전) `SetGridInfo(gridPosition)`을 호출해 `hasGridPosition = true`를 미리 세팅해둠.
- 씬에 직접 배치된 건물: 아무도 `SetGridInfo`를 미리 호출하지 않으므로 `Start()` 시점엔
  `hasGridPosition`이 여전히 `false`.

`BuildingController.Start()`는 이미 이 신호로 그리드 등록 여부를 가르고 있음
(`Assets/Scripts/Building/BuildingController.cs:96-97`: `if (!hasGridPosition) RegisterToGridIfPossible();`).
새 플래그를 추가할 필요 없이 이 값을 인구수 판별에도 그대로 재사용하면 됨(단,
`RegisterToGridIfPossible()`가 그 안에서 `hasGridPosition`을 `true`로 바꿔버리므로, 판별은 그 호출
"이전" 값을 먼저 변수에 저장해둬야 함).

## 제안하는 변경

- **`Assets/Scripts/Building/BuildingController.cs`** (`Start()`): `RegisterToGridIfPossible()` 호출
  전에 `bool builtByConstruction = hasGridPosition;`로 미리 저장. 이후(그리드 등록 로직 다음)
  `if (!builtByConstruction) { BuildingData data = rtsController.GetBuildingData(buildingID); if (data != null) rtsController.AddMaxPopulation(data.maxpopulationamount); }` 추가.
  - `GetBuildingData(int)`(`RTSUnitController.cs:1972`)와 `AddMaxPopulation(int)`(`RTSUnitController.cs:1077`)는
    이미 존재하는 public 메서드라 새 메서드 추가 없이 재사용 가능.

이 외 다른 파일 변경 없음 — `ResourceManager`, `BaseStructure`, `RTSUnitController`는 그대로 둠
(이미 필요한 메서드가 다 있음).

## 예상 동작

- 씬에 미리 배치된 `SupplyDepot`/메인기지 등: 게임 시작(`Start()`) 시점에 `maxpopulationamount`만큼
  인구수 한도가 즉시 늘어남(200 캡은 `ResourceManager.AddMaxPopulation`이 그대로 적용).
- 정상적으로 지어진 건물: 기존과 동일하게 `CompleteConstruction()` 시점에만 반영(이중 계산 없음,
  `builtByConstruction == true`라 `Start()`에서는 건너뜀).
- 씬 배치 건물이 파괴되면 이제 정상적으로 "추가된 적 있는" 한도를 반환하게 되어 다른 건물의 한도를
  잠식하는 부작용도 사라짐.

## 검증 계획

- `npx uloop-cli compile`로 에러 0개 확인.
- Play Mode에서 씬에 보급고를 배치해두고 시작 → `ResourceManager.GetMaxPopulation()`이
  `startMaxPopulation + maxpopulationamount`만큼 늘어나는지 확인.
- 정상 건설(일꾼으로 보급고 건설)도 기존처럼 완공 시 1회만 반영되는지(이중 반영 없음) 확인.

## 구현 (승인 후 적용됨)

`Assets/Scripts/Building/BuildingController.cs`의 `Start()`를 제안대로 수정.

**Before:**
```csharp
void Start()
{
    buildingMarker.SetActive(false);

    rtsController = FindFirstObjectByType<RTSUnitController>();
    placementSystem = FindFirstObjectByType<PlacementSystem>();

    groundOffset = PlacementSystem.GetGroundOffsetY(gameObject);

    SnapToGround();

    if (!hasGridPosition)
        RegisterToGridIfPossible();

    rtsController.BuildingList.Add(this);

    navMeshObstacle = GetComponent<NavMeshObstacle>();
    ...
```

**After:**
```csharp
void Start()
{
    buildingMarker.SetActive(false);

    rtsController = FindFirstObjectByType<RTSUnitController>();
    placementSystem = FindFirstObjectByType<PlacementSystem>();

    groundOffset = PlacementSystem.GetGroundOffsetY(gameObject);

    SnapToGround();

    // RegisterToGridIfPossible()가 hasGridPosition을 true로 바꿔버리기 전에, "정상 건설을 거쳤는지"를
    // 미리 기억해둔다 - 정상 건설된 건물은 BaseStructure.CompleteConstruction()이 Instantiate() 직후
    // (이 Start()가 돌기 전) SetGridInfo()를 호출해 hasGridPosition을 이미 true로 세팅해둔 상태.
    bool builtByConstruction = hasGridPosition;

    if (!hasGridPosition)
        RegisterToGridIfPossible();

    rtsController.BuildingList.Add(this);

    // 씬에 직접 배치해둔 건물(정상 건설 흐름을 안 거친 건물)은 CompleteConstruction()을 거치지 않아
    // 인구수 최대치가 반영된 적이 없으므로, 여기서 한 번만 반영해준다 (doc/0366, 유닛의
    // AddPopulationForExistingUnit과 동일한 패턴 - doc/0333).
    if (!builtByConstruction)
    {
        BuildingData data = rtsController.GetBuildingData(buildingID);
        if (data != null)
            rtsController.AddMaxPopulation(data.maxpopulationamount);
    }

    navMeshObstacle = GetComponent<NavMeshObstacle>();
    ...
```

## 검증

- `npx uloop-cli compile`: 에러 0개(기존에도 있던 무관한 경고 33개만 남음).
- Play Mode에서 `execute-dynamic-code`로 실제 검증(CommandCenter, ID=1, `maxpopulationamount=10` 사용):
  - `BEFORE`: `max=34, current=22`
  - 씬에 직접 `Instantiate()`(정상 건설 흐름 없이 배치)한 뒤 `AFTER`: `max=44, current=22` → `44-34=10`으로
    정확히 `maxpopulationamount`만큼만 증가(이중 계산 없음, `current`는 그대로 — 이 건물은 인구를 소모하지
    않으므로 정상).
  - 테스트 인스턴스에 `Die()` 호출 후 `AFTER_DIE`: `max=34, current=22` → 원래 값으로 정확히 복귀(추가된
    적 없는 한도를 깎아버리던 기존 부작용도 함께 해소됨을 확인).
  - **PASS.** 테스트용 인스턴스는 확인 후 `Die()`로 정리, 씬/에셋 파일은 건드리지 않음.

## 영향받는 파일

- `Assets/Scripts/Building/BuildingController.cs`
