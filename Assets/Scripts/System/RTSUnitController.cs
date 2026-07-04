using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static RTSUnitController;
using static UIController;

// RTS 게임 전체를 총괄하는 중앙 상태 관리자.
// 유닛/건물 선택 상태, 전체 유닛/건물/자원노드 목록, UI 갱신, 생산/건설 자원 소모 검증 등
// 여러 시스템(UserControl, UIController, PlacementSystem, ResourceManager)을 연결하는 허브 역할을 한다.
public class RTSUnitController : MonoBehaviour
{
    // 현재 선택된 유닛들
    public List<UnitController> selectedUnitList;
    public List<BuildingController> selectedBuildingList;

    // 맵에 존재하는 모든 유닛/건물/자원 노드
    public List<UnitController> UnitList;
    public List<BuildingController> BuildingList;
    public List<ResourceNode> ResourceNodeList;

    [SerializeField]
    private UserControl userControl;
    [SerializeField]
    private UIController uIController;
    [SerializeField]
    private PlacementSystem PlacementSystem;
    [SerializeField]
    private ResourceManager resourceManager;
    [SerializeField]
    private UnitDataSO unitDatabase;
    [SerializeField]
    private BuildingDataSO buildingDatabase;

    // ===== 상태 하나로 통합 =====
    public enum SelectState
    {
        None,
        UnitSelect,
        BuildingSelect,
        EnemySelect,
        OreSelect,
        BuildMode
    }

    public enum UnitState
    {
        None,
        Worker,
        AttackUnit
    }

    public enum BuildingState
    {
        None,
        MainBaseSelect,
        Tier1Select,
        Tier2Select,
        Tier3Select,
        SupplyDepot,
        Lab

    }

