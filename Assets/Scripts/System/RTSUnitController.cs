using System.Collections.Generic;
using UnityEngine;

public class RTSUnitController : MonoBehaviour
{
    // 현재 선택된 유닛들
    public List<UnitController> selectedUnitList;

    // 맵에 존재하는 모든 유닛
    public List<UnitController> UnitList;


    // 선택 상태
    public bool SelectMode = false;
    public bool isUnitSelect = false;
    public bool isBuildingSelect = false;
    public bool isOreSelect = false;
    public bool isEnemySelect = false;
    public bool MainBaseSelect = false;
    public bool BuildMode = false;

    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        UnitList = new List<UnitController>();
    }

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
    /// 모든 선택 해제
    /// </summary>
    public void DeselectAll()
    {
        foreach (UnitController unit in selectedUnitList)
        {
            unit.DeselectUnit();
        }

        selectedUnitList.Clear();
    }

    /// <summary>
    /// 유닛 선택
    /// </summary>
    private void SelectUnit(UnitController unit)
    {
        unit.SelectUnit();
        selectedUnitList.Add(unit);
    }

    /// <summary>
    /// 특정 유닛 선택 해제
    /// </summary>
    private void DeselectUnit(UnitController unit)
    {
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
}