using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    // ���� UI�� � ��������
    public enum UISelectionState
    {
        None,

        Worker,            // �ϲ� ����
        CombatUnit,        // ���� ���� ����

        BuildMode,         // �Ǽ� ���

        Tier1Building,     // �跰 ��
        Tier2Building,
        Tier3Building,

        MainBase           // Ŀ�ǵ弾��
    }

    // ��ư ������
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

    //��⿭ ����
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

    private void UpdateResourceUI()
    {
        if (rtsUnitController == null)
            return;

        OreText.text = rtsUnitController.GetOre().ToString();
        GasText.text = rtsUnitController.GetGas().ToString();
        PopulationText.text = $"{rtsUnitController.GetPopulation()}/{rtsUnitController.GetMaxPopulation()}";
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
    /// �Ϲ� �г� ǥ��
    /// </summary>
    public void ShowPanel(UISelectionState state, params CommandButtonData[] commands)
    {
        CurrentState = state;

        SetCommands(commands);
    }

    /// <summary>
    /// �Ǽ���� ǥ�� (��� ��ư �ڵ� �߰�)
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

    //��⿭ ���
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
    public void ShowProductionUI(
        IReadOnlyList<ProductionData> queue,
        Action<int> onCancel)
    {
        currentQueue = queue;
        isShowingProductionQueue = true;

        UpdateQueue(queue, onCancel);
    }

    //���� ��⿭ & ����ð��� �����
    public void HideProductionUI()
    {
        foreach (var slot in queueSlots)
            slot.Clear();

        progressSlider.gameObject.SetActive(false);

        currentQueue = null;
        isShowingProductionQueue = false;
    }

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

    //����ð� ǥ�� ����
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

    //�ϲ�
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

    //��������
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

    //�Ǽ����
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

    //���α���
    public void ShowMainBasePanel(Action onTrainWorker)
    {
        CurrentState = UISelectionState.MainBase;

        SetCommands(

            new CommandButtonData(workerIcon, onTrainWorker)
        );
    }

    //����
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

    //���� 
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

    //����
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