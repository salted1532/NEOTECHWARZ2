# 0198. 연구소(Lab) 공격력/방어력 업그레이드 연구 시스템

(참고: 이 문서는 작성 당시 0195로 번호를 매겼으나, 동시에 다른 세션에서 0195~0197이 먼저 쓰여 번호가 겹쳐 0198로 재번호했습니다. 문서 내 상호 참조가 있다면 0198 기준입니다.)

날짜: 2026-07-21

**상태: 구현 완료.** 아래 설계대로 실제 코드에 적용했습니다. (문서 하단 "코드 변경 후 Unity 에디터에서 추가로 해야 할 작업" 항목은 Unity 에디터 작업이라 여전히 직접 해주셔야 합니다.)

## 요청 내용

연구소(Lab) 건물을 클릭했을 때 OrderPanel에 "공격력 업그레이드" / "방어력 업그레이드" 버튼 2개를 추가한다.
연구소도 생산 건물처럼 대기열을 가지고 있어서, 버튼을 누르면 연구 시간 동안 대기열에서 진행되다가(유닛 생산과 동일한 방식)
완료되면 해당 업그레이드 종류에 맞게 (플레이어) 유닛 전체에 추가 공격력 또는 추가 방어력을 부여한다.
방어력이 적용되면 유닛이 데미지를 받을 때(health) 실제로 받는 데미지가 감소해야 한다.

**추가 확정 사항 (2026-07-21 대화에서 확정):**
- 업그레이드는 **1/2/3업 3단계 레벨업 방식** (한 번에 다음 레벨만 연구 가능, 최대 3레벨).
- 레벨별 비용(공격/방어 공통 스케줄): **1업 광물100/가스100 → 2업 광물200/가스200 → 3업 광물250/가스250**.
- 레벨별 연구 시간: **1업 60초 → 2업 90초 → 3업 120초** (공격/방어 공통).
- 레벨당 보너스량: **공격 +1/레벨, 방어 +1/레벨** (3업 완료 시 각각 총 +3). 시간/보너스/비용 모두 인스펙터에서 숫자로 조정 가능하게 만듦.
- 데미지 감산 공식: **`Max(1, 공격력 - 방어력)`** (최소 1 데미지 보장).
- **같은 타입 중복 연구 금지**: 공격 1업 연구 중에는 공격 2업을 큐에 넣을 수 없음(공격 1업 완료 후에야 2업 큐잉 가능). 반면 **타입이 다르면 동시 큐잉 가능**: 공격 1업 + 방어 1업은 동시에 대기열에 넣고 나란히 진행 가능.
- Lab 선택 시 Info_panel에 건물 이미지, 생산 큐 UI 자리에 현재 연구 중인 항목(공격/방어 아이콘)이 대기열 슬롯으로 보여야 함.
- **아키텍처 요구사항**: 실제 보너스 저장/적용 로직은 새 `Assets/Scripts/Upgrade/` 폴더에 별도 컴포넌트(`UpgradeManager`)로 분리하고, `ResearchQueue`(연구소 큐)나 `UnitController`(유닛)가 이 컴포넌트를 직접 참조하지 않도록 한다 — 항상 `RTSUnitController`를 거쳐서만 보너스를 올리거나(연구 완료 시) 읽도록(유닛이 스탯 조회할 때) 해서 두 시스템(연구소 큐 ↔ 유닛)이 서로 직접 결합되지 않게 유지.

## 조사 내용 요약

