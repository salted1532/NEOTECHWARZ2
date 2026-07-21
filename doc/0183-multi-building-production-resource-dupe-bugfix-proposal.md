# 0183 - 건물 다중 선택 유닛 생산 버그 2건 (자원 복제 + 다른 티어 건물 오큐잉) - 수정 제안

## 요청 1

"현재 건물을 Shift + 클릭 시 여러 건물을 선택 가능한대 그 상태에서 해당 건물의 유닛생산 버튼을
누르면 여러 건물들에게 그 유닛 생산명령을 전부 보내주거든. 그렇게 되면 문제가 2개의 건물에
유닛생산 명령을 내리는 과정에서 자원은 1번만 결제 된다는거야 그리고 2개의 건물의 유닛들을
환불하면 무한으로 돈을 복사할수 있어." — 건물을 Shift+클릭으로 다중 선택한 상태에서 생산 버튼을
누르면 선택된 모든 건물의 대기열에 유닛이 큐잉되는데, 자원은 딱 1회분만 차감된다. 이후 각 건물을
개별 선택해서 그 유닛 생산을 취소(환불)하면, 건물 수만큼 환불을 받아 자원을 무한 복제할 수 있는
심각한 버그.

## 요청 2

"여기에 한가지 더 문제가 Tier1, tier2 등 각 다른 티어의 건물들이 다른 생산유닛을 가지고 있는
건물들인데 Shift + 클릭 시 다른 종류의 건물도 선택되는데 그 때 유닛을 생산하면 그 유닛을
생산하지 않는 건물들도 그 대기열에 들어가는 문제가 발생한다는거야" — Tier1(병영)과 Tier2(공장)처럼
서로 다른 유닛을 생산하는 건물을 Shift+클릭으로 같이 선택한 상태에서 생산 버튼을 누르면, 그 유닛을
생산할 수 없는(다른 티어) 건물의 대기열에도 그 유닛이 큐잉되는 문제.

## 원인

`Assets/Scripts/System/RTSUnitController.cs`

**공통 원인 지점**: `TryProduceUnit(unitID)` (853~881번 줄)과 `SpawnUnit(int unitID)`(747~761번 줄).

1. **자원 1회만 결제 (요청 1)**
   - `TryProduceUnit`은 대기열 확인/자원 소모를 **`selectedBuildingList[0]` 기준으로 딱 1번만**
     수행한다 (`selectedBuildingList[0].IsProductionQueueFull()` 검사 후 `resourceManager.TrySpend(...)`
     1회 호출).
   - 그 다음 호출하는 `SpawnUnit(unitID)`은 **선택된 모든 건물**을 순회하며
     `building.SpawnUnit(unitID)`로 전부 큐잉한다.
   - 결과: 건물 2개 선택 후 생산 버튼 1번 클릭 → 유닛 1개 비용만 차감되지만 건물 A, B 양쪽 대기열에
     각각 유닛이 1개씩 추가됨(공짜로 유닛 1개를 추가로 얻는 셈). `CancelProduction`은
     `selectedBuildingList[0]`만 취소하므로, 건물을 하나씩 개별 선택해서 각각 취소하면 취소할
     때마다 전액 환불되어 유닛 1개 비용을 냈는데 2개(또는 그 이상, 선택 건물 수만큼) 비용을
     환불받는 무한 복제가 가능하다.

