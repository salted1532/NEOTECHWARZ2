using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

        MainBase           // Command center
    }

    // Button data
    // 커맨드 패널의 버튼 하나에 필요한 데이터 (아이콘 / 클릭 콜백 / 활성화 여부)
    public struct CommandButtonData
    {
        public Sprite Icon { get; }
        public Action Callback { get; }
        public bool Interactable { get; }

        public CommandButtonData(
            Sprite icon,
            Action callback,
            bool interactable = true)
        {
            Icon = icon;
            Callback = callback;
            Interactable = interactable;
        }
    }

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private ProductionSlot[] slots;

    [Header("Command Icons")]
    [SerializeField] private Sprite moveIcon;
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite stopIcon;
    [SerializeField] private Sprite patrolIcon;
    [SerializeField] private Sprite holdIcon;
    [SerializeField] private Sprite returnIcon;
    [SerializeField] private Sprite buildIcon;

    [Header("Building Icons")]
    [SerializeField] private Sprite commandCenterIcon;
    [SerializeField] private Sprite supplyDepotIcon;
    [SerializeField] private Sprite barracksIcon;
    [SerializeField] private Sprite factoryIcon;
    [SerializeField] private Sprite airportIcon;
    [SerializeField] private Sprite labIcon;

    [Header("Unit Icons")]
    [SerializeField] private Sprite workerIcon;
    [SerializeField] private Sprite marineIcon;
    [SerializeField] private Sprite vultureIcon;
    [SerializeField] private Sprite goliathIcon;
    [SerializeField] private Sprite tankIcon;
    [SerializeField] private Sprite wraithIcon;
    [SerializeField] private Sprite guardianIcon;

    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;

    [Header("Queue Empty Icons")]
    [SerializeField] private Sprite[] emptyQueueIcons; // 0=1, 1=2, 2=3, 3=4, 4=5

    [SerializeField] private TextMeshProUGUI OreText;
    [SerializeField] private TextMeshProUGUI GasText;
    [SerializeField] private TextMeshProUGUI PopulationText;

    public UISelectionState CurrentState = UISelectionState.None;

    private RTSUnitController rtsUnitController;

    // Production queue
    [SerializeField] private ProductionSlot[] queueSlots;
    [SerializeField] private UnitDataSO database;
    [SerializeField] private Slider progressSlider;

    private IReadOnlyList<ProductionData> currentQueue;
    private bool isShowingProductionQueue;

    private void Start()
    {
        rtsUnitController = FindFirstObjectByType<RTSUnitController>();

        ClearPanel();
        HideProductionUI();
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
        currentQueue = queue;
        isShowingProductionQueue = true;

        UpdateQueue(queue, onCancel);
    }

    // Hide production queue & progress time
    // 생산 대기열 & 진행시간 UI를 숨기고 초기화한다 (생산 건물이 아닌 대상 선택 시 등)
    public void HideProductionUI()
    {
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

    // Worker
    // 일꾼 선택 패널 (이동/공격/정지/순찰/홀드/반환/건설 버튼)
    public void ShowWorkerPanel(
    Action onMove,
    Action onAttack,
    Action onStop,
    Action onPatrol,
    Action onHold,
    Action onReturn,
    Action onBuild)
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
    Action onMove,
    Action onAttack,
    Action onStop,
    Action onPatrol,
    Action onHold)
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
    Action onCommandCenter,
    Action onSupplyDepot,
    Action onBarracks,
    Action onFactory,
    Action onAirport,
    Action onLab,
    Action onCancel)
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
    public void ShowMainBasePanel(Action onTrainWorker)
    {
        CurrentState = UISelectionState.MainBase;

        SetCommands(

            new CommandButtonData(workerIcon, onTrainWorker)
        );
    }

    // Barracks
    // 병영(Tier1 건물) 선택 패널 (마린/벌처 생산 버튼)
    public void ShowBarracksPanel(
    Action onMarine,
    Action onFirebat)
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
    Action onGoliath,
    Action onTank)
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
    Action onWraith,
    Action onGuardian)
    {
        CurrentState = UISelectionState.Tier3Building;

        SetCommands(

            new CommandButtonData(wraithIcon, onWraith),
            new CommandButtonData(guardianIcon, onGuardian)
        );
    }
}