- **Lab 건물**: 전용 `LabController` 없이 범용 `BuildingController`를 그대로 씀. 태그 `"Lab"` → `RTSUnitController.BuildingState.Lab`으로 매핑은 이미 있으나(`RTSUnitController.cs:928`), `UpdateUI()`에서는 `SupplyDepot`/`None`과 묶여 "전용 패널 없음" 취급 중 (`RTSUnitController.cs:1215-1220`).
- **OrderPanel 패턴**: `RTSUnitController.UpdateUI()`가 대표 건물 태그로 분기해 `UIController.ShowXPanel(...)` + `ShowProductionUI(큐, 취소콜백)`을 호출 (`RTSUnitController.cs:1175-1221`). Barracks/Factory/Airport와 동일한 틀을 Lab에도 적용하면 됨.
- **생산 대기열 패턴**: `UnitSpawner.cs` (건물의 자식 컴포넌트) — `List<ProductionData>` FIFO 큐, `Update()`에서 맨 앞 항목 `RemainTime`을 깎다가 0 이하면 완료 처리. `BuildingController`가 `GetProductionQueue`/`SpawnUnit`/`CancelProduction`/`ClearQueue`로 위임. 연구도 동일한 모양(`ResearchQueue`)으로 만들면 기존 패턴과 100% 일관됨.
- **공격력/방어력 저장**: `UnitController.cs:33-34`에 `attackDamage`/`armor`가 인스턴스별 `private` 필드로 존재, `GetAttackDamage()`/`GetArmor()` getter만 있음(`UnitController.cs:1291-1292`). 전역 보너스를 저장할 곳이 없음 → `RTSUnitController`(사실상 싱글턴, `FindFirstObjectByType`로 어디서든 참조)에 `globalAttackBonus`/`globalArmorBonus`를 두고, getter가 이 값을 더해서 반환하도록 하면 **이미 존재하는 유닛과 앞으로 생산될 유닛 모두에게** 자동 적용됨 (별도로 유닛 리스트를 순회할 필요 없음).
- **데미지 처리**: `HealthManager.GetDamage(int damage, ...)` (`HealthManager.cs:65`)는 최종 데미지를 그대로 깎기만 하고 방어력 개념이 없음. 방어력 감산은 공격자 쪽 호출부인 `UnitController.Attack()` (`UnitController.cs:781-808`)에서 `GetDamage` 호출 전에 계산해야 함. 현재는 `attackDamage` 값을 감산 없이 그대로 넘기고 있어 `armor` 필드는 UI 툴팁 표시 외에는 아무 효과가 없는 상태(즉 방어력 감산 로직 자체가 이번에 처음 생기는 것).
- **친화 사격(Friendly Fire)** 이미 구현되어 있어(doc/0008, doc/0051) 플레이어 유닛끼리도 `Attack()`을 탄다 → 방어력 상승 효과를 적/아군 상관없이 바로 테스트 가능.
- **기존 Tech 관련 기능**: doc/0189는 "건물 건설 선행 조건"(건물 A를 지어야 건물 B를 지을 수 있음)이라 이번 스탯 연구와는 무관. 코드베이스에 `Tech`/`Upgrade`/`Research` 시스템은 이번이 처음.
- EnemyController도 동일한 모양의 `armor`/`attackDamage` 필드를 갖고 있지만(doc/0021) 적이 공격하는 로직 자체가 아직 없어서, 이번 방어력 감산 로직은 "플레이어 유닛이 공격받을 때"(주로 friendly fire로 테스트) + "플레이어 유닛이 적을 공격할 때(적의 기존 armor 반영)" 양쪽에 자연스럽게 적용되도록 범용으로 짜되, 연구로 얻는 보너스 자체는 플레이어 쪽에만 붙습니다.

## 설계

### 데미지 감산 공식
`최종 데미지 = Max(1, 공격력 - 방어력)` (최소 1 데미지는 보장 — 흔한 RTS 관례, 방어력이 아무리 높아도 0데미지로 절대 못 죽는 상황 방지).

### 레벨업 구조
공격/방어 각각 0(미연구)~3(최대) 레벨을 가지며, 한 번에 "현재 레벨+1"만 큐에 넣을 수 있습니다(레벨 1을 연구 중일 때 레벨 2를 미리 큐잉할 수는 없음 — 1업이 끝나야 2업 버튼이 다시 활성화됨). 레벨업마다 고정 보너스량만큼 전역 스탯에 누적됩니다(예: 공격 +5/레벨이면 3업 완료 시 총 +15).
비용/연구시간/레벨당 보너스량은 전부 Lab의 `ResearchQueue` 컴포넌트에 Inspector로 노출해 나중에 쉽게 조정 가능하게 합니다. **비용은 요청하신 대로 1업 100/100 → 2업 200/200 → 3업 250/250 (광물/가스, 공격·방어 공통)** 으로 기본값을 넣었고, 연구시간/보너스량은 확정값을 안 주셔서 임의 제안값(60/90/120초, 공격 +5·방어 +2 / 레벨)을 넣었습니다 — 인스펙터에서 바로 조정 가능합니다.

### 새 파일: `Assets/Scripts/Upgrade/UpgradeManager.cs`

연구로 얻은 전역 보너스를 저장/제공하는 **유일한** 컴포넌트. `ResearchQueue`도 `UnitController`도 이 컴포넌트를 직접 참조하지 않고, 항상 `RTSUnitController`의 메서드를 거쳐서만 값을 올리거나 읽습니다 (요청하신 "시스템적 독립성" — 연구소 큐 시스템과 유닛 시스템이 서로 몰라도 되고, 둘 다 RTSUnitController라는 창구 하나만 알면 됨).