2. **다른 티어 건물에도 오큐잉 (요청 2)**
   - `BuildingController.SpawnUnit(int unitID)`(`Assets/Scripts/Building/BuildingController.cs:366`)은
     `UnitSpawner.Enqueue(unitID)`로 그대로 위임하고, `UnitSpawner.Enqueue`
     (`Assets/Scripts/UnitSpawner/UnitSpawner.cs:47`)는 넘어온 `unitID`가 공유
     `UnitDataSO` 데이터베이스에 존재하기만 하면 **건물 종류/태그와 무관하게** 무조건 큐에 추가한다 —
     "이 건물이 이 유닛을 생산할 수 있는 건물 종류인지"를 검증하는 코드가 어디에도 없다.
   - 어떤 유닛을 어느 건물에서 생산할 수 있는지는 오직 UI 배선(`RTSUnitController.cs:1014~1052`의
     `ShowBarracksPanel`/`ShowFactoryPanel`/`ShowAirportPanel`)에만 암묵적으로 존재한다 — 한 건물만
     선택했을 때 그 건물 태그에 맞는 패널만 보여주는 방식으로 우회 방지가 되고 있었을 뿐이다.
   - `SelectBuilding(building)`(375~440번 줄)은 Shift+클릭으로 건물을 추가할 때마다
     `BuildingSelectState`를 **마지막에 클릭한 건물의 태그**로 덮어쓴다. 즉 Tier1 건물을 먼저
     선택하고 Tier2 건물을 Shift+클릭으로 추가하면, `selectedBuildingList`엔 Tier1+Tier2가 모두
     들어있지만 패널은 Tier2(마지막 클릭) 기준으로 표시된다. 이 상태에서 Tier2 패널의 생산 버튼을
     누르면 `TryProduceUnit`이 `selectedBuildingList` 전체(Tier1 건물 포함)를 순회하며 큐잉하므로,
     Tier1 건물의 대기열에도 원래 Tier2 전용 유닛이 들어가 버린다.
   - 두 문제 모두 `TryProduceUnit`/`SpawnUnit` 경로가 "선택된 건물이면 무조건, 생산 가능 여부
     검증 없이" 큐잉하기 때문에 발생하므로 같은 곳에서 함께 고친다.

## 계획된 코드 변경

### `Assets/Scripts/System/RTSUnitController.cs`

Before:
```csharp
    public void SpawnUnit(int unitID)
    {
        if (selectedBuildingList.Count == 0)
        {
            Debug.LogWarning("No buildings selected for spawning units.");
            return;
        }

        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            BuildingController building = selectedBuildingList[i];

            building.SpawnUnit(unitID);
        }
    }
```
```csharp
    /// 유닛 생산 요청 (선택된 건물들에게 큐잉하기 전에 대기열/자원부터 확인)
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (selectedBuildingList.Count == 0)
            return false;

        // 대기열이 가득 찼으면 자원을 소모하기 전에 여기서 먼저 걸러낸다 (자원만 쓰고 큐잉은 안 되는 사고 방지)
        if (selectedBuildingList[0].IsProductionQueueFull())
        {
            Debug.Log("대기열 가득참!");
            return false;
        }

        if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
        {
            if (resourceManager.GetOre() < data.mineral || resourceManager.GetGas() < data.gas)
                Debug.Log("자원부족!");
            else
                Debug.Log("인구수부족!");

            return false; // 자원/인구 부족 → 여기서 그냥 반환, 아무 것도 소모 안 됨
        }

        SpawnUnit(unitID); // 기존 메소드: selectedBuildingList에 실제 큐잉
        return true;
    }
```

