## 2026-07-22

### 요청 내용

> 유닛을 더 추가하려고하는데 스크립터블오브젝트에 추가하면 바로바로 적용이 되도록 코드를 좀 전체적으로 수정해줘
> 원리는 스크립트 오브젝트에 추가하고 tier 라는 필드를 추가해서 0 = 메인기지, 1= 병영, 2=공장, 3=공항 이런식으로

`UnitDataSO`(유닛 데이터베이스) 리스트에 유닛 항목을 추가하고 `tier` 값(0=본진, 1=병영, 2=공장, 3=우주공항)만 지정하면, 코드를 건드리지 않아도 해당 건물의 생산 패널·생산 가능 여부에 자동으로 반영되도록 구조를 바꿔달라는 요청.

### 조사 내용

현재 구조를 훑어보니 "어느 건물이 어느 유닛을 생산 가능한가"라는 규칙이 데이터(SO)가 아니라 **코드에 3중으로 하드코딩**되어 있음.

1. **`RTSUnitController.GetProducerTagForUnit(int unitID)`** (`Assets/Scripts/System/RTSUnitController.cs:937`) — 유닛 ID → 건물 태그(`"MainBase"/"Tier1"/"Tier2"/"Tier3"`) switch문. `TryProduceUnit()`이 여기서 "이 건물이 이 유닛을 생산해도 되는지" 필터링.
2. **`UIController.ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel`** (`Assets/Scripts/UI/UIController.cs:1030~1092`) — 각각 정확히 고정된 개수(1개/2개/2개/2개)의 `ButtonAction` 파라미터만 받는 고정 시그니처. 새 유닛을 추가하려면 메서드 시그니처 자체를 바꿔야 함. 아이콘도 `UnitData.Icon`을 안 쓰고 `UIController`에 유닛별로 따로 박아둔 `workerIcon/marineIcon/vultureIcon/goliathIcon/tankIcon/wraithIcon/guardianIcon` 필드를 씀 (데이터 중복).
3. **`RTSUnitController.UpdateUI()`의 건물 패널 switch** (`RTSUnitController.cs:1260~1298`) — `BuildingState`(선택된 건물의 Unity 태그로 판정)별로 위 1./2.의 메서드를 직접 유닛 ID를 나열해서 호출.

