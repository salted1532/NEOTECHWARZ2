# 0026 - 선택 정보 패널(Info/ProductionQueue/Squad) (설계 → 실제 구현)

> **번호에 대한 메모**: 이 문서는 원래 번호 없이 `doc/select-info-panel-design.md`로 2026-07-04에
> 작성된 "코드 수정 전, 설계만 정리한 검토 문서"다. 세션 로그 번호 규칙(0013)보다 먼저 작성되어
> 원래 몇 번째였는지 알 수 없어, 세션 로그를 전부 `doc/`로 옮기며 임의로 0026번을 부여했다.
>
> 이 설계가 다룬 범위는 이후 여러 세션에 걸쳐 나뉘어 구현/확장되었다:
> - 적/자원 선택 시 Info_panel(아이콘/이름/체력) — **[0003-enemy-resource-selection.md](0003-enemy-resource-selection.md)**
> - 자원 채취 명령 시 마커 깜빡임 피드백 — **[0004-resource-gather-marker-flash.md](0004-resource-gather-marker-flash.md)**
> - `UnitController`가 `IDestructible`을 구현하지 않아 죽은 유닛이 `selectedUnitList`에 남는 버그 수정
>   (이 문서가 3-2절에서 미리 우려했던 바로 그 문제) — **[0009-bugfix-missing-reference-exception.md](0009-bugfix-missing-reference-exception.md)**
> - `BuildingController`도 동일하게 `IDestructible` 미구현 버그 수정 — **[0014-friendly-fire-building-support.md](0014-friendly-fire-building-support.md)**
> - Squad Panel 페이지네이션(12마리 → 최대 60마리) — **[0017-squad-panel-pagination.md](0017-squad-panel-pagination.md)**
> - 공격력/방어력 호버 툴팁 추가 — **[0019-info-panel-attack-armor-hover-tooltip.md](0019-info-panel-attack-armor-hover-tooltip.md)**
>
> 아래는 **설계 당시 제안했던 코드(기존 코드)** → **실제로 구현된 현재 코드(변경 코드)** 형식으로 다시 정리했다.

## 1. 아이콘 보유 (`UnitController` / `BuildingController`)

**기존 코드 (설계 당시 — 아이콘을 스폰 시점에 주입하자는 제안)**
```csharp
// UnitController.cs
[SerializeField] private Sprite icon;
public Sprite GetIcon() => icon;

// UnitSpawner.Spawn() 안에서 Instantiate 직후
spawnunit.GetComponent<UnitController>().SetIcon(data.Icon); // (제안한 주입 방식)
```
```csharp
// BuildingDataSO.cs — BuildingData에 Icon 필드 추가 제안
[field: SerializeField]
public Sprite Icon { get; private set; }

// PlacementSystem.PlaceStructure() 안에서 Instantiate 직후 주입 제안
obj.GetComponent<BuildingController>().SetIcon(data.Icon);
```

**변경 코드 (실제 구현 — 스폰 시점 주입이 아니라 프리팹에 직접 아이콘을 미리 꽂아두는 방식)**
```csharp
// UnitController.cs
[SerializeField]
private Sprite icon; // Squad_panel 등 선택 UI에 표시할 아이콘
...
public Sprite GetIcon() => icon;
```
```csharp
// BuildingController.cs
[SerializeField]
private Sprite icon; // Info_panel 등 선택 UI에 표시할 아이콘
...
public Sprite GetIcon() => icon;
```

**차이점**
- `BuildingDataSO`(ScriptableObject)에는 결국 `Icon` 필드를 추가하지 않았다. 대신 `UnitController`/`BuildingController` 각 프리팹에 아이콘을 직접 인스펙터로 꽂아두는, 스폰 시점 주입 코드가 필요 없는 더 단순한 방식으로 구현됐다.
- `UnitSpawner.Spawn()`, `PlacementSystem.PlaceStructure()` 어디에도 아이콘 주입 코드는 없다 (현재도 `Instantiate`만 하고 끝).

## 2. `Die()` — 선택 목록에서 제거 (막힘 지점이었던 버그 수정)

**기존 코드 (설계 당시 지적한 문제 상황 — 실제 당시 코드)**
```csharp
// UnitController.cs
public void Die()
{
    gatherTargetNode?.LeaveQueue(this);

    RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
    controller?.UnitList.Remove(this);
    // selectedUnitList에서는 제거하지 않음 — 선택된 채로 죽으면 유령 참조가 남는 문제
    Destroy(gameObject);
}
```

**변경 코드 (실제 구현)**
```csharp
public void Die()
{
    gatherTargetNode?.LeaveQueue(this); // 대기열/채취 중에 사망해도 자리를 비워줌

    RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
    controller?.UnitList.Remove(this);
    controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록

    Destroy(gameObject);
}
```
```csharp
// BuildingController.cs — 동일한 패턴
public void Die()
{
    rtsController?.BuildingList.Remove(this);
    rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록

    Destroy(gameObject);
}
```

**차이점** 없음 — 설계 문서가 제안한 그대로 구현됨.

## 3. `UIController.cs` — Info Panel / Squad Panel

**기존 코드 (설계 당시 제안)**
```csharp
[SerializeField] private GameObject infoPanel;
[SerializeField] private Image infoIcon;
[SerializeField] private Slider infoHpSlider;
[SerializeField] private TextMeshProUGUI infoHpText;
[SerializeField] private GameObject squadPanel;
[SerializeField] private ProductionSlot[] squadSlots; // 12개, 콜백 null(표시만)

private HealthManager boundHealth;

public void ShowInfoPanel(Sprite icon, HealthManager health) { /* 구독 갈아끼우기 */ }
public void HideInfoPanel() { /* 구독 해제 */ }
public void ShowSquadPanel(IReadOnlyList<UnitController> units)
{
    // 최대 12개까지 SetData(new CommandButtonData(units[i].GetIcon(), null))
}
public void HideSquadPanel() { /* ... */ }
```