After (`SpawnUnit(int)` 헬퍼는 제거. `TryProduceUnit`이 건물별로 "생산 가능한 태그인지 → 대기열
확인 → 자원 차감 → 큐잉"을 한 세트로 묶어서 순회하도록 변경 — 선택된 건물 수만큼 비용도 함께
차감되므로 결제/환불이 항상 1:1로 맞고, 태그가 안 맞는 건물은 애초에 건너뛴다):
```csharp
    // 유닛 ID가 어느 건물 태그에서 생산 가능한지 매핑 (SelectBuilding()의 태그 스위치와 동일한 짝짓기).
    // 다른 티어 건물이 섞여 선택됐을 때 그 유닛을 생산할 수 없는 건물을 걸러내기 위함.
    private static string GetProducerTagForUnit(int unitID)
    {
        switch (unitID)
        {
            case UnitID.Worker:
                return "MainBase";
            case UnitID.Marine:
            case UnitID.Vulture:
                return "Tier1";
            case UnitID.Goliath:
            case UnitID.Tank:
                return "Tier2";
            case UnitID.Wraith:
            case UnitID.Guardian:
                return "Tier3";
            default:
                return null;
        }
    }

    /// 유닛 생산 요청 (선택된 건물 각각에 대해 생산 가능 여부/대기열/자원을 확인하고,
    /// 실제로 큐잉되는 건물 수만큼만 비용을 차감한다)
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (selectedBuildingList.Count == 0)
            return false;

        string producerTag = GetProducerTagForUnit(unitID);
        bool producedAny = false;

        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            BuildingController building = selectedBuildingList[i];

            // 다른 티어 건물이 섞여 선택된 경우, 이 유닛을 생산할 수 없는 건물은 건너뛴다.
            if (producerTag != null && !building.CompareTag(producerTag))
                continue;

            // 대기열이 가득 찼으면 이 건물은 건너뛴다 (자원만 쓰고 큐잉은 안 되는 사고 방지)
            if (building.IsProductionQueueFull())
                continue;

            if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
            {
                if (resourceManager.GetOre() < data.mineral || resourceManager.GetGas() < data.gas)
                    Debug.Log("자원부족!");
                else
                    Debug.Log("인구수부족!");

                break; // 자원이 부족해진 시점부터는 나머지 건물도 어차피 불가능하므로 중단
            }

            building.SpawnUnit(unitID); // 이 건물 1곳에만 큐잉 (건물마다 자원을 개별 차감했으므로 1:1 대응)
            producedAny = true;
        }

        if (!producedAny)
            Debug.Log("생산 불가 (대기열 가득 참, 자원 부족, 또는 이 유닛을 생산할 수 있는 건물 없음)");

        return producedAny;
    }
```

`selectedBuildingList[0]`만 보던 대기열-가득참 체크와 `resourceManager.TrySpend` 1회 호출을 선택된
건물 개수만큼 반복되는 루프 안으로 옮기고, 그 루프 맨 앞에 태그 검사를 추가한다. 기존
`public void SpawnUnit(int unitID)` 헬퍼(자원/태그 체크 없이 전체 건물에 큐잉만 하던 메서드)는 더
이상 쓰이는 곳이 없어 제거한다(다른 호출부 없음 - 조사에서 `TryProduceUnit`을 통해서만 호출됨을
확인).

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/System/RTSUnitController.cs` (`TryProduceUnit` 로직 변경 + `GetProducerTagForUnit`
  헬퍼 추가, 기존 `SpawnUnit(int)` 헬퍼 제거).
- 동작 변화:
  - 같은 티어 건물 여러 개를 선택하고 생산 버튼을 클릭하면 비용도 건물 수만큼 차감된다(대신 대기열이
    가득 차거나 자원이 부족한 건물은 건너뛰고, 그 시점 이후 건물엔 아예 큐잉되지 않음 → 큐잉된
    건물 수 = 차감된 비용 수 항상 일치).
  - 서로 다른 티어 건물을 섞어서 선택한 상태로 생산 버튼을 클릭하면, 그 유닛을 실제로 생산할 수 있는
    태그의 건물에만 큐잉되고 나머지 건물은 조용히 건너뛴다(자원도 차감 안 됨).
  - 건물 1개만 선택했을 때의 동작은 기존과 동일.
- `CancelProduction`은 여전히 `selectedBuildingList[0]`(현재 화면에 표시 중인 건물)만 취소하는 기존
  동작 그대로 유지 — 이번 수정으로 결제/환불이 항상 1:1로 맞춰지므로, 건물을 하나씩 선택해서 각각
  취소해도 정확히 낸 만큼만 환불된다.
- `BuildingController`/`UnitSpawner`/UI(`ShowBarracksPanel` 등)는 수정하지 않음 — 태그 매핑만
  `RTSUnitController` 쪽에 추가.

## 확인 필요

이대로 진행해도 될까요?
