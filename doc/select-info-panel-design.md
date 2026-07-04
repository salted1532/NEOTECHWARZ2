# 선택 정보 패널(Info/ProductionQueue/Squad) 설계

작성일: 2026-07-04
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

Canvas의 `SelectInfo` 아래에 있는 3개 패널을 다음과 같이 동작시킨다.

- **`Info_panel`**: 단일 유닛/건물 선택 시 이미지(아이콘) + 체력을 계속 표시
- **`productionQueue_Panel`**: 선택된 대상이 "생산 건물"이면 `Info_panel` 위(함께)에 대기열 표시
- **`Squad_panel`**: 유닛을 2마리 이상(여러 마리) 선택하면 즉시 이 패널로 전환, 최대 12마리까지
  `selectedUnitList` 순서대로 아이콘을 표시. 이때 `Info_panel`/`productionQueue_Panel`은 끈다.

이 3개는 화면 하단의 커맨드 버튼 패널(`panelRoot`/`slots`, 이동·공격·정지 등)과는 **별개의 UI 그룹**이다.
커맨드 버튼 패널은 지금 로직(`ShowWorkerPanel`/`ShowAttackUnitPanel`/`Show*BuildingPanel`) 그대로 두고,
이번 작업은 `SelectInfo` 하위 3개 패널의 표시 여부만 다룬다.

## 2. 현재 상태 확인

씬(`SampleScene.unity`)의 `SelectInfo` 하위에는 이미 3개 형제 오브젝트가 있다.

- `productionQueue_Panel` — 자식으로 `ProductionSlot` 7개가 이미 배치돼 있고, `UIController.queueSlots`에
  연결되어 실제로 동작 중 (`UpdateQueue`/`ShowProductionUI`/`HideProductionUI`).
- `Slider`(진행률 바) — `UIController.progressSlider`에 연결되어 `UpdateProductionProgress()`가 갱신 중.
- `Info_panel` — **`m_IsActive: 0`, 자식 없음(빈 오브젝트)**. `UIController`에 이 오브젝트를 가리키는
  필드가 전혀 없다. 즉 아이콘/체력 표시는 아직 아무 것도 구현돼 있지 않다.
- `Squad_panel` — **씬에 아직 존재하지 않는다.** 새로 만들어야 한다 (에디터 작업).

`RTSUnitController.UpdateUI()`는 `RTScurrentSate`(`UnitSelect`/`BuildingSelect`/...)와
`UnitSelectState`(`Worker`/`AttackUnit`), `BuildingSelectState`(`MainBaseSelect`/`Tier1Select`/...)에 따라
커맨드 버튼 패널 + `ShowProductionUI`/`HideProductionUI`만 호출하고 있고, **"여러 개 선택됐는지"를 구분하는
상태가 아예 없다** (`SelectUnit()`이 새로 선택된 유닛 한 마리의 태그만 보고 `UnitSelectState`를 덮어씀).

## 3. 선행 필요 작업 (막힘 지점)

이 기능을 만들려면 아이콘/체력을 "선택된 실제 인스턴스"에서 가져와야 하는데, 지금 코드에는 그 경로가 없다.

### 3-1. 유닛/건물 인스턴스가 자기 아이콘을 모른다

`UnitData.Icon`(`UnitDataSO.cs`)에 아이콘 스프라이트가 이미 있지만, 스폰된 `UnitController` 인스턴스는
자신이 어떤 `UnitData`였는지(ID) 전혀 기억하지 않는다 (`UnitSpawner.Spawn()`이 `Instantiate`만 하고 끝).
건물 쪽은 한술 더 떠서 `BuildingData`에 `Icon` 필드 자체가 없다.

→ 필요한 최소 변경:
- `UnitController`에 `[SerializeField] private Sprite icon;` + `public Sprite GetIcon() => icon;` 추가,
  `UnitSpawner.Spawn()`에서 `Instantiate` 직후 `spawnunit.GetComponent<UnitController>()`에
  `data.Icon`을 넣어주는 setter 호출 한 줄 추가.
- `BuildingDataSO.BuildingData`에 `Icon` 필드 추가, `BuildingController`에도 동일하게
  `GetIcon()`/setter 추가, `PlacementSystem.cs:88` (`Instantiate(data.Prefab)`) 직후 채워줌.
