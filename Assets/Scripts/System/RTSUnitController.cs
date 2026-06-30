using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static RTSUnitController;

public class RTSUnitController : MonoBehaviour
{
    // 현재 선택된 유닛들
    public List<UnitController> selectedUnitList;
    public List<BuildingController> selectedBuildingList;

    // 맵에 존재하는 모든 유닛
    public List<UnitController> UnitList;
    public List<BuildingController> BuildingList;

    [SerializeField]
    private UserControl userControl;
    [SerializeField]
    private UIController uIController;

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

    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        UnitList = new List<UnitController>();
    }

    private void Update()
    {
        UnitList.RemoveAll(unit => unit == null);
        BuildingList.RemoveAll(building => building == null);

        //UIController에게 선택 상황에 맞게 UI창 변경 명령
        switch (RTScurrentSate)
        {
            case SelectState.UnitSelect:
                switch (UnitSelectState)
                {
                    case UnitState.Worker:
                        uIController.ShowWorkerPanel(EnterMoveMode, EnterAttackMode, StopSelectedUnits, EnterPatrolMode, HoldSelectedUnits,EnterReturnMode, BuildModeOn);
                        break;
                    case UnitState.AttackUnit:
                        uIController.ShowAttackUnitPanel(EnterMoveMode, EnterAttackMode, StopSelectedUnits, EnterPatrolMode, HoldSelectedUnits);
                        break;
                }
                break;

            case SelectState.BuildingSelect:

                switch (BuildingSelectState)
                {
                    case BuildingState.MainBaseSelect:
                        uIController.ShowMainBasePanel(TestMethod);
                        break;

                    case BuildingState.Tier1Select:
                        //마린, 벌처 생산 메소드 연결해야함
                        uIController.ShowBarracksPanel(TestMethod, TestMethod);
                        break;

                    case BuildingState.Tier2Select:
                        //골리앗 탱크 생산 메소드 연결해야함
                        uIController.ShowFactoryPanel(TestMethod, TestMethod);
                        break;

                    case BuildingState.Tier3Select:
                        //레이스 배틀 생산 메소드 연결해야함
                        uIController.ShowAirportPanel(TestMethod, TestMethod);
                        break;

                    case BuildingState.SupplyDepot:
                        //구현필요
                        uIController.ClearPanel();
                        break;

                    case BuildingState.Lab:
                        //구현필요
                        uIController.ClearPanel();
                        break;
                    case BuildingState.None:
                        uIController.ClearPanel();
                        break;
                }
                break;

            case SelectState.EnemySelect:
                //구현필요
                uIController.ClearPanel();
                break;

            case SelectState.OreSelect:
                //구현필요
                uIController.ClearPanel();
                break;

            case SelectState.BuildMode:
                uIController.ShowBuildPanel(TestMethod, TestMethod, TestMethod, TestMethod, TestMethod, TestMethod, TestMethod);
                break;

            case SelectState.None:
                uIController.ClearPanel();
                break;
        }
    }

    #region Unit선택 명령

    /// <summary>
    /// 좌클릭 단일 선택
    /// </summary>
    public void ClickSelectUnit(UnitController newUnit)
    {
        DeselectAll();
        SelectUnit(newUnit);

        Debug.Log("단일 선택");
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
    /// 유닛 선택
    /// </summary>
    private void SelectUnit(UnitController unit)
    {
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

    public void PatrolSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].PatrolUnit(end);
        }
    }

    #endregion

    #region Building선택 명령

    /// <summary>
    /// 좌클릭 단일 선택
    /// </summary>
    public void ClickSelectBuilding(BuildingController newbuilding)
    {
        DeselectAll();
        SelectBuilding(newbuilding);

        Debug.Log("건물 단일 선택");
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
    /// 드래그 선택
    /// </summary>
    public void DragSelectBuilding(BuildingController newbuilding)
    {
        if (!selectedBuildingList.Contains(newbuilding))
        {
            SelectBuilding(newbuilding);
        }
    }

    /// <summary>
    /// 유닛 선택
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
    /// 특정 유닛 선택 해제
    /// </summary>
    private void Deselectbuilding(BuildingController building)
    {
        RTScurrentSate = SelectState.None;
        building.DeselecBuilding();
        selectedBuildingList.Remove(building);
    }

    #endregion

    /// <summary>
    /// 모든 선택 해제
    /// </summary>
    public void DeselectAll()
    {
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

    #region UserControl 상태 변화

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

    public void EnterReturnMode()
    {
        //광물 채취후 복귀
    }

    #endregion


    /// <summary>
    /// 테스트용
    /// </summary>
    //건설모드로 변경
    public void BuildModeOn()
    {
        RTScurrentSate = SelectState.BuildMode;
    }

    //UI 버튼 매핑 테스트용
    public void TestMethod()
    {
        
    }



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