`UnitData`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs`)에는 `tier` 필드가 아예 없음. 건물 쪽(`BuildingData`)도 마찬가지로 "이 건물이 어떤 유닛을 생산하는지" 필드가 없고, 대신 씬의 건물 프리팹에 붙은 Unity 태그(`MainBase`/`Tier1`/`Tier2`/`Tier3`/`SupplyDepot`/`Lab`)로만 건물 종류를 구분함 (`RTSUnitController.BuildingState` enum, `TagToBuildingState()`, `BuildingPriorityTags` — 이쪽은 이번 요청 범위 밖이라 그대로 둠, 건물 자체를 SO 추가만으로 자동 인식하게 하려면 별도 요청으로 다뤄야 함).

`Resources.LoadAll` 같은 자동 로드는 코드베이스 어디에도 없고, 모든 데이터는 `UnitDataSO`/`BuildingDataSO` 리스트를 인스펙터에서 채워서 쓰는 구조. 기존 `UnitData` 필드는 `[field: SerializeField]` 오토프로퍼티 패턴이라 인스펙터에 정상 노출됨 — 같은 패턴으로 `tier` 필드를 추가하면 인스펙터에서 바로 편집 가능.

### 변경 방향

1. **`UnitData`에 `tier`(int, 0~3) 필드 추가.** 이 값이 "어느 건물에서 생산되는가"의 유일한 소스가 되도록 함.
2. **`UnitData`에 `shortcutKey`(KeyCode) 필드도 함께 추가.** 현재 생산 버튼 단축키(W/A/S/I/P/F/D)가 `UpdateUI()`의 switch 안에 유닛별로 하드코딩되어 있어서, 이걸 그대로 두면 "SO에 유닛만 추가하면 끝"이 안 됨(추가한 유닛은 단축키가 없는 채로 나옴). 데이터로 옮겨야 진짜 데이터 기반이 됨.
3. **`GetProducerTagForUnit()`을 switch → `tier` 조회로 교체.** `tier` 값 자체를 태그 문자열로 바꾸는 고정 매핑(`{"MainBase","Tier1","Tier2","Tier3"}`)만 남기고, 유닛별 분기는 제거.
4. **`UIController`의 `ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel` 4개 메서드를 범용 메서드 1개(`ShowUnitProductionPanel`)로 통합.** 버튼 개수를 가변 배열로 받음. 하드코딩된 유닛별 아이콘 필드 7개는 삭제하고 `UnitData.Icon`을 사용.
5. **`RTSUnitController`에 `ShowUnitTierPanel(int tier)` 헬퍼 추가.** `unitDatabase.unitData`에서 `tier`가 일치하는 유닛을 모두 찾아 버튼 배열을 만들어 4번의 메서드에 넘김. `UpdateUI()`의 `MainBaseSelect/Tier1Select/Tier2Select/Tier3Select` 케이스는 이 헬퍼 호출 한 줄로 축소.
6. **기존 유닛 7종의 `.asset` 데이터에 `tier`/`shortcutKey` 값을 채워 넣음** (마이그레이션). 새 필드는 기본값이 `tier=0, shortcutKey=None`이라, 값을 안 채우면 기존 유닛 7종이 전부 본진(tier 0) 생산 목록으로 몰리고 단축키도 사라짐 — 반드시 같이 해야 하는 작업.

이후로는 새 유닛을 추가할 때: `UnitDataSO` 에셋의 `unitData` 리스트에 항목 추가 → 이름/능력치/아이콘/프리팹/**tier**/**shortcutKey** 채우기 → `RTSUnitController.UnitID`에 상수 하나 추가(유닛 식별용 ID 상수 자체는 여전히 코드지만, 이건 스폰/타겟팅 등 다른 시스템에서도 정수 ID를 참조하므로 이번 범위에서 제거하지 않음) 이 두 가지만 하면 해당 건물 패널에 자동으로 나타나고 생산도 즉시 동작함.

---

### 코드 변경 (예정)

#### 1) `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `tier`/`shortcutKey` 필드 추가

**기존 코드**
```csharp
    // 코드에서 유닛을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
    ...
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
}
```

**변경 코드**
```csharp
    // 코드에서 유닛을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }

    // 이 유닛을 생산할 수 있는 건물 종류: 0=본진(MainBase), 1=병영(Tier1), 2=공장(Tier2), 3=우주공항(Tier3).
    // 이 값만 지정하면 코드 수정 없이 해당 건물의 생산 패널에 자동으로 나타난다.
    [field: SerializeField]
    public int tier { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
    ...
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }

    // 이 유닛의 생산 버튼을 대신 누르는 키보드 단축키 (없으면 KeyCode.None)
    [field: SerializeField]
    public KeyCode shortcutKey { get; private set; }
}
```

#### 2) `Assets/Scripts/System/RTSUnitController.cs` — `GetProducerTagForUnit` 하드코딩 제거

**기존 코드** (937행 부근)
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
```

**변경 코드**
```csharp
    // tier(0~3) 값 자체를 건물 태그 문자열로 바꾸는 고정 매핑 (tier 필드의 정의 그 자체).
    private static readonly string[] TierProducerTags = { "MainBase", "Tier1", "Tier2", "Tier3" };

    // 유닛 ID가 어느 건물 태그에서 생산 가능한지, UnitData.tier 값으로부터 조회한다.
    // 다른 티어 건물이 섞여 선택됐을 때 그 유닛을 생산할 수 없는 건물을 걸러내기 위함.
    private string GetProducerTagForUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null || data.tier < 0 || data.tier >= TierProducerTags.Length)
            return null;

        return TierProducerTags[data.tier];
    }

    private static readonly UISelectionState[] TierPanelStates =
        { UISelectionState.MainBase, UISelectionState.Tier1Building, UISelectionState.Tier2Building, UISelectionState.Tier3Building };

    // tier(0=본진,1=병영,2=공장,3=우주공항)에 속한 유닛을 unitDatabase에서 모두 찾아 생산 버튼 패널을 구성한다.
    // UnitDataSO에 유닛을 추가하고 tier 값만 지정하면, 코드 수정 없이 해당 건물 패널에 자동으로 나타난다.
    private void ShowUnitTierPanel(int tier)
    {
        List<UnitData> unitsInTier = unitDatabase.unitData.FindAll(d => d.tier == tier);
        CommandButtonData[] commands = new CommandButtonData[unitsInTier.Count];

        for (int i = 0; i < unitsInTier.Count; ++i)
        {
            UnitData data = unitsInTier[i];
            commands[i] = new CommandButtonData(data.Icon, UnitButtonAction(() => TryProduceUnit(data.ID), data.ID, data.shortcutKey));
        }

        uIController.ShowUnitProductionPanel(TierPanelStates[tier], commands);
    }
