using System;
using System.Collections.Generic;
using UnityEngine;
using static UIController;

// RTS 게임 전체를 총괄하는 중앙 상태 관리자.
// 유닛/건물 선택 상태, 전체 유닛/건물/자원노드 목록, UI 갱신, 생산/건설 자원 소모 검증 등
// 여러 시스템(UserControl, UIController, PlacementSystem, ResourceManager)을 연결하는 허브 역할을 한다.
public class RTSUnitController : MonoBehaviour
{
    // 현재 선택된 유닛들
    public List<UnitController> selectedUnitList;
    public List<BuildingController> selectedBuildingList;
    public List<EnemyUnitController> selectedEnemyList;
    public EnemyBuildingController selectedEnemyBuilding; // 적 건물도 항상 단일 선택 (doc/0244)
    // 아군 OC 유닛 선택 목록 - AllyController가 EnemyUnitController를 상속하지 않는 완전 독립 클래스라서
    // (doc/0452) selectedEnemyList와 별개로 둔다. 아군 OC 건물(AllyBuildingController)은 EnemyBuildingController를
    // 그대로 상속하므로 selectedEnemyBuilding을 그대로 다형성으로 재사용 - 별도 목록 불필요.
    public List<AllyController> selectedAllyList;
    public ResourceNode selectedResourceNode; // 광물/가스는 항상 단일 선택
    public MissionItem selectedMissionItem; // 유물/데이터베이스 등 미션 오브젝트도 항상 단일 선택 (doc/0455)
    public BaseStructure selectedBaseStructure; // 건설 중인 건물 기반도 항상 단일 선택

    // ===== 부대 지정(컨트롤 그룹) - Ctrl+숫자(1~9,0)로 저장, 숫자만 누르면 해당 부대를 선택 =====
    // 인덱스는 눌린 숫자와 그대로 대응(1→[0], 2→[1], ..., 9→[8], 0→[9]) - UserControl에서 이미 이렇게 매핑해 넘겨준다.
    // 필드 초기화 시점(Awake보다 먼저, 오브젝트 생성 즉시)에 각 슬롯까지 채워야 한다 - Awake()에서 채우면
    // ControlGroupPanel처럼 다른 오브젝트가 이 컴포넌트의 Awake보다 먼저 Update를 도는 실행 순서일 때
    // 슬롯이 아직 null이라 NullReferenceException이 난다(doc/0330).
    private readonly List<UnitController>[] controlGroupUnits = CreateGroupSlots<UnitController>();
    private readonly List<BuildingController>[] controlGroupBuildings = CreateGroupSlots<BuildingController>();

    private static List<T>[] CreateGroupSlots<T>()
    {
        var slots = new List<T>[10];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new List<T>();
        return slots;
    }

    // 고급유닛 특성(트레이트) 선택 상태 (doc/0228). UpgradeManager(연구 보너스)와 동급의,
    // 매치 동안만 유지되는 unitID 단위 전역 상태. 유닛 개체가 아니라 "그 유닛 종류 전체"가 공유하는
    // 선택이라 UnitController가 직접 들고 있지 않고 여기서만 기록/조회한다.
    private readonly Dictionary<int, TraitChoice> chosenTraits = new Dictionary<int, TraitChoice>();

    // 지정형 액티브 스킬(단일 유닛/범위) 슬롯 6 클릭 → 클릭 확정 사이의 대기 상태 (doc/0323).
    // "A공격모드"와 동일한 패턴 - UserControl이 클릭을 확정하면 Confirm...Target()이 이 값을 읽어 각 유닛에 전달한다.
    private int pendingSkillUnitID;
    private UnitTraitOption pendingSkillTrait;

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
    // OC(적 진영) 유닛 데이터베이스 - EnemyUnitController.Start()가 자기 enemyUnitID로 스탯을 조회할 때 사용 (doc/0232).
    [SerializeField]
    private EnemyUnitDataSO enemyUnitDatabase;
    // OC(적 진영) 건물 데이터베이스 - EnemyBuildingController.Start()가 자기 enemyBuildingID로 이름/체력을
    // 조회할 때 사용 (doc/0245).
    [SerializeField]
    private EnemyBuildingDataSO enemyBuildingDatabase;
    // 스포어 브루드(외계종족) 유닛/건물 데이터베이스 - OC와는 별개 진영이라 SO 에셋도 분리(doc/0444).
    // enemyUnitID/enemyBuildingID는 OC 쪽과 겹치지 않게 배정돼 있으므로, OC 쪽에서 못 찾으면 이쪽에서 조회한다.
    [SerializeField]
    private EnemyUnitDataSO sporeBroodUnitDatabase;
    [SerializeField]
    private EnemyBuildingDataSO sporeBroodBuildingDatabase;
    [SerializeField]
    private UpgradeManager upgradeManager;
    [SerializeField]
    private DamageMultiplierTableSO damageMultiplierTable;

    // ===== 상태 하나로 통합 =====
    public enum SelectState
    {
        None,
        UnitSelect,
        BuildingSelect,
        EnemySelect,
        EnemyBuildingSelect,
        OreSelect,
        BaseStructureSelect,
        BuildMode,
        AllySelect, // 아군 OC 유닛 선택 (doc/0452)
        MissionItemSelect // 유물/데이터베이스 등 미션 오브젝트 선택 (doc/0455)
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

    // 고급유닛 특성(2택1) 선택 상태 (doc/0228). None=아직 안 고름.
    public enum TraitChoice { None, A, B }

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
        selectedEnemyList = new List<EnemyUnitController>();
        selectedAllyList = new List<AllyController>();
        UnitList = new List<UnitController>();
        BuildingList = new List<BuildingController>();
        ResourceNodeList = new List<ResourceNode>();

        BuildingController.OnLanded += HandleMainBaseLanded;
    }

    private void OnDestroy()
    {
        BuildingController.OnLanded -= HandleMainBaseLanded;
    }

    // 메인기지가 착륙할 때마다 자원을 든 모든 일꾼의 반납 목적지를 다시 계산시킨다 (doc/0420).
    private void HandleMainBaseLanded(BuildingController building)
    {
        if (!building.CompareTag("MainBase"))
            return;

        foreach (UnitController unit in UnitList)
        {
            if (unit != null)
                unit.ReturnCargo(); // 내부에서 isWorker/자원 소지 여부를 스스로 확인하므로 안전하게 전체 호출
        }
    }

    // 매 프레임 새 델리게이트를 할당하지 않도록 미리 만들어둔 null 판정 (doc/0498 Tier 4-2)
    private static readonly System.Predicate<UnitController> IsNullUnit = unit => unit == null;
    private static readonly System.Predicate<BuildingController> IsNullBuilding = building => building == null;
    private static readonly System.Predicate<ResourceNode> IsNullResourceNode = node => node == null;

    private void Update()
    {
        UnitList.RemoveAll(IsNullUnit);
        BuildingList.RemoveAll(IsNullBuilding);
        ResourceNodeList.RemoveAll(IsNullResourceNode);

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
        newUnit.GetComponent<UnitAudio>()?.PlaySelectVoice();
        newUnit.GetComponent<UnitAudio>()?.PlaySelectSFX();

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
            newUnit.GetComponent<UnitAudio>()?.PlaySelectVoice();
            newUnit.GetComponent<UnitAudio>()?.PlaySelectSFX();
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

            // 드래그로 여러 마리가 한꺼번에 선택돼도, 이번 드래그로 새로 선택된 첫 번째 유닛(리스트가
            // 비어있다가 1개가 된 순간)의 대사만 재생한다 - 나머지는 재생하지 않음 (doc/0262).
            if (selectedUnitList.Count == 1)
            {
                newUnit.GetComponent<UnitAudio>()?.PlaySelectVoice();
                newUnit.GetComponent<UnitAudio>()?.PlaySelectSFX();
            }
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
        unit.DeselectUnit();
        selectedUnitList.Remove(unit);

        if (selectedUnitList.Count == 0)
            RTScurrentSate = SelectState.None;
    }

    // Squad_panel Shift+Click: 그 유닛만 선택 목록에서 제거한다 (나머지는 그대로 선택 유지).
    public void RemoveUnitFromSelection(UnitController unit)
    {
        if (unit == null || !selectedUnitList.Contains(unit))
            return;

        DeselectUnit(unit);
    }