- (참고) 이전에 논의했던 "유닛 종류(마린/탱크 등) 분류용 `UnitTypeID`"를 이번에 같이 넣으면
  `UnitDataSO`를 다시 찾아 조회하는 방식으로 갈 수도 있지만, 지금 당장은 스폰 시점에 아이콘 하나만
  직접 꽂아주는 쪽이 훨씬 적은 변경으로 끝난다. `UnitTypeID`는 필요해지면 별도로 붙이면 됨.

### 3-2. Info_panel의 체력 표시는 `HealthManager`를 붙잡아야 한다

`HealthManager`는 이미 `GetHealth()/GetMaxHealth()/OnHealthChanged` 이벤트를 갖고 있지만,
지금 이걸 구독해서 UI에 반영하는 코드가 프로젝트 어디에도 없다 (자원/인구 텍스트처럼 `Update()`에서
매 프레임 폴링하는 방식이 아니라, 선택된 대상이 바뀔 때마다 **구독 대상을 갈아끼워야** 하므로 별도 처리 필요).

- 선택이 바뀔 때: 이전에 구독하던 `HealthManager.OnHealthChanged`를 반드시 해제하고 새 대상으로 재구독.
  (해제 안 하면 이전 선택 대상이 데미지 받을 때도 계속 콜백이 날아옴)
- **관련 버그**: `UnitController.Die()`가 `RTSUnitController.UnitList`에서는 자신을 지우지만
  `selectedUnitList`에서는 지우지 않는다. 지금까지는 아무도 `selectedUnitList`의 살아있음을 신경 안 써서
  무해했지만, `Info_panel`이 선택된 유닛의 `HealthManager`를 직접 붙잡는 순간 "선택된 채로 죽은 유닛"의
  아이콘/체력이 화면에 계속 남는 문제가 생긴다. `Die()`(및 `BuildingController.Die()`)에서
  `selectedUnitList`/`selectedBuildingList`에서도 자신을 제거하도록 같이 고쳐야 한다.

### 3-3. "생산 건물인지" 판정 기준이 암묵적이다

지금은 `BuildingSelectState`가 `MainBaseSelect/Tier1Select/Tier2Select/Tier3Select`이면 생산 대기열을
보여주고, `SupplyDepot/Lab`이면 안 보여주는 식으로 **태그 기반 switch문에 하드코딩**돼 있다
(`RTSUnitController.UpdateUI()`). `productionQueue_Panel`을 `Info_panel`과 나란히 켜고 끄는 조건도
결국 이 판정을 그대로 재사용하면 되므로 새 판정 로직은 필요 없고, 같은 switch 안에 `Info_panel` 호출만
나란히 추가하면 된다. 다만 `BuildingController.GetProductionQueue()`가 `UnitSpawner`(자식 컴포넌트)가
없으면 널 참조 예외가 날 수 있어서, 이 참에 `UnitSpawner == null` 방어 코드를 추가해두는 걸 권장.

## 4. 변경 대상별 정리

### `UnitController.cs`
- `[SerializeField] private Sprite icon;` + `public Sprite GetIcon() => icon;` 추가
- `Die()`에서 `controller?.selectedUnitList.Remove(this);` 추가 (선택된 채로 죽었을 때 UI가 갱신되도록)

### `BuildingController.cs`
- 아이콘 필드/접근자 추가 (`UnitController`와 동일 패턴)
- `Die()`에서 `controller?.selectedBuildingList.Remove(this);` 추가
- `GetProductionQueue()`/`GetProductionProgress()`/`CancelProduction()`에 `UnitSpawner == null` 가드 추가
  (또는 `public bool HasProductionQueue => UnitSpawner != null;` 하나 추가해서 UI 쪽 판정에 재사용)

### `UnitSpawner.cs`
- `Spawn()`에서 `Instantiate` 직후 `spawnunit.GetComponent<UnitController>()`에 `data.Icon` 주입

### `BuildingDataSO.cs`
- `BuildingData`에 `Icon` (Sprite) 필드 추가

### `PlacementSystem.cs`
- 건물 `Instantiate` 직후(현재 88번째 줄 부근) 아이콘 주입

