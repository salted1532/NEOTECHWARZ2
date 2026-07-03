# ResourceManager (Ore / Gas / Population) 설계

작성일: 2026-07-03
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

`ResourceManager`가 Ore(미네랄), Gas, Population(인구수) 3종 자원을 관리한다.

- 건물 건설(`PlacementSystem`) / 유닛 생산(`BuildingController.SpawnUnit`)이 발생하기 전에,
  **`RTSUnitController`를 통해** "지금 지을 수 있는지 / 생산할 수 있는지"를 먼저 확인한다.
- 확인 결과 자원이 충분하면 → 요청이 받아들여져서 실제 건설/생산이 진행되고, 그 시점에
  Ore/Gas/Population이 차감된다.
- 부족하면 → 요청은 그냥 무시되고(반환) 아무 것도 소모되지 않는다.

[[health-manager-design]] 다룰 때 "전투(공격 대상 선정)"는 개별 유닛의 문제라 중앙(RTS
컨트롤러)이 관여하면 안 된다고 정리했는데, 자원은 반대로 **플레이어 전체가 공유하는
전역 상태**이므로 `RTSUnitController`가 중개(mediator) 역할을 하는 게 자연스럽다.

## 2. 데이터 모델에서 미리 채워야 할 부분

현재 `UnitData`(`UnitDataSO.cs`)에는 인구수 비용 필드가 없고, `BuildingData`
(`BuildingDataSO.cs`)에는 "보급고가 인구수 한도를 얼마나 늘려주는지" 필드가 없다.
자원 체크 로직을 만들기 전에 아래 필드 추가가 먼저 필요하다.

```csharp
// UnitData (UnitDataSO.cs)에 추가
[field: SerializeField]
public int population { get; private set; } // 이 유닛 1기가 소모하는 인구수

// BuildingData (BuildingDataSO.cs)에 추가
[field: SerializeField]
public int populationProvided { get; private set; } // 이 건물이 늘려주는 인구수 한도 (보급고=8 등, 대부분 건물은 0)
```

## 3. ResourceManager 코드

`Assets/Scripts/Resource/ResourceManager.cs` 교체안:

```csharp
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int startOre = 50;
    [SerializeField] private int startGas = 0;
    [SerializeField] private int startMaxPopulation = 10;

    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int maxPopulation;       // 인구수 한도 (보급고 등으로 증가)

    public event System.Action OnResourceChanged;

    private void Awake()
    {
        currentOre = startOre;
        currentGas = startGas;
        maxPopulation = startMaxPopulation;
    }

    // ===== Get =====
    public int GetOre() => currentOre;
    public int GetGas() => currentGas;
    public int GetPopulation() => currentPopulation;
    public int GetMaxPopulation() => maxPopulation;

    // ===== 채집(일꾼)으로 자원 획득 =====
    public void AddOre(int amount)
    {
        if (amount <= 0) return;
        currentOre += amount;
        OnResourceChanged?.Invoke();
    }

    public void AddGas(int amount)
    {
        if (amount <= 0) return;
        currentGas += amount;
        OnResourceChanged?.Invoke();
    }

    // ===== 인구수 한도 변경 (보급고 건설/파괴) =====
    public void AddMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation += amount;
        OnResourceChanged?.Invoke();
    }

    public void RemoveMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation = Mathf.Max(0, maxPopulation - amount);
        OnResourceChanged?.Invoke();
    }

    // ===== 생산/건설 가능 여부만 확인 (차감 없음) =====
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;
        if (currentPopulation + populationCost > maxPopulation) return false;

        return true;
    }

    // ===== 확인 + 실제 차감을 한 번에 (요청이 "받아들여지는" 시점) =====
    public bool TrySpend(int oreCost, int gasCost, int populationCost = 0)
    {
        if (!CanAfford(oreCost, gasCost, populationCost))
            return false;

        currentOre -= oreCost;
        currentGas -= gasCost;
        currentPopulation += populationCost;

        OnResourceChanged?.Invoke();
        return true;
    }

    // ===== 유닛 사망/건물 파괴 시 인구수 반환 =====
    public void ReleasePopulation(int amount)
    {
        if (amount <= 0) return;
        currentPopulation = Mathf.Max(0, currentPopulation - amount);
        OnResourceChanged?.Invoke();
    }
}
```

### 설계 포인트