    // Squad_panel Ctrl+Click: 현재 선택 목록에서 그 유닛과 같은 종류(unitID)만 남기고 나머지는 선택 해제한다.
    public void KeepOnlySameUnitTypeInSelection(UnitController unit)
    {
        if (unit == null)
            return;

        int unitID = unit.GetUnitID();

        // 뒤에서부터 순회 - 앞에서부터 Remove하면 인덱스가 밀려서 일부를 건너뛸 수 있다.
        for (int i = selectedUnitList.Count - 1; i >= 0; i--)
        {
            UnitController other = selectedUnitList[i];
            if (other != null && other.GetUnitID() != unitID)
                DeselectUnit(other);
        }
    }

    // 건설모드 진입 시점에 선택돼 있던 일꾼을 건설 담당자로 그대로 사용한다.
    // (SelectUnit()이 IsBuildMode() 중엔 새 선택을 막으므로, 건설모드에 있는 한 selectedUnitList는 그대로 유지된다)
    public UnitController GetSelectedWorker()
    {
        foreach (UnitController unit in selectedUnitList)
        {
            if (unit != null && unit.CompareTag("Worker") && !unit.IsConstructing())
                return unit;
        }
        return null;
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

    // 선택된 유닛 여러 마리에게 동시에 내리는 명령 1건당 대표 유닛 1마리의 대사만 재생한다
    // (드래그로 다수 선택해도 이동/공격명령 대사가 겹쳐 시끄러워지지 않도록, doc/0255).
    private void PlayRepresentativeUnitVoice(System.Action<UnitAudio> play)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            if (selectedUnitList[i] == null)
                continue;

            UnitAudio audio = selectedUnitList[i].GetComponent<UnitAudio>();
            if (audio != null)
            {
                play(audio);
                return;
            }
        }
    }

    public void MoveSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].MoveTo(end);
        }

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayOrderVoice();
            audio.PlayOrderSFX();
        });
    }

    /// <summary>
    /// 선택된 유닛으로 특정 적 유닛을 추격 공격 (우클릭 적 클릭 / A 모드에서 적 클릭)
    /// </summary>
    public void AttackSelectedUnits(EnemyUnitController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackUnitTarget(target);
        }

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
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

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
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

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
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

        PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
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

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
    }

    /// <summary>
    /// 적 건물 지정 공격 (우클릭 적 건물 클릭 / A 모드에서 적 건물 클릭): 건물은 움직이지 않으므로
    /// AttackFriendlyTarget(거리 상관없이 파괴될 때까지 계속 추격/공격)을 그대로 재사용한다 (doc/0248).
    /// </summary>
    public void AttackEnemyBuildingSelectedUnits(EnemyBuildingController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
    }

    /// <summary>
    /// 아군 OC 유닛 지정 공격 (A 모드에서 아군 OC 클릭, doc/0450): Tag가 "Enemy"가 아니라서
    /// AttackRange(자동/지정 추격 공용 트리거)가 감지하지 못하므로, AttackSelectedUnits(orderedTarget
    /// 경로)가 아니라 AttackFriendlyTarget(거리 상관없이 파괴될 때까지 계속 추격/공격, Tag와 무관하게
    /// 직접 거리 판정)을 재사용한다 - 플레이어 자신의 유닛/건물을 강제공격할 때와 동일한 경로.
    /// target 타입은 AllyController(doc/0452, EnemyUnitController와 독립) - AttackFriendlyTarget이
    /// MonoBehaviour를 받으므로 아래 로직은 변경 없음.
    /// </summary>
    public void AttackAllyUnitSelectedUnits(AllyController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
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

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayAttackOrderVoice();
            audio.PlayOrderSFX();
        });
    }
    // 정지/홀드는 명령 확인음(orderSFX)을 재생하지 않는다 (doc/0289 - 사용자 요청).
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

        PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
    }

    public void PatrolSelectedUnits(Vector3 end)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].PatrolUnit(end);
        }

        PlayRepresentativeUnitVoice(audio =>
        {
            audio.PlayOrderVoice();
            audio.PlayOrderSFX();
        });
    }

    //자원 채취 명령
    public void GatherSelectedUnits(ResourceNode node)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].Gather(node);
        }

        PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
    }

    //건물 우클릭 명령 (일꾼이 자원을 들고 있으면 반환, 아니면 그냥 이동)
    public void MoveToBuildingSelectedUnits(BuildingController building)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].MoveToBuilding(building);
        }

        PlayRepresentativeUnitVoice(audio => audio.PlayOrderSFX());
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
        building.GetComponent<BuildingAudio>()?.PlaySelectVoice();

        // 생산 건물이면 선택하는 순간 자기 랠리 포인트 위치에 이동 포인터를 잠깐 보여준다(3초 후 자동
        // 사라짐 - UserControl의 기존 이동/랠리 확정 포인터를 그대로 재사용).
        if (IsProductionBuildingState(BuildingSelectState))
            userControl.ShowMovePointerAt(building.GetRallyPos());
    }

    private static bool IsProductionBuildingState(BuildingState state) =>
        state == BuildingState.MainBaseSelect || state == BuildingState.Tier1Select ||
        state == BuildingState.Tier2Select || state == BuildingState.Tier3Select;

    /// <summary>
    /// 특정 건물 선택 해제
    /// </summary>
    private void Deselectbuilding(BuildingController building)
    {
        building.DeselecBuilding();
        selectedBuildingList.Remove(building);

        if (selectedBuildingList.Count == 0)
            RTScurrentSate = SelectState.None;
    }

    // Squad_panel Shift+Click: 그 건물만 선택 목록에서 제거한다 (나머지는 그대로 선택 유지).
    public void RemoveBuildingFromSelection(BuildingController building)
    {
        if (building == null || !selectedBuildingList.Contains(building))
            return;

        Deselectbuilding(building);
    }

    // Squad_panel Ctrl+Click: 현재 선택 목록에서 그 건물과 같은 종류(buildingID)만 남기고 나머지는 선택 해제한다.
    public void KeepOnlySameBuildingTypeInSelection(BuildingController building)
    {
        if (building == null)
            return;

        int buildingID = building.GetBuildingID();

        for (int i = selectedBuildingList.Count - 1; i >= 0; i--)
        {
            BuildingController other = selectedBuildingList[i];
            if (other != null && other.GetBuildingID() != buildingID)
                Deselectbuilding(other);
        }
    }
    public void SetRallySelectBuilding(Vector3 position)
    {
        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            selectedBuildingList[i].SetRallyPosition(position);
        }
    }

    // "리프트" 버튼: 선택된 건물들 중 대표 건물을 공중으로 띄운다.
    public void LiftSelectedBuilding()
    {
        GetRepresentativeBuilding()?.LiftOff();
    }

    // "착륙" 버튼: 선택된 건물들 중 대표 건물의 착륙 위치 선택 모드로 진입한다.
    public void BeginLandingSelectedBuilding()
    {
        GetRepresentativeBuilding()?.BeginLanding();
    }

    // 선택된 건물들 중 대표 건물이 현재 공중에 떠 있는지 (UserControl 우클릭 분기용)
    public bool IsSelectedBuildingLifted()
    {
        BuildingController building = GetRepresentativeBuilding();
        return building != null && building.IsLifted();
    }

    // 공중에 뜬 대표 건물을 공중유닛처럼 우클릭/Move버튼 지점으로 수평 이동시킨다 (착륙하지 않고 계속 공중에 떠 있음).
    public void MoveSelectedLiftedBuilding(Vector3 destination)
    {
        GetRepresentativeBuilding()?.MoveWhileLifted(destination);
    }

    #endregion

    #region Enemy선택 관련

    /// <summary>
    /// 좌클릭 선택 처리 (적은 항상 단일 선택)
    /// </summary>
    public void ClickSelectEnemy(EnemyUnitController enemy)
    {
        DeselectAll();
        SelectEnemy(enemy);

        Debug.Log("적 선택");
    }

    private void SelectEnemy(EnemyUnitController enemy)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.EnemySelect;

        enemy.SelectEnemy();
        selectedEnemyList.Add(enemy);
    }

    // 안개에 가려지는 등, 외부 이벤트로 특정 적 유닛의 선택을 해제해야 할 때 호출한다
    // (ClearSelectedEnemyBuildingIfMatches와 동일한 패턴, doc/0360).
    public void ClearSelectedEnemyIfMatches(EnemyUnitController enemy)
    {
        if (!selectedEnemyList.Contains(enemy))
            return;

        enemy.DeselectEnemy();
        selectedEnemyList.Remove(enemy);
        RTScurrentSate = SelectState.None;
    }

    #endregion

    #region Ally선택 관련 (doc/0452 - AllyController가 EnemyUnitController와 독립된 타입이라 병행 구현)

    /// <summary>
    /// 좌클릭 선택 처리 (아군 OC도 항상 단일 선택 - ClickSelectEnemy와 동일한 패턴)
    /// </summary>
    public void ClickSelectAlly(AllyController ally)
    {
        DeselectAll();
        SelectAlly(ally);
    }

    private void SelectAlly(AllyController ally)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.AllySelect;

        ally.SelectAlly();
        selectedAllyList.Add(ally);
    }

    // 안개에 가려지는 등, 외부 이벤트로 특정 아군 OC 유닛의 선택을 해제해야 할 때 호출한다
    // (ClearSelectedEnemyIfMatches와 동일한 패턴, doc/0360).
    public void ClearSelectedAllyIfMatches(AllyController ally)
    {
        if (!selectedAllyList.Contains(ally))
            return;

        ally.DeselectAlly();
        selectedAllyList.Remove(ally);
        RTScurrentSate = SelectState.None;
    }

    #endregion

    #region EnemyBuilding선택 관련

    /// <summary>
    /// 좌클릭 선택 처리 (적 건물은 항상 단일 선택)
    /// </summary>
    public void ClickSelectEnemyBuilding(EnemyBuildingController building)
    {
        DeselectAll();
        SelectEnemyBuilding(building);
    }

    private void SelectEnemyBuilding(EnemyBuildingController building)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.EnemyBuildingSelect;

        building.SelectEnemyBuilding();
        selectedEnemyBuilding = building;
    }

    // 적 건물이 파괴되거나(Die) 안개에 가려져서(doc/0360) 선택 상태가 유령 참조로 남지 않도록 정리한다.
    public void ClearSelectedEnemyBuildingIfMatches(EnemyBuildingController building)
    {
        if (selectedEnemyBuilding != building)
            return;

        building.DeselectEnemyBuilding();
        selectedEnemyBuilding = null;
        RTScurrentSate = SelectState.None;
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

    #region MissionItem선택 관련 (doc/0455 - Ore/Gas선택과 동일한 패턴)

    /// <summary>
    /// 좌클릭 선택 처리 (미션 오브젝트도 항상 단일 선택)
    /// </summary>
    public void ClickSelectMissionItem(MissionItem item)
    {
        DeselectAll();
        SelectMissionItem(item);
    }

    private void SelectMissionItem(MissionItem item)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.MissionItemSelect;

        item.SelectItem();
        selectedMissionItem = item;
    }

    // 미션 오브젝트가 반납 완료 등으로 비활성화/파괴될 때 선택 상태가 유령 참조로 남지 않도록 정리한다.
    public void ClearSelectedMissionItemIfMatches(MissionItem item)
    {
        if (selectedMissionItem != item)
            return;

        selectedMissionItem = null;
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

        foreach (EnemyUnitController enemy in selectedEnemyList)
        {
            enemy.DeselectEnemy();
        }

        foreach (AllyController ally in selectedAllyList)
        {
            ally.DeselectAlly();
        }

        selectedEnemyBuilding?.DeselectEnemyBuilding();
        selectedResourceNode?.DeselectResource();
        selectedMissionItem?.DeselectItem();
        selectedBaseStructure?.DeselectStructure();

        RTScurrentSate = SelectState.None;
        selectedUnitList.Clear();
        selectedBuildingList.Clear();
        selectedEnemyList.Clear();
        selectedAllyList.Clear();
        selectedEnemyBuilding = null;
        selectedResourceNode = null;
        selectedMissionItem = null;
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
        int memberCount = PurgeAndCountControlGroup(groupIndex);

        if (memberCount == 0)
        {
            Debug.Log($"[SelectControlGroup] 그룹 {groupIndex}: 인원 0명이라 선택 취소");
            return;
        }

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);

        Debug.Log($"[SelectControlGroup] 그룹 {groupIndex}: 저장된 인원 {memberCount}명, IsBuildMode={IsBuildMode()}, " +
            $"실제 선택된 유닛 {selectedUnitList.Count}개 / 건물 {selectedBuildingList.Count}개");
    }

    // 그룹 내 죽은/파괴된 대상을 정리하고 남은 인원수(유닛+건물)를 반환한다.
    // ControlGroupPanel(UI)이 "전멸해서 버튼을 없애야 하는지" 매 프레임 확인하는 용도로도 쓴다.
    public int PurgeAndCountControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return 0;

        controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
        controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

        return controlGroupUnits[groupIndex].Count + controlGroupBuildings[groupIndex].Count;
    }

    // 더블클릭 시 카메라가 이동할 기준 좌표. 유닛이 여러 개면 리스트의 [0]번째(가장 먼저 저장/추가된) 유닛을 우선하고,
    // 유닛이 하나도 없으면(건물만 지정된 그룹) 건물 [0]번째를 대신 사용한다.
    public bool TryGetControlGroupFocusPosition(int groupIndex, out Vector3 position)
    {
        position = default;

        if (PurgeAndCountControlGroup(groupIndex) == 0)
            return false;

        if (controlGroupUnits[groupIndex].Count > 0)
        {
            position = controlGroupUnits[groupIndex][0].transform.position;
            return true;
        }

        if (controlGroupBuildings[groupIndex].Count > 0)
        {
            position = controlGroupBuildings[groupIndex][0].transform.position;
            return true;
        }

        return false;
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

    // 생산 건물 패널의 랠리 버튼(고정 슬롯 6) 데이터. M(이동)/A(공격)처럼 위치 지정 대기 모드로 들어간다 -
    // 그 다음 클릭한 지점이 새로 생산된 유닛의 집결지가 된다(건물 우클릭과 동일한 동작).
    private ButtonAction RallyButtonAction()
    {
        return ButtonAction.Simple(
            EnterRallyMode,
            LocalizationManager.GetText("cmd.rally.title"),
            LocalizationManager.GetText("cmd.rally.desc", ShortcutTag(KeyCode.Y)),
            KeyCode.Y);
    }

    // 공중에 뜬 건물의 "이동" 버튼(M)용
    public void EnterBuildingMoveMode()
    {
        userControl.SetOrderState("BuildingMove");
    }

    #endregion

    #region 생산 관련

    //건물의 대기열 정보 반환용 (대표 건물 기준)
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        return GetRepresentativeBuilding()?.GetProductionQueue();
    }

    //생산 진행 시간 반환 (대표 건물 기준)
    public float GetProductionProgress()
    {
        BuildingController building = GetRepresentativeBuilding();
        return building != null ? building.GetProductionProgress() : 0f;
    }

    //대기열 취소 (취소된 유닛 가격만큼 환불, 대표 건물 기준)
    public void CancelProduction(int index)
    {
        BuildingController building = GetRepresentativeBuilding();
        if (building == null)
            return;

        int canceledUnitID = building.CancelProduction(index);
        RefundUnit(canceledUnitID);
    }

    // ESC 단축키 전용: 대기열의 "맨 뒤" 항목(가장 최근에 추가된 항목)부터 하나씩 취소한다.
    // 큐가 꽉 찼으면 인덱스 4 → 3 → 2 → ... 순서로, ESC를 누를 때마다 한 칸씩 역순 취소된다.
    public void CancelLastQueuedProduction()
    {
        IReadOnlyList<ProductionData> queue = GetProductionQueue();

        if (queue == null || queue.Count == 0)
            return;

        CancelProduction(queue.Count - 1);
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

    // 생산 큐를 거치지 않고(맵에 미리 배치된 시작 유닛 등) 존재하는 유닛의 인구수를 현재 사용량에 반영한다
    // (unitID로 DB에서 조회) - UnitController.Start()가 자신이 생산된 유닛이 아닐 때만 호출한다.
    public void AddPopulationForExistingUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data != null)
            resourceManager.AddPopulationDirect(data.population);
    }

    // 여러 건물이 섞여 선택됐을 때 패널/생산 대기열/리프트 버튼에 쓸 "대표 건물"을 우선순위
    // (MainBase > Tier1 > Tier2 > Tier3 > SupplyDepot > Lab)로 고른다. 우선순위 태그의 건물이 하나도
    // 없으면(이론상 발생 안 함) 선택된 첫 번째 건물을 그대로 쓴다.
    private static readonly string[] BuildingPriorityTags =
        { "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab" };

    private BuildingController GetRepresentativeBuilding()
    {
        if (selectedBuildingList.Count == 0)
            return null;

        foreach (string tag in BuildingPriorityTags)
        {
            BuildingController match = selectedBuildingList.Find(b => b != null && b.CompareTag(tag));
            if (match != null)
                return match;
        }

        return selectedBuildingList[0];
    }

    // 건물 태그 → 커맨드 패널 상태 매핑 (SelectBuilding()의 태그 스위치와 동일한 짝짓기).
    private static BuildingState TagToBuildingState(string tag)
    {
        switch (tag)
        {
            case "MainBase": return BuildingState.MainBaseSelect;
            case "Tier1": return BuildingState.Tier1Select;
            case "Tier2": return BuildingState.Tier2Select;
            case "Tier3": return BuildingState.Tier3Select;
            case "SupplyDepot": return BuildingState.SupplyDepot;
            case "Lab": return BuildingState.Lab;
            default: return BuildingState.None;
        }
    }

    // tier(0~3) 값 자체를 건물 태그 문자열로 바꾸는 고정 매핑 (tier 필드의 정의 그 자체).
    private static readonly string[] TierProducerTags = { "MainBase", "Tier1", "Tier2", "Tier3" };

    // 유닛 ID가 어느 건물 태그에서 생산 가능한지, UnitData.tier 값으로부터 조회한다.
    // 다른 티어 건물이 섞여 선택됐을 때 그 유닛을 생산할 수 없는 건물을 걸러내기 위함.
    private string GetProducerTagForUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null || data.tier < 0 || data.tier >= TierProducerTags.Length)
            return null;

        return TierProducerTags[data.tier];
    }

    private static readonly UISelectionState[] TierPanelStates =
        { UISelectionState.MainBase, UISelectionState.Tier1Building, UISelectionState.Tier2Building, UISelectionState.Tier3Building };

    // tier별 유닛 목록은 unitDatabase(런타임에 안 바뀌는 SO)에서 한 번만 뽑아 캐싱해둔다 - 매 프레임
    // 다시 FindAll할 필요가 없다.
    private readonly Dictionary<int, List<UnitData>> unitsByTierCache = new Dictionary<int, List<UnitData>>();

    private List<UnitData> GetUnitsInTier(int tier)
    {
        if (!unitsByTierCache.TryGetValue(tier, out var list))
        {
            list = unitDatabase.unitData.FindAll(d => d.tier == tier);
            unitsByTierCache[tier] = list;
        }
        return list;
    }

    // ShowUnitTierPanel이 마지막으로 만든 버튼 배열/활성화 상태 캐시. 이 패널은 UpdateUI()를 통해 매 프레임
    // 호출되지만, 선행 건물 완공 여부(IsUnitPrerequisiteMet)가 프레임 사이에 실제로 바뀌지 않는 한
    // CommandButtonData[]와 그 안의 델리게이트들을 다시 만들 필요가 없다.
    private int lastTierPanelTier = -1;
    private CommandButtonData[] lastTierPanelCommands;
    private bool[] lastTierPanelInteractable;

    // tier(0=본진,1=병영,2=공장,3=우주공항)에 속한 유닛을 unitDatabase에서 모두 찾아 생산 버튼 패널을 구성한다.
    // UnitDataSO에 유닛을 추가하고 tier 값만 지정하면, 코드 수정 없이 해당 건물 패널에 자동으로 나타난다.
    private void ShowUnitTierPanel(int tier)
    {
        List<UnitData> unitsInTier = GetUnitsInTier(tier);

        bool needsRebuild = tier != lastTierPanelTier
            || lastTierPanelCommands == null
            || lastTierPanelInteractable == null
            || lastTierPanelInteractable.Length != unitsInTier.Count;

        if (!needsRebuild)
        {
            for (int i = 0; i < unitsInTier.Count; i++)
            {
                if (lastTierPanelInteractable[i] != IsUnitPrerequisiteMet(unitsInTier[i].ID))
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (needsRebuild)
        {
            CommandButtonData[] commands = new CommandButtonData[unitsInTier.Count];
            bool[] interactable = new bool[unitsInTier.Count];

            for (int i = 0; i < unitsInTier.Count; ++i)
            {
                UnitData data = unitsInTier[i];
                bool met = IsUnitPrerequisiteMet(data.ID);
                interactable[i] = met;
                commands[i] = new CommandButtonData(
                    data.Icon,
                    UnitButtonAction(() => TryProduceUnit(data.ID), data.ID, data.shortcutKey),
                    met);
            }

            lastTierPanelCommands = commands;
            lastTierPanelInteractable = interactable;
            lastTierPanelTier = tier;
        }

        uIController.ShowUnitProductionPanel(TierPanelStates[tier], lastTierPanelCommands);
    }

    /// 유닛 생산 요청 (선택된 건물 각각에 대해 생산 가능 여부/대기열/자원을 확인하고,
    /// 실제로 큐잉되는 건물 수만큼만 비용을 차감한다)
    public bool TryProduceUnit(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return false;

        if (!IsUnitPrerequisiteMet(unitID))
            return false;

        if (selectedBuildingList.Count == 0)
            return false;

        string producerTag = GetProducerTagForUnit(unitID);
        bool producedAny = false;

        for (int i = 0; i < selectedBuildingList.Count; ++i)
        {
            BuildingController building = selectedBuildingList[i];

            // 다른 티어 건물이 섞여 선택된 경우, 이 유닛을 생산할 수 없는 건물은 건너뛴다.
            if (producerTag != null && !building.CompareTag(producerTag))
                continue;

            // 대기열이 가득 찼으면 이 건물은 건너뛴다 (자원만 쓰고 큐잉은 안 되는 사고 방지)
            if (building.IsProductionQueueFull())
                continue;

            if (!resourceManager.TrySpend(data.mineral, data.gas, data.population))
            {
                if (resourceManager.GetOre() < data.mineral || resourceManager.GetGas() < data.gas)
                {
                    Debug.Log("자원부족!");
                    SoundManager.Instance?.PlayInsufficientResourcesWarning();
                    uIController.ShowWarning(LocalizationManager.GetText("warning.resource"));
                }
                else
                {
                    Debug.Log("인구수부족!");
                    SoundManager.Instance?.PlayInsufficientPopulationWarning();
                    uIController.ShowWarning(LocalizationManager.GetText("warning.population"));
                }

                break; // 자원이 부족해진 시점부터는 나머지 건물도 어차피 불가능하므로 중단
            }

            building.SpawnUnit(unitID); // 이 건물 1곳에만 큐잉 (건물마다 자원을 개별 차감했으므로 1:1 대응)
            producedAny = true;
        }

        if (!producedAny)
            Debug.Log("생산 불가 (대기열 가득 참, 자원 부족, 또는 이 유닛을 생산할 수 있는 건물 없음)");

        return producedAny;
    }

    // 연구 요청 (대표 건물 기준 - Lab이 여러 개 선택된 경우도 대표 건물 하나만 처리)
    public bool TryResearch(ResearchType type)
    {
        BuildingController building = GetRepresentativeBuilding();
        if (building == null || !building.CanEnqueueResearch(type))
            return false;

        var (ore, gas) = building.GetResearchCost(type);

        if (!resourceManager.TrySpend(ore, gas))
        {
            Debug.Log("자원부족!");
            SoundManager.Instance?.PlayInsufficientResourcesWarning();
            uIController.ShowWarning(LocalizationManager.GetText("warning.resource"));
            return false;
        }

        building.EnqueueResearch(type);
        return true;
    }

    // 대표 건물의 연구 대기열 반환 (UI 표시용)
    public IReadOnlyList<ResearchData> GetResearchQueue()
    {
        return GetRepresentativeBuilding()?.GetResearchQueue();
    }

    // 지정한 연구를 지금 큐잉할 수 있는지 (버튼 활성화 여부 판단용)
    public bool CanResearch(ResearchType type)
    {
        BuildingController building = GetRepresentativeBuilding();
        return building != null && building.CanEnqueueResearch(type);
    }

    // 대기열 취소 (취소된 연구 비용만큼 환불, 대표 건물 기준)
    public void CancelResearch(int index)
    {
        BuildingController building = GetRepresentativeBuilding();
        if (building == null)
            return;

        int canceledType = building.CancelResearch(index);
        if (canceledType < 0)
            return;

        RefundResearch(building, (ResearchType)canceledType);
    }

    private void RefundResearch(BuildingController building, ResearchType type)
    {
        var (ore, gas) = building.GetResearchCost(type);
        resourceManager.AddOre(ore);
        resourceManager.AddGas(gas);
    }

    // 건물 파괴 시 대기열에 남아있던 연구들 환불 (RefundProductionQueue와 동일한 패턴)
    public void RefundResearchQueue(BuildingController building, List<ResearchData> queue)
    {
        if (queue == null)
            return;

        foreach (ResearchData item in queue)
            RefundResearch(building, item.Type);
    }

    // buildingID에 해당하는 건물이 최소 1개 완공되어 있는지 (건설 중인 BaseStructure는 포함 안 됨)
    public bool HasCompletedBuilding(int buildingID) =>
        BuildingList.Exists(b => b != null && b.GetBuildingID() == buildingID);

    // buildingID 건설에 필요한 선행 건물 조건을 만족하는지 (선행 건물이 없으면 항상 true)
    public bool IsBuildingPrerequisiteMet(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null || data.requiredBuildingID == 0)
            return true;

        return HasCompletedBuilding(data.requiredBuildingID);
    }

    // unitID 생산에 필요한 선행 건물 조건을 만족하는지 (선행 건물이 없으면 항상 true)
    public bool IsUnitPrerequisiteMet(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null || data.requiredBuildingID == 0)
            return true;

        return HasCompletedBuilding(data.requiredBuildingID);
    }

    /// 건물 건설 요청 (PlacementSystem이 실제로 배치를 확정하기 직전에 호출)
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        if (!IsBuildingPrerequisiteMet(buildingID))
            return false; // 버튼 자체가 비활성화되므로(doc/0189) 일반 플레이에서는 도달하지 않는 방어용 분기

        if (!resourceManager.TrySpend(data.mineral, data.gas))
        {
            // 자원부족은 "건설 실패"(장애물 등)와 다른 원인이므로, 일꾼의 건설 실패 음성이 아니라
            // 유닛 생산/연구와 동일한 전역 "자원부족" 나레이션을 재생한다 (doc/0272 - 예전엔 원인 구분
            // 없이 항상 일꾼의 buildFailVoice가 나가서 장애물 음성처럼 들렸음).
            SoundManager.Instance?.PlayInsufficientResourcesWarning();
            uIController.ShowWarning(LocalizationManager.GetText("warning.resource"));
            return false;
        }

        return true;
    }

    #endregion

    #region 버튼 툴팁 데이터 구성

    // 커맨드 버튼 설명 템플릿의 {0}에 채워넣는 단축키 표시 - "<color=yellow>Y</color>" 형태
    // (doc/0481, 예전엔 각 설명 문자열에 이 태그를 직접 손으로 박아뒀었음).
    private static string ShortcutTag(KeyCode key) => $"<color=yellow>{key}</color>";

    // 유닛 생산 버튼용 ButtonAction 생성 (제목=유닛명, 비용=광물/가스/인구수)
    private ButtonAction UnitButtonAction(Action callback, int unitID, KeyCode shortcut = KeyCode.None)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string unitName = LocalizationManager.GetTextOrFallback($"unit.nta.{unitID}.name", data.unitName);

        string description = string.IsNullOrEmpty(data.description)
            ? LocalizationManager.GetText("unit.trainfallback", unitName)
            : LocalizationManager.GetTextOrFallback($"unit.nta.{unitID}.desc", data.description);

        if (data.requiredBuildingID != 0 && !HasCompletedBuilding(data.requiredBuildingID))
        {
            string requiredName = GetBuildingName(data.requiredBuildingID);
            description += LocalizationManager.GetText("cmd.requiresbuilding", requiredName);
        }

        return ButtonAction.WithCost(callback, unitName, description, data.mineral, data.gas, data.population, shortcut);
    }

    // 연구 버튼용 ButtonAction 생성 (제목="Attack/Armor Upgrade Lv.N", 비용=다음 레벨 광물/가스, 최대 레벨이면 "MAX" 표시)
    private ButtonAction ResearchButtonAction(ResearchType type)
    {
        BuildingController building = GetRepresentativeBuilding();
        int currentLevel = building != null ? building.GetResearchLevel(type) : 0;
        var (ore, gas) = building != null ? building.GetResearchCost(type) : (0, 0);

        string baseTitle = LocalizationManager.GetText(type == ResearchType.Attack ? "research.attack.name" : "research.armor.name");
        bool maxed = currentLevel >= ResearchQueue.MaxLevel;

        string title = maxed
            ? LocalizationManager.GetText("research.title.maxed", baseTitle)
            : LocalizationManager.GetText("research.title.level", baseTitle, currentLevel + 1);
        string description = maxed
            ? LocalizationManager.GetText("research.desc.maxed", baseTitle)
            : LocalizationManager.GetText(type == ResearchType.Attack ? "research.desc.attack" : "research.desc.armor", currentLevel, currentLevel + 1);

        return ButtonAction.WithCost(() => TryResearch(type), title, description, ore, gas, 0);
    }

    // 건물 건설 버튼용 ButtonAction 생성 (제목=건물명, 비용=광물/가스/인구수)
    private ButtonAction BuildingButtonAction(Action callback, int buildingID, KeyCode shortcut = KeyCode.None)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return ButtonAction.Simple(callback, string.Empty, string.Empty);

        string buildingName = LocalizationManager.GetTextOrFallback($"building.nta.{buildingID}.name", data.Name);

        string description = string.IsNullOrEmpty(data.description)
            ? LocalizationManager.GetText("building.constructfallback", buildingName)
            : LocalizationManager.GetTextOrFallback($"building.nta.{buildingID}.desc", data.description);

        if (data.requiredBuildingID != 0 && !HasCompletedBuilding(data.requiredBuildingID))
        {
            string requiredName = GetBuildingName(data.requiredBuildingID);
            description += LocalizationManager.GetText("cmd.requiresbuilding", requiredName);
        }

        return ButtonAction.WithCost(callback, buildingName, description, data.mineral, data.gas, data.population, shortcut);
    }

    // Info_panel에 표시할 유닛/건물 이름 조회 (UnitController.unitID / BuildingController.buildingID 기준)
    public string GetUnitName(int unitID)
    {
        UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
        return data != null ? LocalizationManager.GetTextOrFallback($"unit.nta.{unitID}.name", data.unitName.Trim()) : string.Empty;
    }

    public string GetBuildingName(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        return data != null ? LocalizationManager.GetTextOrFallback($"building.nta.{buildingID}.name", data.Name.Trim()) : string.Empty;
    }

    // Info_panel 설명 조회 - 대기열(생산/연구) 패널이 같이 뜨는 건물은 SO의 infoDescription을 비워뒀으므로
    // BuildingController.GetDescription()이 그대로 빈 문자열을 반환한다. 보급고만 예외로, 정적 텍스트 대신
    // 실시간 인구수 정보를 매 프레임 새로 계산해서 보여준다 (doc/0479).
    private string GetBuildingInfoDescription(BuildingController building)
    {
        if (building.GetBuildingID() != BuildingID.SupplyDepot)
            return building.GetDescription();

        BuildingData data = GetBuildingData(BuildingID.SupplyDepot);
        int populationAdded = data != null ? data.maxpopulationamount : 0;
        return $"Current Population : {GetPopulation()}/{GetMaxPopulation()}\nPopulation Capacity Added : +{populationAdded}";
    }

    #endregion

    #region 고급유닛 특성(트레이트) 스킬 시스템 (doc/0228)

    public TraitChoice GetChosenTrait(int unitID) =>
        chosenTraits.TryGetValue(unitID, out TraitChoice choice) ? choice : TraitChoice.None;

    // 특성 선택 오버레이(SkillSelect)에서 유저가 하나를 고르면 호출된다.
    // 1) 이 유닛 종류의 선택을 저장하고, 2) 지금 살아있는 같은 종류 유닛 전체에 즉시 반영한다.
    // 이후 같은 unitID로 새로 생산되는 유닛도 UnitController.Start()에서 자동으로 같은 선택을 물려받는다.
    public void ChooseTrait(int unitID, TraitChoice choice)
    {
        chosenTraits[unitID] = choice;

        foreach (UnitController unit in UnitList)
        {
            if (unit != null && unit.GetUnitID() == unitID)
                unit.ApplyTrait(choice);
        }
    }

    // order panel 스킬 버튼(슬롯 6) 클릭/단축키로 호출된다. 선택된 유닛 중 이 스킬의 대상 unitID와
    // 일치하는 유닛들만 각자 자기 쿨다운에 따라 개별적으로 스킬을 사용한다(여러 마리를 함께 선택했어도
    // 한 마리씩 자기 쿨다운을 갖고 독립적으로 발동).
    public void ActivateSkill(int unitID, UnitTraitOption trait)
    {
        if (trait.targetType != SkillTargetType.None)
        {
            // 단일/범위 지정형: 쿨다운을 아직 시작하지 않는다 - "사거리까지 이동해서 실제로 스킬을 쓴 시점"부터
            // 시작해야 하므로(doc/0323 확정 사항), 여기서는 지정 모드 진입만 하고 클릭 확정은
            // ConfirmSkillUnitTarget/ConfirmSkillAreaTarget으로 넘긴다.
            pendingSkillUnitID = unitID;
            pendingSkillTrait = trait;
            userControl.SetOrderState(trait.targetType == SkillTargetType.SingleUnit ? "SkillUnit" : "SkillGround");
            return;
        }

        // 자기자신 논타겟 스킬(은신/쉴드 전개 등)은 이동할 필요가 없어 즉시 발동 + 즉시 쿨다운 시작
        foreach (UnitController unit in selectedUnitList)
        {
            if (unit == null || unit.GetUnitID() != unitID)
                continue;

            if (!unit.CanUseSkill())
                continue;

            unit.UseTraitSkill(trait, SkillActivationContext.Self);
            unit.StartSkillCooldown(trait.cooldown);
        }
    }

    // UserControl이 SkillUnit 모드에서 적 클릭을 확정하면 호출 (doc/0323). 여러 마리를 함께 선택했어도
    // 한 마리씩 자기 쿨다운에 따라 독립적으로 이동-후-발동한다(doc/0228과 동일한 원칙).
    public void ConfirmSkillUnitTarget(GameObject target)
    {
        foreach (UnitController unit in selectedUnitList)
        {
            if (unit == null || unit.GetUnitID() != pendingSkillUnitID || !unit.CanUseSkill())
                continue;

            unit.MoveToUseSkillOnUnit(target, pendingSkillTrait);
        }
    }

    // UserControl이 SkillGround 모드에서 땅 클릭을 확정하면 호출.
    public void ConfirmSkillAreaTarget(Vector3 point)
    {
        foreach (UnitController unit in selectedUnitList)
        {
            if (unit == null || unit.GetUnitID() != pendingSkillUnitID || !unit.CanUseSkill())
                continue;

            unit.MoveToUseSkillOnArea(point, pendingSkillTrait);
        }
    }

    // 범위 지정 모드 중 마우스를 따라다니는 범위 마커(사용자 제작 예정)가 반지름을 읽어가는 용도.
    public float GetPendingSkillAreaRadius() => pendingSkillTrait != null ? pendingSkillTrait.areaRadius : 0f;

    // 유닛 선택 UI가 갱신될 때마다(UpdateUI) 함께 호출되어, 선택된 유닛 종류에 따라 아래 셋 중 하나로 정리한다.
    // - 고급유닛이 아니면: 오버레이/스킬슬롯 둘 다 숨김
    // - 아직 특성을 안 골랐으면: SkillSelect 오버레이 표시 (order panel의 기존 명령은 그대로 사용 가능 - 비모달)
    // - 이미 골랐으면: 오버레이는 숨기고, 액티브 스킬이면 order panel 슬롯 6에 스킬 버튼 표시(패시브면 버튼 없음)
    // 스킬 슬롯(6 또는 7)에 실제로 뭔가 표시된 적이 있는지, 있었다면 어느 슬롯(폴백 여부)이었는지 기억해둔다.
    // UpdateUnitSkillUI()는 선택 종류와 무관하게 매 프레임 호출되는데, 예전엔 "지금 선택된 유닛이 없으면
    // ClearUnitSkillSlot(useFallbackSlot)"을 매번 실행했다 - useFallbackSlot은 "마지막으로 선택했던
    // 유닛이 Worker였는지"라는 낡은 값(UnitSelectState)이라, 건물 등 유닛이 아닌 걸 선택 중일 때도 매
    // 프레임 그 낡은 값 기준으로 아무 슬롯이나 계속 Clear()해버렸다. 마침 그 슬롯을 다른 용도(건물 랠리
    // 버튼 등)로 겸용하고 있으면, 그 다른 용도의 SetData()와 매 프레임 Clear()가 서로 싸우면서 반투명하게
    // 보이거나 눌림 색상 전환이 씹히는 버그가 생겼다(doc/0363). 이제는 스킬 슬롯이 "실제로 표시됐던 경우"
    // 딱 그 슬롯만, 표시가 끝나는 시점에 한 번만 정리한다 - 스킬을 보여준 적 없는 컨텍스트는 아예 건드리지 않는다.
    private bool skillSlotShown;
    private bool skillSlotUsedFallback;

    private void ClearSkillSlotIfShown()
    {
        if (!skillSlotShown)
            return;

        uIController.ClearUnitSkillSlot(skillSlotUsedFallback);
        skillSlotShown = false;
    }

    // 유닛이 선택된 상태(건물/선택없음 아님)에서 슬롯 6을 비운다. 일꾼(useFallbackSlot=true)은
    // ShowWorkerPanel이 슬롯 6을 매 프레임 직접 채우므로 예외로 두고 건드리지 않는다 - 그 외(일꾼이 아닌
    // 유닛 선택 중)엔 슬롯 6이 스킬 전용 컨텍스트라, 직전까지 뭐가 있었든(예: 일꾼 Build/취소 버튼 잔상,
    // doc/0368) skillSlotShown 여부와 무관하게 무조건 비운다.
    private void ClearUnitContextSkillSlot(bool useFallbackSlot)
    {
        if (useFallbackSlot)
            return;

        uIController.ClearUnitSkillSlot(false);
        skillSlotShown = false;
    }

    private void UpdateUnitSkillUI()
    {
        // Worker는 슬롯 6을 Build 버튼이 이미 쓰고 있으므로(doc/0251), 스킬은 그 다음 슬롯(7)으로 넘긴다.
        bool useFallbackSlot = UnitSelectState == UnitState.Worker;

        if (selectedUnitList.Count == 0)
        {
            // 건물 선택/선택 없음 - 슬롯 6은 랠리 버튼이 쓸 수도 있으므로(doc/0363) 스킬이 실제로
            // 그렸을 때만 조심스럽게 지운다.
            uIController.HideSkillSelectPanel();
            ClearSkillSlotIfShown();
            return;
        }

        UnitController representative = selectedUnitList[0];
        UnitData data = GetUnitData(representative.GetUnitID());

        if (data == null || !data.hasTraitChoice)
        {
            uIController.HideSkillSelectPanel();
            ClearUnitContextSkillSlot(useFallbackSlot);
            return;
        }

        TraitChoice chosen = GetChosenTrait(data.ID);

        if (chosen == TraitChoice.None)
        {
            ClearUnitContextSkillSlot(useFallbackSlot);
            uIController.ShowSkillSelectPanel(
                new CommandButtonData(data.traitA.icon, ButtonAction.Simple(
                    () => ChooseTrait(data.ID, TraitChoice.A), data.traitA.skillName, data.traitA.description)),
                new CommandButtonData(data.traitB.icon, ButtonAction.Simple(
                    () => ChooseTrait(data.ID, TraitChoice.B), data.traitB.skillName, data.traitB.description)));
            return;
        }

        uIController.HideSkillSelectPanel();

        // 액티브/패시브 상관없이 슬롯 6엔 항상 선택된 트레이트를 넣는다(호버 시 설명을 볼 수 있어야 하므로).
        // 액티브면 클릭/단축키로 실제 사용 가능하고, 패시브면 버튼만 비활성화(클릭 불가)로 보여준다.
        UnitTraitOption trait = chosen == TraitChoice.A ? data.traitA : data.traitB;

        float skillCooldownRemaining = representative.GetSkillCooldownRemaining();
        bool onCooldown = trait.isActiveSkill && skillCooldownRemaining > 0f;

        string description = trait.isActiveSkill
            ? $"{trait.description} " + LocalizationManager.GetText("trait.shortcutsuffix", ShortcutTag(trait.shortcutKey))
                + (onCooldown ? LocalizationManager.GetText("trait.cooldownsuffix", skillCooldownRemaining) : "")
            : trait.description;

        // 진단용(doc/0368): 스킬이 이 슬롯을 처음 차지하는 순간인데 이미 다른 버튼이 남아있으면
        // 정상 흐름이 아니다(위 ClearUnitContextSkillSlot이 미리 비웠어야 함) - 그래도 스킬 표시는
        // 항상 보장하기 위해 덮어쓰기는 그대로 진행하고 로그만 남긴다.
        if (!skillSlotShown && uIController.IsSkillSlotOccupied(useFallbackSlot))
        {
            Debug.LogWarning($"[RTSUnitController] Unit skill slot(fallback={useFallbackSlot}) already had a button " +
                "before the skill claimed it - check other slot 6/7 writers (Worker Build / Rally / Cancel).");
        }

        uIController.ShowUnitSkillSlot(new CommandButtonData(
            trait.icon,
            ButtonAction.Simple(
                () => ActivateSkill(data.ID, trait),
                trait.skillName,
                description,
                trait.isActiveSkill ? trait.shortcutKey : KeyCode.None),
            trait.isActiveSkill && !onCooldown), // Interactable = 액티브이면서 쿨다운이 끝났을 때만 true
            useFallbackSlot);
        skillSlotShown = true;
        skillSlotUsedFallback = useFallbackSlot;

        // 쿨다운 원형 오버레이 (doc/0323 후속) - 매 프레임 대표 유닛의 남은 쿨다운 비율로 갱신된다.
        float normalizedCooldown = trait.cooldown > 0f ? Mathf.Clamp01(skillCooldownRemaining / trait.cooldown) : 0f;
        uIController.SetUnitSkillCooldown(normalizedCooldown, useFallbackSlot);
    }

    #endregion

    #region UI관련

    private void UpdateUI()
    {
        // 건물과 유닛이 함께 선택된 경우 유닛 명령 패널을 우선한다 (Shift+클릭으로 유닛/건물을 섞어
        // 선택해도 selectedUnitList가 하나라도 있으면 항상 유닛 패널이 보임).
        SelectState uiState = RTScurrentSate;

        if ((uiState == SelectState.UnitSelect || uiState == SelectState.BuildingSelect)
            && selectedUnitList.Count > 0)
        {
            uiState = SelectState.UnitSelect;
        }

        // 고급유닛 특성 선택 오버레이 / order panel 스킬 버튼(슬롯 6) 갱신 (doc/0228).
        // switch보다 먼저, 매 프레임 무조건 호출해서 유닛 선택이 풀리고 건물/적 선택 등으로 넘어갈 때도
        // 오버레이가 화면에 남아있지 않게 한다(내부에서 selectedUnitList.Count == 0이면 알아서 숨김).
        UpdateUnitSkillUI();

        //UIController에서 현재 상황에 맞게 UI창 상태 변경
        switch (uiState)
        {
            case SelectState.UnitSelect:
                switch (UnitSelectState)
                {
                    case UnitState.Worker:
                        uIController.ShowWorkerPanel(
                            ButtonAction.Simple(EnterMoveMode, LocalizationManager.GetText("cmd.move.title"), LocalizationManager.GetText("cmd.move.desc", ShortcutTag(KeyCode.M)), KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, LocalizationManager.GetText("cmd.attack.title"), LocalizationManager.GetText("cmd.attack.desc", ShortcutTag(KeyCode.A)), KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, LocalizationManager.GetText("cmd.stop.title"), LocalizationManager.GetText("cmd.stop.desc", ShortcutTag(KeyCode.S)), KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, LocalizationManager.GetText("cmd.patrol.title"), LocalizationManager.GetText("cmd.patrol.desc", ShortcutTag(KeyCode.P)), KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, LocalizationManager.GetText("cmd.hold.title"), LocalizationManager.GetText("cmd.hold.desc", ShortcutTag(KeyCode.H)), KeyCode.H),
                            ButtonAction.Simple(EnterReturnMode, LocalizationManager.GetText("cmd.returncargo.title"), LocalizationManager.GetText("cmd.returncargo.desc", ShortcutTag(KeyCode.R)), KeyCode.R),
                            ButtonAction.Simple(BuildModeOn, LocalizationManager.GetText("cmd.build.title"), LocalizationManager.GetText("cmd.build.desc", ShortcutTag(KeyCode.B)), KeyCode.B));
                        break;

                    case UnitState.AttackUnit:
                        uIController.ShowAttackUnitPanel(
                            ButtonAction.Simple(EnterMoveMode, LocalizationManager.GetText("cmd.move.title"), LocalizationManager.GetText("cmd.move.desc", ShortcutTag(KeyCode.M)), KeyCode.M),
                            ButtonAction.Simple(EnterAttackMode, LocalizationManager.GetText("cmd.attack.title"), LocalizationManager.GetText("cmd.attack.desc", ShortcutTag(KeyCode.A)), KeyCode.A),
                            ButtonAction.Simple(StopSelectedUnits, LocalizationManager.GetText("cmd.stop.title"), LocalizationManager.GetText("cmd.stop.desc", ShortcutTag(KeyCode.S)), KeyCode.S),
                            ButtonAction.Simple(EnterPatrolMode, LocalizationManager.GetText("cmd.patrol.title"), LocalizationManager.GetText("cmd.patrol.desc", ShortcutTag(KeyCode.P)), KeyCode.P),
                            ButtonAction.Simple(HoldSelectedUnits, LocalizationManager.GetText("cmd.hold.title"), LocalizationManager.GetText("cmd.hold.desc", ShortcutTag(KeyCode.H)), KeyCode.H));
                        break;
                }

                uIController.HideProductionUI();

                // 유닛을 여러 마리 선택했으면 Squad_panel만, 한 마리면 Info_panel만 보여준다.
                if (selectedUnitList.Count > 1)
                {
                    uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit, RemoveUnitFromSelection, KeepOnlySameUnitTypeInSelection);
                }
                else if (selectedUnitList.Count == 1)
                {
                    UnitController unit = selectedUnitList[0];
                    // 영웅 유닛(heroName이 채워진 unitID=0 유닛)은 이름을 데이터베이스 대신 자기 자신에게서 가져온다 (doc/0304).
                    string displayName = string.IsNullOrEmpty(unit.GetHeroName()) ? GetUnitName(unit.GetUnitID()) : unit.GetHeroName();
                    uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetAttackDamage(), unit.GetArmor(),
                        unit.GetAttackType(), unit.GetArmorType(), unit.GetSizeType(), unit.GetShotCount(), unit.GetDescription(),
                        unit.GetCanAttackGround(), unit.GetCanAttackAir());
                }
                else
                {
                    uIController.HideInfoPanel();
                }
                break;

            case SelectState.BuildingSelect:

                BuildingController representativeBuilding = GetRepresentativeBuilding();

                // 건물을 여러 개 선택했으면 Squad_panel(아이콘 그리드), 한 개면 Info_panel을 보여준다 (유닛과 동일한 패턴).
                if (selectedBuildingList.Count > 1)
                {
                    uIController.ShowBuildingSquadPanel(selectedBuildingList, ClickSelectBuilding, RemoveBuildingFromSelection, KeepOnlySameBuildingTypeInSelection);
                }
                else if (selectedBuildingList.Count == 1)
                {
                    BuildingController building = selectedBuildingList[0];
                    uIController.ShowInfoPanel(building.GetIcon(), GetBuildingName(building.GetBuildingID()), building.GetHealthManager(), GetBuildingInfoDescription(building));
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                // 공중에 뜬 건물은 생산/연구 등 모든 커맨드를 막고 아래에서 Land/Move 버튼만 노출한다.
                // (여러 건물이 섞여 선택돼도 항상 대표 건물 하나를 기준으로 판단해 패널을 통일한다)
                bool selectedBuildingLifted = representativeBuilding != null && representativeBuilding.IsLifted();

                if (selectedBuildingLifted)
                {
                    // ClearPanel()이 아니라 리프트/이동 슬롯을 보호하는 전용 메서드를 쓴다 - 매 프레임 호출되므로
                    // ClearPanel()로 그 두 슬롯까지 매번 껐다 켰다 하면 실행 중이던 클릭 코루틴/단축키가 끊긴다.
                    uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: true);
                    uIController.HideProductionUI();
                }
                else
                {
                    // 우선순위(MainBase > Tier1 > Tier2 > Tier3 > SupplyDepot > Lab)에 따라 뽑힌 대표 건물의
                    // 태그로 어느 생산 패널을 보여줄지 정한다. 예: MainBase + Tier1(병영) 선택 시 MainBase가
                    // 우선이므로 일꾼 생산 버튼이 보인다.
                    switch (representativeBuilding != null ? TagToBuildingState(representativeBuilding.tag) : BuildingState.None)
                    {
                        case BuildingState.MainBaseSelect:
                            ShowUnitTierPanel(0);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            uIController.ShowBuildingRallyCommand(RallyButtonAction());
                            break;

                        case BuildingState.Tier1Select:
                            ShowUnitTierPanel(1);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            uIController.ShowBuildingRallyCommand(RallyButtonAction());
                            break;

                        case BuildingState.Tier2Select:
                            ShowUnitTierPanel(2);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            uIController.ShowBuildingRallyCommand(RallyButtonAction());
                            break;

                        case BuildingState.Tier3Select:
                            ShowUnitTierPanel(3);
                            uIController.ShowProductionUI(
                                GetProductionQueue(),
                                CancelProduction);
                            uIController.ShowBuildingRallyCommand(RallyButtonAction());
                            break;

                        case BuildingState.Lab:
                            uIController.ShowLabPanel(
                                ResearchButtonAction(ResearchType.Attack), CanResearch(ResearchType.Attack),
                                ResearchButtonAction(ResearchType.Armor), CanResearch(ResearchType.Armor));

                            uIController.ShowResearchUI(GetResearchQueue(), CancelResearch);
                            break;

                        case BuildingState.SupplyDepot:
                        case BuildingState.None:
                            uIController.ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false);
                            uIController.HideProductionUI();
                            uIController.HideResearchUI();
                            break;
                    }
                }

                // 리프트 가능한 건물이면(전용 패널이 없는 SupplyDepot/Lab 포함) 고정 슬롯에 리프트/착륙 버튼을 덧붙인다.
                if (representativeBuilding != null && representativeBuilding.CanLift())
                {
                    uIController.ShowBuildingLiftCommand(
                        representativeBuilding.IsLifted(),
                        representativeBuilding.IsLifted()
                            ? ButtonAction.Simple(BeginLandingSelectedBuilding, LocalizationManager.GetText("cmd.land.title"), LocalizationManager.GetText("cmd.land.desc", ShortcutTag(KeyCode.L)), KeyCode.L)
                            : ButtonAction.Simple(LiftSelectedBuilding, LocalizationManager.GetText("cmd.liftoff.title"), LocalizationManager.GetText("cmd.liftoff.desc", ShortcutTag(KeyCode.L)), KeyCode.L));

                    // 공중에 뜬 상태에서만 고정 슬롯(0번)에 "이동" 버튼을 추가로 노출한다.
                    if (representativeBuilding.IsLifted())
                    {
                        uIController.ShowBuildingMoveCommand(
                            ButtonAction.Simple(EnterBuildingMoveMode, LocalizationManager.GetText("cmd.move.title"), LocalizationManager.GetText("cmd.moveairborne.desc", ShortcutTag(KeyCode.M)), KeyCode.M));
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
                    EnemyUnitController enemy = selectedEnemyList[0];
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetHealthManager(), enemy.GetAttackDamage(), enemy.GetArmor(),
                        enemy.GetAttackType(), enemy.GetArmorType(), enemy.GetSizeType(), enemy.GetShotCount(), enemy.GetDescription(),
                        enemy.GetCanAttackGround(), enemy.GetCanAttackAir());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.AllySelect: // 아군 OC 유닛 Info Panel (doc/0452, EnemySelect와 동일한 패턴)

                if (selectedAllyList.Count > 0)
                {
                    AllyController ally = selectedAllyList[0];
                    uIController.ShowInfoPanel(ally.GetIcon(), ally.GetAllyName(), ally.GetHealthManager(), ally.GetAttackDamage(), ally.GetArmor(),
                        ally.GetAttackType(), ally.GetArmorType(), ally.GetSizeType(), ally.GetShotCount(), ally.GetDescription(),
                        ally.GetCanAttackGround(), ally.GetCanAttackAir());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.EnemyBuildingSelect:

                if (selectedEnemyBuilding != null)
                {
                    // 건물은 공격을 하지 않으므로 공격력/방어력 없이 아이콘/이름/체력만 표시 (3-인자 오버로드,
                    // 아군 건물 Info_panel과 동일한 패턴).
                    uIController.ShowInfoPanel(selectedEnemyBuilding.GetIcon(), selectedEnemyBuilding.GetBuildingName(), selectedEnemyBuilding.GetHealthManager(), selectedEnemyBuilding.GetDescription());
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
                        selectedResourceNode.GetName(),
                        selectedResourceNode.RemainingAmount,
                        selectedResourceNode.GetDescription());
                }
                else
                {
                    uIController.HideInfoPanel();
                }

                uIController.ClearPanel();
                uIController.HideProductionUI();
                uIController.HideSquadPanel();
                break;

            case SelectState.MissionItemSelect: // 유물/데이터베이스 등 - 체력/전투 스탯 없이 이름+이미지만 (doc/0455)

                if (selectedMissionItem != null)
                {
                    uIController.ShowInfoPanel(selectedMissionItem.GetIcon(), selectedMissionItem.GetItemName(), null,
                        selectedMissionItem.GetDescription());
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
                        selectedBaseStructure.GetHealthManager());

                    uIController.ShowBaseStructureCommandPanel(
                        ButtonAction.Simple(
                            CancelSelectedBaseStructure,
                            LocalizationManager.GetText("cmd.cancel.title"),
                            LocalizationManager.GetText("cmd.cancelconstruction.desc", ShortcutTag(KeyCode.T)),
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
                    IsBuildingPrerequisiteMet(BuildingID.Factory),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Airport), BuildingID.Airport, KeyCode.P),
                    IsBuildingPrerequisiteMet(BuildingID.Airport),
                    BuildingButtonAction(() => PlacementSystem.StartPlacement(BuildingID.Lab), BuildingID.Lab, KeyCode.L),
                    ButtonAction.Simple(
                        CancelBuildMode,
                        LocalizationManager.GetText("cmd.cancel.title"),
                        LocalizationManager.GetText("cmd.cancelbuildmode.desc", ShortcutTag(KeyCode.T)),
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

    #region 업그레이드(연구) 전역 보너스 창구
    // 실제 저장은 UpgradeManager가 담당하고, RTSUnitController는 그 창구 역할만 한다.
    // ResearchQueue(연구소 큐)도 UnitController(유닛)도 UpgradeManager를 직접 참조하지 않고 이 메서드들만 사용한다.
    public int GlobalAttackBonus => upgradeManager.GetBonus(ResearchType.Attack);
    public int GlobalArmorBonus => upgradeManager.GetBonus(ResearchType.Armor);
    public DamageMultiplierTableSO DamageMultiplierTable => damageMultiplierTable;

    // unitID로 UnitData를 조회한다 (UnitController가 자기 자신의 스탯을 SO에서 가져올 때 사용).
    public UnitData GetUnitData(int unitID) => unitDatabase.unitData.Find(d => d.ID == unitID);

    // buildingID로 BuildingDataSO에서 BuildingData를 조회한다 (BuildingController가 씬에 미리 배치된
    // 자신의 Size를 SO에서 가져와 그리드에 등록할 때 사용, doc/0247).
    public BuildingData GetBuildingData(int buildingID) => buildingDatabase.buildingData.Find(d => d.ID == buildingID);

    // enemyUnitID로 OC Unit Data SO(EnemyUnitDataSO)에서 UnitData를 조회한다 (EnemyUnitController가
    // 자기 자신의 스탯을 SO에서 가져올 때 사용, doc/0232). OC 쪽에 없으면 스포어 브루드 쪽에서 조회한다(doc/0444).
    public UnitData GetEnemyUnitData(int enemyUnitID) =>
        enemyUnitDatabase?.unitData.Find(d => d.ID == enemyUnitID) ??
        sporeBroodUnitDatabase?.unitData.Find(d => d.ID == enemyUnitID);

    // enemyBuildingID로 OC Building Data SO(EnemyBuildingDataSO)에서 BuildingData를 조회한다
    // (EnemyBuildingController가 자기 자신의 이름/체력을 SO에서 가져올 때 사용, doc/0245). OC 쪽에 없으면
    // 스포어 브루드 쪽에서 조회한다(doc/0444).
    public BuildingData GetEnemyBuildingData(int enemyBuildingID) =>
        enemyBuildingDatabase?.buildingData.Find(d => d.ID == enemyBuildingID) ??
        sporeBroodBuildingDatabase?.buildingData.Find(d => d.ID == enemyBuildingID);

    // ResearchQueue가 연구 완료 시 호출 (UpgradeManager를 직접 모른 채로 보너스를 반영)
    public void AddGlobalBonus(ResearchType type, int amount)
    {
        upgradeManager.AddBonus(type, amount);
        SoundManager.Instance?.PlayUpgradeCompleteVoice();
    }

    #endregion

    #region 선택 상태 확인

    // 상태 확인용
    public bool IsUnitSelect() => RTScurrentSate == SelectState.UnitSelect;
    public bool IsBuildingSelect() => RTScurrentSate == SelectState.BuildingSelect;
    public bool IsBuildMode() => RTScurrentSate == SelectState.BuildMode;

    #endregion
}