```csharp
using UnityEngine;

// 연구(공격력/방어력) 종류. Upgrade 시스템과 연구소 큐(ResearchQueue) 양쪽에서 공유하는 타입.
public enum ResearchType
{
    Attack,
    Armor
}

// 연구로 얻은 전역 공격력/방어력 보너스를 저장하는 컴포넌트.
// 이 컴포넌트는 RTSUnitController에서만 참조한다 - ResearchQueue(연구소)나 UnitController(유닛)가
// 직접 이 컴포넌트를 찾거나 호출하지 않는다. 항상 RTSUnitController.AddGlobalBonus/GlobalAttackBonus/
// GlobalArmorBonus를 거쳐서만 값이 오가도록 해서, 연구소 큐 시스템과 유닛 시스템이 서로 독립적으로 유지된다.
public class UpgradeManager : MonoBehaviour
{
    private int attackBonus;
    private int armorBonus;

    public int GetBonus(ResearchType type) => type == ResearchType.Attack ? attackBonus : armorBonus;

    public void AddBonus(ResearchType type, int amount)
    {
        if (type == ResearchType.Attack)
            attackBonus += amount;
        else
            armorBonus += amount;
    }
}
```

### 새 파일: `Assets/Scripts/Building/ResearchQueue.cs`

`UnitSpawner.cs`와 동일한 모양(FIFO 큐 + Update에서 타이머 차감)이지만, 완료 시 `Instantiate` 대신 `RTSUnitController.AddGlobalBonus(type, amount)`를 호출해 해당 타입의 레벨을 1 올리고 보너스를 반영합니다. `UpgradeManager`를 직접 참조하지 않습니다.

