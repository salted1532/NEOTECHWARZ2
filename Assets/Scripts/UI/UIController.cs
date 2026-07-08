using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 하단 커맨드 패널(선택된 유닛/건물에 따른 명령 버튼들), 자원 표시, 생산 대기열 UI를 총괄하는 컨트롤러.
// RTSUnitController가 현재 선택 상태에 맞는 ShowXXXPanel 메서드를 호출해 패널 내용을 갱신한다.
public class UIController : MonoBehaviour
{
    // Which state the UI is currently in
    // 선택 종류: None(없음) / Worker(일꾼) / CombatUnit(전투유닛) / BuildMode(건설모드) /
    // Tier1~3Building(티어별 생산건물) / MainBase(커맨드센터, 본진)
    public enum UISelectionState
    {
        None,

        Worker,            // Worker selected
        CombatUnit,        // Combat unit selected

        BuildMode,         // Build mode

        Tier1Building,     // Tier 1 building
        Tier2Building,
        Tier3Building,

        MainBase,          // Command center

        BaseStructureSelect // 건설 중인 건물 기반(BaseStructure) 선택
    }

    // Button action + tooltip data
    // 버튼 하나의 동작과, 툴팁에 표시할 제목/설명/비용을 함께 묶은 데이터.
    // RTSUnitController가 버튼에 Action을 연결할 때 이 정보도 함께 채워서 넘긴다.
    public readonly struct ButtonAction
    {
        public Action Callback { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; } // 이 버튼을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)

        private ButtonAction(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            bool hasCost,
            KeyCode shortcut)
        {
            Callback = callback;
            Title = title;
            Description = description;
            Ore = ore;
            Gas = gas;
            Population = population;
            HasCost = hasCost;
            Shortcut = shortcut;
        }

        // 이동/공격/정지 등 비용이 없는 일반 명령 버튼용
        public static ButtonAction Simple(Action callback, string title, string description, KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, title, description, 0, 0, 0, false, shortcut);
        }

