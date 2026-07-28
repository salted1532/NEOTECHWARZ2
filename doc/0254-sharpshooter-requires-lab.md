# 0254 - 샤프슈터(Sharpshooter) 생산에 연구소(Lab) 선행 조건 추가 - 수정 제안

## 날짜

2026-07-28

## 요청 내용

"현재 병영(Tier1)에서 생성되는 샤프슈터가 연구소(lab)이 건설되어야 생성할수 있도록 해줘" — 지금은
병영(Barracks, Tier1)에서 바로 생산 가능한 Sharpshooter(NTA 진영, `New Unit Data SO.asset` ID=4)를,
연구소(Lab, `BuildingID.Lab = 6`)가 최소 1개 완공되어 있어야만 생산할 수 있도록 막아달라는 요청.

## 조사 내용

- 건물↔건물 선행 조건("테크 트리")은 [[0189]]에서 이미 구현되어 있다
  (`BuildingData.requiredBuildingID`, `RTSUnitController.HasCompletedBuilding` /
  `IsBuildingPrerequisiteMet`, `TryConstructBuilding`에서 최종 검사, `BuildingButtonAction`에서
  잠금 툴팁, `ShowBuildPanel`에서 버튼 `interactable`). 하지만 이건 "건물을 짓기 위한 선행 건물"
  조건이고, "유닛을 생산하기 위한 선행 건물" 조건은 아직 없다.
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs`의 `UnitData`에는 `tier`(생산 가능 건물 종류: 0=본진,
  1=병영, 2=공장, 3=우주공항)만 있고, "이 유닛을 생산하려면 특정 건물이 추가로 완공되어 있어야 한다"는
  필드가 없다.
- `Assets/Scripts/System/RTSUnitController.cs`
  - `ShowUnitTierPanel(int tier)`(1021번 줄)이 `unitDatabase.unitData`에서 `tier`가 일치하는 유닛을
    모두 찾아 생산 버튼 패널을 구성한다. 지금은 전부 `new CommandButtonData(icon, action)`(2-인자,
    `interactable` 기본값 `true`)로 만든다.
  - `TryProduceUnit(int unitID)`(1037번 줄)이 실제 생산(자원 차감 + 큐잉)을 담당하는 최종 관문이다.
    지금은 대기열 여유/자원/인구만 확인한다.
  - `UnitButtonAction(...)`(1176번 줄)이 생산 버튼의 제목/설명/비용 툴팁을 만든다.
    `BuildingButtonAction`(1210번 줄)은 이미 잠겨 있을 때 `"\n<color=red>Requires {건물명}</color>"`을
    설명에 덧붙이는 처리가 있는데, `UnitButtonAction`에는 이 처리가 없다.
  - `HasCompletedBuilding(int buildingID)`(1145번 줄)는 유닛/건물 구분 없이 재사용 가능한 헬퍼라서
    그대로 가져다 쓸 수 있다.
  - `BuildingID.Lab = 6`(120번 줄)으로 이미 정의되어 있다.
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset`의 Sharpshooter 항목(ID=4, tier=1)이
  실제로 수정할 대상. (OC 진영의 대응 유닛 `Railgunner`도 `OC Unit Data SO.asset`에 동일 스탯으로
  존재하지만, 이번 요청은 "샤프슈터"만 명시했으므로 Railgunner는 건드리지 않는 것으로 계획함 —
  대칭 적용을 원하면 알려달라고 아래에서 확인 요청.)

## 계획된 코드 변경

### 1. `Assets/Scripts/ScriptableObject/UnitDataSO.cs`

`UnitData`에 선행 건물 ID 필드 추가 (0 = 조건 없음). `BuildingDataSO.cs`의 `requiredBuildingID`와
동일한 패턴.

Before:
```csharp
    [field: SerializeField]
    public int productionTime { get; private set; }
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
```

After:
```csharp
    [field: SerializeField]
    public int productionTime { get; private set; }
    // 이 유닛을 생산하기 전에 미리 완공되어 있어야 하는 건물의 ID (RTSUnitController.BuildingID 상수, 0이면 조건 없음)
    [field: SerializeField]
    public int requiredBuildingID { get; private set; }
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
```

### 2. `Assets/Scripts/ScriptableObject/New Unit Data SO.asset`

Sharpshooter(ID 4) 항목에 `requiredBuildingID: 6`(Lab) 추가.

Before:
```yaml
  - <unitName>k__BackingField: 'Sharpshooter '
    <description>k__BackingField: 'train Sharpshooter

      shortcut key [<color=yellow>S</color>]'
    <ID>k__BackingField: 4
    <tier>k__BackingField: 1
```

After:
```yaml
  - <unitName>k__BackingField: 'Sharpshooter '
    <description>k__BackingField: 'train Sharpshooter

      shortcut key [<color=yellow>S</color>]'
    <ID>k__BackingField: 4
    <tier>k__BackingField: 1
    <requiredBuildingID>k__BackingField: 6
```
(다른 필드들 사이 실제 삽입 위치는 Unity가 다음에 에셋을 저장할 때 자동 정리되므로, 지금은 YAML
아무 곳에나 새 키만 추가해도 인스펙터에 정상 반영됨. 나머지 유닛들은 필드를 안 넣으면 기본값 0
(조건 없음)으로 유지.)

### 3. `Assets/Scripts/System/RTSUnitController.cs`

