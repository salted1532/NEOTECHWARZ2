using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;

public class RTSUnitController : MonoBehaviour
{
    // 현재 선택된 유닛들
    public List<UnitController> selectedUnitList;
    public List<BuildingController> selectedBuildingList;

    // 맵에 존재하는 모든 유닛
    public List<UnitController> UnitList;
    public List<BuildingController> BuildingList;

    // ===== 상태 하나로 통합 =====
    public enum SelectState
    {
        None,
        UnitSelect,
        BuildingSelect,
        EnemySelect,
        OreSelect,
        MainBaseSelect,
        BuildMode

    }

    public SelectState RTScurrentSate = SelectState.None;

    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        UnitList = new List<UnitController>();
    }

    private void Update()
    {
        UnitList.RemoveAll(unit => unit == null);
        BuildingList.RemoveAll(building => building == null);
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
        Selectbuilding(newbuilding);

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
            Selectbuilding(newbuilding);
        }
    }

    /// <summary>
    /// 드래그 선택
    /// </summary>
    public void DragSelectBuilding(BuildingController newbuilding)
    {
        if (!selectedBuildingList.Contains(newbuilding))
        {
            Selectbuilding(newbuilding);
        }
    }

    /// <summary>
    /// 유닛 선택
    /// </summary>
    private void Selectbuilding(BuildingController building)
    {
        RTScurrentSate = SelectState.BuildingSelect;
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

    // 상태 확인용
    public bool IsNone() => RTScurrentSate == SelectState.None;
    public bool IsUnitSelect() => RTScurrentSate == SelectState.UnitSelect;
    public bool IsBuildingSelect() => RTScurrentSate == SelectState.BuildingSelect;
    public bool IsEnemySelect() => RTScurrentSate == SelectState.EnemySelect;
    public bool IsOreSelect() => RTScurrentSate == SelectState.OreSelect;
    public bool IsMainBaseSelect() => RTScurrentSate == SelectState.MainBaseSelect;
    public bool IsBuildMode() => RTScurrentSate == SelectState.BuildMode;
}