```csharp
using System.Collections.Generic;
using UnityEngine;

// 연구 대기열 한 항목의 상태 (어떤 종류를, 몇 레벨째 연구 중인지)
[System.Serializable]
public class ResearchData
{
    public ResearchType Type;
    public int Level; // 이 연구가 완료되면 도달하는 레벨 (1, 2, 3)
    public float RemainTime;
    public float TotalTime;

    public float Progress => 1f - (RemainTime / TotalTime);

    public ResearchData(ResearchType type, int level, float researchTime)
    {
        Type = type;
        Level = level;
        RemainTime = researchTime;
        TotalTime = researchTime;
    }
}

// 연구소(Lab)에 부착되어 공격력/방어력 연구 대기열(레벨 1~3)을 관리하고, 완료되면 전역 보너스를 올리는 컴포넌트.
public class ResearchQueue : MonoBehaviour
{
    public const int MaxLevel = 3;

    // 공격/방어 각각 "다음 레벨" 1개씩만 의미가 있으므로 동시에 최대 2개(둘 다 큐잉)까지만 허용
    private const int MaxQueueSize = 2;

    [Header("레벨별 연구 시간(초) - [0]=1업 [1]=2업 [2]=3업")]
    [SerializeField] private float[] attackResearchTime = { 60f, 90f, 120f };
    [SerializeField] private float[] armorResearchTime = { 60f, 90f, 120f };

    [Header("레벨별 연구 비용 (광물/가스, 공격·방어 공통) - [0]=1업 [1]=2업 [2]=3업")]
    [SerializeField] private int[] researchOreCost = { 100, 200, 250 };
    [SerializeField] private int[] researchGasCost = { 100, 200, 250 };

    [Header("레벨업 1회당 적용되는 보너스량 (누적)")]
    [SerializeField] private int attackBonusPerLevel = 1;
    [SerializeField] private int armorBonusPerLevel = 1;

    private readonly List<ResearchData> researchQueue = new();

    private RTSUnitController rtsController;
    private BuildingController buildingController;

    private int attackLevel; // 0~3
    private int armorLevel;  // 0~3

    void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        buildingController = GetComponentInParent<BuildingController>();
    }

    void Update()
    {
        Research();
    }

    public int GetLevel(ResearchType type) => type == ResearchType.Attack ? attackLevel : armorLevel;

    private bool IsQueued(ResearchType type) =>
        researchQueue.Exists(r => r.Type == type);

    public bool CanEnqueue(ResearchType type) =>
        GetLevel(type) < MaxLevel && !IsQueued(type) && researchQueue.Count < MaxQueueSize;

    // "현재 레벨+1"을 연구하는 데 드는 비용. 이미 최대 레벨이면 (0,0) 반환.
    public (int ore, int gas) GetCost(ResearchType type)
    {
        int nextLevel = GetLevel(type) + 1;
        if (nextLevel > MaxLevel)
            return (0, 0);

        return (researchOreCost[nextLevel - 1], researchGasCost[nextLevel - 1]);
    }

    // 지정한 타입의 다음 레벨을 대기열에 추가한다. (자원 소모는 호출측 RTSUnitController.TryResearch에서 먼저 처리된 뒤 호출됨)
    public void Enqueue(ResearchType type)
    {
        if (!CanEnqueue(type))
            return;

        int nextLevel = GetLevel(type) + 1;
        float time = (type == ResearchType.Attack ? attackResearchTime : armorResearchTime)[nextLevel - 1];

        researchQueue.Add(new ResearchData(type, nextLevel, time));
    }

    // 매 프레임 대기열 맨 앞 항목의 남은 시간을 줄이고, 0 이하가 되면 연구를 완료 처리한다.
    private void Research()
    {
        if (researchQueue.Count == 0)
            return;

        if (buildingController != null && !TerritoryManager.IsInsideAlliedTerritory(transform.position))
            return; // 영토 밖이면 타이머가 멈춘다 (생산 큐와 동일한 규칙)

        ResearchData current = researchQueue[0];
        current.RemainTime -= Time.deltaTime;

        if (current.RemainTime > 0)
            return;

        ResearchType type = current.Type;
        researchQueue.RemoveAt(0);
        Complete(type);
    }

    private void Complete(ResearchType type)
    {
        if (type == ResearchType.Attack)
            attackLevel++;
        else
            armorLevel++;

        int bonus = type == ResearchType.Attack ? attackBonusPerLevel : armorBonusPerLevel;
        rtsController.AddGlobalBonus(type, bonus); // UpgradeManager를 직접 만지지 않고 RTSUnitController를 거쳐서 반영
    }

    // 대기열의 특정 인덱스 항목을 취소하고, 환불을 위해 그 ResearchType을 int로 반환한다 (유효하지 않으면 -1).
    // (레벨은 Complete()에서만 올라가므로, 큐에서 제거만 하는 시점엔 아직 레벨이 바뀌지 않아 GetCost()로 그대로 환불액을 되짚을 수 있음)
    public int Cancel(int index)
    {
        if (index < 0 || index >= researchQueue.Count)
            return -1;

        ResearchType type = researchQueue[index].Type;
        researchQueue.RemoveAt(index);
        return (int)type;
    }

    // 건물 파괴 시 호출: 대기열에 남아있던 항목 전체를 반환(제거)한다 - 환불 자체는 호출측이 처리.
    public List<ResearchData> ClearQueue()
    {
        List<ResearchData> remaining = new List<ResearchData>(researchQueue);
        researchQueue.Clear();
        return remaining;
    }

    public IReadOnlyList<ResearchData> GetResearchQueue() => researchQueue;

    public float GetResearchProgress() => researchQueue.Count == 0 ? 0f : researchQueue[0].Progress;
}
```

### `Assets/Scripts/Building/BuildingController.cs` — Lab 위임 메서드 추가

기존 코드 (UnitSpawner 캐싱 부분):
```csharp
private UnitSpawner UnitSpawner;
...
UnitSpawner = GetComponentInChildren<UnitSpawner>();
```

변경 코드:
```csharp
private UnitSpawner UnitSpawner;
private ResearchQueue researchQueue;
...
UnitSpawner = GetComponentInChildren<UnitSpawner>();
researchQueue = GetComponentInChildren<ResearchQueue>();
```

기존 코드 (생산 위임 메서드들 근처, `ClearProductionQueue()` 아래):
```csharp
public IReadOnlyList<ProductionData> ClearProductionQueue()
{
    return UnitSpawner != null ? UnitSpawner.ClearQueue() : null;
}
```

