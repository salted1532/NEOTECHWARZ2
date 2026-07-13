using System;
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
    public List<EnemyController> selectedEnemyList;
    public ResourceNode selectedResourceNode; // 광물/가스는 항상 단일 선택
    public BaseStructure selectedBaseStructure; // 건설 중인 건물 기반도 항상 단일 선택

    // ===== 부대 지정(컨트롤 그룹) - Ctrl+숫자(1~9,0)로 저장, 숫자만 누르면 해당 부대를 선택 =====
    // 인덱스는 눌린 숫자와 그대로 대응(1→[0], 2→[1], ..., 9→[8], 0→[9]) - UserControl에서 이미 이렇게 매핑해 넘겨준다.
    private readonly List<UnitController>[] controlGroupUnits = new List<UnitController>[10];
    private readonly List<BuildingController>[] controlGroupBuildings = new List<BuildingController>[10];

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
        BaseStructureSelect,
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

    // ShowBuildPanel의 버튼 순서 및 PlacementSystem.StartPlacement(int)와 매칭되는 건물 ID
    public static class BuildingID
    {
        public const int CommandCenter = 1;
        public const int SupplyDepot = 2;
        public const int Barracks = 3;
        public const int Factory = 4;
        public const int Airport = 5;
        public const int Lab = 6;
    }


    private void Awake()
    {
        selectedUnitList = new List<UnitController>();
        selectedBuildingList = new List<BuildingController>();
        selectedEnemyList = new List<EnemyController>();
        UnitList = new List<UnitController>();
        BuildingList = new List<BuildingController>();
        ResourceNodeList = new List<ResourceNode>();

        for (int i = 0; i < controlGroupUnits.Length; i++)
        {
            controlGroupUnits[i] = new List<UnitController>();
            controlGroupBuildings[i] = new List<BuildingController>();
        }
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

    // 건설모드 진입 시점에 선택돼 있던 일꾼을 건설 담당자로 그대로 사용한다.
    // (SelectUnit()이 IsBuildMode() 중엔 새 선택을 막으므로, 건설모드에 있는 한 selectedUnitList는 그대로 유지된다)
    public UnitController GetSelectedWorker()
    {
        if (selectedUnitList.Count == 0)
            return null;

        UnitController unit = selectedUnitList[0];
        return unit != null && unit.CompareTag("Worker") && !unit.IsConstructing() ? unit : null;
    }

    // BaseStructure(건설 중단된 건물 기반) 우클릭: 선택된 일꾼을 보내 붙여서 건설을 재개시킨다.
    public void AssignBuilderToStructure(BaseStructure structure)
    {
        UnitController worker = GetSelectedWorker();
        if (worker == null)
            return;

        // NavMeshObstacle 때문에 중심점 자체엔 도달할 수 없으므로, 콜라이더 표면에서 일꾼과 가장 가까운
        // 지점을 목적지로 삼는다(자원/건물 접근 시 DistanceToTarget이 쓰는 것과 동일한 방식).
        Vector3 destination = structure.transform.position;
        if (structure.TryGetComponent<Collider>(out var collider))
            destination = collider.ClosestPoint(worker.transform.position);

        worker.GoBuild(
            destination,
            onArrived: () => worker.BeginConstruction(structure),
            onCancelled: null);
    }

    public void MoveSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].MoveTo(end);
        }
    }

    /// <summary>
    /// 선택된 유닛으로 특정 적 유닛을 추격 공격 (우클릭 적 클릭 / A 모드에서 적 클릭)
    /// </summary>
    public void AttackSelectedUnits(EnemyController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackUnitTarget(target);
        }
    }

    /// <summary>
    /// 바닥 공격-이동 명령 (A 모드에서 땅 클릭): 이동 중 교전 후 다시 이 지점으로 이동을 재개한다.
    /// </summary>
    public void AttackGroundSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackMoveTo(end);
        }
    }

    /// <summary>
    /// 아군 강제 공격 (A 모드에서 아군 좌클릭): 대상이 죽을 때까지 거리 상관없이 끝까지 추격 공격한다.
    /// (대상 자신이 선택되어 있어도 자기 자신을 공격하지는 않는다)
    /// </summary>
    public void AttackFriendlySelectedUnits(UnitController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            if (selectedUnitList[i] == target)
                continue;

            selectedUnitList[i].AttackFriendlyTarget(target);
        }
    }

    /// <summary>
    /// 아군 유닛 우클릭 = 계속 따라다니기: Idle 상태를 유지해 도중에 만나는 적은 AttackRange가 알아서 자동 교전한다.
    /// (대상 자신이 선택되어 있어도 자기 자신을 따라다니지는 않는다)
    /// </summary>
    public void FollowSelectedUnits(UnitController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            if (selectedUnitList[i] == target)
                continue;

            selectedUnitList[i].FollowUnit(target);
        }
    }

    /// <summary>
    /// 아군 건물 강제 공격 (A 모드에서 아군 건물 좌클릭): 대상이 파괴될 때까지 끝까지 공격한다.
    /// </summary>
    public void AttackFriendlyBuildingSelectedUnits(BuildingController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }
    }

    /// <summary>
    /// 아군 구조체(건설 중인 BaseStructure) 강제 공격 (A 모드에서 아군 구조체 좌클릭): 대상이 파괴(건설 취소)될 때까지 끝까지 공격한다.
    /// </summary>
    public void AttackFriendlyStructureSelectedUnits(BaseStructure target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
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

    // "리프트" 버튼: 선택된 건물(단일 취급)을 공중으로 띄운다.
    public void LiftSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].LiftOff();
    }

    // "착륙" 버튼: 선택된 건물(단일 취급)의 착륙 위치 선택 모드로 진입한다.
    public void BeginLandingSelectedBuilding()
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].BeginLanding();
    }

    // 선택된 건물(단일 취급)이 현재 공중에 떠 있는지 (UserControl 우클릭 분기용)
    public bool IsSelectedBuildingLifted()
    {
        return selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();
    }

    // 공중에 뜬 건물을 공중유닛처럼 우클릭/Move버튼 지점으로 수평 이동시킨다 (착륙하지 않고 계속 공중에 떠 있음).
    public void MoveSelectedLiftedBuilding(Vector3 destination)
    {
        if (selectedBuildingList.Count == 0) return;
        selectedBuildingList[0].MoveWhileLifted(destination);
    }

    #endregion

    #region Enemy선택 관련

    /// <summary>
    /// 좌클릭 선택 처리 (적은 항상 단일 선택)
    /// </summary>
    public void ClickSelectEnemy(EnemyController enemy)
    {
        DeselectAll();
        SelectEnemy(enemy);

        Debug.Log("적 선택");
    }

    private void SelectEnemy(EnemyController enemy)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.EnemySelect;

        enemy.SelectEnemy();
        selectedEnemyList.Add(enemy);
    }

    #endregion

    #region Ore/Gas선택 관련

    /// <summary>
    /// 좌클릭 선택 처리 (자원 노드는 항상 단일 선택)
    /// </summary>
    public void ClickSelectResource(ResourceNode node)
    {
        DeselectAll();
        SelectResource(node);

        Debug.Log("자원 선택");
    }

    private void SelectResource(ResourceNode node)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.OreSelect;

        node.SelectResource();
        selectedResourceNode = node;
    }

    // 채취 중 노드가 고갈되어 파괴될 때(ResourceNode.Extract) 선택 상태가 유령 참조로 남지 않도록 정리한다.
    public void ClearSelectedResourceIfMatches(ResourceNode node)
    {
        if (selectedResourceNode != node)
            return;

        selectedResourceNode = null;
        RTScurrentSate = SelectState.None;
    }

    #endregion

    #region BaseStructure선택 관련

    /// <summary>
    /// 좌클릭 선택 처리 (BaseStructure는 항상 단일 선택)
    /// </summary>
    public void ClickSelectStructure(BaseStructure structure)
    {
        DeselectAll();
        SelectStructure(structure);
    }

    private void SelectStructure(BaseStructure structure)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.BaseStructureSelect;

        structure.SelectStructure();
        selectedBaseStructure = structure;
    }

    // 건설이 완료되어 BaseStructure가 파괴될 때 선택 상태가 유령 참조로 남지 않도록 정리한다.
    public void ClearSelectedStructureIfMatches(BaseStructure structure)
    {
        if (selectedBaseStructure != structure)
            return;

        selectedBaseStructure = null;
        RTScurrentSate = SelectState.None;
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

        foreach (EnemyController enemy in selectedEnemyList)
        {
            enemy.DeselectEnemy();
        }

        selectedResourceNode?.DeselectResource();
        selectedBaseStructure?.DeselectStructure();

        RTScurrentSate = SelectState.None;
        selectedUnitList.Clear();
        selectedBuildingList.Clear();
        selectedEnemyList.Clear();
        selectedResourceNode = null;
        selectedBaseStructure = null;
    }

    #region 부대 지정(컨트롤 그룹)

    // Ctrl+숫자: 현재 선택된 유닛/건물을 지정한 그룹 번호(0~9)에 저장한다(기존 저장 내용은 덮어씀).
    // 아무 것도 선택돼 있지 않으면 아무 것도 하지 않는다(실수로 빈 선택을 눌러 기존 그룹을 날리는 것 방지).
    public void AssignControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        if (selectedUnitList.Count == 0 && selectedBuildingList.Count == 0)
            return;

        controlGroupUnits[groupIndex].Clear();
        controlGroupUnits[groupIndex].AddRange(selectedUnitList);

        controlGroupBuildings[groupIndex].Clear();
        controlGroupBuildings[groupIndex].AddRange(selectedBuildingList);
    }

    // Shift+숫자: 현재 선택된 유닛/건물 중 그 그룹에 아직 없는 대상만 추가한다(기존 멤버는 그대로 유지).
    // Ctrl(AssignControlGroup)과 달리 완전 교체가 아니라 병합이라, 유닛 하나가 여러 그룹에 동시에 속할 수 있다.
    public void AddSelectedToControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        if (selectedUnitList.Count == 0 && selectedBuildingList.Count == 0)
            return;

        foreach (UnitController unit in selectedUnitList)
        {
            if (unit != null && !controlGroupUnits[groupIndex].Contains(unit))
                controlGroupUnits[groupIndex].Add(unit);
        }

        foreach (BuildingController building in selectedBuildingList)
        {
            if (building != null && !controlGroupBuildings[groupIndex].Contains(building))
                controlGroupBuildings[groupIndex].Add(building);
        }
    }

    // 숫자만 누르면: 저장된 그룹의 유닛/건물을 선택 상태로 되돌린다. 그 사이 죽거나 파괴된 대상은 자동으로 걸러진다.
    // 그룹이 비어있으면(저장한 적 없거나 전부 사라짐) 기존 선택을 그대로 둔다.
    public void SelectControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
        controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

        if (controlGroupUnits[groupIndex].Count == 0 && controlGroupBuildings[groupIndex].Count == 0)
            return;

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);
    }

    #endregion

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

    // 공중에 뜬 건물의 "이동" 버튼(M)용
    public void EnterBuildingMoveMode()
    {
        userControl.SetOrderState("BuildingMove");
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

    //대기열 취소 (취소된 유닛 가격만큼 환불)
    public void CancelProduction(int index)
    {
        if (selectedBuildingList.Count == 0)
            return;

        int canceledUnitID = selectedBuildingList[0].CancelProduction(index);
        RefundUnit(canceledUnitID);
    }

    // 생산 건물이 파괴됐을 때 대기열에 남아있던 유닛들을 전부 환불한다.
    public void RefundProductionQueue(IReadOnlyList<ProductionData> queue)
    {
        if (queue == null)
            return;

        foreach (ProductionData item in queue)
            RefundUnit(item.UnitID);
    }

    // 유닛 하나의 가격(광물/가스/인구수)만큼 환불한다. 생산 시 TryProduceUnit이 이미 소모해둔 것을 그대로 되돌리는 것.
    private void RefundUnit(int unitID)
    {
        if (unitID < 0)
            return;

        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return;

        resourceManager.AddOre(data.mineral);
        resourceManager.AddGas(data.gas);
        resourceManager.ReleasePopulation(data.population);
    }

    // BaseStructure 건설을 취소했을 때 건물 가격(광물/가스) 전액을 환불한다. (건설 중엔 인구수를 소모하지 않으므로 인구수 환불은 없음)
    public void RefundBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return;

        resourceManager.AddOre(data.mineral);
        resourceManager.AddGas(data.gas);
    }

    // Info_panel의 "취소" 버튼/단축키(T)에서 호출.
    public void CancelSelectedBaseStructure()
    {
        selectedBaseStructure?.CancelConstruction();
    }

    public void AddMaxPopulation(int amount) => resourceManager.AddMaxPopulation(amount);

    // 건물이 파괴됐을 때 그 건물 종류가 제공하던 인구수 한도를 되돌린다 (buildingID로 DB에서 조회).
    public void RemoveMaxPopulationForBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data != null)
            resourceManager.RemoveMaxPopulation(data.maxpopulationamount);
    }

    // 유닛이 죽었을 때 그 유닛이 차지하던 인구수만큼 현재 인구수에서 반환한다 (unitID로 DB에서 조회).
    // RefundUnit()과 달리 광물/가스는 돌려주지 않는다 - 이미 살아서 존재했던 유닛이 죽은 것이지, 생산 취소가 아니기 때문.
    public void ReleaseUnitPopulation(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data != null)
            resourceManager.ReleasePopulation(data.population);
    }

    /// 유닛 생산 요청 (선택된 건물들에게 큐잉하기 전에 대기열/자원부터 확인)
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (selectedBuildingList.Count == 0)
            return false;

        // 대기열이 가득 찼으면 자원을 소모하기 전에 여기서 먼저 걸러낸다 (자원만 쓰고 큐잉은 안 되는 사고 방지)
        if (selectedBuildingList[0].IsProductionQueueFull())
        {
            Debug.Log("대기열 가득참!");
            return false;
        }

        if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
        {
            if (resourceManager.GetOre() < data.mineral || resourceManager.GetGas() < data.gas)
                Debug.Log("자원부족!");
            else
                Debug.Log("인구수부족!");

            return false; // 자원/인구 부족 → 여기서 그냥 반환, 아무 것도 소모 안 됨
        }

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

    #region 버튼 툴팁 데이터 구성

    // 유닛 생산 버튼용 ButtonAction 생성 (제목=유닛명, 비용=광물/가스/인구수)
    private ButtonAction UnitButtonAction(Action callback, int unitID, KeyCode shortcut = KeyCode.None)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Train {data.unitName}."
            : data.description;

        return ButtonAction.WithCost(callback, data.unitName, description, data.mineral, data.gas, data.population, shortcut);
    }

    // 건물 건설 버튼용 ButtonAction 생성 (제목=건물명, 비용=광물/가스/인구수)
    private ButtonAction BuildingButtonAction(Action callback, int buildingID, KeyCode shortcut = KeyCode.None)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string description = string.IsNullOrEmpty(data.description)
            ? $"Construct {data.Name}."
            : data.description;

        return ButtonAction.WithCost(callback, data.Name, description, data.mineral, data.gas, data.population, shortcut);
    }

    // Info_panel에 표시할 유닛/건물 이름 조회 (UnitController.unitID / BuildingController.buildingID 기준)
    public string GetUnitName(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        return data != null ? data.unitName.Trim() : string.Empty;
    }

    public string GetBuildingName(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        return data != null ? data.Name.Trim() : string.Empty;
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
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]", KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]", KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]", KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]", KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]", KeyCode.H),
                            ButtonAction.Simple(EnterReturnMode, "Return Cargo", "Return gathered resources to base. \nshortcut key [<color=yellow>R</color>]", KeyCode.R),
                            ButtonAction.Simple(BuildModeOn, "Build", "Enter build mode. \nshortcut key [<color=yellow>B</color>]", KeyCode.B));
                        break;

                    case UnitState.AttackUnit:
                        uIController.ShowAttackUnitPanel(
                            ButtonAction.Simple(EnterMoveMode, "Move", "Move to a location. \nshortcut key [<color=yellow>M</color>]", KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, "Attack", "Attack a target or location. \nshortcut key [<color=yellow>A</color>]", KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, "Stop", "Stop the current action. \nshortcut key [<color=yellow>S</color>]", KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, "Patrol", "Patrol along a path. \nshortcut key [<color=yellow>P</color>]", KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, "Hold", "Hold the current position. \nshortcut key [<color=yellow>H</color>]", KeyCode.H));
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
                    uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor());
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
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetComponent<HealthManager>());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                // 공중에 뜬 건물은 생산/연구 등 모든 커맨드를 막고 아래에서 Land/Move 버튼만 노출한다.
                bool selectedBuildingLifted = selectedBuildingList.Count > 0 && selectedBuildingList[0].IsLifted();

                if (selectedBuildingLifted)
                {
                    // ClearPanel()이 아니라 리프트/이동 슬롯을 보호하는 전용 메서드를 쓴다 - 매 프레임 호출되므로
                    // ClearPanel()로 그 두 슬롯까지 매번 껐다 켰다 하면 실행 중이던 클릭 코루틴/단축키가 끊긴다.
                    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: true);
                    uIController.HideProductionUI();
                }
                else
                {
                    switch (BuildingSelectState)
                    {
                        case BuildingState.MainBaseSelect:
                            uIController.ShowMainBasePanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Worker), UnitID.Worker, KeyCode.W));
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier1Select:
                            uIController.ShowBarracksPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Marine), UnitID.Marine, KeyCode.A),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Vulture), UnitID.Vulture, KeyCode.S));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier2Select:
                            uIController.ShowFactoryPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Goliath), UnitID.Goliath, KeyCode.I),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Tank), UnitID.Tank, KeyCode.P));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.Tier3Select:
                            uIController.ShowAirportPanel(
                                UnitButtonAction(() => TryProduceUnit(UnitID.Wraith), UnitID.Wraith, KeyCode.F),
                                UnitButtonAction(() => TryProduceUnit(UnitID.Guardian), UnitID.Guardian, KeyCode.D));

                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            break;

                        case BuildingState.SupplyDepot:
                        case BuildingState.Lab:
                        case BuildingState.None:
                            uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
                            uIController.HideProductionUI();
                            break;
                    }
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 고정 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (selectedBuildingList.Count > 0 && selectedBuildingList[0].CanLift())
                {
                    BuildingController building = selectedBuildingList[0];

                    uIController.ShowBuildingLiftCommand(
                        building.IsLifted(),
                        building.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, "Land", "Choose a landing site. \nshortcut key [<color=yellow>L</color>]", KeyCode.L)
                            : ButtonAction.Simple(LiftSelectedBuilding, "Lift Off", "Lift the building into the air. \nshortcut key [<color=yellow>L</color>]", KeyCode.L));

                    // 공중에 뜬 상태에서만 고정 슬롯(0번)에 "이동" 버튼을 추가로 노출한다.
                    if (building.IsLifted())
                    {
                        uIController.ShowBuildingMoveCommand(
                            ButtonAction.Simple(EnterBuildingMoveMode, "Move", "Move to a location while airborne. \nshortcut key [<color=yellow>M</color>]", KeyCode.M));
                    }
                }
                else if (selectedBuildingList.Count > 0)
                {
                    // 리프트 불가능한 건물(CanLift() == false)이면, 이전에 선택했던 다른 건물의 리프트/이동 버튼이
                    // 잔상으로 남지 않도록 정리한다.
                    uIController.ClearBuildingLiftSlots();
                }
                break;

            case SelectState.EnemySelect:

                if (selectedEnemyList.Count > 0)
                {
                    EnemyController enemy = selectedEnemyList[0];
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetComponent<HealthManager>(), enemy.GetAttackDamage(), enemy.GetArmor());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.OreSelect:

                if (selectedResourceNode != null)
                {
                    uIController.ShowResourceInfoPanel(
                        selectedResourceNode.GetIcon(),
                        selectedResourceNode.Type == ResourceType.Ore ? "Ore" : "Gas",
                        selectedResourceNode.RemainingAmount);
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.BaseStructureSelect:

                if (selectedBaseStructure != null)
                {
                    uIController.ShowBaseStructureInfoPanel(
                        selectedBaseStructure.GetIcon(),
                        GetBuildingName(selectedBaseStructure.GetBuildingID()),
                        selectedBaseStructure.GetComponent<HealthManager>());

                    uIController.ShowBaseStructureCommandPanel(
                        ButtonAction.Simple(
                            CancelSelectedBaseStructure,
                            "Cancel",
                            "Cancel construction and refund resources. \nshortcut key [<color=yellow>T</color>]",
                            KeyCode.T));
                }
                else
                {
                    uIController.HideInfoPanel();
                    uIController.ClearPanel();
                }

                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.BuildMode:
                uIController.ShowBuildPanel(
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.CommandCenter), BuildingID.CommandCenter, KeyCode.C),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.SupplyDepot), BuildingID.SupplyDepot, KeyCode.S),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Barracks), BuildingID.Barracks, KeyCode.B),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Factory), BuildingID.Factory, KeyCode.F),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport, KeyCode.P),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab, KeyCode.L),
                    ButtonAction.Simple(
                        CancelBuildMode,
                        "Cancel",
                        "Exit build mode. \nshortcut key [<color=yellow>T</color>]",
                        KeyCode.T));

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
    // 건설모드(배치 프리뷰 포함) 취소 - Cancel 버튼과 우클릭 명령 가로채기가 공유해서 쓴다.
    public void CancelBuildMode()
    {
        PlacementSystem.StopPlacement();
        ReturnState();
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