        // 유닛 생산/건물 건설처럼 광물/가스/인구 비용이 있는 버튼용
        public static ButtonAction WithCost(
            Action callback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            KeyCode shortcut = KeyCode.None)
        {
            return new ButtonAction(callback, title, description, ore, gas, population, true, shortcut);
        }
    }

    // Button data
    // 커맨드 패널의 버튼 하나에 필요한 데이터 (아이콘 / 클릭 콜백 / 활성화 여부 / 툴팁 정보)
    public readonly struct CommandButtonData
    {
        public Sprite Icon { get; }
        public Action Callback { get; }
        public bool Interactable { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; }

        public CommandButtonData(
            Sprite icon,
            ButtonAction action,
            bool interactable = true)
        {
            Icon = icon;
            Callback = action.Callback;
            Interactable = interactable;
            Title = action.Title;
            Description = action.Description;
            Ore = action.Ore;
            Gas = action.Gas;
            Population = action.Population;
            HasCost = action.HasCost;
            Shortcut = action.Shortcut;
        }

        // 취소 버튼/빈 대기열 슬롯 등 툴팁이 필요 없는 버튼용
        public CommandButtonData(
            Sprite icon,
            Action callback,
            bool interactable = true)
            : this(icon, ButtonAction.Simple(callback, string.Empty, string.Empty), interactable)
        {
        }
    }

    [Header("Command Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private ProductionSlot[] slots;

    [Header("Command Icons (ShowWorkerPanel / ShowAttackUnitPanel)")]
    [SerializeField] private Sprite moveIcon;
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite stopIcon;
    [SerializeField] private Sprite patrolIcon;
    [SerializeField] private Sprite holdIcon;
    [SerializeField] private Sprite returnIcon; // ShowWorkerPanel 전용
    [SerializeField] private Sprite buildIcon;  // ShowWorkerPanel 전용 (건설모드 진입)

    [Header("Building Icons (ShowBuildPanel)")]
    [SerializeField] private Sprite commandCenterIcon;
    [SerializeField] private Sprite supplyDepotIcon;
    [SerializeField] private Sprite barracksIcon;
    [SerializeField] private Sprite factoryIcon;
    [SerializeField] private Sprite airportIcon;
    [SerializeField] private Sprite labIcon;

    [Header("Unit Icons (ShowMainBasePanel / ShowBarracksPanel / ShowFactoryPanel / ShowAirportPanel)")]
    [SerializeField] private Sprite workerIcon;   // ShowMainBasePanel
    [SerializeField] private Sprite marineIcon;   // ShowBarracksPanel
    [SerializeField] private Sprite vultureIcon;  // ShowBarracksPanel
    [SerializeField] private Sprite goliathIcon;  // ShowFactoryPanel
    [SerializeField] private Sprite tankIcon;     // ShowFactoryPanel
    [SerializeField] private Sprite wraithIcon;   // ShowAirportPanel
    [SerializeField] private Sprite guardianIcon; // ShowAirportPanel

    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;

    [Header("Queue Empty Icons")]
    [SerializeField] private Sprite[] emptyQueueIcons; // 0=1, 1=2, 2=3, 3=4, 4=5

    [Header("Resource Text")]
    [SerializeField] private TextMeshProUGUI OreText;
    [SerializeField] private TextMeshProUGUI GasText;
    [SerializeField] private TextMeshProUGUI PopulationText;

    public UISelectionState CurrentState = UISelectionState.None;

    private RTSUnitController rtsUnitController;

    [Header("Production Queue Panel (SelectInfo)")]
    [SerializeField] private GameObject productionPanel;
    [SerializeField] private ProductionSlot[] queueSlots;
    [SerializeField] private UnitDataSO database;
    [SerializeField] private Slider progressSlider;

    private IReadOnlyList<ProductionData> currentQueue;
    private bool isShowingProductionQueue;

    [Header("Info Panel (SelectInfo)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TextMeshProUGUI infoNameText;
    [SerializeField] private TextMeshProUGUI infoHpText;
    [SerializeField] private Image attackDamageImage; // 호버 시 "Attack Damge : N" 툴팁 표시
    [SerializeField] private Image armorImage;         // 호버 시 "Armor : N" 툴팁 표시

    private HealthManager infoBoundHealth; // 현재 Info_panel이 구독 중인 대상 (선택이 바뀌면 구독 해제 후 갈아끼움)
    private int infoAttackDamage;
    private int infoArmor;

    [Header("Squad Panel (SelectInfo)")]
    [SerializeField] private GameObject squadPanel;
    [SerializeField] private ProductionSlot[] squadSlots; // 인스펙터에서 12개(slot0~11) 연결
    [SerializeField] private Button[] squadPageButtons; // 인스펙터에서 5개(page1~5) 순서대로 연결, 한 페이지당 12마리 (최대 60마리)

    private const int SquadUnitsPerPage = 12;

    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private int squadCurrentPage;

    private void Start()
    {
        rtsUnitController = FindFirstObjectByType<RTSUnitController>();

        ClearPanel();
        HideProductionUI();
        HideInfoPanel();
        HideSquadPanel();
        SetupSquadPageButtons();
        SetupInfoStatHoverTooltips();
    }

    private void Update()
    {
        UpdateProductionProgress();
        UpdateResourceUI();
    }

    // 자원(광물/가스)과 인구수 텍스트를 매 프레임 최신 값으로 갱신
    private void UpdateResourceUI()
    {
        if (rtsUnitController == null)
            return;

        OreText.text = rtsUnitController.GetOre().ToString();
        GasText.text = rtsUnitController.GetGas().ToString();
        PopulationText.text = $"{rtsUnitController.GetPopulation()}/{rtsUnitController.GetMaxPopulation()}";
    }

    // 커맨드 패널을 비우고 숨긴다 (선택 해제 시 호출)
    public void ClearPanel()
    {
        CurrentState = UISelectionState.None;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].Clear();
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Show generic panel
    /// </summary>
    // 범용 패널 표시: 지정한 상태로 전환하고 주어진 커맨드 버튼들을 그대로 슬롯에 채운다.
    public void ShowPanel(UISelectionState state, params CommandButtonData[] commands)
    {
        CurrentState = state;

        SetCommands(commands);
    }

    /// <summary>
    /// Show build mode panel (auto-adds a cancel button)
    /// </summary>
    // 건설모드 패널 표시 (건물 목록 뒤에 취소 버튼을 자동으로 추가해서 표시)
    public void ShowBuildPanel(CommandButtonData[] buildingCommands, Action onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(AddCancelCommand(buildingCommands, onCancel));
    }

    // 커맨드 패널의 각 슬롯에 데이터를 채우거나(모자란 슬롯은) 비운다.
    private void SetCommands(params CommandButtonData[] commands)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (commands != null && i < commands.Length)
            {
                slots[i].SetData(commands[i]);
            }
            else
            {
                slots[i].Clear();
            }
        }
    }

    // 기존 커맨드 배열 끝에 취소(Cancel) 버튼 하나를 추가한 새 배열을 만들어 반환한다 (슬롯 수 초과 시 잘라냄).
    private CommandButtonData[] AddCancelCommand(CommandButtonData[] commands, Action onCancel)
    {
        int commandCount = commands == null ? 0 : commands.Length;
        int maxCount = Mathf.Min(commandCount + 1, slots.Length);

        CommandButtonData[] result = new CommandButtonData[maxCount];

        for (int i = 0; i < commandCount && i < result.Length; i++)
        {
            result[i] = commands[i];
        }

        if (result.Length > 0)
        {
            result[result.Length - 1] =
                new CommandButtonData(cancelIcon, onCancel);
        }

        return result;
    }

    // Production queue
    // 생산 대기열 슬롯 UI 갱신: 큐에 있는 만큼 아이콘/취소 콜백을 채우고 나머지는 빈 슬롯으로 표시한다.
    public void UpdateQueue(
    IReadOnlyList<ProductionData> queue,
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

            int unitIndex = database.unitData.FindIndex(d => d.ID == queue[queueIndex].UnitID);

            if (unitIndex == -1)
            {
                queueSlots[i].Clear();
                continue;
            }

            UnitData data = database.unitData[unitIndex];

            queueSlots[i].SetData(
                new CommandButtonData(
                    data.Icon,
                    () => onCancel(queueIndex)
                )
            );
        }
    }
    // 생산 대기열 UI를 표시 상태로 전환하고 즉시 갱신한다.
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

    // Hide production queue & progress time
    // 생산 대기열 & 진행시간 UI를 숨기고 초기화한다 (생산 건물이 아닌 대상 선택 시 등)
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

    // 빈 대기열 슬롯에 "다음 슬롯 번호" 아이콘을 비활성 상태로 표시한다.
    private void SetEmptyQueueSlot(int index)
    {
        queueSlots[index].SetData(
            new CommandButtonData(
                emptyQueueIcons[index],
                null,
                false
            )
        );
    }

    // Progress time display
    // 생산시간 표시(프로그레스 바) 갱신: 대기열 맨 앞 항목의 진행률을 슬라이더에 반영한다.
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

    // ===== Info Panel (단일 유닛/건물 선택 시) =====
    // Squad_panel과는 항상 배타적이고, productionPanel과는 독립적으로 동시에 켜질 수 있다
    // (생산 건물 선택 시 ShowInfoPanel + ShowProductionUI가 같이 호출됨).
    // 건물/자원 등 공격력·방어력이 없는 대상용 (0으로 표시됨)
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health)
    {
        ShowInfoPanel(icon, unitName, health, 0, 0);
    }

    // 유닛 선택 시 공격력/방어력도 함께 받아 저장해둔다.
    // AttackDamageImage/ArmorImage 호버 시(SetupInfoStatHoverTooltips) 이 값을 툴팁으로 보여준다.
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

    // 자원(Ore/Gas) 선택 시처럼 공격력/방어력 개념이 없는 대상에서는 두 아이콘 자체를 숨긴다.
    private void SetCombatStatsVisible(bool visible)
    {
        if (attackDamageImage != null)
            attackDamageImage.gameObject.SetActive(visible);

        if (armorImage != null)
            armorImage.gameObject.SetActive(visible);
    }

    // attackDamageImage/armorImage에 EventTrigger를 붙여, 호버 시 TooltipUI로 현재 선택 유닛의
    // 공격력/방어력을 "Attack Damge : N" / "Armor : N" 형식으로 보여준다 (인스펙터에서 이미지만 연결하면 됨).
    private void SetupInfoStatHoverTooltips()
    {
        AddStatHoverTooltip(attackDamageImage, () => $"Attack Damge : {infoAttackDamage}");
        AddStatHoverTooltip(armorImage, () => $"Armor : {infoArmor}");
    }

    private void AddStatHoverTooltip(Image image, Func<string> textProvider)
    {
        if (image == null)
            return;

        EventTrigger trigger = image.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = image.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => TooltipUI.Instance?.Show(image.rectTransform, textProvider(), string.Empty));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => TooltipUI.Instance?.Hide());
        trigger.triggers.Add(exitEntry);
    }

    public void HideInfoPanel()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        BindInfoHealth(null);
    }

    // 자원 노드(광물/가스) 선택 시 Info_panel 표시: 체력 대신 남은 채취량을 체력 텍스트 자리에 표시한다.
    public void ShowResourceInfoPanel(Sprite icon, string resourceName, int remainingAmount)
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
            infoNameText.text = resourceName;

        SetCombatStatsVisible(false);
        BindInfoHealth(null); // 자원은 HealthManager가 없으므로 체력 구독은 해제

        if (infoHpText != null)
            infoHpText.text = remainingAmount.ToString();
    }

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

    // BaseStructure(건설 중) 선택 시 커맨드 패널: 취소(환불) 버튼 하나만 표시. 기존 취소 아이콘(cancelIcon)을 재사용.
    public void ShowBaseStructureCommandPanel(ButtonAction onCancelConstruction)
    {
        CurrentState = UISelectionState.BaseStructureSelect;

        SetCommands(new CommandButtonData(cancelIcon, onCancelConstruction));
    }

    // Info_panel이 구독 중인 HealthManager를 교체한다. 매 프레임 같은 대상으로 호출돼도
    // 불필요하게 재구독하지 않도록 방어하고, 대상이 죽거나 선택 해제되면 이전 구독을 반드시 해제한다.
    private void BindInfoHealth(HealthManager health)
    {
        if (infoBoundHealth == health)
        {
            return;
        }

        if (infoBoundHealth != null)
            infoBoundHealth.OnHealthChanged -= UpdateInfoHpText;

        infoBoundHealth = health;

        if (infoBoundHealth != null)
        {
            infoBoundHealth.OnHealthChanged += UpdateInfoHpText;
            UpdateInfoHpText(infoBoundHealth.GetHealth(), infoBoundHealth.GetMaxHealth());
        }
        else if (infoHpText != null)
        {
            infoHpText.text = string.Empty;
        }
    }

    private void UpdateInfoHpText(int currentHp, int maxHealth)
    {
        if (infoHpText != null)
            infoHpText.text = $"{currentHp}/{maxHealth}";
    }

    // ===== Squad Panel (유닛 다중 선택 시) =====
    // selectedUnitList를 12마리씩 페이지로 나누고, 현재 페이지의 12마리만 squadSlots에 채운다.
    // 매 프레임 호출되므로(UpdateUI) 선택 내용이 실제로 바뀐 경우에만 페이지를 0으로 리셋한다.
    // 슬롯을 클릭하면 onSelectUnit(그 유닛)이 호출되어 단일 선택으로 좁혀지도록 한다.
    public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit)
    {
        HideInfoPanel();
        HideProductionUI();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectUnit = onSelectUnit;

        if (!SquadUnitsEqual(squadUnitsSnapshot, units))
        {
            squadUnitsSnapshot.Clear();
            squadUnitsSnapshot.AddRange(units);
            squadCurrentPage = 0;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadUnitsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);
        RefreshSquadSlots();
    }

    // 페이지 버튼(page1~5) 클릭 시 호출: 해당 페이지의 12마리로 슬롯을 다시 채운다.
    public void SelectSquadPage(int page)
    {
        if (squadPageButtons == null || page < 0 || page >= squadPageButtons.Length)
            return;

        if (!squadPageButtons[page].gameObject.activeSelf)
            return;

        squadCurrentPage = page;
        RefreshSquadSlots();
    }

    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 채운다.
    private void RefreshSquadSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int unitIndex = startIndex + i;

            if (unitIndex < squadUnitsSnapshot.Count)
            {
                UnitController unit = squadUnitsSnapshot[unitIndex];
                squadSlots[i].SetData(new CommandButtonData(unit.GetIcon(), () => squadOnSelectUnit(unit)));
            }
            else
            {
                squadSlots[i].Clear();
            }
        }
    }

    // 선택된 유닛 수로 채워지는 페이지 버튼만 보이도록 켠다 (예: 36마리면 1~3페이지만 SetActive(true), 나머지는 숨김).
    private void UpdateSquadPageButtons(int pageCount)
    {
        if (squadPageButtons == null)
            return;

        for (int i = 0; i < squadPageButtons.Length; i++)
        {
            if (squadPageButtons[i] == null)
                continue;

            squadPageButtons[i].gameObject.SetActive(i < pageCount);
        }
    }

    // page1~5 버튼의 onClick을 코드에서 연결 (인스펙터에서는 squadPageButtons 배열에 버튼만 드래그하면 됨)
    private void SetupSquadPageButtons()
    {
        if (squadPageButtons == null)
            return;

        for (int i = 0; i < squadPageButtons.Length; i++)
        {
            if (squadPageButtons[i] == null)
                continue;

            int page = i;
            squadPageButtons[i].onClick.AddListener(() => SelectSquadPage(page));
        }
    }

    private static bool SquadUnitsEqual(List<UnitController> current, IReadOnlyList<UnitController> incoming)
    {
        if (current.Count != incoming.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != incoming[i])
                return false;
        }

        return true;
    }

    public void HideSquadPanel()
    {
        if (squadPanel != null)
            squadPanel.SetActive(false);

        for (int i = 0; i < squadSlots.Length; i++)
            squadSlots[i]?.Clear();

        squadUnitsSnapshot.Clear();
        squadCurrentPage = 0;
    }

    // Worker
    // 일꾼 선택 패널 (이동/공격/정지/순찰/홀드/반환/건설 버튼)
    public void ShowWorkerPanel(
    ButtonAction onMove,
    ButtonAction onAttack,
    ButtonAction onStop,
    ButtonAction onPatrol,
    ButtonAction onHold,
    ButtonAction onReturn,
    ButtonAction onBuild)
    {
        CurrentState = UISelectionState.Worker;

        SetCommands(

            new CommandButtonData(moveIcon, onMove),
            new CommandButtonData(attackIcon, onAttack),
            new CommandButtonData(stopIcon, onStop),
            new CommandButtonData(patrolIcon, onPatrol),
            new CommandButtonData(holdIcon, onHold),
            new CommandButtonData(returnIcon, onReturn),
            new CommandButtonData(buildIcon, onBuild)
        );
    }

    // Combat unit
    // 전투유닛 선택 패널 (이동/공격/정지/순찰/홀드 버튼)
    public void ShowAttackUnitPanel(
    ButtonAction onMove,
    ButtonAction onAttack,
    ButtonAction onStop,
    ButtonAction onPatrol,
    ButtonAction onHold)
    {
        CurrentState = UISelectionState.CombatUnit;

        SetCommands(

            new CommandButtonData(moveIcon, onMove),
            new CommandButtonData(attackIcon, onAttack),
            new CommandButtonData(stopIcon, onStop),
            new CommandButtonData(patrolIcon, onPatrol),
            new CommandButtonData(holdIcon, onHold)
        );
    }

    // Build mode
    // 건설모드 패널 (건물 종류별 버튼 + 취소 버튼)
    public void ShowBuildPanel(
    ButtonAction onCommandCenter,
    ButtonAction onSupplyDepot,
    ButtonAction onBarracks,
    ButtonAction onFactory,
    ButtonAction onAirport,
    ButtonAction onLab,
    ButtonAction onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(

            new CommandButtonData(commandCenterIcon, onCommandCenter),
            new CommandButtonData(supplyDepotIcon, onSupplyDepot),
            new CommandButtonData(barracksIcon, onBarracks),
            new CommandButtonData(factoryIcon, onFactory),
            new CommandButtonData(airportIcon, onAirport),
            new CommandButtonData(labIcon, onLab),
            new CommandButtonData(cancelIcon, onCancel)
        );
    }

    // Main base
    // 본진(커맨드센터) 선택 패널 (일꾼 생산 버튼)
    public void ShowMainBasePanel(ButtonAction onTrainWorker)
    {
        CurrentState = UISelectionState.MainBase;

        SetCommands(

            new CommandButtonData(workerIcon, onTrainWorker)
        );
    }

    // Barracks
    // 병영(Tier1 건물) 선택 패널 (마린/벌처 생산 버튼)
    public void ShowBarracksPanel(
    ButtonAction onMarine,
    ButtonAction onFirebat)
    {
        CurrentState = UISelectionState.Tier1Building;

        SetCommands(

            new CommandButtonData(marineIcon, onMarine),
            new CommandButtonData(vultureIcon, onFirebat)
        );
    }

    // Factory
    // 공장(Tier2 건물) 선택 패널 (골리앗/탱크 생산 버튼)
    public void ShowFactoryPanel(
    ButtonAction onGoliath,
    ButtonAction onTank)
    {
        CurrentState = UISelectionState.Tier2Building;

        SetCommands(

            new CommandButtonData(goliathIcon, onGoliath),
            new CommandButtonData(tankIcon, onTank)
        );
    }

    // Starport
    // 우주공항(Tier3 건물) 선택 패널 (레이스/가디언 생산 버튼)
    public void ShowAirportPanel(
    ButtonAction onWraith,
    ButtonAction onGuardian)
    {
        CurrentState = UISelectionState.Tier3Building;

        SetCommands(

            new CommandButtonData(wraithIcon, onWraith),
            new CommandButtonData(guardianIcon, onGuardian)
        );
    }
}
