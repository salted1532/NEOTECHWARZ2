# 0601 - Squad_panel 유닛별 체력 텍스트 표시

날짜: 2026-08-17

**상태: 구현 완료** (사용자 확인 후 그대로 적용, Unity 컴파일 0 에러 확인)

## 요청 내용

> Squad_panel에서 slot들에게 health_Text라는 텍스트를 추가했는데 해당 텍스트는 각 유닛별 체력을 보여주는 텍스트가 될거야 스쿼드 패널에서 각 유닛들이 보여지는게 각 유닛별로 체력도 보이도록해줘 그리고 공격 받게 되었을시 체력이 깍이는것도 갱신되도록 해줘
>
> (추가) 체력 표시는 현재체력/최대체력 이런식으로 보여주도록 해줘
>
> (추가) 최대체력일땐 초록색이다가 1/3씩 줄어들때마다 초록색 → 노란색 → 빨간색 순으로 체력 텍스트 색깔이 변하도록 기능을 추가하고 싶어
>
> (추가) 체력이 많은 경우 10000/10000 이런식으로 글자수가 많으면 줄바꿈이 발생하는데 줄바꿈이 발생할때 폰트 크기를 1씩 줄여서 줄바꿈 안일어나도록 할수 있어?

## 조사 내용

- `Assets/prefabs/Game/GameManager.prefab`에서 Squad_panel의 12개 슬롯(Slot0~11, `UIController.squadSlots`) 각각에 이미 `health_Text`(TextMeshProUGUI) 자식 오브젝트가 하나씩 추가돼 있음을 확인. 플레이스홀더 텍스트가 이미 `100/100` 형식으로 박혀 있어 요청한 "현재체력/최대체력" 포맷과 일치.
- Squad 슬롯도 [[0599]]와 동일하게 `ProductionSlot` 컴포넌트를 재사용함 (`Assets/Scripts/UI/UIController.cs:279` `squadSlots`). OrderButtons 슬롯엔 `shortcut_key_Text`만, Squad 슬롯엔 `health_Text`만 있고 한 슬롯에 TMP_Text 자식이 하나뿐임 — 즉 슬롯 타입마다 자식 이름이 다름.
- **주의**: 현재 `shortcutKeyText` 자동 연결은 `GetComponentInChildren<TMP_Text>(true)`(타입 기준)으로 돼 있음(doc/0599). 여기에 같은 방식으로 `healthText` 필드를 추가하면, Squad 슬롯(자식이 `health_Text` 하나뿐)에서 `shortcutKeyText`도 `GetComponentInChildren<TMP_Text>`로 자동 연결되면서 실수로 `health_Text`를 가리키게 되고, 반대로 OrderButtons 슬롯에서는 `healthText`가 `shortcut_key_Text`를 가리키는 오류가 생김 (둘 다 "그 슬롯에 있는 유일한 TMP_Text"라 타입 검색만으로는 구분 불가). 따라서 이번 변경에서 두 필드 모두 **자식 이름으로** 찾도록(`transform.Find("shortcut_key_Text")` / `transform.Find("health_Text")`) 고쳐야 함.
- `Assets/Scripts/Unit/HealthManager.cs`에 이미 `OnHealthChanged` 이벤트(`(currentHp, maxHealth)`)가 있고, 데미지(`GetDamage`)/회복(`Heal`)/초기화 등 체력이 바뀌는 모든 경로에서 이 이벤트를 발생시킴. `GetHealth()`/`GetMaxHealth()`로 현재 값도 즉시 조회 가능.
- Info_panel(단일 선택)의 체력 표시가 이미 동일 패턴으로 구현돼 있음 — `UIController.BindInfoHealth(HealthManager)` (`UIController.cs:836`)가 이전 구독을 해제하고 새 `HealthManager`를 구독해서 `OnHealthChanged`가 올 때마다 `infoHpText.text = $"{currentHp}/{maxHealth}"`로 갱신. Squad_panel도 슬롯마다 이 패턴을 그대로 슬롯 단위로 적용하면 "공격받아 깎이는 것도 실시간 갱신"이 이벤트 기반으로 자동 해결됨 (매 프레임 폴링 불필요).
- `UnitController.GetHealthManager()` (`Assets/Scripts/Unit/UnitController.cs:2256`), `BuildingController.GetHealthManager()` (`Assets/Scripts/Building/BuildingController.cs:464`)가 이미 있어 캐싱된 `HealthManager` 참조를 바로 얻을 수 있음.
- 색 구간은 비율(`currentHp / maxHealth`) 기준 3구간(초록 > 2/3, 노랑 2/3~1/3, 빨강 ≤ 1/3)으로 나누면 "1/3씩 줄 때마다" 요청과 정확히 맞음. `Color.green`/`Color.yellow`/`Color.red`(Unity 기본 색상)를 그대로 쓰면 별도 색상 정의가 필요 없음.
- `health_Text`의 현재 TMP 설정 확인(`GameManager.prefab` 3092행 부근): `m_fontSize: 8`, `m_enableAutoSizing: 0`(꺼짐), `m_TextWrappingMode: 1`(줄바꿈 켜짐), 박스 크기 12x12. 그래서 "100/100" 정도만 넘어가도(예: "10000/10000") 고정 폰트 크기 8이 박스 폭을 넘어 줄바꿈이 발생함.
- TextMeshPro에는 이미 "박스에 맞을 때까지 폰트 크기를 자동으로 줄이는" 기능(Auto Size, `enableAutoSizing` + `fontSizeMin`/`fontSizeMax`)이 내장돼 있음. 매 프레임 폰트를 1씩 줄여보는 수동 루프를 직접 구현하는 대신, 이 네이티브 기능을 켜는 것이 훨씬 간단하고 정확함(TMP가 레이아웃이 바뀔 때마다 텍스트가 박스 안에 한 줄로 들어가는 최적 크기를 알아서 찾아줌 - 줄바꿈이 필요할 만큼 좁아지면 그 전에 이미 폭에 맞게 축소되므로 사실상 줄바꿈이 일어나지 않음).
- 적용 위치: `ProductionSlot.Awake()`에서 `healthText`를 찾은 직후 `enableAutoSizing = true`로 켜고, `fontSizeMax`는 프리팹에 이미 지정된 현재 크기(8)를 그대로 상한으로 쓰고 `fontSizeMin`은 1로 지정 - 요청한 "1까지 줄여서라도 줄바꿈 방지"에 그대로 대응됨. 프리팹 12개를 손으로 고칠 필요 없이 코드 한 곳에서 모든 `health_Text`에 일괄 적용됨.
- Squad_panel은 유닛 다중 선택뿐 아니라 건물 다중 선택(`RefreshSquadBuildingSlots`)에도 같은 슬롯을 재사용함. 건물 쪽에서 `BindHealth`를 호출하지 않으면, 유닛 선택 → 건물 선택으로 바뀔 때 이전 유닛의 체력 구독이 해제되지 않고 `health_Text`에 죽은 유닛의 체력이 남아있는(또는 계속 갱신되는) 버그가 생김. 따라서 건물 슬롯도 동일하게 `BindHealth(building.GetHealthManager())`를 호출해야 함.