- `CanAfford`(조회 전용)와 `TrySpend`(조회+차감)를 분리했다. UI에서 "생산 버튼을 눌러도
  되는지 미리 흐리게 표시"하는 등 실제로 소모하지 않고 확인만 하고 싶을 때는 `CanAfford`를,
  실제 생산/건설이 확정되는 순간에는 `TrySpend` 하나만 부르면 된다 (내부에서 다시
  `CanAfford`를 호출하므로 두 번 검증할 필요 없음).
- Ore/Gas는 한 번 소모하면 끝이지만, Population은 유닛이 "살아있는 동안" 계속 잡고
  있는 자원이라 `TrySpend`로 늘어난 값을 유닛이 죽을 때 `ReleasePopulation`으로
  반드시 되돌려줘야 한다 (그렇지 않으면 인구수가 영구적으로 새서 한도가 줄어드는
  것과 같은 효과가 남).

## 4. RTSUnitController 중개 메소드

`RTSUnitController`에 `ResourceManager` 참조를 추가하고, 생산/건설 진입점을 감싼다.

```csharp
[SerializeField] private ResourceManager resourceManager;
[SerializeField] private UnitDataSO unitDatabase;
[SerializeField] private BuildingDataSO buildingDatabase;

/// 유닛 생산 요청 (선택된 건물들에게 큐잉하기 전에 자원부터 확인)
public bool TryProduceUnit(int unitID)
{
    UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
    if (data == null)
        return false;

    if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
        return false; // 자원 부족 → 여기서 그냥 반환, 아무 것도 소모 안 됨

    SpawnUnit(unitID); // 기존 메소드: selectedBuildingList에 실제 큐잉
    return true;
}

/// 건물 건설 요청 (PlacementSystem이 실제로 배치를 확정하기 직전에 호출)
public bool TryConstructBuilding(int buildingID)
{
    BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
    if (data == null)
        return false;

    return resourceManager.TrySpend(data.mineral, data.gas);
}
```

## 5. 실제 연결 지점

| 이벤트 | 파일:위치 | 필요한 변경 |
|---|---|---|
| 유닛 생산 버튼 클릭 | `RTSUnitController.SpawnUnit(unitID)` (`RTSUnitController.cs:367`) | 외부(UI)에서 이 메소드를 직접 부르지 말고 `TryProduceUnit(unitID)`을 통하게 변경 |
| 건물 배치 확정 | `PlacementSystem.PlaceStructure()` (`PlacementSystem.cs:63`) | `Instantiate` 하기 전에 `rtsController.TryConstructBuilding(data.ID)` 확인, 실패 시 배치 취소 |
| 유닛 사망 | `UnitController.Die()` ([[health-manager-design]] 참고) | `resourceManager.ReleasePopulation(data.population)` 호출 |
| 보급고(SupplyDepot) 건설 완료 | `PlacementSystem.PlaceStructure()` 성공 시 | `resourceManager.AddMaxPopulation(data.populationProvided)` |
| 보급고 파괴 | `BuildingController.Die()` | `resourceManager.RemoveMaxPopulation(data.populationProvided)` |
| 자원 채집(일꾼) | (아직 코드 없음, Worker 관련 로직 추가 시) | `resourceManager.AddOre(amount)` / `AddGas(amount)` |

## 6. 판단이 필요한 열린 질문

1. **여러 건물을 동시에 선택한 상태에서 생산 버튼을 누르면?**
   현재 `RTSUnitController.SpawnUnit`(`RTSUnitController.cs:367`)은 `selectedBuildingList`
   전체에 대해 반복문으로 `SpawnUnit`을 호출한다. 즉 배럭 3개를 선택하고 마린 생산을
   누르면 3개 건물 모두에 마린이 큐잉된다. 자원 체크를 도입하면:
   - (a) 건물 개수만큼 비용을 곱해서 한 번에 확인/차감할지
   - (b) 건물 하나당 한 번씩 개별로 `TrySpend`를 호출해서, 자원이 바닥나면 일부
     건물만 큐잉되고 나머지는 실패하게 둘지
   결정이 필요하다.
2. **건설/생산 취소 시 환불 여부.** `UnitSpawner.Cancel(index)`(`UnitSpawner.cs:117`)로
   큐에서 이미 차감된 생산을 취소할 수 있는데, 이때 Ore/Gas/Population을 돌려줄지
   그대로 소모 처리할지 정책이 필요하다 (스타크래프트는 100% 환불).
3. **건물 건설은 "배치 확정 시 즉시 차감" vs "건설 완료 시 차감"?** 위 코드는 배치
   확정(`PlaceStructure`) 시점에 즉시 차감하는 걸 전제로 했다. 건설 중 취소가
   가능하게 만들 계획이라면 취소 시 환불 로직도 같이 필요하다.
