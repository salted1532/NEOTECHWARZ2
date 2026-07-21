using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using FischlWorks_FogWar;


// 플레이어의 마우스/키보드 입력을 해석하는 컨트롤러.
// 좌클릭 선택(드래그 박스 포함), 우클릭/키보드 명령(이동·공격·순찰·정지·홀드·랠리) 발행,
// 명령 대기 상태(OrderState)에 따른 커서 포인터 표시를 담당하며 실제 명령 실행은 RTSUnitController에 위임한다.
public class UserControl : MonoBehaviour
{
    [SerializeField]
    private LayerMask layerUnit;
    [SerializeField]
    private LayerMask layerGround;
    [SerializeField]
    private LayerMask layerEnemy;
    [SerializeField]
    private LayerMask layerBuilding;
    [SerializeField]
    private LayerMask layerOre;
    [SerializeField]
    private LayerMask layerGas;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private CameraControl mainCameraControl; // 더블클릭 시 카메라 이동에 사용 (MinimapController와 동일한 참조 방식)

    [SerializeField]
    private RectTransform dragRectangle;


    private GameObject pointer;

    private GameObject attackPointer;
    private GameObject movePointer;

    [SerializeField]
    private GameObject pointerPrefab;
    [SerializeField]
    private GameObject attackPointerPrefab;

