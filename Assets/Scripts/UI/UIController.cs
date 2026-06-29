using System;
using UnityEngine;

public class UIController : MonoBehaviour
{
    // 현재 UI가 어떤 상태인지
    public enum UISelectionState
    {
        None,

        Worker,            // 일꾼 선택
        CombatUnit,        // 공격 유닛 선택

        BuildMode,         // 건설 모드

        Tier1Building,     // 배럭 등
        Tier2Building,
        Tier3Building,

        MainBase           // 커맨드센터
    }

    // 버튼 데이터
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
    [SerializeField] private Sprite firebatIcon;
    [SerializeField] private Sprite vultureIcon;
    [SerializeField] private Sprite tankIcon;
    [SerializeField] private Sprite goliathIcon;
    [SerializeField] private Sprite wraithIcon;
    [SerializeField] private Sprite guardianIcon;

    [Header("Common")]
    [SerializeField] private Sprite cancelIcon;

    public UISelectionState CurrentState = UISelectionState.None;

    private RTSUnitController rtsUnitController;

    private void Start()
    {
        ClearPanel();
    }

    

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
    /// 일반 패널 표시
    /// </summary>
    public void ShowPanel(UISelectionState state, params CommandButtonData[] commands)
    {
        CurrentState = state;

        SetCommands(commands);
    }

    /// <summary>
    /// 건설모드 표시 (취소 버튼 자동 추가)
    /// </summary>
    public void ShowBuildPanel(CommandButtonData[] buildingCommands, Action onCancel)
    {
        CurrentState = UISelectionState.BuildMode;

        SetCommands(AddCancelCommand(buildingCommands, onCancel));
    }

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

    //일꾼
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

    //공격유닛
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

    //건설모드
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

    //메인기지
    public void ShowMainBasePanel(Action onTrainWorker)
    {
        CurrentState = UISelectionState.MainBase;

        SetCommands(

            new CommandButtonData(workerIcon, onTrainWorker)
        );
    }

    //병영
    public void ShowBarracksPanel(
    Action onMarine,
    Action onFirebat)
    {
        CurrentState = UISelectionState.Tier1Building;

        SetCommands(

            new CommandButtonData(marineIcon, onMarine),
            new CommandButtonData(firebatIcon, onFirebat)
        );
    }

    //공장 
    public void ShowFactoryPanel(
    Action onVulture,
    Action onTank,
    Action onGoliath)
    {
        CurrentState = UISelectionState.Tier2Building;

        SetCommands(

            new CommandButtonData(vultureIcon, onVulture),
            new CommandButtonData(tankIcon, onTank),
            new CommandButtonData(goliathIcon, onGoliath)
        );
    }

    //공항
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