## 계획된 코드 변경

### `Assets/Scripts/UI/ProductionSlot.cs`

#### 기존 코드

```csharp
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text shortcutKeyText; // 슬롯 단축키 표시용 (예: KeyCode.Y → "Y") - 비워두면 자식에서 자동 탐색

    ...

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        if (shortcutKeyText == null)
            shortcutKeyText = GetComponentInChildren<TMP_Text>(true);

        if (shortcutKeyText != null)
            shortcutKeyText.color = Color.yellow;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
        ...
    }

    public void SetData(UIController.CommandButtonData data)
    {
        ...
    }

    public void Clear()
    {
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;
        ...
    }
```

#### 변경 코드

```csharp
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text shortcutKeyText; // 슬롯 단축키 표시용 (예: KeyCode.Y → "Y") - 비워두면 자식에서 자동 탐색
    [SerializeField] private TMP_Text healthText; // Squad_panel 전용: "현재체력/최대체력" 표시 - 비워두면 자식에서 자동 탐색

    ...

    private HealthManager boundHealth; // Squad_panel에서 이 슬롯이 지금 구독 중인 유닛/건물의 체력 (없으면 null)

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        // 슬롯 종류마다 있는 TMP_Text 자식이 다르므로(OrderButtons=shortcut_key_Text, Squad=health_Text)
        // 타입 검색이 아니라 이름으로 찾아야 서로 잘못 연결되지 않는다.
        if (shortcutKeyText == null)
        {
            Transform t = transform.Find("shortcut_key_Text");
            if (t != null) shortcutKeyText = t.GetComponent<TMP_Text>();
        }

        if (healthText == null)
        {
            Transform t = transform.Find("health_Text");
            if (t != null) healthText = t.GetComponent<TMP_Text>();
        }

        if (shortcutKeyText != null)
            shortcutKeyText.color = Color.yellow;

        // 체력이 커져서(예: 10000/10000) 박스 폭을 넘으면 줄바꿈되는 대신, TMP 자동 크기 조절로 한 줄에 맞게 폰트를 줄인다.
        if (healthText != null)
        {
            healthText.enableAutoSizing = true;
            healthText.fontSizeMax = healthText.fontSize;
            healthText.fontSizeMin = 1f;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
        ...
    }

    public void SetData(UIController.CommandButtonData data)
    {
        ...
    }

    /// <summary>
    /// Squad_panel 전용: 이 슬롯의 체력 텍스트를 지정한 HealthManager에 구독시킨다 (null이면 구독 해제만 함).
    /// 이미 같은 대상을 구독 중이면 아무 것도 하지 않는다.
    /// </summary>
    public void BindHealth(HealthManager health)
    {
        if (boundHealth == health)
            return;

        if (boundHealth != null)
            boundHealth.OnHealthChanged -= UpdateHealthText;

        boundHealth = health;

        if (boundHealth != null)
        {
            boundHealth.OnHealthChanged += UpdateHealthText;
            UpdateHealthText(boundHealth.GetHealth(), boundHealth.GetMaxHealth());
        }
        else if (healthText != null)
        {
            healthText.text = string.Empty;
        }
    }

    // 비율(현재/최대) 기준 3구간: >2/3 초록, 2/3~1/3 노랑, ≤1/3 빨강 - "1/3씩 줄 때마다" 요청 그대로.
    private void UpdateHealthText(int currentHp, int maxHealth)
    {
        if (healthText == null)
            return;

        healthText.text = $"{currentHp}/{maxHealth}";

        float ratio = maxHealth > 0 ? (float)currentHp / maxHealth : 0f;
        healthText.color = ratio > 2f / 3f ? Color.green : ratio > 1f / 3f ? Color.yellow : Color.red;
    }

    public void Clear()
    {
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;
        BindHealth(null); // 재사용/비활성화 시 이전 유닛의 체력 구독을 반드시 해제 (안 그러면 죽은/교체된 유닛 이벤트가 계속 이 슬롯을 갱신함)
        ...
    }
```