```

#### 3) `Assets/Scripts/System/RTSUnitController.cs` — `UpdateUI()`의 건물 패널 switch 단순화

**기존 코드** (1262~1298행)
```csharp
                        case BuildingState.MainBaseSelect:
                            uIController.ShowMainBasePanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier1Select:
                            uIController.ShowBarracksPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier2Select:
                            uIController.ShowFactoryPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier3Select:
                            uIController.ShowAirportPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;
```

**변경 코드**
```csharp
                        case BuildingState.MainBaseSelect:
                            ShowUnitTierPanel(0);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier1Select:
                            ShowUnitTierPanel(1);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier2Select:
                            ShowUnitTierPanel(2);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier3Select:
                            ShowUnitTierPanel(3);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;
```

#### 4) `Assets/Scripts/UI/UIController.cs` — 고정 패널 메서드 4개 → 범용 메서드 1개, 하드코딩 아이콘 필드 삭제

**기존 코드** (182~189행, 유닛 아이콘 필드)
```csharp
    [Header("Unit Icons (ShowMainBasePanel / ShowBarracksPanel / ShowFactoryPanel / ShowAirportPanel)")]
    [SerializeField] private Sprite workerIcon;   // ShowMainBasePanel
    [SerializeField] private Sprite marineIcon;   // ShowBarracksPanel
    [SerializeField] private Sprite vultureIcon;  // ShowBarracksPanel
    [SerializeField] private Sprite goliathIcon;  // ShowFactoryPanel
    [SerializeField] private Sprite tankIcon;     // ShowFactoryPanel
    [SerializeField] private Sprite wraithIcon;   // ShowAirportPanel
    [SerializeField] private Sprite guardianIcon; // ShowAirportPanel
```

**변경 코드**
```csharp
    // 유닛 생산 패널 아이콘은 더 이상 여기 고정 필드로 두지 않고, UnitData.Icon을 그대로 사용한다
    // (ShowUnitProductionPanel 참고). 새 유닛을 추가해도 이 파일을 건드릴 필요가 없도록 하기 위함.
```

**기존 코드** (1028~1092행, 4개 고정 시그니처 메서드)
```csharp
    // Main base
    // 본진(커맨드센터) 선택 패널 (일꾼 생산 버튼)
    public void ShowMainBasePanel(ButtonAction onTrainWorker)
    {
        CurrentState = UISelectionState.MainBase;

        // 리프트 슬롯(8)은 여기 포함되지 않아도 건드리지 않는다 - RTSUnitController가 바로 뒤이어 독립적으로 채운다.
        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(workerIcon, onTrainWorker)
            },
            LiftSlotOnlyProtected);
    }

    // Barracks
    // 병영(Tier1 건물) 선택 패널 (마린/벌처 생산 버튼)
    public void ShowBarracksPanel(
    ButtonAction onMarine,
    ButtonAction onFirebat)
    {
        CurrentState = UISelectionState.Tier1Building;

        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(marineIcon, onMarine),
                new CommandButtonData(vultureIcon, onFirebat)
            },
            LiftSlotOnlyProtected);
    }

    // Factory
    // 공장(Tier2 건물) 선택 패널 (골리앗/탱크 생산 버튼)
    public void ShowFactoryPanel(
    ButtonAction onGoliath,
    ButtonAction onTank)
    {
        CurrentState = UISelectionState.Tier2Building;

        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(goliathIcon, onGoliath),
                new CommandButtonData(tankIcon, onTank)
            },
            LiftSlotOnlyProtected);
    }

    // Starport
    // 우주공항(Tier3 건물) 선택 패널 (레이스/가디언 생산 버튼)
    public void ShowAirportPanel(
    ButtonAction onWraith,
    ButtonAction onGuardian)
    {
        CurrentState = UISelectionState.Tier3Building;

        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(wraithIcon, onWraith),
                new CommandButtonData(guardianIcon, onGuardian)
            },
            LiftSlotOnlyProtected);
    }
