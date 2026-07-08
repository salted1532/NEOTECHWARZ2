# 0043. 건물 건설/유닛 생산을 ResourceManager와 실제로 연결

**날짜:** 2026-07-09

## 요청 내용
> 이제 건물이나 유닛 생산시 자원매니저와 연결하려고해. 건물 건설, 유닛 생산 (자원 사용)자원매니저랑 연결 + 인구수 추가
> 건물 건설, 유닛 생산 시 해당 명령 받았을 시 자원매니저에서 현재 자원, 인구수가 건설,생산이 가능한지 확인
> 가능하면 가격만큼 자원을 마이너스 하고 유닛이면 대기열추가, 건물이면 일꾼이 건물을 지으러 가 구조체(건물이 지어지는과정)를 짓는다. 해당 내용에 맞도록 구현해줘

## 조사 결과 (현재 코드 상태 - 둘 다 이미 만들어져 있는데 실제로는 연결이 안 돼 있었음)
- `ResourceManager.cs`에 `CanAfford`/`TrySpend`/`AddMaxPopulation`/`RemoveMaxPopulation`이 이미 다 구현되어 있음.
- `RTSUnitController.TryProduceUnit(unitID)`도 이미 `resourceManager.TrySpend(mineral, gas, population)` 체크 후 `SpawnUnit(unitID)`를 호출하도록 완성돼 있는데, **정작 생산 패널 4곳(MainBase/Barracks/Factory/Airport)의 버튼이 전부 `TryProduceUnit`이 아니라 `SpawnUnit`을 직접 호출**하고 있어서 자원 체크를 완전히 건너뛰고 있었음(공짜로 무한 생산 가능한 상태).
- `RTSUnitController.TryConstructBuilding(buildingID)`도 이미 `resourceManager.TrySpend(mineral, gas)`를 호출하도록 구현돼 있는데, **`PlacementSystem.PlaceStructure()`가 이걸 아예 호출하지 않아서** 건물도 공짜로 지어지고 있었음.
- "인구수 추가"(보급고 등 건물이 최대 인구수를 늘려주는 것)는 `ResourceManager.AddMaxPopulation`/`RemoveMaxPopulation`이 이미 준비돼 있지만 실제로 호출하는 곳이 어디에도 없음 — 건물이 실제로 "완공"되는 시점([[0038-base-structure-construction-progress|BaseStructure.CompleteConstruction()]])에 걸어줘야 함(건설 중엔 아직 인구수가 늘면 안 됨 - SC 시리즈에서도 건설 중인 보급고는 인구수에 반영 안 됨).

## 설계안

### 1. `RTSUnitController.cs` — 생산 버튼들이 `TryProduceUnit`을 쓰도록 교체 + 인구수 헬퍼 추가

**4개 생산 패널의 버튼 콜백을 `SpawnUnit` → `TryProduceUnit`으로 교체**:
```csharp
// 기존 코드
                            UnitButtonAction(() => SpawnUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
...
                            UnitButtonAction(() => SpawnUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                            UnitButtonAction(() => SpawnUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));
...
                            UnitButtonAction(() => SpawnUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                            UnitButtonAction(() => SpawnUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));
...
                            UnitButtonAction(() => SpawnUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                            UnitButtonAction(() => SpawnUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));
```
```csharp
// 변경 코드
                            UnitButtonAction(() => TryProduceUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
...
                            UnitButtonAction(() => TryProduceUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                            UnitButtonAction(() => TryProduceUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));
...
                            UnitButtonAction(() => TryProduceUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                            UnitButtonAction(() => TryProduceUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));
...
                            UnitButtonAction(() => TryProduceUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                            UnitButtonAction(() => TryProduceUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));
```
(`TryProduceUnit`이 `bool`을 반환하지만 버튼 콜백은 `Action`(반환값 없음)이라 그냥 결과를 버리고 호출하는 것으로 충분 - 기존에도 이미 이런 형태로 쓰기 위해 만들어져 있던 메서드.)

**인구수 증감 헬퍼 추가** (`AddOre`/`AddGas` 아래):
```csharp
    public void AddMaxPopulation(int amount) => resourceManager.AddMaxPopulation(amount);

    // 건물이 파괴됐을 때 그 건물 종류가 제공하던 인구수 한도를 되돌린다 (buildingID로 DB에서 조회).
    public void RemoveMaxPopulationForBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data != null)
            resourceManager.RemoveMaxPopulation(data.population);
    }
```

### 2. `PlacementSystem.cs` — 배치 시 자원 확인/소모 추가

```csharp
// 기존 코드
        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size);
```
```csharp
// 변경 코드
        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        if (rtsController == null || !rtsController.TryConstructBuilding(data.ID))
            return; // 자원/인구가 부족하면 배치하지 않음 (여기서 자원이 실제로 차감됨)

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size);
```
(일꾼 확인을 자원 확인보다 먼저 하는 이유: `TryConstructBuilding`은 호출 즉시 자원을 실제로 차감하므로, 일꾼이 없어서 어차피 배치가 취소될 상황이라면 자원을 차감하기 전에 먼저 걸러야 함.)

### 3. `BaseStructure.cs` — 완공 시 인구수 반영

```csharp
// 기존 코드 (CompleteConstruction)
        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;
        }
```
```csharp
// 변경 코드
        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            rtsController?.AddMaxPopulation(data.population); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
        }
```

### 4. `BuildingController.cs` — 파괴 시 인구수 반환 (요청엔 없었지만 짝을 맞추기 위한 보조 처리)

건설 완료 시 인구수를 더해주는 이상, 그 건물이 파괴됐을 때 되돌려주지 않으면 보급고를 계속 부수고 다시 지어도 인구수 한도가 영원히 누적되기만 하는 명백한 버그가 생김 — 요청엔 없었지만 같이 처리.
```csharp
// 기존 코드
    public void Die()
    {
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록

        Destroy(gameObject);
    }
```
```csharp
// 변경 코드
    public void Die()
    {
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록
        rtsController?.RemoveMaxPopulationForBuilding(buildingID); // 이 건물이 제공하던 인구수 한도를 반환

        Destroy(gameObject);
    }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **자원 차감 시점**: 명령을 내리는 그 순간(유닛은 생산 버튼 클릭 시, 건물은 배치 클릭 시) — 완공/생산 완료를 기다리지 않고 즉시 차감(기존에 이미 그렇게 설계돼 있던 `TryProduceUnit`/`TryConstructBuilding`의 동작 그대로).
- **인구수 한도 증가 시점**: 건물이 배치되는 순간이 아니라, `BaseStructure`가 건설을 마치고 실제 건물이 스폰되는 순간 — 건설 중인 보급고는 아직 인구수에 반영되지 않음.
- **다중 건물 선택 후 유닛 생산**: 기존 `SpawnUnit`/`TryProduceUnit` 구조를 그대로 사용 — 자원은 한 번만 체크/차감하고 선택된 모든 생산 건물에 동일하게 큐잉되는 기존 동작을 그대로 둠(이번 요청 범위 밖이라 새로 설계하지 않음).
- **건물 파괴 시 인구수 반환**: 요청엔 없었지만, 인구수를 늘려주는 기능과 반드시 쌍을 이뤄야 하는 명백한 후속 버그라 함께 처리(`BuildingController.Die()`에서 `RemoveMaxPopulationForBuilding` 호출).

## 변경 예정 파일
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/Building/BuildingController.cs`

## 상태
**적용 완료** — [[0044-production-and-construction-refunds|0044]]와 함께 실제 코드에 반영함 (설계와 구현 간 차이 없음).