    public SelectState RTScurrentSate = SelectState.None;
    public BuildingState BuildingSelectState = BuildingState.None;
    public UnitState UnitSelectState = UnitState.None;
    public static class UnitID
    {
        public const int Worker = 1;
        public const int Marine = 2;
        public const int Vulture = 3;
        public const int Goliath = 4;
        public const int Tank = 5;
        public const int Wraith = 6;
        public const int Guardian = 7;
    }


    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        selectedBuildingList = new List<BuildingController>();
        UnitList = new List<UnitController>();
        BuildingList = new List<BuildingController>();
        ResourceNodeList = new List<ResourceNode>();
    }

    private void Update()
    {
        UnitList.RemoveAll(unit => unit == null);
        BuildingList.RemoveAll(building => building == null);
        ResourceNodeList.RemoveAll(node => node == null);

        //UI 갱신
        UpdateUI();
    }

    #region Unit선택 관련

    /// <summary>
    /// 좌클릭 선택 처리
    /// </summary>
    public void ClickSelectUnit(UnitController newUnit)
    {
        DeselectAll();
        SelectUnit(newUnit);

        Debug.Log("유닛 선택");
    }

    /// <summary>
    /// Shift + 클릭
    /// </summary>
    public void ShiftClickSelectUnit(UnitController newUnit)
    {
        if (selectedUnitList.Contains(newUnit))
        {
            DeselectUnit(newUnit);
        }
        else
        {
            SelectUnit(newUnit);
        }
    }

    /// <summary>
    /// 드래그 선택
    /// </summary>
    public void DragSelectUnit(UnitController newUnit)
    {
        if (!selectedUnitList.Contains(newUnit))
        {
            SelectUnit(newUnit);

        }
    }

    /// <summary>
    /// 단일 선택
    /// </summary>
    private void SelectUnit(UnitController unit)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.UnitSelect;

        switch (unit.tag)
        {
            case "Worker":
                UnitSelectState = UnitState.Worker;
                break;
            default:
                UnitSelectState = UnitState.AttackUnit;
                break;
        }
        unit.SelectUnit();
        selectedUnitList.Add(unit);
    }

    /// <summary>
    /// 특정 유닛 선택 해제
    /// </summary>
    private void DeselectUnit(UnitController unit)
    {
        RTScurrentSate = SelectState.None;
        unit.DeselectUnit();
        selectedUnitList.Remove(unit);
    }

    /// <summary>
    /// 현재 선택된 유닛 반환
    /// </summary>
    public List<UnitController> GetSelectedUnits()
    {
        return selectedUnitList;
    }

    public void MoveSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].MoveTo(end);
        }
    }

    /// <summary>
    /// 선택된 유닛 공격
    /// </summary>
    public void AttackSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackToUnit(end);
        }
    }

    /// <summary>
    /// 바닥 공격 명령
    /// </summary>
    public void AttackGroundSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackToGround(end);
        }
    }
    public void StopSelectedUnits()
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].StopUnit();
        }
    }
    public void HoldSelectedUnits()
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].HoldUnit();
        }
    }

    //자원 반환(Return Cargo) 명령
    public void EnterReturnMode()
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].ReturnCargo();
        }
    }

    public void PatrolSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].PatrolUnit(end);
        }
    }

    //자원 채취 명령
    public void GatherSelectedUnits(ResourceNode node)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].Gather(node);
        }
    }

    //건물 우클릭 명령 (일꾼이 자원을 들고 있으면 반환, 아니면 그냥 이동)
    public void MoveToBuildingSelectedUnits(BuildingController building)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].MoveToBuilding(building);
        }
    }

    #endregion

    #region Building선택 관련

    /// <summary>
    /// 좌클릭 선택 처리
    /// </summary>
    public void ClickSelectBuilding(BuildingController newbuilding)
    {
        DeselectAll();
        SelectBuilding(newbuilding);

        Debug.Log("건물 선택");
    }

    /// <summary>
    /// Shift + 클릭
    /// </summary>
    public void ShiftClickSelectBuilding(BuildingController newbuilding)
    {
        if (selectedBuildingList.Contains(newbuilding))
        {
            Deselectbuilding(newbuilding);
        }
        else
        {
            SelectBuilding(newbuilding);
        }
    }

    /// <summary>
    /// 단일 선택
    /// </summary>
    public void SelectBuilding(BuildingController building)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.BuildingSelect;

        switch (building.tag)
        {
            case "MainBase":
                BuildingSelectState = BuildingState.MainBaseSelect;
                break;

            case "Tier1":
                BuildingSelectState = BuildingState.Tier1Select;
                break;

            case "Tier2":
                BuildingSelectState = BuildingState.Tier2Select;
                break;

            case "Tier3":
                BuildingSelectState = BuildingState.Tier3Select;
                break;

            case "SupplyDepot":
                BuildingSelectState = BuildingState.SupplyDepot;
                break;

            case "Lab":
                BuildingSelectState = BuildingState.Lab;
                break;

            default:
                BuildingSelectState = BuildingState.None;
                break;
        }

        building.SelectBuilding();
        selectedBuildingList.Add(building);
    }

    /// <summary>
    /// 특정 건물 선택 해제
    /// </summary>
    private void Deselectbuilding(BuildingController building)
    {
        RTScurrentSate = SelectState.None;
        building.DeselecBuilding();
        selectedBuildingList.Remove(building);
    }
    public void SetRallySelectBuilding(Vector3 position)
    {
        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            selectedBuildingList[i].SetRallyPosition(position);
        }
    }

    #endregion

    /// <summary>
    /// 모든 선택 해제
    /// </summary>
    public void DeselectAll()
    {
        if (IsBuildMode())
            return;

        foreach (UnitController unit in selectedUnitList)
        {
            unit.DeselectUnit();
        }

        foreach (BuildingController building in selectedBuildingList)
        {
            building.DeselecBuilding();
        }

        RTScurrentSate = SelectState.None;
        selectedUnitList.Clear();
        selectedBuildingList.Clear();
    }

    #region UserControl 상태 전환

    public void EnterMoveMode()
    {
        userControl.SetOrderState("Move");
    }

    public void EnterAttackMode()
    {
        userControl.SetOrderState("Attack");
    }

    public void EnterPatrolMode()
    {
        userControl.SetOrderState("Patrol");
    }

    public void EnterRallyMode()
    {
        userControl.SetOrderState("Rally");
    }

    #endregion

    #region 생산 관련

    public void SpawnUnit(int unitID)
    {
        if (selectedBuildingList.Count == 0)
        {
            Debug.LogWarning("No buildings selected for spawning units.");
            return;
        }

        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            BuildingController building = selectedBuildingList[i];

            building.SpawnUnit(unitID);
        }
    }

    //건물의 대기열 정보 반환용
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        if (selectedBuildingList.Count == 0)
            return null;

        return selectedBuildingList[0].GetProductionQueue();
    }

    //생산 진행 시간 반환
    public float GetProductionProgress()
    {
        if (selectedBuildingList.Count == 0)
            return 0f;

        return selectedBuildingList[0].GetProductionProgress();
    }

    //대기열 취소
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        selectedBuildingList[0].CancelProduction(index);
    }

    /// 유닛 생산 요청 (선택된 건물들에게 큐잉하기 전에 자원부터 확인)
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
            return false; // 자원 부족 → 여기서 그냥 반환, 아무 것도 소모 안 됨

        SpawnUnit(unitID); // 기존 메소드: selectedBuildingList에 실제 큐잉
        return true;
    }

    /// 건물 건설 요청 (PlacementSystem이 실제로 배치를 확정하기 직전에 호출)
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        return resourceManager.TrySpend(data.mineral, data.gas);
    }

    #endregion

    #region UI관련

    private void UpdateUI()
    {
        //UIController에서 현재 상황에 맞게 UI창 상태 변경
        switch (RTScurrentSate)
        {
            case SelectState.UnitSelect:
                switch (UnitSelectState)
                {
                    case UnitState.Worker:
                        uIController.ShowWorkerPanel(
                            EnterMoveMode,
                            EnterAttackMode,
                            StopSelectedUnits,
                            EnterPatrolMode,
                            HoldSelectedUnits,
                            EnterReturnMode,
                            BuildModeOn);
                        break;

                    case UnitState.AttackUnit:
                        uIController.ShowAttackUnitPanel(
                            EnterMoveMode,
                            EnterAttackMode,
                            StopSelectedUnits,
                            EnterPatrolMode,
                            HoldSelectedUnits);
                        break;
                }

                uIController.HideProductionUI();

                // 유닛을 여러 마리 선택했으면 Squad_panel만, 한 마리면 Info_panel만 보여준다.
                if (selectedUnitList.Count > 1)
                {
                    uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit);
                }
                else if (selectedUnitList.Count == 1)
                {
                    UnitController unit = selectedUnitList[0];
                    uIController.ShowInfoPanel(unit.GetIcon(), unit.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }
                break;

            case SelectState.BuildingSelect:

                // 건물은 항상 단일 선택 취급 (Squad_panel은 유닛 다중 선택 전용) -> Info_panel 표시
                if (selectedBuildingList.Count > 0)
                {
                    BuildingController building = selectedBuildingList[0];
                    uIController.ShowInfoPanel(building.GetIcon(), building.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                switch (BuildingSelectState)
                {
                    case BuildingState.MainBaseSelect:
                        uIController.ShowMainBasePanel(() => SpawnUnit(UnitID.Worker));
                        uIController.ShowProductionUI(
                            GetProductionQueue(),
                            CancelProduction);
                        break;

                    case BuildingState.Tier1Select:
                        uIController.ShowBarracksPanel(
                            () => SpawnUnit(UnitID.Marine),
                            () => SpawnUnit(UnitID.Vulture));

                        uIController.ShowProductionUI(
                            GetProductionQueue(),
                            CancelProduction);
                        break;

                    case BuildingState.Tier2Select:
                        uIController.ShowFactoryPanel(
                            () => SpawnUnit(UnitID.Goliath),
                            () => SpawnUnit(UnitID.Tank));

                        uIController.ShowProductionUI(
                            GetProductionQueue(),
                            CancelProduction);
                        break;

                    case BuildingState.Tier3Select:
                        uIController.ShowAirportPanel(
                            () => SpawnUnit(UnitID.Wraith),
                            () => SpawnUnit(UnitID.Guardian));

                        uIController.ShowProductionUI(
                            GetProductionQueue(),
                            CancelProduction);
                        break;

                    case BuildingState.SupplyDepot:
                    case BuildingState.Lab:
                    case BuildingState.None:
                        uIController.ClearPanel();
                        uIController.HideProductionUI();
                        break;
                }
                break;

            case SelectState.BuildMode:
                uIController.ShowBuildPanel(
                    () => PlacementSystem.StartPlacement(1),
                    () => PlacementSystem.StartPlacement(2),
                    () => PlacementSystem.StartPlacement(3),
                    () => PlacementSystem.StartPlacement(4),
                    () => PlacementSystem.StartPlacement(5),
                    () => PlacementSystem.StartPlacement(6),
                    () =>
                    {
                        PlacementSystem.StopPlacement();
                        ReturnState();
                    });

                uIController.HideProductionUI();
                uIController.HideInfoPanel();
                uIController.HideSquadPanel();
                break;

            default:
                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideInfoPanel();
                uIController.HideSquadPanel();
                break;
        }
    }

    #endregion

    #region RTSController 상태 전환

    //건설모드 진입
    public void BuildModeOn()
    {
        RTScurrentSate = SelectState.BuildMode;
    }
    //상태 초기화
    public void ReturnState()
    {
        RTScurrentSate = SelectState.UnitSelect;
    }

    #endregion

    #region 자원 관련
    public int GetOre() => resourceManager.GetOre();
    public int GetGas() => resourceManager.GetGas();
    public int GetPopulation() => resourceManager.GetPopulation();
    public int GetMaxPopulation() => resourceManager.GetMaxPopulation();
    public void AddOre(int amount) => resourceManager.AddOre(amount);
    public void AddGas(int amount) => resourceManager.AddGas(amount);

    #endregion

    #region Test용

    /// <summary>
    /// 테스트용
    /// </summary>
    //UI 버튼 연결 테스트용
    public void TestMethod()
    {

    }

    #endregion

    #region 선택 상태 확인

    // 상태 확인용
    public bool IsNone() => RTScurrentSate == SelectState.None;
    public bool IsUnitSelect() => RTScurrentSate == SelectState.UnitSelect;
    public bool IsBuildingSelect() => RTScurrentSate == SelectState.BuildingSelect;
    public bool IsEnemySelect() => RTScurrentSate == SelectState.EnemySelect;
    public bool IsOreSelect() => RTScurrentSate == SelectState.OreSelect;
    public bool IsBuildMode() => RTScurrentSate == SelectState.BuildMode;

    #endregion

    #region Building 상태 확인

    public bool IsBuildingNone() => BuildingSelectState == BuildingState.None;
    public bool IsMainBase() => BuildingSelectState == BuildingState.MainBaseSelect;
    public bool IsTier1Building() => BuildingSelectState == BuildingState.Tier1Select;
    public bool IsTier2Building() => BuildingSelectState == BuildingState.Tier2Select;
    public bool IsTier3Building() => BuildingSelectState == BuildingState.Tier3Select;
    public bool IsSupplyDepot() => BuildingSelectState == BuildingState.SupplyDepot;
    public bool IsLab() => BuildingSelectState == BuildingState.Lab;

    #endregion
}