변경 코드 (연구 위임 메서드 추가):
```csharp
public IReadOnlyList<ProductionData> ClearProductionQueue()
{
    return UnitSpawner != null ? UnitSpawner.ClearQueue() : null;
}

// 연구소(Lab) 전용 위임 메서드들 (UnitSpawner 위임 메서드와 동일한 패턴)
public bool CanEnqueueResearch(ResearchType type) =>
    researchQueue != null && researchQueue.CanEnqueue(type);

public int GetResearchLevel(ResearchType type) =>
    researchQueue != null ? researchQueue.GetLevel(type) : 0;

public (int ore, int gas) GetResearchCost(ResearchType type) =>
    researchQueue != null ? researchQueue.GetCost(type) : (0, 0);

public void EnqueueResearch(ResearchType type) => researchQueue?.Enqueue(type);

public IReadOnlyList<ResearchData> GetResearchQueue() => researchQueue?.GetResearchQueue();

public int CancelResearch(int index) => researchQueue != null ? researchQueue.Cancel(index) : -1;

public List<ResearchData> ClearResearchQueue() =>
    researchQueue != null ? researchQueue.ClearQueue() : null;
```

기존 코드 (`Die()` 안, 생산 큐 환불 줄):
```csharp
rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
```

변경 코드 (연구 큐 환불 추가):
```csharp
rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
rtsController?.RefundResearchQueue(this, ClearResearchQueue()); // 대기열에 남아있던 연구들 환불
```

### `Assets/Scripts/System/RTSUnitController.cs`

**(A) 전역 보너스 창구 추가** (자원 관련 필드들 근처) — 실제 저장은 `UpgradeManager`가 하고, `RTSUnitController`는 그 창구 역할만 함 (연구소 큐도 유닛도 `UpgradeManager`를 직접 참조하지 않고 이 메서드들만 사용):
```csharp
[SerializeField] private UpgradeManager upgradeManager; // 인스펙터에서 연결 (resourceManager와 동일한 방식)

public int GlobalAttackBonus => upgradeManager.GetBonus(ResearchType.Attack);
public int GlobalArmorBonus => upgradeManager.GetBonus(ResearchType.Armor);

// ResearchQueue가 연구 완료 시 호출 (UpgradeManager를 직접 모른 채로 보너스를 반영)
public void AddGlobalBonus(ResearchType type, int amount) => upgradeManager.AddBonus(type, amount);
```

**(B) 연구 요청/큐 조회/취소/환불 메서드 추가** (`TryProduceUnit` 근처, 생산 메서드들과 같은 구획):
```csharp
// 연구 요청 (대표 건물 기준 - Lab이 여러 개 선택된 경우도 대표 건물 하나만 처리)
public bool TryResearch(ResearchType type)
{
    BuildingController building = GetRepresentativeBuilding();
    if (building == null || !building.CanEnqueueResearch(type))
        return false;

    var (ore, gas) = building.GetResearchCost(type);

    if (!resourceManager.TrySpend(ore, gas))
    {
        Debug.Log("자원부족!");
        return false;
    }

    building.EnqueueResearch(type);
    return true;
}

public IReadOnlyList<ResearchData> GetResearchQueue()
{
    return GetRepresentativeBuilding()?.GetResearchQueue();
}

public bool CanResearch(ResearchType type)
{
    BuildingController building = GetRepresentativeBuilding();
    return building != null && building.CanEnqueueResearch(type);
}

// 대기열 취소 (취소된 연구 비용만큼 환불, 대표 건물 기준)
public void CancelResearch(int index)
{
    BuildingController building = GetRepresentativeBuilding();
    if (building == null)
        return;

    int canceledType = building.CancelResearch(index);
    if (canceledType < 0)
        return;

    RefundResearch(building, (ResearchType)canceledType);
}

private void RefundResearch(BuildingController building, ResearchType type)
{
    var (ore, gas) = building.GetResearchCost(type);
    resourceManager.AddOre(ore);
    resourceManager.AddGas(gas);
}

// 건물 파괴 시 대기열에 남아있던 연구들 환불 (RefundProductionQueue와 동일한 패턴)
public void RefundResearchQueue(BuildingController building, List<ResearchData> queue)
{
    if (queue == null)
        return;

    foreach (ResearchData item in queue)
        RefundResearch(building, item.Type);
}
```