### `Assets/Scripts/UI/UIController.cs`

#### 기존 코드

```csharp
            if (unitIndex < squadUnitsSnapshot.Count)
            {
                UnitController unit = squadUnitsSnapshot[unitIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    unit.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectUnit(unit),
                        () => squadOnShiftClickUnit(unit),
                        () => squadOnCtrlClickUnit(unit),
                        GetUnitDisplayName(unit),
                        LocalizationManager.GetText("squad.unittooltip"))));
            }
            else
            {
                squadSlots[i].Clear();
            }
```

```csharp
            if (buildingIndex < squadBuildingsSnapshot.Count)
            {
                BuildingController building = squadBuildingsSnapshot[buildingIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    building.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectBuilding(building),
                        () => squadOnShiftClickBuilding(building),
                        () => squadOnCtrlClickBuilding(building),
                        LocalizationManager.GetText("squad.buildingtitle"),
                        LocalizationManager.GetText("squad.buildingtooltip"))));
            }
            else
            {
                squadSlots[i].Clear();
            }
```

#### 변경 코드

```csharp
            if (unitIndex < squadUnitsSnapshot.Count)
            {
                UnitController unit = squadUnitsSnapshot[unitIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    unit.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectUnit(unit),
                        () => squadOnShiftClickUnit(unit),
                        () => squadOnCtrlClickUnit(unit),
                        GetUnitDisplayName(unit),
                        LocalizationManager.GetText("squad.unittooltip"))));
                squadSlots[i].BindHealth(unit.GetHealthManager());
            }
            else
            {
                squadSlots[i].Clear();
            }
```