**변경 코드 (실제 구현)**
```csharp
[Header("Info Panel (SelectInfo)")]
[SerializeField] private GameObject infoPanel;
[SerializeField] private Image infoIcon;
[SerializeField] private TextMeshProUGUI infoNameText;
[SerializeField] private TextMeshProUGUI infoHpText;
[SerializeField] private Image attackDamageImage; // 0019에서 추가
[SerializeField] private Image armorImage;         // 0019에서 추가

private HealthManager infoBoundHealth;

public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health)
{
    ShowInfoPanel(icon, unitName, health, 0, 0);
}

public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health, int attackDamage, int armor)
{
    HideSquadPanel();

    if (infoPanel != null)
        infoPanel.SetActive(true);

    if (infoIcon != null)
    {
        infoIcon.sprite = icon;
        infoIcon.enabled = icon != null;
    }

    if (infoNameText != null)
        infoNameText.text = unitName;

    infoAttackDamage = attackDamage;
    infoArmor = armor;

    SetCombatStatsVisible(true);
    BindInfoHealth(health);
}

public void HideInfoPanel()
{
    if (infoPanel != null)
        infoPanel.SetActive(false);

    BindInfoHealth(null);
}

// Squad Panel: 클릭 시 해당 유닛 하나만 단일 선택으로 좁히는 콜백까지 지원 (설계안의 "표시만" 범위를 넘어섬),
// 이후 0017에서 12마리 고정 → 12 x 5페이지(최대 60마리)로 확장됨.
public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit) { /* ... */ }
public void HideSquadPanel() { /* ... */ }
```

**차이점**
- `infoHpSlider`(슬라이더)는 만들지 않고 `infoHpText`(텍스트, `"현재HP/최대HP"`)만 사용.
- Squad Panel은 "표시만"이 아니라 처음부터 클릭 시 단일 선택으로 좁히는 콜백(`onSelectUnit`)을 지원하도록 구현됐고, 이후 세션([0017-squad-panel-pagination.md](0017-squad-panel-pagination.md))에서 12마리 고정 상한을 12 × 5페이지(최대 60마리)로 확장했다.
- 이후 세션([0019-info-panel-attack-armor-hover-tooltip.md](0019-info-panel-attack-armor-hover-tooltip.md))에서 공격력/방어력 호버 툴팁을 위한 `attackDamageImage`/`armorImage`, `SetCombatStatsVisible` 등이 추가됨 — 설계 당시엔 없던 범위.

## 4. `RTSUnitController.cs` — 선택 상태에 따라 호출

**기존 코드 (설계 당시 제안)**
```csharp
private void UpdateSelectionInfoPanel()
{
    if (selectedUnitList.Count > 1)
    {
        uIController.ShowSquadPanel(selectedUnitList);
        return;
    }

    if (selectedUnitList.Count == 1)
    {
        UnitController unit = selectedUnitList[0];
        uIController.ShowInfoPanel(unit.GetIcon(), unit.GetComponent<HealthManager>());
        return;
    }

    if (selectedBuildingList.Count == 1)
    {
        BuildingController building = selectedBuildingList[0];
        uIController.ShowInfoPanel(building.GetIcon(), building.GetComponent<HealthManager>());
        return;
    }

    uIController.HideSquadPanel();
    uIController.HideInfoPanel();
}
```

**변경 코드 (실제 구현 — 별도 헬퍼로 분리하지 않고 기존 `UpdateUI()` 안에 통합)**
```csharp
// UpdateUI() 내 SelectState.UnitSelect 분기
if (selectedUnitList.Count > 1)
{
    uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit);
}
else if (selectedUnitList.Count == 1)
{
    UnitController unit = selectedUnitList[0];
    uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor());
}
else
{
    uIController.HideInfoPanel();
}

// UpdateUI() 내 SelectState.BuildingSelect 분기
if (selectedBuildingList.Count > 0)
{
    BuildingController building = selectedBuildingList[0];
    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetComponent<HealthManager>());
}
else
{
    uIController.HideInfoPanel();
}
```

**차이점**
- 별도의 `UpdateSelectionInfoPanel()` 메서드를 새로 만들지 않고, 기존 `UpdateUI()`의 유닛/건물 선택 `switch` 분기 안에 그대로 끼워 넣었다.
- `ShowInfoPanel`에 아이콘/체력뿐 아니라 이름(`GetUnitName`/`GetBuildingName`)도 함께 넘긴다 — 설계 당시엔 이름 표시가 범위에 없었다.
- Squad Panel 콜백으로 `ClickSelectUnit`을 넘겨 클릭 시 단일 선택 전환이 되도록 함 (설계 문서가 "나중에 추가 가능"이라고 남겨둔 부분이 실제로 처음부터 구현됨).

## 5. 설계 문서가 남긴 열린 질문에 대한 실제 결론
- **건물 다중 선택 시 Squad Panel?** → 실제로는 건물은 항상 `selectedBuildingList[0]`(첫 번째)만 `ShowInfoPanel`로 표시, Squad Panel은 유닛 전용으로 확정.
- **13마리 이상 선택 시?** → 자르지 않고 [0017-squad-panel-pagination.md](0017-squad-panel-pagination.md)에서 페이지네이션(최대 60마리)으로 해결.
- **Squad Panel 안에 개별 체력 표시?** → 여전히 아이콘만 표시, 체력바는 미구현(README 로드맵의 "유닛/건물 체력바 UI" 항목).