**(C) 버튼 데이터 헬퍼 추가** (`UnitButtonAction`/`BuildingButtonAction` 근처) — 버튼 제목에 "다음 레벨"을 표시하고, 최대 레벨이면 "MAX"로 표시:
```csharp
private ButtonAction ResearchButtonAction(ResearchType type)
{
    BuildingController building = GetRepresentativeBuilding();
    int currentLevel = building != null ? building.GetResearchLevel(type) : 0;
    var (ore, gas) = building != null ? building.GetResearchCost(type) : (0, 0);

    string baseTitle = type == ResearchType.Attack ? "Attack Upgrade" : "Armor Upgrade";
    bool maxed = currentLevel >= ResearchQueue.MaxLevel;

    string title = maxed ? $"{baseTitle} (MAX)" : $"{baseTitle} Lv.{currentLevel + 1}";
    string description = maxed
        ? $"{baseTitle} fully researched."
        : (type == ResearchType.Attack
            ? $"Research increased attack damage for all units. (Lv.{currentLevel} → Lv.{currentLevel + 1})"
            : $"Research increased armor for all units. (Lv.{currentLevel} → Lv.{currentLevel + 1})");

    return ButtonAction.WithCost(() => TryResearch(type), title, description, ore, gas, 0);
}
```

**(D) `UpdateUI()`의 `BuildingState.Lab` 분기 분리:**

기존 코드 (`RTSUnitController.cs:1215-1220`):
```csharp
case BuildingState.SupplyDepot:
case BuildingState.Lab:
case BuildingState.None:
    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
    uIController.HideProductionUI();
    break;
```

변경 코드:
```csharp
case BuildingState.Lab:
    uIController.ShowLabPanel(
        ResearchButtonAction(ResearchType.Attack), CanResearch(ResearchType.Attack),
        ResearchButtonAction(ResearchType.Armor), CanResearch(ResearchType.Armor));

    uIController.ShowResearchUI(GetResearchQueue(), CancelResearch);
    break;

case BuildingState.SupplyDepot:
case BuildingState.None:
    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
    uIController.HideProductionUI();
    uIController.HideResearchUI();
    break;
```

(다른 4개 케이스 `MainBaseSelect`/`Tier1Select`/`Tier2Select`/`Tier3Select`에도 `uIController.HideResearchUI();`를 `ShowProductionUI` 옆에 추가해, Lab에서 다른 건물로 선택을 옮겼을 때 연구 큐 UI 상태가 안 꺼진 채 남지 않도록 정리합니다.)

### `Assets/Scripts/UI/UIController.cs`

**(A) enum에 Lab 상태 추가:**

기존 코드:
```csharp
public enum UISelectionState
{
    None,
    Worker,
    CombatUnit,
    BuildMode,
    Tier1Building,
    Tier2Building,
    Tier3Building,
    MainBase,
    BaseStructureSelect
}
```

변경 코드:
```csharp
public enum UISelectionState
{
    None,
    Worker,
    CombatUnit,
    BuildMode,
    Tier1Building,
    Tier2Building,
    Tier3Building,
    MainBase,
    LabBuilding,
    BaseStructureSelect
}
```

**(B) 아이콘 필드 추가** (`Unit Icons` 헤더 아래에):
```csharp
[Header("Research Icons (ShowLabPanel)")]
[SerializeField] private Sprite attackResearchIcon;
[SerializeField] private Sprite armorResearchIcon;
```

**(C) `ShowLabPanel` 추가** (`ShowAirportPanel` 아래):
```csharp
// Lab
// 연구소 선택 패널 (공격력/방어력 업그레이드 버튼)
public void ShowLabPanel(
    ButtonAction onAttackResearch, bool attackInteractable,
    ButtonAction onArmorResearch, bool armorInteractable)
{
    CurrentState = UISelectionState.LabBuilding;

    SetCommands(
        new CommandButtonData[]
        {
            new CommandButtonData(attackResearchIcon, onAttackResearch, attackInteractable),
            new CommandButtonData(armorResearchIcon, onArmorResearch, armorInteractable)
        },
        LiftSlotOnlyProtected);
}
```

**(D) 연구 큐 UI** — 기존 생산 큐 UI(`productionPanel`/`queueSlots`/`progressSlider`)를 그대로 재사용합니다 (Lab 패널과 생산 패널은 동시에 표시될 일이 없으므로 UI 오브젝트를 새로 안 만들어도 됨).

기존 코드 (필드 선언부):
```csharp
private IReadOnlyList<ProductionData> currentQueue;
private bool isShowingProductionQueue;
```

변경 코드:
```csharp
private IReadOnlyList<ProductionData> currentQueue;
private bool isShowingProductionQueue;

private IReadOnlyList<ResearchData> currentResearchQueue;
private bool isShowingResearchQueue;
```

