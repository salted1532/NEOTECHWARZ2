# 0039. BaseStructure 선택 + Info_panel(아이콘/HealthManager 기반 체력/이름)

**날짜:** 2026-07-09

## 요청 내용 (2단계로 나뉜 요청)

**1차 요청:**
> 이제 BaseStructure를 클릭 시 선택되고 선택되면 마커 활성화 + 현재 지어지고 있는 건물에 대한 정보에 대해서 selectinfo의 info_panel에 건물 아이콘, BaseStructure의 현재 체력(지어지고 있는 건물 체력의 최대체력을 기반으로 건설시간을 나눠서 체력이 계속 차도록. 만약 중간에 공격을 당해도 BaseStructure의 체력은 계속 찰수 있음 이 BaseStructure은 그냥 건물이기 떄문에 아이콘 + 체력(차오르는) + 이름만 보이고 나머지는 안보이도록(건물이니깐 공격력 방어력 않보이도록 해줘

**2차 요청(설계 수정):**
> BaseStructure 아래에 healthmanager를 넣어뒀는데 그걸 이용해서 체력을 담당했으면 좋겠네 BaseStructure는 지어질 건물의 체력을 계산해서 초단 올라갈 걸 계산하고 healthmanager에다가 지어질떄 동안 체력이 오르도록 그리고 UI에 표현되도록

즉, 처음엔 "진행률만 계산해서 그 값을 그대로 표시"하는 방식으로 설계했는데, 사용자가 직접 `BaseStructure.prefab`의 루트 오브젝트에 `HealthManager` 컴포넌트를 이미 추가해뒀고(확인함 - `maxHealth: 100` 플레이스홀더, `NavMeshObstacle`도 함께 추가돼있음), 이 실제 `HealthManager`를 체력의 근거로 쓰고, 초당 증가량을 계산해서 `HealthManager`에 실제로 반영(Heal)하고, UI는 그 `HealthManager`를 그대로 구독해서 보여주는 방식으로 변경.

## 조사 결과
- `BaseStructure.prefab`을 다시 읽어보니 루트 `BaseStructure` 오브젝트(fileID 7639526730160681910)에 사용자가 이미 `HealthManager`(`maxHealth: 100`, 나중에 코드로 덮어씀)와 `NavMeshObstacle`(`m_Carve: 0`)을 추가해두심 — 별도 자식 오브젝트가 아니라 `BaseStructure` 스크립트와 같은 오브젝트에 있음, 그래서 `GetComponent<HealthManager>()`로 바로 참조 가능.
- `HealthManager.cs`는 현재 `GetDamage(int)`(감소)/`Heal(int)`(증가, maxHealth 상한 클램프)만 있고, **최대체력을 런타임에 바꾸거나 현재체력을 절대값으로 지정하는 API가 없음** — `BaseStructure`가 "지어질 건물마다 다른 최대체력"을 반영하려면 `SetMaxHealth(int)`를, "건설 시작 시 체력 0에서 시작"하려면 `SetHealth(int)`를 새로 추가해야 함.
  - `HealthManager.Awake()`가 `currentHp = maxHealth`로 풀피 초기화하므로, `BaseStructure.Initialize()`에서 `SetMaxHealth`만 호출하면 (기존 currentHp가 이미 플레이스홀더 maxHealth와 같아서) 체력이 꽉 찬 채로 남는다 → `SetHealth(0)`을 명시적으로 한 번 더 호출해서 0부터 시작하게 해야 함.
- `UIController.ShowInfoPanel(icon, name, HealthManager health)`(3-인자 오버로드, 건물/자원 등에서 쓰는 것)가 이미 `BindInfoHealth(health)`로 `HealthManager.OnHealthChanged` 이벤트를 구독해서 체력 텍스트를 자동 갱신하는 인프라를 갖추고 있음 — 이번엔 이걸 그대로 재사용하되, 공격력/방어력만 숨기는(`SetCombatStatsVisible(false)`) 새 오버로드/메서드를 추가하면 [[0038-base-structure-construction-progress|기존 계획]]보다 훨씬 간단해짐(수동으로 currentHealth/maxHealth를 매 프레임 계산해서 넘겨줄 필요가 없어짐).
- "공격을 당해도 체력은 계속 찰 수 있음": 이 설계에서는 `Heal()`이 매 프레임 계산된 만큼 **더해지는** 방식이라, 중간에 실제로 `GetDamage()`가 호출돼 깎이더라도 다음 프레임에 건설 진행률만큼의 회복량이 계속 더해짐 — 요청한 동작이 자연스럽게 성립함(0에서부터 다시 채우거나 진행률로 강제 고정하는 게 아니라 "계속 위로 더해짐"이라는 원 표현과 정확히 일치).

## 설계안

### 1. `HealthManager.cs` — 체력 직접 설정 API 추가

`Heal()` 메서드 아래에 추가:
```csharp
    // 최대 체력을 동적으로 재설정한다 (예: BaseStructure가 어떤 건물을 지을지에 따라 최대체력을 다시 지정할 때).
    // 현재 체력이 새 최대치를 넘지 않도록 클램프한다.
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 현재 체력을 절대값으로 지정한다 (데미지/회복처럼 상대적 증감이 아니라 특정 값으로 강제 설정).
    public void SetHealth(int newCurrent)
    {
        if (isDead)
            return;

        currentHp = Mathf.Clamp(newCurrent, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }
```

### 2. `BaseStructure.cs` — HealthManager 기반으로 재작성

```csharp
// 기존 코드 (필드 + Awake + Initialize)
    private int buildingID;
    private float remainingBuildTime;

    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)

    private void Awake()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }

    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    public void Initialize(int buildingID, float buildTime)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
    }
```
```csharp
// 변경 코드
    private int buildingID;
    private float remainingBuildTime;

    private float healthPerSecond; // 건설 중 초당 채워지는 체력량 (완공될 건물의 최대체력 ÷ 건설시간)
    private float healAccumulator; // HealthManager.Heal()은 int만 받으므로 소수점 나머지를 누적해뒀다가 1 이상 모이면 반영
    private Sprite icon; // 완공될 건물의 아이콘(Info_panel 표시용, 완공될 건물 프리팹에서 미리 읽어옴)

    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;

    private void Awake()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    // 완공될 건물의 최대체력/아이콘을 프리팹에서 미리 읽어와 HealthManager와 Info_panel 표시에 반영한다.
    public void Initialize(int buildingID, float buildTime)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;

        int finalMaxHealth = 0;

        BuildingData data = buildingDatabase != null
            ? buildingDatabase.buildingData.Find(d => d.ID == buildingID)
            : null;

        if (data != null && data.Prefab != null)
        {
            if (data.Prefab.TryGetComponent<HealthManager>(out var finishedHealth))
                finalMaxHealth = finishedHealth.GetMaxHealth();

            if (data.Prefab.TryGetComponent<BuildingController>(out var controller))
                icon = controller.GetIcon();
        }

        healthPerSecond = buildTime > 0f ? finalMaxHealth / buildTime : finalMaxHealth;

        if (healthManager != null)
        {
            healthManager.SetMaxHealth(finalMaxHealth);
            healthManager.SetHealth(0); // 건설 시작 시점엔 0에서부터 진행률만큼 차오르게 함
        }
    }
```

**`Update()` 수정** (건설 시간 감소와 함께 HealthManager도 매 프레임 채움):
```csharp
// 기존 코드
    private void Update()
    {
        if (builder == null)
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지

        remainingBuildTime -= Time.deltaTime;

        if (remainingBuildTime <= 0f)
            CompleteConstruction();
    }
```
```csharp
// 변경 코드
    private void Update()
    {
        if (builder == null)
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지

        remainingBuildTime -= Time.deltaTime;

        if (healthManager != null)
        {
            healAccumulator += healthPerSecond * Time.deltaTime;

            if (healAccumulator >= 1f)
            {
                int wholeHeal = Mathf.FloorToInt(healAccumulator);
                healAccumulator -= wholeHeal;
                healthManager.Heal(wholeHeal);
            }
        }

        if (remainingBuildTime <= 0f)
            CompleteConstruction();
    }
```

**선택/아이콘 접근용 메서드 추가** (`GetBuildingID()` 아래):
```csharp
    public Sprite GetIcon() => icon;

    // 좌클릭 선택 시(RTSUnitController) 마커를 켠다. 우클릭 피드백 깜빡임(FlashMarker)과 같은 마커를 공유한다.
    public void SelectStructure()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(true);
    }

    public void DeselectStructure()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }
```

**`FlashMarkerRoutine()`이 깜빡임 후 선택 상태로 복원하도록 수정**:
```csharp
// 기존 코드
        markerFlashRoutine = null;
    }
```
```csharp
// 변경 코드 (FlashMarkerRoutine 마지막 부분)
        // 깜빡이는 도중 선택된 상태였다면 꺼진 채로 두지 않고 선택 마커 상태로 복원
        bool isSelected = rtsController != null && rtsController.selectedBaseStructure == this;
        buildingMarker.SetActive(isSelected);

        markerFlashRoutine = null;
    }
```

**완공되어 자신을 파괴하기 직전, 선택 상태 정리 추가** (`CompleteConstruction()`):
```csharp
// 기존 코드
        if (builder != null)
            builder.FinishConstruction();

        Destroy(gameObject);
    }
```
```csharp
// 변경 코드
        if (builder != null)
            builder.FinishConstruction();

        rtsController?.ClearSelectedStructureIfMatches(this);

        Destroy(gameObject);
    }
```

### 3. `RTSUnitController.cs` — BaseStructure 선택 상태 추가

(1차 설계와 동일, 필드/enum/선택 진입점/DeselectAll 확장은 이전과 같음 - 아래는 UpdateUI 부분만 변경)

**필드/enum**: `selectedBaseStructure` 필드, `SelectState.BaseStructureSelect` 추가 (이전과 동일).

**선택 진입점**: `ClickSelectStructure`/`SelectStructure`/`ClearSelectedStructureIfMatches` 추가 (이전과 동일).

**`DeselectAll()` 확장**: `selectedBaseStructure?.DeselectStructure(); ... selectedBaseStructure = null;` (이전과 동일).

**`UpdateUI()` 케이스** (HealthManager를 직접 넘기도록 수정):
```csharp
            case SelectState.BaseStructureSelect:

                if (selectedBaseStructure != null)
                {
                    uIController.ShowBaseStructureInfoPanel(
                        selectedBaseStructure.GetIcon(),
                        GetBuildingName(selectedBaseStructure.GetBuildingID()),
                        selectedBaseStructure.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;
```

### 4. `UIController.cs` — Info_panel 전용 표시 메서드 추가 (HealthManager 구독)

`ShowResourceInfoPanel` 바로 아래에 추가:
```csharp
    // BaseStructure(건설 중인 건물 기반) 선택 시 Info_panel 표시: 공격력/방어력은 숨기고,
    // 체력은 실제 HealthManager를 그대로 구독해서(BindInfoHealth) 건설 진행에 따라 자동으로 갱신되게 한다.
    public void ShowBaseStructureInfoPanel(Sprite icon, string buildingName, HealthManager health)
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
            infoNameText.text = buildingName;

        SetCombatStatsVisible(false);
        BindInfoHealth(health);
    }
```

### 5. `UserControl.cs` — 좌클릭 선택 연결
(1차 설계와 동일 - `HandleLeftClick()`의 "건물 클릭" 블록에 `BaseStructure` 좌클릭 분기 추가)

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **체력 근거**: 사용자가 직접 붙여둔 실제 `HealthManager`를 그대로 사용. `BaseStructure.Initialize()`가 완공될 건물의 최대체력으로 `SetMaxHealth()`를 호출하고 체력을 0으로 리셋, 이후 `Update()`에서 초당 `finalMaxHealth / buildTime`만큼 `Heal()`로 계속 더해줌.
- **공격당해도 계속 찬다**: `Heal()`이 매 프레임 "더하는" 방식이라 별도 처리 없이 자연히 성립(공격으로 깎여도 다음 프레임에 건설 진행분만큼 다시 더해짐). 이번 범위에서 `BaseStructure`를 실제로 공격하는 경로(적 자동 타게팅, A 모드 강제공격 등)는 만들지 않음 — 이미 있는 `HealthManager`가 0까지 깎이면 `IDestructible`이 없어서 `Destroy(gameObject)`로 그냥 파괴되는 게 기본 동작인데, 이번 요청 범위 밖이라 별도 처리(건설 취소/일꾼 해제 등)는 추가하지 않음.
- **완공 시 최종 체력 값**: 부동소수점 누적 특성상 정확히 max에 도달하지 않고 근사치로 끝날 수 있음(예: 499/500) — `BaseStructure` 자체는 완공과 동시에 파괴되고 실제로 스폰되는 완성된 건물은 자신의 `HealthManager.Awake()`로 별도로 풀피 초기화되므로 실질적 영향 없음.
- **UI 갱신 방식**: `ShowResourceInfoPanel`처럼 매 프레임 값을 넘겨 계산하는 대신, 건물/자원과 동일하게 `HealthManager.OnHealthChanged` 이벤트 구독 방식(`BindInfoHealth`)을 그대로 재사용 — 다른 대상들과 일관된 패턴.

## 변경 예정 파일
- `Assets/Scripts/Unit/HealthManager.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