    [Header("Mouse Cursor")]
    [SerializeField]
    private Texture2D cursorDefaultTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField]
    private Texture2D cursorSelectEnemyTexture;
    [SerializeField]
    private Texture2D cursorSelectAllyTexture;
    [SerializeField]
    private Texture2D cursorSelectNeutralTexture;
    [SerializeField]
    private Texture2D cursorCommandEnemyTexture;
    [SerializeField]
    private Texture2D cursorCommandAllyTexture;
    [SerializeField]
    private Texture2D cursorCommandNeutralTexture;
    [SerializeField]
    private Vector2 cursorHotspot = Vector2.zero;

    // 마우스 아래에 있는 대상의 진영 (선택/명령 커서의 색을 고른다)
    private enum CursorTarget { None, Enemy, Ally, Neutral }

    private Texture2D currentCursorTexture; // 직전 프레임에 적용한 텍스처 (같으면 SetCursor 재호출 생략)

    private RTSUnitController rtsUnitController;
    private csFogWar fogWar;

    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;

    // (Shift 없이) 클릭으로 확정하려던 단일 선택 동작. 실제 선택은 즉시 하지 않고 마우스를 놓을 때 실행한다.
    // 마우스 업 시 드래그 범위 안에 유닛이 하나라도 걸리면 이 값은 버려지고 드래그 유닛 선택이 우선한다.
    private Action pendingLeftClickSelect;

    private enum OrderState
    {
        None,
        Attack,
        Move,
        Patrol,
        Rally,
        BuildingMove // 공중에 뜬 건물의 "이동" 버튼(M) 전용 - Move와 분리해야 HandleLeftClick에서 유닛용 MoveSelectedUnits와 섞이지 않는다
    }

    [SerializeField]
    private OrderState UsercurrentState = OrderState.None;

    // 부대 지정(컨트롤 그룹) 단축키 - 인덱스가 그대로 그룹 번호(0~9)가 되도록 키보드 위쪽 숫자(1~9,0) 순서로 배치.
    private static readonly KeyCode[] controlGroupKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
    };

    private const float ControlGroupDoubleClickThreshold = 0.3f; // 이 시간(초) 안에 같은 그룹 키를 다시 누르면 더블클릭으로 간주
    private readonly float[] lastControlGroupPressTime = new float[10];

    private const float UnitDoubleClickThreshold = 0.3f; // 이 시간(초) 안에 유닛을 다시 클릭하면 더블클릭으로 간주
    private float lastUnitClickTime = float.NegativeInfinity;

    private void Awake()
    {
        mainCamera = Camera.main;
        rtsUnitController = GetComponent<RTSUnitController>();

        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 안개에 가려진 대상 클릭 차단 기능이 비활성화됩니다.", this);

        attackPointer = Instantiate(attackPointerPrefab);
        movePointer = Instantiate(pointerPrefab);

        attackPointer.SetActive(false);
        movePointer.SetActive(false);

        start = Vector2.zero;
        end = Vector2.zero;

        for (int i = 0; i < lastControlGroupPressTime.Length; i++)
            lastControlGroupPressTime[i] = float.NegativeInfinity;

        DrawDragRectangle();
    }

    private void Update()
    {
        //마우스 입력 관리
        HandleMouse();
        //키보드 입력 관리
        HandlekeyBoard();

        // 입력 상황에 따라 포인터 생성
        UpdatePointer();

        // 입력 상황에 따라 마우스 커서 아이콘 갱신
        UpdateCursor();
    }

    // 좌클릭(드래그 시작/중/종료)과 우클릭 입력을 처리한다.
    private void HandleMouse()
    {
        // 좌클릭 시
        // 드래그 시작
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();
            pendingLeftClickSelect = null;

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            HandleLeftClick();
        }

        // 드래그 중
        if (Input.GetMouseButton(0))
        {
            end = Input.mousePosition;
            DrawDragRectangle();
        }

        // 드래그 종료
        if (Input.GetMouseButtonUp(0))
        {
            CalculateDragRect();
            SelectObject();

            start = Vector2.zero;
            end = Vector2.zero;

            DrawDragRectangle();
        }


        // 우클릭 시
        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClick();
        }
    }

    /// <summary>
    /// 좌클릭 관리
    /// </summary>
    private void HandleLeftClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit unitHit;
        RaycastHit groundHit;
        RaycastHit enemyHit;
        RaycastHit BuildingHit;
        RaycastHit OreHit;
        RaycastHit GasHit;

        bool clickedUnit = Physics.Raycast(ray, out unitHit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out groundHit, Mathf.Infinity, layerGround);
        bool clickedEnemy = Physics.Raycast(ray, out enemyHit, Mathf.Infinity, layerEnemy);
        bool clickedBuilding = Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding);
        bool clickedOre = Physics.Raycast(ray, out OreHit, Mathf.Infinity, layerOre);
        bool clickedGas = Physics.Raycast(ray, out GasHit, Mathf.Infinity, layerGas);

        // 1. 유닛 클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 해당 아군을 강제로 공격, 아니면 선택)
        if (clickedUnit)
        {
            UnitController unit = unitHit.transform.GetComponent<UnitController>();

            if (unit != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackFriendlySelectedUnits(unit);
                    unit.FlashMarker(); // 어느 아군이 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = unit.transform.position;
                    attackPointer.SetActive(true);

                    UsercurrentState = OrderState.None;

                    return;
                }

                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                {
                    bool isDoubleClick = Time.time - lastUnitClickTime <= UnitDoubleClickThreshold;
                    lastUnitClickTime = Time.time;

                    if (isDoubleClick)
                        pendingLeftClickSelect = () => { if (unit != null) SelectAllVisibleUnitsOfSameType(unit); };
                    else
                        pendingLeftClickSelect = () => { if (unit != null) rtsUnitController.ClickSelectUnit(unit); };
                }

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 2. 적 클릭 = 선택 또는 공격 명령 (A 모드 중이면 해당 적을 추격 공격, 아니면 선택)
        // 땅 클릭보다 먼저 처리해야 한다: 적은 지면 위에도 서 있어 clickedGround도 함께 true가 되기 때문에,
        // 더 구체적인 대상(적)이 우선권을 가져야 "A 모드에서 적을 직접 지정"이 가능하다.
        if (clickedEnemy)
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null && IsRevealedByFog(enemyHit.point))
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackSelectedUnits(enemy);
                    enemy.FlashMarker(); // 어느 적이 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = enemy.transform.position;
                    attackPointer.SetActive(true);

                    UsercurrentState = OrderState.None;

                    return;
                }

                pendingLeftClickSelect = () => { if (enemy != null) rtsUnitController.ClickSelectEnemy(enemy); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 3. 건물 클릭 = 선택 또는 아군 건물 강제 공격 (A 모드 중이면 해당 건물을 강제로 공격, 아니면 선택)
        // 건물도 지면 위에 서 있어 clickedGround가 함께 true가 되므로, 땅 클릭보다 먼저 처리해야
        // "A 모드에서 건물을 직접 지정"이 땅 공격-이동으로 새지 않는다 (적/아군 유닛과 동일한 이유).
        if (clickedBuilding)
        {
            BuildingController building = BuildingHit.transform.GetComponent<BuildingController>();

            if (building != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackFriendlyBuildingSelectedUnits(building);
                    building.FlashMarker(); // 어느 건물이 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = building.transform.position;
                    attackPointer.SetActive(true);

                    UsercurrentState = OrderState.None;

                    return;
                }

                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectBuilding(building);
                else
                    pendingLeftClickSelect = () => { if (building != null) rtsUnitController.ClickSelectBuilding(building); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }

            // 건설 중인 BaseStructure 좌클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 강제로 공격, 아니면 선택)
            BaseStructure baseStructure = BuildingHit.transform.GetComponent<BaseStructure>();
            if (baseStructure != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackFriendlyStructureSelectedUnits(baseStructure);
                    baseStructure.FlashMarker(); // 어느 구조체가 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = baseStructure.transform.position;
                    attackPointer.SetActive(true);

                    UsercurrentState = OrderState.None;

                    return;
                }

                pendingLeftClickSelect = () => { if (baseStructure != null) rtsUnitController.ClickSelectStructure(baseStructure); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 4. 땅 클릭 = 명령 처리
        if (clickedGround)
        {
            if (UsercurrentState == OrderState.Move)
            {
                rtsUnitController.MoveSelectedUnits(groundHit.point);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }

            if (UsercurrentState == OrderState.Attack)
            {
                rtsUnitController.AttackGroundSelectedUnits(groundHit.point);

                attackPointer.transform.position = groundHit.point;
                attackPointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }

            if (UsercurrentState == OrderState.Patrol)
            {
                rtsUnitController.PatrolSelectedUnits(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }

            if (UsercurrentState == OrderState.Rally)
            {
                rtsUnitController.SetRallySelectBuilding(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }

            if (UsercurrentState == OrderState.BuildingMove)
            {
                rtsUnitController.MoveSelectedLiftedBuilding(groundHit.point);

                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

                return;
            }
        }

        // 5. 광물 클릭 = 선택 처리
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null && IsRevealedByFog(OreHit.point))
            {
                pendingLeftClickSelect = () => { if (node != null) rtsUnitController.ClickSelectResource(node); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 5. 가스 클릭 = 선택 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null && IsRevealedByFog(GasHit.point))
            {
                pendingLeftClickSelect = () => { if (node != null) rtsUnitController.ClickSelectResource(node); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 6. 아무것도 아닌 곳 클릭 = 선택 해제
        // (Shift를 누른 채 빈 바닥에서 드래그를 시작한 경우엔, 곧이어 시작될 드래그 선택이
        //  기존 선택에 "추가"되어야 하므로 여기서 기존 선택을 지우지 않는다)
        // (땅 클릭은 선택 해제하지 않는다 - 평상시 좌클릭으로 빈 땅을 눌러도 기존 선택을 유지)
        if (!Input.GetKey(KeyCode.LeftShift) && !clickedGround)
            rtsUnitController.DeselectAll();
    }

    /// <summary>
    /// 우클릭 관리
    /// </summary>
    private void HandleRightClick()
    {
        // 건설모드(배치 프리뷰 포함) 중 우클릭 = 배치 취소 + 그 자리에서 원래 우클릭 명령(이동/추적/공격 등) 수행.
        // ReturnState()로 UnitSelect로 되돌리면 아래 기존 분기들이 selectedUnitList(건설 맡던 일꾼)에 대해 그대로 동작한다.
        if (rtsUnitController.IsBuildMode())
            rtsUnitController.CancelBuildMode();

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit unitHit;
        RaycastHit groundHit;
        RaycastHit enemyHit;
        RaycastHit BuildingHit;
        RaycastHit OreHit;
        RaycastHit GasHit;

        bool clickedUnit = Physics.Raycast(ray, out unitHit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out groundHit, Mathf.Infinity, layerGround);
        bool clickedEnemy = Physics.Raycast(ray, out enemyHit, Mathf.Infinity, layerEnemy);
        bool clickedBuilding = Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding);
        bool clickedOre = Physics.Raycast(ray, out OreHit, Mathf.Infinity, layerOre);
        bool clickedGas = Physics.Raycast(ray, out GasHit, Mathf.Infinity, layerGas);

        // 0. 아군 유닛 우클릭 = 계속 따라다니기 (Idle 상태 유지 - 적 만나면 AttackRange가 자동 교전)
        // 아군도 지면 위에 서 있어 clickedGround가 함께 true가 되므로, 땅 클릭보다 먼저 처리하고 여기서 return 한다.
        if (clickedUnit && rtsUnitController.IsUnitSelect())
        {
            UnitController unit = unitHit.transform.GetComponent<UnitController>();

            if (unit != null)
            {
                rtsUnitController.FollowSelectedUnits(unit);
                unit.FlashMarker(); // 어느 아군을 따라갈지 마커 깜빡임으로 표시

                movePointer.transform.position = unit.transform.position;
                movePointer.SetActive(true);

                return;
            }
        }

        // 1. 적 우클릭 = 공격 명령 (추격 후 공격)
        // 적은 지면 위에도 서 있어 clickedGround도 함께 true가 되므로, 이동 명령이 끼어들지 않도록
        // 땅 클릭보다 먼저 처리하고 여기서 return 한다.
        if (clickedEnemy && rtsUnitController.IsUnitSelect())
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null)
            {
                // 안개에 가려진 적은 추격 공격 대신 그 지점으로 이동만 시킨다 (보이지 않는 대상을 특정해서 명령할 수 없음)
                if (IsRevealedByFog(enemyHit.point))
                {
                    rtsUnitController.AttackSelectedUnits(enemy);
                    enemy.FlashMarker(); // 어느 적이 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = enemy.transform.position;
                    attackPointer.SetActive(true);
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(enemyHit.point);

                    movePointer.transform.position = enemyHit.point;
                    movePointer.SetActive(true);
                }

                return;
            }
        }

        // 2. 땅 클릭 = 명령 처리
        if (clickedGround)
        {
            if (rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.MoveSelectedUnits(groundHit.point);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;
            }

            if (rtsUnitController.IsBuildingSelect())
            {
                // 선택된 건물이 공중에 떠 있으면 공중유닛처럼 그 지점으로 이동시키고, 지상 건물이면 기존처럼 랠리 포인트를 지정한다.
                if (rtsUnitController.IsSelectedBuildingLifted())
                    rtsUnitController.MoveSelectedLiftedBuilding(groundHit.point);
                else
                    rtsUnitController.SetRallySelectBuilding(groundHit.point);

                UsercurrentState = OrderState.Rally;
                UpdatePointer();
                movePointer.transform.position = groundHit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;

            }
        }

        // 건물 우클릭
        if(clickedBuilding)
        {
            BuildingController building = BuildingHit.transform.GetComponent<BuildingController>();

            if (building != null && rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.MoveToBuildingSelectedUnits(building);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = building.transform.position;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;
            }

            // 건설이 중단된 BaseStructure 우클릭 = 선택된 일꾼을 보내 건설 재개
            BaseStructure baseStructure = BuildingHit.transform.GetComponent<BaseStructure>();
            if (baseStructure != null && rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.AssignBuilderToStructure(baseStructure);
                baseStructure.FlashMarker();

                movePointer.transform.position = baseStructure.transform.position;
                movePointer.SetActive(true);
            }
        }

        // 5. 광물 클릭 = 명령 처리
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                // 안개에 가려진 자원은 채취 명령 대신 그 지점으로 이동만 시킨다
                if (IsRevealedByFog(OreHit.point))
                {
                    rtsUnitController.GatherSelectedUnits(node);
                    node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(OreHit.point);

                    movePointer.transform.position = OreHit.point;
                    movePointer.SetActive(true);
                }
            }
        }

        // 5. 가스 클릭 = 명령 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                // 안개에 가려진 자원은 채취 명령 대신 그 지점으로 이동만 시킨다
                if (IsRevealedByFog(GasHit.point))
                {
                    rtsUnitController.GatherSelectedUnits(node);
                    node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(GasHit.point);

                    movePointer.transform.position = GasHit.point;
                    movePointer.SetActive(true);
                }
            }
        }
    }

    private void HandlekeyBoard()
    {
        // 유닛 명령(Attack/Move/Stop/Patrol/Hold/Return/Build)과 건물 건설/유닛 생산 단축키는
        // 이제 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지해서 스스로 클릭되므로 여기서 따로 처리하지 않는다.
        // (Rally는 대응하는 버튼이 없는 순수 키보드 전용 모드 전환이라 그대로 남겨둠)
        if (rtsUnitController.IsUnitSelect())
        {
            //건물 랠리 설정
            if (Input.GetKeyDown(KeyCode.Y))
            {
                UsercurrentState = OrderState.Rally;
            }
        }

        if (rtsUnitController.IsBuildMode())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                rtsUnitController.ReturnState();
            }
        }

        // ESC: 생산 건물을 선택 중이면 대기열의 맨 뒤 항목부터 하나씩 취소한다 (인덱스 4 → 3 → ... 순서).
        if (rtsUnitController.IsBuildingSelect() && Input.GetKeyDown(KeyCode.Escape))
        {
            rtsUnitController.CancelLastQueuedProduction();
        }

        // ESC: 공격(A)/이동(M)/순찰(P)/랠리(Y)/건물이동(M) 등 위치·대상 지정을 기다리는 대기 상태를 취소한다.
        // HandleControlGroupInput의 그룹 선택 취소 로직과 동일한 패턴(상태를 None으로 되돌리고 포인터를 끔).
        if (Input.GetKeyDown(KeyCode.Escape) && UsercurrentState != OrderState.None)
        {
            UsercurrentState = OrderState.None;

            attackPointer.SetActive(false);
            movePointer.SetActive(false);
        }

        HandleControlGroupInput();
    }

    // 부대 지정(컨트롤 그룹): Ctrl+숫자(1~9,0)는 덮어쓰기 저장, Shift+숫자는 겹치지 않는 대상만 추가(병합),
    // 숫자만 누르면 저장된 부대를 선택한다.
    private void HandleControlGroupInput()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        for (int i = 0; i < controlGroupKeys.Length; i++)
        {
            if (!Input.GetKeyDown(controlGroupKeys[i]))
                continue;

            if (ctrlHeld)
                rtsUnitController.AssignControlGroup(i);
            else if (shiftHeld)
                rtsUnitController.AddSelectedToControlGroup(i);
            else
            {
                rtsUnitController.SelectControlGroup(i);

                bool isDoubleClick = Time.time - lastControlGroupPressTime[i] <= ControlGroupDoubleClickThreshold;
                lastControlGroupPressTime[i] = Time.time;

                if (isDoubleClick && mainCameraControl != null &&
                    rtsUnitController.TryGetControlGroupFocusPosition(i, out Vector3 focusPosition))
                {
                    // MinimapController의 클릭 이동과 동일하게 z를 -30 보정해 카메라가 유닛 바로 위가 아니라 살짝 아래쪽에서 비추게 한다
                    focusPosition.z -= 20f;
                    mainCameraControl.JumpToWorldXZ(focusPosition);
                }

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                {
                    UsercurrentState = OrderState.None;

                    // 명령을 취소했으니 마우스를 따라다니던 대기 중 마커(공격/이동 포인터)도 그 자리에 남지 않도록 끈다
                    attackPointer.SetActive(false);
                    movePointer.SetActive(false);
                }
            }

            break; // 한 프레임에 숫자 키 하나만 처리하면 충분
        }
    }


    /// <summary>
    /// 드래그 박스 표시
    /// </summary>
    private void DrawDragRectangle()
    {
        // 드래그 범위를 나타내는 Image UI의 위치
        dragRectangle.position = (start + end) * 0.5f;
        // 드래그 범위를 나타내는 Image UI의 크기
        dragRectangle.sizeDelta = new Vector2(Mathf.Abs(start.x - end.x), Mathf.Abs(start.y - end.y));
    }

    /// <summary>
    /// 드래그 영역 계산
    /// </summary>
    private void CalculateDragRect()
    {
        if (Input.mousePosition.x < start.x)
        {
            dragRect.xMin = Input.mousePosition.x;
            dragRect.xMax = start.x;
        }
        else
        {
            dragRect.xMin = start.x;
            dragRect.xMax = Input.mousePosition.x;
        }

        if (Input.mousePosition.y < start.y)
        {
            dragRect.yMin = Input.mousePosition.y;
            dragRect.yMax = start.y;
        }
        else
        {
            dragRect.yMin = start.y;
            dragRect.yMax = Input.mousePosition.y;
        }
    }

    /// <summary>
    /// 마우스를 놓는 시점에 선택을 확정한다.
    /// 드래그 범위 안에 유닛이 있으면 유닛(드래그) 선택을 우선하고, 없으면 대기해둔 단일 클릭 선택을 실행한다.
    /// </summary>
    private void SelectObject()
    {
        //드래그 범위 안에 들어오는 유닛부터 먼저 계산
        List<UnitController> unitsInDrag = new List<UnitController>();

        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            if (dragRect.Contains(screenPos))
            {
                unitsInDrag.Add(unit);
            }
        }

        if (unitsInDrag.Count > 0)
        {
            // 드래그 범위 안에 유닛이 있으면 드래그 유닛 선택이 우선 - 대기 중이던 단일 클릭 선택은 취소한다.
            pendingLeftClickSelect = null;

            // Shift가 아니면 기존 선택을 지우고 드래그로 잡힌 유닛들만 새로 선택한다 (Shift면 기존 선택에 추가).
            if (!Input.GetKey(KeyCode.LeftShift))
                rtsUnitController.DeselectAll();

            foreach (UnitController unit in unitsInDrag)
            {
                rtsUnitController.DragSelectUnit(unit);
            }

            return;
        }

        // 드래그로 걸린 유닛이 없으면(제자리 클릭이거나 빈 범위로 드래그) 마우스를 놓는 시점에 단일 클릭 선택을 확정한다.
        pendingLeftClickSelect?.Invoke();
        pendingLeftClickSelect = null;
    }

    // 더블클릭한 유닛과 같은 종류(GetUnitID() 일치)이면서 현재 카메라 화면 안에 보이는 유닛을 전부 선택한다.
    private void SelectAllVisibleUnitsOfSameType(UnitController referenceUnit)
    {
        int unitID = referenceUnit.GetUnitID();
        List<UnitController> matches = new List<UnitController>();

        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            if (unit == null || unit.GetUnitID() != unitID)
                continue;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);

            if (screenPos.z <= 0f)
                continue; // 카메라 뒤쪽에 있으면 화면에 보이지 않는 것으로 취급

            if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height)
                continue;

            matches.Add(unit);
        }

        if (matches.Count == 0)
            return;

        rtsUnitController.DeselectAll();

        foreach (UnitController unit in matches)
            rtsUnitController.DragSelectUnit(unit);
    }

    // 현재 명령 대기 상태(공격/이동/순찰/랠리)에 맞는 포인터 아이콘을 마우스가 가리키는 지면 위치에 표시한다.
    private void UpdatePointer()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerGround))
            return;

        if (UsercurrentState == OrderState.Attack)
        {
            attackPointer.SetActive(true);
            movePointer.SetActive(false);

            attackPointer.transform.position = hit.point;
        }
        else if (UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally || UsercurrentState == OrderState.BuildingMove)
        {
            movePointer.SetActive(true);
            attackPointer.SetActive(false);

            movePointer.transform.position = hit.point;
        }
        else
        {

        }
    }

    // 상황(UI 위/명령 대기 중 호버 대상/선택 가능 대상 호버)에 맞춰 실제 마우스 커서 아이콘을 바꾼다.
    // 모양(기본/선택/명령)은 OrderState가, 색(적/아군/중립)은 마우스 아래 대상의 진영이 결정한다.
    private void UpdateCursor()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetCursorTexture(cursorDefaultTexture);
            return;
        }

        bool commandPending =
            UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move ||
            UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally ||
            UsercurrentState == OrderState.BuildingMove;

        CursorTarget target = GetHoveredTarget();
        Texture2D texture;

        if (commandPending)
        {
            texture = target switch
            {
                CursorTarget.Enemy => cursorCommandEnemyTexture,
                CursorTarget.Ally => cursorCommandAllyTexture,
                _ => cursorCommandNeutralTexture, // 땅/자원/빈 곳은 전부 중립 취급 (이동/공격-이동 지점 지정)
            };
        }
        else if (target != CursorTarget.None)
        {
            texture = target switch
            {
                CursorTarget.Enemy => cursorSelectEnemyTexture,
                CursorTarget.Ally => cursorSelectAllyTexture,
                _ => cursorSelectNeutralTexture,
            };
        }
        else
        {
            texture = cursorDefaultTexture;
        }

        SetCursorTexture(texture);
    }

    // 마우스 아래에 있는 대상의 진영(적/아군/중립자원)을 판별한다. 아무 것도 없으면 None(땅 등).
    private CursorTarget GetHoveredTarget()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit enemyHit, Mathf.Infinity, layerEnemy) && IsRevealedByFog(enemyHit.point))
            return CursorTarget.Enemy;

        if (Physics.Raycast(ray, Mathf.Infinity, layerUnit | layerBuilding))
            return CursorTarget.Ally;

        if (Physics.Raycast(ray, out RaycastHit resourceHit, Mathf.Infinity, layerOre | layerGas) && IsRevealedByFog(resourceHit.point))
            return CursorTarget.Neutral;

        return CursorTarget.None;
    }

    // 안개에 가려져(현재 시야 밖) 있는 대상은 클릭 선택/명령/호버 대상에서 제외한다. fogWar가 없는 씬에서는 항상 보이는 것으로 취급.
    private bool IsRevealedByFog(Vector3 worldPosition)
    {
        if (fogWar == null)
            return true;

        return fogWar.CheckVisibility(worldPosition, 0);
    }

    private void SetCursorTexture(Texture2D texture)
    {
        if (texture == currentCursorTexture)
            return;

        currentCursorTexture = texture;
        Cursor.SetCursor(texture, cursorHotspot, CursorMode.Auto);
    }

    // 외부(RTSUnitController)에서 문자열로 명령 대기 상태를 전환할 때 사용 (예: "Move", "Attack", "Patrol", "Rally")
    public void SetOrderState(string state)
    {
        if (Enum.TryParse(state, out OrderState orderState))
        {
            UsercurrentState = orderState;
        }
        else
        {
            Debug.LogWarning($"Unknown OrderState : {state}");
        }
    }
}