기존 코드 (`ShowProductionUI`/`HideProductionUI`):
```csharp
public void ShowProductionUI(
    IReadOnlyList<ProductionData> queue,
    Action<int> onCancel)
{
    if (productionPanel != null)
        productionPanel.SetActive(true);

    currentQueue = queue;
    isShowingProductionQueue = true;

    UpdateQueue(queue, onCancel);
}

public void HideProductionUI()
{
    if (productionPanel != null)
        productionPanel.SetActive(false);

    foreach (var slot in queueSlots)
        slot.Clear();

    progressSlider.gameObject.SetActive(false);

    currentQueue = null;
    isShowingProductionQueue = false;
}
```

변경 코드 (상대 큐 상태 정리 + 연구용 대응 메서드 추가):
```csharp
public void ShowProductionUI(
    IReadOnlyList<ProductionData> queue,
    Action<int> onCancel)
{
    if (productionPanel != null)
        productionPanel.SetActive(true);

    currentQueue = queue;
    isShowingProductionQueue = true;

    currentResearchQueue = null;
    isShowingResearchQueue = false;

    UpdateQueue(queue, onCancel);
}

public void HideProductionUI()
{
    if (productionPanel != null)
        productionPanel.SetActive(false);

    foreach (var slot in queueSlots)
        slot.Clear();

    progressSlider.gameObject.SetActive(false);

    currentQueue = null;
    isShowingProductionQueue = false;
}

// 연구 대기열 슬롯 UI 갱신 (UpdateQueue와 동일한 패턴이지만, UnitDataSO 조회 대신 ResearchType→아이콘 매핑을 그대로 씀)
public void UpdateResearchQueue(
    IReadOnlyList<ResearchData> queue,
    Action<int> onCancel)
{
    if (queue == null)
    {
        for (int i = 0; i < queueSlots.Length; i++)
            SetEmptyQueueSlot(i);

        return;
    }

    for (int i = 0; i < queueSlots.Length; i++)
    {
        if (i >= queue.Count)
        {
            SetEmptyQueueSlot(i);
            continue;
        }

        int queueIndex = i;
        Sprite icon = queue[queueIndex].Type == ResearchType.Attack ? attackResearchIcon : armorResearchIcon;

        queueSlots[i].SetData(
            new CommandButtonData(icon, () => onCancel(queueIndex))
        );
    }
}

public void ShowResearchUI(
    IReadOnlyList<ResearchData> queue,
    Action<int> onCancel)
{
    if (productionPanel != null)
        productionPanel.SetActive(true);

    currentResearchQueue = queue;
    isShowingResearchQueue = true;

    currentQueue = null;
    isShowingProductionQueue = false;

    UpdateResearchQueue(queue, onCancel);
}

public void HideResearchUI()
{
    if (productionPanel != null)
        productionPanel.SetActive(false);

    foreach (var slot in queueSlots)
        slot.Clear();

    progressSlider.gameObject.SetActive(false);

    currentResearchQueue = null;
    isShowingResearchQueue = false;
}
```

기존 코드 (`UpdateProductionProgress`):
```csharp
private void UpdateProductionProgress()
{
    if (!isShowingProductionQueue ||
        currentQueue == null ||
        currentQueue.Count == 0)
    {
        progressSlider.gameObject.SetActive(false);
        return;
    }

    progressSlider.gameObject.SetActive(true);
    progressSlider.value = currentQueue[0].Progress;
}
```

변경 코드:
```csharp
private void UpdateProductionProgress()
{
    if (isShowingResearchQueue && currentResearchQueue != null && currentResearchQueue.Count > 0)
    {
        progressSlider.gameObject.SetActive(true);
        progressSlider.value = currentResearchQueue[0].Progress;
        return;
    }

    if (!isShowingProductionQueue ||
        currentQueue == null ||
        currentQueue.Count == 0)
    {
        progressSlider.gameObject.SetActive(false);
        return;
    }

    progressSlider.gameObject.SetActive(true);
    progressSlider.value = currentQueue[0].Progress;
}
```

### `Assets/Scripts/Unit/UnitController.cs` — 전역 보너스 반영 + 방어력 감산

기존 코드:
```csharp
public int GetAttackDamage() => attackDamage;
public int GetArmor() => armor;
```

변경 코드:
```csharp
public int GetAttackDamage() => attackDamage + (rtsController != null ? rtsController.GlobalAttackBonus : 0);
public int GetArmor() => armor + (rtsController != null ? rtsController.GlobalArmorBonus : 0);
```

기존 코드 (`Attack()`):
```csharp
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    targetHealth.GetDamage(attackDamage, transform.position, attackType); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용
    GetComponent<UnitEffects>()?.PlayAttack();
}
```