### `UIController.cs` (핵심 변경)
- 새 필드 그룹 추가
  - `[SerializeField] private GameObject infoPanel;`
  - `[SerializeField] private Image infoIcon;`
  - `[SerializeField] private Slider infoHpSlider;` / `[SerializeField] private TextMeshProUGUI infoHpText;`
  - `[SerializeField] private GameObject squadPanel;`
  - `[SerializeField] private ProductionSlot[] squadSlots;` (인스펙터에서 12개 연결, 기존 `ProductionSlot`을
    아이콘 표시 용도로 재사용 — 콜백은 `null`로 채워서 클릭 비활성으로 두거나, 나중에 "클릭 시 해당 유닛만
    단일 선택"으로 확장 가능)
  - 현재 구독 중인 대상을 추적할 `private HealthManager boundHealth;`
- 새 메서드
  - `ShowInfoPanel(Sprite icon, HealthManager health)` — `boundHealth`가 있으면 먼저 `OnHealthChanged` 구독
    해제, 새 `health` 구독 후 즉시 1회 갱신, `infoPanel.SetActive(true)`
  - `HideInfoPanel()` — 구독 해제 + `infoPanel.SetActive(false)`
  - `ShowSquadPanel(IReadOnlyList<UnitController> units)` — 최대 12개까지 `squadSlots[i].SetData(new CommandButtonData(units[i].GetIcon(), null))`,
    나머지 슬롯은 `Clear()`, `squadPanel.SetActive(true)`, 동시에 `HideInfoPanel()` + `HideProductionUI()` 호출
  - `HideSquadPanel()`
- `HideProductionUI()`/`ShowProductionUI()` 자체는 그대로 두되, `productionQueue_Panel`을 Info_panel과
  세트로 여닫을 거라면 이 두 메서드 안에서 `productionQueue_Panel` GameObject의 `SetActive`도 같이
  해주는 편이 안전 (지금은 부모 오브젝트 자체는 항상 켜진 채로 자식 슬롯만 껐다 켰다 하는 구조라 문제는
  없지만, "Info_panel과 나란히 켜고 끈다"는 요구사항을 명확히 만족시키려면 부모 단위로 맞추는 게 깔끔함)

### `RTSUnitController.cs`
- `UpdateUI()` 진입부에서 (커맨드 패널 switch와는 별개로) 선택 정보 패널 갱신 로직을 하나 추가:

```csharp
private void UpdateSelectionInfoPanel()
{
    if (selectedUnitList.Count > 1)
    {
        uIController.ShowSquadPanel(selectedUnitList); // 최대 12개는 UIController 쪽에서 자름
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

  - `Update()`에서 기존 `UpdateUI()`와 나란히 `UpdateSelectionInfoPanel()`을 호출.
  - `productionQueue_Panel` 표시 여부는 건드리지 않음 — 기존 `BuildingSelectState` switch가 이미
    `ShowProductionUI`/`HideProductionUI`를 알맞게 호출하고 있으므로 그대로 둔다. 즉 "생산 건물 선택 시
    Info_panel 위에 대기열도 같이 뜬다"는 요구사항은 **두 호출이 같은 프레임에 나란히 실행되는 것만으로
    자연히 만족**된다 (별도 연동 코드 불필요).

## 5. 확인이 필요한 부분 (가정)

- **건물 다중 선택**: 지금 `selectedBuildingList`도 Shift+클릭으로 여러 개 선택 가능한 구조인데,
  건물을 2개 이상 선택했을 때도 `Squad_panel`을 띄울지, 아니면 건물은 항상 단일 선택 취급할지 확인 필요.
  위 설계는 "건물 다중 선택은 이번 범위 밖"으로 가정하고 `selectedBuildingList.Count == 1`만 처리함.
- **Squad_panel 안의 아이콘 클릭 동작**: 이번 설계는 "표시만" 한다고 가정 (콜백 `null`). 클릭 시 해당
  유닛만 단일 선택으로 좁히는 기능은 `ProductionSlot`이 이미 `Callback` 파라미터를 지원하므로 나중에
  쉽게 추가 가능.
- **13마리 이상 선택 시**: `selectedUnitList`에서 처음 12개만 보여주고 나머지는 그냥 잘라내는 것으로
  가정 (스크롤/페이지네이션 없음).
- **Squad_panel 안에서 개별 체력 표시 여부**: 이번 설계는 아이콘만 표시. 슬롯마다 미니 체력바까지
  넣는 건 범위 밖으로 가정 (필요하면 `ProductionSlot`에 체력바 UI를 추가하는 후속 작업으로 분리 권장).
