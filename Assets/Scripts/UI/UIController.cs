using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 하단 커맨드 패널(선택된 유닛/건물에 따른 명령 버튼들), 자원 표시, 생산 대기열 UI를 총괄하는 컨트롤러.
// RTSUnitController가 현재 선택 상태에 맞는 ShowXXXPanel 메서드를 호출해 패널 내용을 갱신한다.
public class UIController : MonoBehaviour
{
    // PlacementSystem/UnitController 등 RTSUnitController를 거치지 않는 곳에서도 ShowWarning()을 바로
    // 호출할 수 있도록 TooltipUI.Instance와 동일한 싱글턴 패턴을 쓴다 (doc/0480).
    public static UIController Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

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

        LabBuilding,       // 연구소(Lab) 선택 (공격력/방어력 업그레이드)

        BaseStructureSelect // 건설 중인 건물 기반(BaseStructure) 선택
    }

    // Button action + tooltip data
    // 버튼 하나의 동작과, 툴팁에 표시할 제목/설명/비용을 함께 묶은 데이터.
    // RTSUnitController가 버튼에 Action을 연결할 때 이 정보도 함께 채워서 넘긴다.
    public readonly struct ButtonAction
    {
        public Action Callback { get; }
        // Squad_panel 전용: Shift+Click(선택 목록에서 제거)/Ctrl+Click(같은 종류만 남기기) 콜백.
        // 일반 커맨드 버튼(WithCost/Simple)은 항상 null - ProductionSlot이 null이면 기본 Callback으로 처리한다.
        public Action ShiftClickCallback { get; }
        public Action CtrlClickCallback { get; }
        public string Title { get; }
        public string Description { get; }
        public int Ore { get; }
        public int Gas { get; }
        public int Population { get; }
        public bool HasCost { get; }
        public KeyCode Shortcut { get; } // 이 버튼을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)

        private ButtonAction(
            Action callback,
            Action shiftClickCallback,
            Action ctrlClickCallback,
            string title,
            string description,
            int ore,
            int gas,
            int population,
            bool hasCost,
            KeyCode shortcut)
        {
            Callback = callback;
            ShiftClickCallback = shiftClickCallback;
            CtrlClickCallback = ctrlClickCallback;
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
            return new ButtonAction(callback, null, null, title, description, 0, 0, 0, false, shortcut);
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
            return new ButtonAction(callback, null, null, title, description, ore, gas, population, true, shortcut);
        }

        // Squad_panel 슬롯 전용: 기본 클릭 외에 Shift+Click/Ctrl+Click 동작을 추가로 지정한다.
        public static ButtonAction WithModifierClicks(
            Action callback,
            Action shiftClickCallback,
            Action ctrlClickCallback,
            string title,
            string description)
        {
            return new ButtonAction(callback, shiftClickCallback, ctrlClickCallback, title, description, 0, 0, 0, false, KeyCode.None);
        }
    }

    // Button data
    // 커맨드 패널의 버튼 하나에 필요한 데이터 (아이콘 / 클릭 콜백 / 활성화 여부 / 툴팁 정보)
    public readonly struct CommandButtonData
    {
        public Sprite Icon { get; }
        public Action Callback { get; }
        public Action ShiftClickCallback { get; }
        public Action CtrlClickCallback { get; }
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
            ShiftClickCallback = action.ShiftClickCallback;
            CtrlClickCallback = action.CtrlClickCallback;
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

    // 건물 리프트/이동 버튼 전용 고정 슬롯 인덱스. 건물 선택 컨텍스트 안에서는 이 두 슬롯을 절대 Clear()(SetActive(false))로
    // 건드리지 않는다 - 매 프레임 SetActive(false)→true를 반복하면 실행 중이던 클릭 코루틴(단축키 시뮬레이션, ProductionSlot.
    // SimulateClickRoutine)이 그 순간 강제 종료되어 버튼이 눌린 채로 멈추고 단축키도 동작하지 않는 버그가 생긴다.
    private const int BuildingMoveSlotIndex = 0;
    private const int BuildingLiftSlotIndex = 8;
    private static readonly HashSet<int> LiftSlotOnlyProtected = new HashSet<int> { BuildingLiftSlotIndex };

    // 생산 건물(MainBase/Tier1/Tier2/Tier3) 전용 랠리 버튼 고정 슬롯. 위 리프트/이동 슬롯과 동일한 이유로
    // 보호가 필요함 - ShowUnitProductionPanel이 매 프레임 호출되므로 보호 안 하면 이 슬롯도 같이 Clear()된다.
    // 슬롯 6은 tier당 유닛 수(현재 최대 3개)로는 절대 안 채워지는 여유 슬롯이라 실제 생산 버튼과 안 겹친다.
    // (유닛 스킬 슬롯(UnitSkillSlotIndex)과 물리적으로 같은 슬롯을 겸용하는데, 예전엔
    // RTSUnitController.UpdateUnitSkillUI()가 매 프레임 이 슬롯을 잘못 Clear()해버리는 버그가 있었음 -
    // 그 버그를 고친 뒤(doc/0363) 슬롯 6으로 되돌림.)
    private const int BuildingRallySlotIndex = 6;
    private static readonly HashSet<int> LiftAndRallySlotsProtected = new HashSet<int> { BuildingLiftSlotIndex, BuildingRallySlotIndex };

    // 고급유닛 특성(트레이트) 선택으로 얻은 액티브 스킬 버튼의 고정 슬롯(doc/0228). BuildingLiftSlotIndex와
    // 동일한 이유로 보호가 필요함 - RTSUnitController.UpdateUnitSkillUI()가 ShowAttackUnitPanel과는 별도로
    // 매 프레임 독립적으로 이 슬롯만 갱신하므로, ShowAttackUnitPanel의 SetCommands가 이 슬롯을 건드리면
    // (Clear→SetData가 같은 프레임에 반복되어) 단축키 클릭 시뮬레이션 코루틴이 끊길 수 있다.
    private const int UnitSkillSlotIndex = 6;
    private static readonly HashSet<int> UnitSkillSlotProtected = new HashSet<int> { UnitSkillSlotIndex };

    // 슬롯 6이 이미 다른 오더 버튼(Worker의 Build)에 쓰이고 있을 때 스킬 버튼이 대신 들어갈 슬롯(doc/0251).
    private const int UnitSkillFallbackSlotIndex = 7;
    private static readonly HashSet<int> UnitSkillFallbackSlotProtected = new HashSet<int> { UnitSkillFallbackSlotIndex };

    // 고급유닛 특성(2택1) 선택 오버레이 "SkillSelect" (doc/0228). 커맨드 패널(slots[])과는 완전히 별개의
    // 독립 UI 오브젝트 - order panel 위쪽에 겹쳐 보이도록 이미 배치되어 있음. 그래서 이 오버레이가 떠 있는
    // 동안에도 이동/공격 등 기존 커맨드 패널 명령은 그대로(비모달) 사용할 수 있다.
    [Header("Skill Trait Select Overlay (SkillSelect)")]
    [SerializeField] private GameObject skillSelectPanel; // "SkillSelect" 루트 오브젝트
    [SerializeField] private ProductionSlot[] skillSelectSlots; // Slot0=traitA(장점 극대화), Slot1=traitB(단점 보완)
    [SerializeField] private float skillSelectVerticalMargin = 8f; // order panel 상단에서 얼마나 띄울지

    [Header("Command Icons (ShowWorkerPanel / ShowAttackUnitPanel)")]
    [SerializeField] private Sprite moveIcon;
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite stopIcon;
    [SerializeField] private Sprite patrolIcon;
    [SerializeField] private Sprite holdIcon;
    [SerializeField] private Sprite returnIcon; // ShowWorkerPanel 전용
    [SerializeField] private Sprite buildIcon;  // ShowWorkerPanel 전용 (건설모드 진입)

    // 건설 버튼(ShowBuildPanel)/생산 버튼(ShowUnitTierPanel) 아이콘은 더 이상 여기 고정 필드로 두지 않고,
    // BuildingData.ConstructionIcon/UnitData.ProductionIcon을 그대로 사용한다. 새 건물/유닛을 추가해도
    // 이 파일을 건드릴 필요가 없도록 하기 위함 (doc/0514).

    [Header("Research Icons (ShowLabPanel)")]
    [SerializeField] private Sprite attackResearchIcon;
    [SerializeField] private Sprite armorResearchIcon;

    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;
    [SerializeField] private Sprite liftOffIcon; // 리프트 버튼(지상 상태일 때)
    [SerializeField] private Sprite landIcon;    // 착륙 버튼(공중 상태일 때)
    [SerializeField] private Sprite rallyIcon;   // 생산 건물 랠리 버튼(고정 슬롯 6)

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

    // 연구 대기열도 위 productionPanel/queueSlots/progressSlider를 그대로 재사용한다
    // (Lab 패널과 생산 패널은 동시에 표시될 일이 없음)
    private IReadOnlyList<ResearchData> currentResearchQueue;
    private bool isShowingResearchQueue;

    [Header("Info Panel (SelectInfo)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TextMeshProUGUI infoNameText;
    [SerializeField] private TextMeshProUGUI infoHpText;
    [SerializeField] private TextMeshProUGUI infoText; // 선택한 유닛/건물 자체에 대한 설명 (doc/0476) - 생산/건설 버튼 호버 툴팁과는 별개

    [Header("Warning Text (건설실패/인구수부족/자원부족)")]
    [SerializeField] private TextMeshProUGUI warningText; // Canvas 직속 - doc/0480
    private Coroutine warningHideCoroutine;
    [SerializeField] private Image attackDamageImage; // 호버 시 "Attack Damge : N" 툴팁 표시
    [SerializeField] private Image armorImage;         // 호버 시 "Armor : N" 툴팁 표시

    private HealthManager infoBoundHealth; // 현재 Info_panel이 구독 중인 대상 (선택이 바뀌면 구독 해제 후 갈아끼움)
    private int infoAttackDamage; // 연구 보너스 제외한 기본값 (doc/0517)
    private int infoArmor;        // 연구 보너스 제외한 기본값 (doc/0517)
    private int infoAttackBonus;  // 연구소 공격력 연구로 얻은 전역 보너스 - 0이면 툴팁에 표기 안 함 (doc/0517)
    private int infoArmorBonus;   // 연구소 방어력 연구로 얻은 전역 보너스 - 0이면 툴팁에 표기 안 함 (doc/0517)
    private AttackEffectType infoAttackType;
    private ArmorType infoArmorType;
    private SizeType infoSizeType;
    private int infoShotCount = 1; // 공격 1회당 동시에 나가는 투사체 개수 - 2 이상이면 툴팁에 "x2"로 표기 (doc/0293)
    private bool infoCanAttackGround = true; // 공격력 툴팁의 "Attack Target" 표기용 (doc/0478)
    private bool infoCanAttackAir = true;

    [Header("Squad Panel (SelectInfo)")]
    [SerializeField] private GameObject squadPanel;
    [SerializeField] private ProductionSlot[] squadSlots; // 인스펙터에서 12개(slot0~11) 연결
    [SerializeField] private Button[] squadPageButtons; // 인스펙터에서 5개(page1~5) 순서대로 연결, 한 페이지당 12마리 (최대 60마리)

    private const int SquadUnitsPerPage = 12;

    private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
    private Action<UnitController> squadOnSelectUnit;
    private Action<UnitController> squadOnShiftClickUnit;
    private Action<UnitController> squadOnCtrlClickUnit;
    private int squadCurrentPage;

    // 건물 다중 선택용 (유닛과 같은 squadPanel/squadSlots를 공유 - 유닛/건물 선택은 항상 배타적으로 표시됨)
    private readonly List<BuildingController> squadBuildingsSnapshot = new List<BuildingController>();
    private Action<BuildingController> squadOnSelectBuilding;
    private Action<BuildingController> squadOnShiftClickBuilding;
    private Action<BuildingController> squadOnCtrlClickBuilding;
    private bool squadShowingBuildings;

    // 스쿼드 슬롯은 ShowSquadPanel/ShowBuildingSquadPanel을 통해 매 프레임 호출되지만(RTSUnitController.UpdateUI),
    // 선택된 유닛/건물 목록·페이지·모드(유닛/건물)가 그대로면 슬롯 버튼(아이콘+델리게이트)을 다시 만들 필요가 없다.
    private bool squadContentDirty = true;
    private int lastRefreshedSquadPage = -1;
    private bool lastRefreshedWasBuildingMode;

    private void Start()
    {
        rtsUnitController = FindFirstObjectByType<RTSUnitController>();

        ClearPanel();
        HideProductionUI();
        HideInfoPanel();
        HideSquadPanel();
        HideSkillSelectPanel();
        SetupSquadPageButtons();
        SetupInfoStatHoverTooltips();
    }

    private void Update()
    {
        UpdateProductionProgress();
        UpdateResourceUI();
    }

    // 자원(광물/가스)과 인구수 텍스트를 매 프레임 확인하되, 마지막으로 표시한 값과 같으면 다시 쓰지 않는다
    // (TMP 텍스트 재할당은 매번 메시 재생성을 유발하므로, 값이 실제로 바뀔 때만 갱신해도 화면 결과는 동일함).
    private int lastDisplayedOre = int.MinValue;
    private int lastDisplayedGas = int.MinValue;
    private int lastDisplayedPopulation = int.MinValue;
    private int lastDisplayedMaxPopulation = int.MinValue;

    private void UpdateResourceUI()
    {
        if (rtsUnitController == null)
            return;

        int ore = rtsUnitController.GetOre();
        int gas = rtsUnitController.GetGas();
        int population = rtsUnitController.GetPopulation();
        int maxPopulation = rtsUnitController.GetMaxPopulation();

        if (ore != lastDisplayedOre)
        {
            OreText.text = ore.ToString();
            lastDisplayedOre = ore;
        }

        if (gas != lastDisplayedGas)
        {
            GasText.text = gas.ToString();
            lastDisplayedGas = gas;
        }

        if (population != lastDisplayedPopulation || maxPopulation != lastDisplayedMaxPopulation)
        {
            PopulationText.text = $"{population}/{maxPopulation}";
            lastDisplayedPopulation = population;
            lastDisplayedMaxPopulation = maxPopulation;
        }
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
    public void ShowBuildPanel(CommandButtonData[] buildingCommands, ButtonAction onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(AddCancelCommand(buildingCommands, onCancel));
    }

    // 커맨드 패널의 각 슬롯에 데이터를 채우거나(모자란 슬롯은) 비운다.
    private void SetCommands(params CommandButtonData[] commands)
    {
        SetCommands(commands, null);
    }

    // protectedSlotIndices에 포함된 슬롯은 commands 배열에 포함되지 않아도 Clear()하지 않고 그대로 둔다.
    // (건물 리프트/이동 버튼처럼 별도 경로에서 매 프레임 독립적으로 갱신되는 슬롯이 이 호출 때문에 매 프레임
    // 껐다 켜졌다 하면서 실행 중이던 클릭 코루틴/단축키가 끊기는 것을 막기 위함 - 위 BuildingLiftSlotIndex 주석 참고)
    private void SetCommands(CommandButtonData[] commands, HashSet<int> protectedSlotIndices)
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
            else if (protectedSlotIndices == null || !protectedSlotIndices.Contains(i))
            {
                slots[i].Clear();
            }
        }
    }

    // 기존 커맨드 배열 끝에 취소(Cancel) 버튼 하나를 추가한 새 배열을 만들어 반환한다 (슬롯 수 초과 시 잘라냄).
    private CommandButtonData[] AddCancelCommand(CommandButtonData[] commands, ButtonAction onCancel)
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

            if (queueSlots[i] == null) continue;

            int unitIndex = database.unitData.FindIndex(d => d.ID == queue[queueIndex].UnitID);

            if (unitIndex == -1)
            {
                queueSlots[i].Clear();
                continue;
            }

            UnitData data = database.unitData[unitIndex];

            queueSlots[i].SetData(
                new CommandButtonData(
                    data.ProductionIcon,
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

        // 연구 큐 UI와 슬롯/슬라이더를 공유하므로, 생산 큐를 보여줄 땐 연구 큐 상태를 꺼서
        // UpdateProductionProgress()가 지난 프레임의 연구 진행률을 덮어쓰지 않게 한다.
        currentResearchQueue = null;
        isShowingResearchQueue = false;

        UpdateQueue(queue, onCancel);
    }

    // Hide production queue & progress time
    // 생산 대기열 & 진행시간 UI를 숨기고 초기화한다 (생산 건물이 아닌 대상 선택 시 등)
    public void HideProductionUI()
    {
        if (productionPanel != null)
            productionPanel.SetActive(false);

        foreach (var slot in queueSlots)
            slot?.Clear();

        progressSlider.gameObject.SetActive(false);

        currentQueue = null;
        isShowingProductionQueue = false;

        // 연구 큐도 같은 UI 오브젝트를 쓰므로 같이 꺼준다 (그래야 다음 프레임에 연구 진행률로 되살아나지 않음).
        currentResearchQueue = null;
        isShowingResearchQueue = false;
    }

    // 연구 대기열 슬롯 UI 갱신: UpdateQueue와 동일한 패턴이지만, UnitDataSO 조회 대신
    // ResearchType → 아이콘(attackResearchIcon/armorResearchIcon) 매핑을 그대로 쓴다.
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

            if (queueSlots[i] == null) continue;

            int queueIndex = i;
            Sprite icon = queue[queueIndex].Type == ResearchType.Attack ? attackResearchIcon : armorResearchIcon;

            queueSlots[i].SetData(
                new CommandButtonData(icon, () => onCancel(queueIndex))
            );
        }
    }

    // 연구 대기열 UI를 표시 상태로 전환하고 즉시 갱신한다 (생산 큐 UI와 동일한 패널/슬롯/슬라이더를 재사용).
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

    // Hide research queue & progress time
    public void HideResearchUI()
    {
        if (productionPanel != null)
            productionPanel.SetActive(false);

        foreach (var slot in queueSlots)
            slot?.Clear();

        progressSlider.gameObject.SetActive(false);

        currentResearchQueue = null;
        isShowingResearchQueue = false;

        currentQueue = null;
        isShowingProductionQueue = false;
    }

    // 빈 대기열 슬롯에 "다음 슬롯 번호" 아이콘을 비활성 상태로 표시한다.
    private void SetEmptyQueueSlot(int index)
    {
        if (queueSlots[index] == null) return;

        queueSlots[index].SetData(
            new CommandButtonData(
                emptyQueueIcons[index],
                null,
                false
            )
        );
    }

    // Progress time display
    // 생산/연구 시간 표시(프로그레스 바) 갱신: 대기열 맨 앞 항목의 진행률을 슬라이더에 반영한다.
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

    // ===== Info Panel (단일 유닛/건물 선택 시) =====
    // Squad_panel과는 항상 배타적이고, productionPanel과는 독립적으로 동시에 켜질 수 있다
    // (생산 건물 선택 시 ShowInfoPanel + ShowProductionUI가 같이 호출됨).
    // 건물 등 공격력·방어력 개념이 없는 대상용 - 아이콘 자체를 숨긴다 (ShowResourceInfoPanel/
    // ShowBaseStructureInfoPanel과 동일한 패턴, doc/0249). 아군 건물(BuildingController)/
    // 적 건물(EnemyBuildingController) 선택 둘 다 이 오버로드를 쓰므로 여기 한 곳만 고치면 양쪽에 대칭 적용된다.
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health, string description = "")
    {
        ShowInfoPanel(icon, unitName, health, 0, 0, AttackEffectType.Bullet, ArmorType.Light, SizeType.Medium, 1, description);
        SetCombatStatsVisible(false);
    }

    // 유닛 선택 시 공격 관련 스탯 전부(공격타입/공격력/투사체 개수)와 방어 관련 스탯 전부(방어력/장갑타입/
    // 크기)를 함께 받아 저장해둔다. AttackDamageImage/ArmorImage 호버 시(SetupInfoStatHoverTooltips) 이
    // 값을 툴팁으로 보여준다 (doc/0293).
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health, int attackDamage, int armor,
        AttackEffectType attackType, ArmorType armorType, SizeType sizeType, int shotCount, string description = "",
        bool canAttackGround = true, bool canAttackAir = true, int attackBonus = 0, int armorBonus = 0)
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

        if (infoText != null)
            infoText.text = description;

        infoAttackDamage = attackDamage;
        infoArmor = armor;
        infoAttackBonus = attackBonus;
        infoArmorBonus = armorBonus;
        infoAttackType = attackType;
        infoArmorType = armorType;
        infoSizeType = sizeType;
        infoShotCount = shotCount;
        infoCanAttackGround = canAttackGround;
        infoCanAttackAir = canAttackAir;

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

    // attackDamageImage/armorImage에 EventTrigger를 붙여, 호버 시 TooltipUI로 현재 선택 유닛의 공격/방어
    // 스탯을 전부 보여준다 (인스펙터에서 이미지만 연결하면 됨). 공격 아이콘은 공격타입/공격력(투사체를
    // 여러 발 동시에 쏘는 유닛은 "x2"처럼 배수 표기), 방어 아이콘은 방어력/장갑타입/유닛 크기 (doc/0293).
    private void SetupInfoStatHoverTooltips()
    {
        AddStatHoverTooltip(attackDamageImage, () =>
            LocalizationManager.GetText("infopanel.attacktooltip", infoAttackType, FormatStatWithBonus(infoAttackDamage, infoAttackBonus),
                infoShotCount > 1 ? $" (x{infoShotCount})" : string.Empty, GetAttackTargetText()));
        AddStatHoverTooltip(armorImage, () =>
            LocalizationManager.GetText("infopanel.armortooltip", FormatStatWithBonus(infoArmor, infoArmorBonus), infoArmorType, infoSizeType));
    }

    // 연구소 연구로 얻은 전역 보너스를 기본값에 합산하지 않고 "6 +2"처럼 별도 표기한다 (doc/0517) -
    // 보너스가 없으면(적/아군/미연구 상태) 지금까지와 동일하게 숫자만 표시.
    private static string FormatStatWithBonus(int baseValue, int bonus) =>
        bonus > 0 ? $"{baseValue + bonus} ({baseValue} +{bonus})" : baseValue.ToString();

    // 공격력 툴팁에 "Attack Target : Ground/Air" 식으로 표기할 텍스트를 만든다 (doc/0478).
    private string GetAttackTargetText()
    {
        if (infoCanAttackGround && infoCanAttackAir)
            return LocalizationManager.GetText("infopanel.attacktarget.groundair");
        if (infoCanAttackGround)
            return LocalizationManager.GetText("infopanel.attacktarget.ground");
        if (infoCanAttackAir)
            return LocalizationManager.GetText("infopanel.attacktarget.air");
        return LocalizationManager.GetText("infopanel.attacktarget.none");
    }

    // 건설실패/인구수부족/자원부족 등 경고 문구를 2초간 띄운다 (doc/0480). 표시 중에 새 경고가 들어오면
    // 타이머를 처음부터 다시 시작한다.
    public void ShowWarning(string message)
    {
        if (warningText == null)
            return;

        SoundManager.Instance?.PlayActionFailedWarning(); // 모든 실패 경고 공통 SFX(doc/0524)
        warningText.text = message;

        if (warningHideCoroutine != null)
            StopCoroutine(warningHideCoroutine);
        warningHideCoroutine = StartCoroutine(HideWarningAfterDelay());
    }

    private IEnumerator HideWarningAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        warningText.text = string.Empty;
        warningHideCoroutine = null;
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
    public void ShowResourceInfoPanel(Sprite icon, string resourceName, int remainingAmount, string description = "")
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

        if (infoText != null)
            infoText.text = description;

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

        if (infoText != null)
            infoText.text = string.Empty; // 건설 중인 건물은 완공될 건물의 설명을 아직 안 읽어옴 - 이전 선택의 설명이 남지 않도록 비움

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
    public void ShowSquadPanel(
        IReadOnlyList<UnitController> units,
        Action<UnitController> onSelectUnit,
        Action<UnitController> onShiftClickUnit,
        Action<UnitController> onCtrlClickUnit)
    {
        HideInfoPanel();
        HideProductionUI();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectUnit = onSelectUnit;
        squadOnShiftClickUnit = onShiftClickUnit;
        squadOnCtrlClickUnit = onCtrlClickUnit;
        squadShowingBuildings = false;

        if (!SquadUnitsEqual(squadUnitsSnapshot, units))
        {
            squadUnitsSnapshot.Clear();
            squadUnitsSnapshot.AddRange(units);
            squadCurrentPage = 0;
            squadContentDirty = true;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadUnitsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);

        if (squadContentDirty || lastRefreshedSquadPage != squadCurrentPage || lastRefreshedWasBuildingMode)
        {
            RefreshSquadSlots();
            lastRefreshedSquadPage = squadCurrentPage;
            lastRefreshedWasBuildingMode = false;
            squadContentDirty = false;
        }
    }

    // 건물 다중 선택(Shift+클릭) 시 유닛과 같은 방식으로 Squad_panel에 건물 아이콘 그리드를 보여준다.
    // 생산 대기열/커맨드 패널(HideProductionUI 등)은 건드리지 않는다 - RTSUnitController.UpdateUI()가
    // "대표 건물" 기준으로 바로 이어서 갱신한다.
    public void ShowBuildingSquadPanel(
        IReadOnlyList<BuildingController> buildings,
        Action<BuildingController> onSelectBuilding,
        Action<BuildingController> onShiftClickBuilding,
        Action<BuildingController> onCtrlClickBuilding)
    {
        HideInfoPanel();

        if (squadPanel != null)
            squadPanel.SetActive(true);

        squadOnSelectBuilding = onSelectBuilding;
        squadOnShiftClickBuilding = onShiftClickBuilding;
        squadOnCtrlClickBuilding = onCtrlClickBuilding;
        squadShowingBuildings = true;

        if (!SquadBuildingsEqual(squadBuildingsSnapshot, buildings))
        {
            squadBuildingsSnapshot.Clear();
            squadBuildingsSnapshot.AddRange(buildings);
            squadCurrentPage = 0;
            squadContentDirty = true;
        }

        int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadBuildingsSnapshot.Count / SquadUnitsPerPage));
        squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

        UpdateSquadPageButtons(pageCount);

        if (squadContentDirty || lastRefreshedSquadPage != squadCurrentPage || !lastRefreshedWasBuildingMode)
        {
            RefreshSquadBuildingSlots();
            lastRefreshedSquadPage = squadCurrentPage;
            lastRefreshedWasBuildingMode = true;
            squadContentDirty = false;
        }
    }

    // 페이지 버튼(page1~5) 클릭 시 호출: 해당 페이지의 12개(유닛 또는 건물)로 슬롯을 다시 채운다.
    public void SelectSquadPage(int page)
    {
        if (squadPageButtons == null || page < 0 || page >= squadPageButtons.Length)
            return;

        if (!squadPageButtons[page].gameObject.activeSelf)
            return;

        squadCurrentPage = page;
        lastRefreshedSquadPage = page;
        lastRefreshedWasBuildingMode = squadShowingBuildings;
        squadContentDirty = false;

        if (squadShowingBuildings)
            RefreshSquadBuildingSlots();
        else
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
        }
    }

    // Squad_panel 툴팁 제목용 유닛 이름 조회 (database에 없으면 "Unit"로 대체 - Title이 비어있으면
    // ProductionSlot이 툴팁 자체를 안 띄우므로 항상 비어있지 않은 값을 돌려줘야 한다).
    // heroName을 먼저 확인 - 구조된 유닛(unitID=0, enemyDataUnitID로만 종류 구분)은 NTA database에
    // 없으므로 heroName을 안 보면 항상 fallback 문구로 떨어짐 (doc/0519, Info Panel과 동일 우선순위).
    private string GetUnitDisplayName(UnitController unit)
    {
        if (!string.IsNullOrEmpty(unit.GetHeroName()))
            return unit.GetHeroName();

        if (database != null)
        {
            UnitData data = database.unitData.Find(d => d.ID == unit.GetUnitID());
            if (data != null && !string.IsNullOrEmpty(data.unitName))
                return LocalizationManager.GetTextOrFallback($"unit.nta.{data.ID}.name", data.unitName.Trim());
        }

        return LocalizationManager.GetText("squad.unitfallback");
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
            squadPageButtons[i].onClick.AddListener(() => SoundManager.Instance?.PlayUIClick()); // 버튼 클릭 공통 사운드(doc/0648)
        }
    }

    // 현재 squadCurrentPage 기준으로 squadSlots(12칸)를 건물로 채운다 (RefreshSquadSlots의 건물 버전).
    private void RefreshSquadBuildingSlots()
    {
        int startIndex = squadCurrentPage * SquadUnitsPerPage;

        for (int i = 0; i < squadSlots.Length; i++)
        {
            if (squadSlots[i] == null)
                continue;

            int buildingIndex = startIndex + i;

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

    private static bool SquadBuildingsEqual(List<BuildingController> current, IReadOnlyList<BuildingController> incoming)
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
        squadBuildingsSnapshot.Clear();
        squadCurrentPage = 0;
        squadShowingBuildings = false;
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

        // 슬롯 7은 Worker용 스킬 대체 슬롯(doc/0251) - 지금은 항상 비어있지만, 나중에 Worker도 스킬이
        // 생기면 RTSUnitController.UpdateUnitSkillUI()가 이 슬롯을 독립적으로 채운다. 여기서 같이
        // Clear()해버리면 그때도 같은 버그(매 프레임 Clear/SetData 반복)가 재현되므로 미리 보호해둔다.
        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(moveIcon, onMove),
                new CommandButtonData(attackIcon, onAttack),
                new CommandButtonData(stopIcon, onStop),
                new CommandButtonData(patrolIcon, onPatrol),
                new CommandButtonData(holdIcon, onHold),
                new CommandButtonData(returnIcon, onReturn),
                new CommandButtonData(buildIcon, onBuild)
            },
            UnitSkillFallbackSlotProtected);
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

        // UnitSkillSlotIndex(6)는 보호해서 여기서 건드리지 않는다 - RTSUnitController.UpdateUnitSkillUI()가
        // ShowUnitSkillSlot()/ClearUnitSkillSlot()으로 독립적으로 관리한다 (doc/0228).
        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(moveIcon, onMove),
                new CommandButtonData(attackIcon, onAttack),
                new CommandButtonData(stopIcon, onStop),
                new CommandButtonData(patrolIcon, onPatrol),
                new CommandButtonData(holdIcon, onHold)
            },
            UnitSkillSlotProtected);
    }

    // 특성(트레이트) 선택으로 얻은 액티브 스킬을 고정 슬롯(index 6)에 표시한다 (doc/0228).
    // ShowAttackUnitPanel이 이 슬롯을 protectedSlotIndices로 보호하므로, 매 프레임 독립적으로 갱신해도
    // Move/Attack 등 나머지 버튼과 간섭하지 않는다(BuildingLiftSlotIndex와 동일한 패턴).
    // useFallbackSlot=true면(=슬롯 6을 이미 다른 오더 버튼이 쓰고 있으면, 현재는 Worker의 Build) 슬롯 7에 넣는다.
    public void ShowUnitSkillSlot(CommandButtonData data, bool useFallbackSlot = false)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        if (index < slots.Length)
            slots[index]?.SetData(data);
    }

    public void ClearUnitSkillSlot(bool useFallbackSlot = false)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        if (index < slots.Length)
            slots[index]?.Clear();
    }

    // 스킬이 슬롯 6/7을 처음 차지하려는 순간 이미 다른 버튼(일꾼 Build/취소 등 잔상)이 남아있는지 확인하는
    // 진단용 (doc/0368). 정상 흐름이면 RTSUnitController.UpdateUnitSkillUI가 미리 비워두므로 항상 false다 -
    // true가 찍히면 슬롯 6을 공유하는 다른 경로에서 새로운 잔상 버그가 생겼다는 신호.
    public bool IsSkillSlotOccupied(bool useFallbackSlot)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        return index < slots.Length && slots[index] != null && slots[index].HasData;
    }

    // 스킬 슬롯 위에 쿨다운 원형 오버레이를 갱신한다 (doc/0323 후속). normalizedRemaining: 1=방금 사용(꽉 참) ~ 0=사용 가능.
    public void SetUnitSkillCooldown(float normalizedRemaining, bool useFallbackSlot = false)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        if (index < slots.Length)
            slots[index]?.SetCooldownFill(normalizedRemaining);
    }

    // Unit production (MainBase / Tier1 / Tier2 / Tier3)
    // tier별 유닛 생산 패널 (본진/병영/공장/우주공항 공용). 버튼 개수가 유닛 데이터 개수만큼 가변적이므로
    // 4개로 나뉘어 있던 ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel을 통합했다.
    // 실제 버튼 구성(어느 유닛을 넣을지)은 RTSUnitController.ShowUnitTierPanel()이 UnitData.tier로 판단해서 넘긴다.
    public void ShowUnitProductionPanel(UISelectionState state, CommandButtonData[] commands)
    {
        CurrentState = state;

        // 리프트 슬롯(8)/랠리 슬롯(6)은 여기 포함되지 않아도 건드리지 않는다 - RTSUnitController가
        // 바로 뒤이어 독립적으로 채운다.
        SetCommands(commands, LiftAndRallySlotsProtected);
    }

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

    // Skill Trait Select Overlay ("SkillSelect", doc/0228)
    // 고급유닛을 처음 선택했을 때(아직 특성을 안 골랐을 때) slot0=traitA/slot1=traitB에 카드를 채워 띄운다.
    // 커맨드 패널(slots[])과 완전히 분리된 독립 오브젝트라 이 오버레이가 떠 있어도 이동/공격 등
    // 기존 명령은 그대로 사용 가능하다(비모달).
    public void ShowSkillSelectPanel(CommandButtonData traitA, CommandButtonData traitB)
    {
        if (skillSelectPanel != null)
            skillSelectPanel.SetActive(true);

        if (skillSelectSlots != null)
        {
            if (skillSelectSlots.Length > 0)
                skillSelectSlots[0]?.SetData(traitA);
            if (skillSelectSlots.Length > 1)
                skillSelectSlots[1]?.SetData(traitB);
        }

        PositionSkillSelectAbovePanel();
    }

    public void HideSkillSelectPanel()
    {
        if (skillSelectPanel != null)
            skillSelectPanel.SetActive(false);

        if (skillSelectSlots != null)
        {
            for (int i = 0; i < skillSelectSlots.Length; i++)
                skillSelectSlots[i]?.Clear();
        }
    }

    // SkillSelect 오버레이를 커맨드 패널(panelRoot) 바로 위쪽 중앙에 붙인다. 화면 해상도나 커맨드 패널의
    // 실제 배치가 달라져도 항상 맞게 뜨도록 보여줄 때마다 다시 계산한다 (예전에 이 오브젝트에 잘못 남아있던
    // TooltipUI.PositionAboveTarget()과 동일한 방식 - 대상만 "호버 중인 버튼"에서 "panelRoot 고정"으로 바뀜, doc/0228).
    // Canvas가 Screen Space - Overlay라 카메라 인자는 null로 충분하다.
    private void PositionSkillSelectAbovePanel()
    {
        if (skillSelectPanel == null || panelRoot == null)
            return;

        RectTransform overlayRect = skillSelectPanel.transform as RectTransform;
        RectTransform panelRect = panelRoot.transform as RectTransform;
        RectTransform parentRect = overlayRect != null ? overlayRect.parent as RectTransform : null;

        if (overlayRect == null || panelRect == null || parentRect == null)
            return;

        Vector3[] corners = new Vector3[4];
        panelRect.GetWorldCorners(corners); // 0:bottom-left 1:top-left 2:top-right 3:bottom-right
        Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, topCenterWorld);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out Vector2 localPoint))
            return;

        // 오버레이의 피벗이 중앙이라고 가정하고, 오버레이 절반 높이 + 여백만큼 위로 밀어 커맨드 패널과 겹치지 않게 한다.
        localPoint.y += overlayRect.rect.height * 0.5f + skillSelectVerticalMargin;
        overlayRect.anchoredPosition = localPoint;
    }

    // 건물 커맨드 패널의 고정 슬롯(BuildingLiftSlotIndex)에 리프트/착륙 버튼을 추가로 표시한다.
    // ShowMainBasePanel/ShowBarracksPanel/ShowFactoryPanel/ShowAirportPanel 호출 "이후"에, 혹은
    // 전용 패널이 없는 건물(SupplyDepot/Lab 등) 선택 시 단독으로 호출된다.
    public void ShowBuildingLiftCommand(bool isLifted, ButtonAction onLiftOrLand)
    {
        if (BuildingLiftSlotIndex >= slots.Length || slots[BuildingLiftSlotIndex] == null)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        Sprite icon = isLifted ? landIcon : liftOffIcon;
        slots[BuildingLiftSlotIndex].SetData(new CommandButtonData(icon, onLiftOrLand));
    }

    // 공중에 뜬 건물 전용 "이동" 버튼 (고정 슬롯 BuildingMoveSlotIndex). ShowBuildingLiftCommand(Land 버튼)와 함께 사용.
    public void ShowBuildingMoveCommand(ButtonAction onMove)
    {
        if (BuildingMoveSlotIndex >= slots.Length || slots[BuildingMoveSlotIndex] == null)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        slots[BuildingMoveSlotIndex].SetData(new CommandButtonData(moveIcon, onMove));
    }

    // 생산 건물(MainBase/Tier1/Tier2/Tier3) 전용 "랠리" 버튼 (고정 슬롯 BuildingRallySlotIndex).
    // ShowUnitProductionPanel 호출 "이후"에, 그 tier의 유닛 생산 버튼과 나란히 표시된다.
    public void ShowBuildingRallyCommand(ButtonAction onRally)
    {
        if (BuildingRallySlotIndex >= slots.Length || slots[BuildingRallySlotIndex] == null)
            return;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        slots[BuildingRallySlotIndex].SetData(new CommandButtonData(rallyIcon, onRally));
    }

    // 건물 선택 컨텍스트에서 생산 패널이 없을 때(SupplyDepot/Lab/None, 또는 공중 상태) 사용.
    // 리프트 슬롯(8)은 항상 보호하고, protectMoveSlot이 true면(공중 상태) 이동 슬롯(0)도 함께 보호한다.
    // 나머지 슬롯만 비워서, 착륙 직후처럼 이동 슬롯을 더 이상 보호하지 않아도 되는 시점엔 자연스럽게 정리된다.
    public void ClearBuildingPanelExceptLiftSlots(bool protectMoveSlot)
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (i == BuildingLiftSlotIndex || (protectMoveSlot && i == BuildingMoveSlotIndex))
                continue;

            slots[i].Clear();
        }
    }

    // 리프트 불가능한 건물을 선택했을 때(CanLift() == false) 이전에 선택했던 다른 건물의 리프트 버튼이
    // 잔상으로 남지 않도록 정리한다. 이동(0번) 슬롯은 건드리지 않는다 - 그 슬롯은 항상 방금 실행된 패널
    // 표시 로직(ShowXPanel/ClearBuildingPanelExceptLiftSlots)이 이미 올바르게 채우거나 비워둔 상태라,
    // 여기서 다시 지우면 Lab처럼 canLift=false이면서 슬롯 0에 실제 커맨드(공격 업그레이드 등)를 쓰는
    // 패널의 버튼을 지워버리는 버그가 생긴다.
    public void ClearBuildingLiftSlots()
    {
        if (BuildingLiftSlotIndex < slots.Length)
            slots[BuildingLiftSlotIndex]?.Clear();
    }
}