변경 코드:
```csharp
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    int targetArmor = GetTargetArmor(enemy);
    int finalDamage = Mathf.Max(1, GetAttackDamage() - targetArmor); // 방어력만큼 감산, 최소 1 데미지는 보장
    targetHealth.GetDamage(finalDamage, transform.position, attackType); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용
    GetComponent<UnitEffects>()?.PlayAttack();
}
```

새 private 헬퍼 추가 (`Attack()` 아래):
```csharp
// 공격 대상의 방어력을 조회한다 (아군 유닛이면 연구 보너스 포함, 적 유닛이면 EnemyController의 armor, 그 외(건물/자원)는 0).
private int GetTargetArmor(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var friendlyUnit))
        return friendlyUnit.GetArmor();

    if (target.TryGetComponent<EnemyController>(out var enemyUnit))
        return enemyUnit.GetArmor();

    return 0;
}
```

## 요약 / 영향받는 파일

| 파일 | 변경 내용 |
|---|---|
| `Assets/Scripts/Upgrade/UpgradeManager.cs` (신규) | 전역 공격/방어 보너스 저장 컴포넌트, `ResearchType` enum 정의 |
| `Assets/Scripts/Building/ResearchQueue.cs` (신규) | 연구 대기열 컴포넌트 (UnitSpawner와 동일한 패턴), 레벨 1~3 관리 |
| `Assets/Scripts/Building/BuildingController.cs` | ResearchQueue 위임 메서드 6개 + Die() 환불 훅 |
| `Assets/Scripts/System/RTSUnitController.cs` | UpgradeManager 창구(GlobalAttackBonus/GlobalArmorBonus/AddGlobalBonus), 연구 요청/취소/환불, ResearchButtonAction, UpdateUI() Lab 분기 |
| `Assets/Scripts/UI/UIController.cs` | LabBuilding 상태, 연구 아이콘 필드, ShowLabPanel, 연구용 큐 UI(ShowResearchUI/HideResearchUI/UpdateResearchQueue), UpdateProductionProgress 확장 |
| `Assets/Scripts/Unit/UnitController.cs` | GetAttackDamage/GetArmor가 RTSUnitController 경유로 전역 보너스 반영, Attack()에 방어력 감산 로직 추가 |

## 코드 변경 후 Unity 에디터에서 추가로 해야 할 작업 (제가 대신 할 수 없는 부분)

1. **Lab.prefab**에 `ResearchQueue` 컴포넌트를 부착 (UnitSpawner가 생산 건물의 자식으로 붙어있는 것과 동일한 방식 — Lab 프리팹 자식 오브젝트 혹은 루트에 추가).
2. 씬(또는 RTSUnitController가 있는 오브젝트)에 `UpgradeManager` 컴포넌트를 하나 추가하고, **RTSUnitController** 인스펙터의 `upgradeManager` 필드에 연결 (ResourceManager를 연결하는 것과 동일한 방식).
3. **UIController** 인스펙터에서 새로 추가된 `attackResearchIcon`/`armorResearchIcon` 스프라이트 2개를 연결.
4. (선택) `ResearchQueue`의 연구 시간/비용/보너스량 기본값(60/90/120초, 광물·가스 100/200/250, 공격·방어 +1/레벨)을 원하는 값으로 조정.

## 확정된 사항 (전부 반영 완료 — 2026-07-21 대화에서 최종 확정)

1. **레벨업 방식** — 1/2/3업 3단계, 레벨당 전역 보너스 누적 적용. 같은 타입 중복 큐잉 금지, 다른 타입은 동시 큐잉 가능.
2. **데미지 공식** — `Max(1, 공격력 - 방어력)`.
3. **레벨별 비용** — 1업 광물100/가스100, 2업 광물200/가스200, 3업 광물250/가스250 (공격/방어 공통).
4. **레벨별 연구 시간** — 1업 60초, 2업 90초, 3업 120초 (공격/방어 공통).
5. **레벨당 보너스량** — 공격 +1, 방어 +1 (3업 완료 시 각각 총 +3).
6. 시간/비용/보너스량 모두 `ResearchQueue` 인스펙터에서 숫자로 조정 가능.
7. **아키텍처**: 보너스 저장은 새 `Assets/Scripts/Upgrade/UpgradeManager.cs`로 분리, `ResearchQueue`/`UnitController` 모두 이를 직접 참조하지 않고 `RTSUnitController`를 거쳐서만 접근.

승인 완료 — 아래 설계대로 실제 코드에 적용합니다.