```

**변경 코드**
```csharp
    // Unit production (MainBase / Tier1 / Tier2 / Tier3)
    // tier별 유닛 생산 패널 (본진/병영/공장/우주공항 공용). 버튼 개수가 유닛 데이터 개수만큼 가변적이므로
    // 4개로 나뉘어 있던 ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel을 통합했다.
    // 실제 버튼 구성(어느 유닛을 넣을지)은 RTSUnitController.ShowUnitTierPanel()이 UnitData.tier로 판단해서 넘긴다.
    public void ShowUnitProductionPanel(UISelectionState state, CommandButtonData[] commands)
    {
        CurrentState = state;

        // 리프트 슬롯(8)은 여기 포함되지 않아도 건드리지 않는다 - RTSUnitController가 바로 뒤이어 독립적으로 채운다.
        SetCommands(commands, LiftSlotOnlyProtected);
    }
```

#### 5) `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 기존 유닛 7종 마이그레이션

새 필드(`tier`, `shortcutKey`)는 기본값(`tier=0`, `shortcutKey=None`)으로 직렬화되므로, 기존 7개 항목에 지금 코드상 동작과 동일하도록 값을 채워 넣음 (아래는 코드에서 실제로 쓰이던 값 기준 — 설명 텍스트에 적힌 R/G 같은 단축키는 실제 코드(I/D)와 애초에 안 맞았던 기존 오탈자라 코드 기준을 따름):

| 유닛 (ID) | tier | shortcutKey |
|---|---|---|
| Worker Drone (1) | 0 (본진) | W |
| Assault Trooper (2) | 1 (병영) | A |
| Scout Drone (3) | 1 (병영) | S |
| Ranger IFV (4) | 2 (공장) | I |
| Pulasr Tank (5) | 2 (공장) | P |
| Firehawk (6) | 3 (우주공항) | F |
| Guardian Drone (7) | 3 (우주공항) | D |

### 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `tier`, `shortcutKey` 필드 추가
- `Assets/Scripts/System/RTSUnitController.cs` — `GetProducerTagForUnit` 데이터 기반으로 교체, `ShowUnitTierPanel` 헬퍼 신설, `UpdateUI()` switch 단순화
- `Assets/Scripts/UI/UIController.cs` — `ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel` → `ShowUnitProductionPanel` 통합, 하드코딩 유닛 아이콘 필드 7개 삭제
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 기존 유닛 7종에 `tier`/`shortcutKey` 값 채움 (마이그레이션)

**범위 밖(이번에 안 건드림):** 건물(`BuildingData`) 쪽은 여전히 씬 오브젝트의 Unity 태그(`MainBase`/`Tier1`/`Tier2`/`Tier3`/`SupplyDepot`/`Lab`)로 종류를 구분함. "새 건물을 `BuildingDataSO`에 추가하면 자동으로 태그/패널까지 인식"하는 부분은 이번 요청(유닛 tier)과 별개 주제라 포함하지 않음 — 필요하면 별도로 요청.

**구현 후 새 유닛 추가 절차:** ① `RTSUnitController.UnitID`에 상수 추가(유닛 식별 ID, 스폰/전투 등 다른 시스템도 참조하므로 유지) ② `UnitDataSO` 에셋에 새 `unitData` 항목 추가 → 이름/능력치/아이콘/프리팹/`tier`/`shortcutKey` 채우기. 이후엔 코드 수정 없이 해당 tier 건물의 생산 패널에 자동으로 나타나고 생산도 즉시 동작함.

---

**적용 완료** (2026-07-22) — 사용자 확인 후 위 변경 사항을 그대로 프로젝트에 적용함. 5개 파일 모두 제안대로 수정됨.