```csharp
            if (buildingIndex < squadBuildingsSnapshot.Count)
            {
                BuildingController building = squadBuildingsSnapshot[buildingIndex];
                squadSlots[i].SetData(new CommandButtonData(
                    building.GetIcon(),
                    ButtonAction.WithModifierClicks(
                        () => squadOnSelectBuilding(building),
                        () => squadOnShiftClickBuilding(building),
                        () => squadOnCtrlClickBuilding(building),
                        LocalizationManager.GetText("squad.buildingtitle"),
                        LocalizationManager.GetText("squad.buildingtooltip"))));
                squadSlots[i].BindHealth(building.GetHealthManager());
            }
            else
            {
                squadSlots[i].Clear();
            }
```

## 요약

- `ProductionSlot`에 `healthText` 필드와 `BindHealth(HealthManager)`를 추가한다. `HealthManager.OnHealthChanged` 이벤트를 구독해 `"현재체력/최대체력"` 형식으로 텍스트를 갱신하며, 이벤트 기반이라 매 프레임 폴링 없이도 피격 즉시 반영된다.
- 같은 갱신 함수에서 체력 비율에 따라 텍스트 색도 함께 바꾼다: 2/3 초과 초록, 1/3 초과~2/3 이하 노랑, 1/3 이하 빨강.
- `healthText`를 찾은 직후 TMP의 내장 Auto Size 기능을 켠다(`enableAutoSizing = true`, `fontSizeMax` = 기존 크기, `fontSizeMin` = 1) - 체력 숫자가 길어져도(예: "10000/10000") 줄바꿈 대신 폰트가 자동으로 줄어들어 한 줄에 들어간다. 수동으로 폰트를 1씩 줄이는 루프 대신 TMP 네이티브 기능을 사용.
- `shortcutKeyText`/`healthText` 자동 연결을 타입 검색(`GetComponentInChildren<TMP_Text>`)에서 자식 이름 검색(`transform.Find`)으로 바꾼다 — 슬롯 종류마다 실제로 있는 TMP_Text 자식이 다르므로, 이름으로 구분하지 않으면 두 필드가 서로 잘못된 오브젝트를 가리키게 됨.
- `Clear()`가 `BindHealth(null)`을 호출해 슬롯이 재사용/숨김될 때 이전 구독을 해제한다 (Info_panel의 `BindInfoHealth`와 동일한 안전장치).
- `UIController.RefreshSquadSlots`/`RefreshSquadBuildingSlots`에서 `SetData` 직후 `BindHealth(...)`를 호출해 유닛/건물 모두 슬롯별로 체력을 구독시킨다 (건물도 함께 처리해야 슬롯 재사용 시 이전 유닛의 체력 표시가 남는 버그를 막을 수 있음).
- 프리팹의 `health_Text`는 이미 존재하고 이름도 일치하므로 프리팹 YAML은 건드릴 필요 없음.

## 영향받는 파일

- `Assets/Scripts/UI/ProductionSlot.cs` (변경 예정, 아직 미적용)
- `Assets/Scripts/UI/UIController.cs` (변경 예정, 아직 미적용)