#### 3-1. 선행 조건 확인 헬퍼 추가 (`IsBuildingPrerequisiteMet` 바로 아래)

```csharp
    // unitID 생산에 필요한 선행 건물 조건을 만족하는지 (선행 건물이 없으면 항상 true)
    public bool IsUnitPrerequisiteMet(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null || data.requiredBuildingID == 0)
            return true;

        return HasCompletedBuilding(data.requiredBuildingID);
    }
```

#### 3-2. `TryProduceUnit`: 실제 생산을 막는 최종 관문에 선행 조건 검사 추가

Before:
```csharp
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (selectedBuildingList.Count == 0)
            return false;
```

After:
```csharp
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (!IsUnitPrerequisiteMet(unitID))
            return false;

        if (selectedBuildingList.Count == 0)
            return false;
```

#### 3-3. `ShowUnitTierPanel`: 잠겨 있으면 버튼 `interactable = false`

Before:
```csharp
        for (int i = 0; i < unitsInTier.Count; ++i)
        {
            UnitData data = unitsInTier[i];
            commands[i] = new CommandButtonData(data.Icon, UnitButtonAction(() => TryProduceUnit(data.ID), data.ID, data.shortcutKey));
        }
```

After:
```csharp
        for (int i = 0; i < unitsInTier.Count; ++i)
        {
            UnitData data = unitsInTier[i];
            commands[i] = new CommandButtonData(
                data.Icon,
                UnitButtonAction(() => TryProduceUnit(data.ID), data.ID, data.shortcutKey),
                IsUnitPrerequisiteMet(data.ID));
        }
```

#### 3-4. `UnitButtonAction`: 잠겨 있으면 툴팁에 선행 건물 안내 추가 (`BuildingButtonAction`과 동일 패턴)

Before:
```csharp
    private ButtonAction UnitButtonAction(Action callback, int unitID, KeyCode shortcut = KeyCode.None)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Train {data.unitName}."
            : data.description;

        return ButtonAction.WithCost(callback, data.unitName, description, data.mineral, data.gas, data.population, shortcut);
    }
```

After:
```csharp
    private ButtonAction UnitButtonAction(Action callback, int unitID, KeyCode shortcut = KeyCode.None)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Train {data.unitName}."
            : data.description;

        if (data.requiredBuildingID != 0 && !HasCompletedBuilding(data.requiredBuildingID))
        {
            string requiredName = GetBuildingName(data.requiredBuildingID);
            description += $"\n<color=red>Requires {requiredName}</color>";
        }

        return ButtonAction.WithCost(callback, data.unitName, description, data.mineral, data.gas, data.population, shortcut);
    }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/ScriptableObject/UnitDataSO.cs`(`requiredBuildingID` 필드 추가),
  `Assets/Scripts/ScriptableObject/New Unit Data SO.asset`(Sharpshooter → Lab 선행 조건 값 채움),
  `Assets/Scripts/System/RTSUnitController.cs`(`IsUnitPrerequisiteMet` 헬퍼 추가, `TryProduceUnit`에
  선행 조건 검사 추가, `ShowUnitTierPanel`이 잠금 플래그를 `CommandButtonData`에 전달,
  `UnitButtonAction`에 잠김 안내 문구 추가).
- 동작 변화:
  - 연구소(Lab)가 최소 1개 완공되어 있지 않으면 병영(Barracks) 생산 패널의 Sharpshooter 버튼이
    회색으로 비활성화되고, 마우스 클릭/단축키(S) 모두 먹지 않는다. 툴팁에는
    `"Requires Lab"`이 빨간 글씨로 덧붙는다.
  - 완공 여부는 [[0189]]와 동일하게 `BuildingList`(완공된 건물에만 붙는 `BuildingController`가
    자기 자신을 등록하는 리스트) 기준이라, 건설 중인(아직 완성 안 된) Lab은 조건을 만족시키지 못한다.
    다 지은 Lab이 나중에 파괴되면 다시 잠긴다(Lab을 다시 지어야 Sharpshooter 생산 가능).
  - 버튼이 비활성화돼도 `TryProduceUnit`이 최종 관문에서 한 번 더 막기 때문에, UI를 우회하는 경로가
    있어도 실제 생산까지 이어지지 않는다.
  - Assault Trooper/Scout Drone 등 다른 유닛은 `requiredBuildingID`를 지정하지 않으므로 지금과
    동일하게 항상 생산 가능하다.
  - 사용자 확인 결과 OC 진영의 대응 유닛 `Railgunner`(`OC Unit Data SO.asset`, `EnemyUnitDataSO`가
    재사용하는 동일한 `UnitData` 구조)에도 대칭으로 `requiredBuildingID: 6`을 채웠다. 다만 현재
    `Assets/Scripts/Enemy/` 쪽에는 `TryProduceUnit`에 해당하는 생산 큐/자원 검사 로직 자체가 아직
    없어서(적 유닛은 별도 스포너/씬 배치로 생성되는 것으로 보임), 이 값은 지금 당장 적 AI의 실제
    생산 제한으로 동작하지는 않는다 — 나중에 적 진영에도 동일한 생산 게이팅 시스템이 생기면 그대로
    반영되도록 데이터만 맞춰둔 것.

## 구현 결과

승인 후 위 계획대로 그대로 구현 완료. `New Unit Data SO.asset`(Sharpshooter)과
`OC Unit Data SO.asset`(Railgunner) 둘 다 `requiredBuildingID: 6` 적용.
