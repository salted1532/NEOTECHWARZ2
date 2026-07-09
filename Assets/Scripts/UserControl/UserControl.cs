using System;
using UnityEngine;
using UnityEngine.EventSystems;


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
    private RectTransform dragRectangle;


    private GameObject pointer;

    private GameObject attackPointer;
    private GameObject movePointer;

    [SerializeField]
    private GameObject pointerPrefab;
    [SerializeField]
    private GameObject attackPointerPrefab;

    private RTSUnitController rtsUnitController;

    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;
    private Vector3 mousePos;

    private enum OrderState
    {
        None,
        Attack,
        Move,
        Patrol,
        Rally
    }

    [SerializeField]
    private OrderState UsercurrentState = OrderState.None;

    private void Awake()
    {
        mainCamera = Camera.main;
        rtsUnitController = GetComponent<RTSUnitController>();

        attackPointer = Instantiate(attackPointerPrefab);
        movePointer = Instantiate(pointerPrefab);

        attackPointer.SetActive(false);
        movePointer.SetActive(false);

        start = Vector2.zero;
        end = Vector2.zero;

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
                    rtsUnitController.ClickSelectUnit(unit);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 2. 적 클릭 = 선택 또는 공격 명령 (A 모드 중이면 해당 적을 추격 공격, 아니면 선택)
        // 땅 클릭보다 먼저 처리해야 한다: 적은 지면 위에도 서 있어 clickedGround도 함께 true가 되기 때문에,
        // 더 구체적인 대상(적)이 우선권을 가져야 "A 모드에서 적을 직접 지정"이 가능하다.
        if (clickedEnemy)
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null)
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

                rtsUnitController.ClickSelectEnemy(enemy);

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
                    rtsUnitController.ClickSelectBuilding(building);

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

                rtsUnitController.ClickSelectStructure(baseStructure);

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
        }

        // 5. 광물 클릭 = 선택 처리
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                rtsUnitController.ClickSelectResource(node);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 5. 가스 클릭 = 선택 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
                rtsUnitController.ClickSelectResource(node);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 6. 아무것도 아닌 곳 클릭 = 선택 해제
        // (Shift를 누른 채 빈 바닥에서 드래그를 시작한 경우엔, 곧이어 시작될 드래그 선택이
        //  기존 선택에 "추가"되어야 하므로 여기서 기존 선택을 지우지 않는다)
        if (!Input.GetKey(KeyCode.LeftShift))
            rtsUnitController.DeselectAll();
    }

    /// <summary>
    /// 우클릭 관리
    /// </summary>
    private void HandleRightClick()
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
                rtsUnitController.AttackSelectedUnits(enemy);
                enemy.FlashMarker(); // 어느 적이 공격 대상인지 마커 깜빡임으로 표시

                attackPointer.transform.position = enemy.transform.position;
                attackPointer.SetActive(true);

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
                rtsUnitController.GatherSelectedUnits(node);
                node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
            }
        }

        // 5. 가스 클릭 = 명령 처리
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                rtsUnitController.GatherSelectedUnits(node);
                node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
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
    /// 드래그 범위 내 모든것 선택
    /// </summary>
    private void SelectObject()
    {
        //유닛 선택
        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            if (dragRect.Contains(screenPos))
            {
                rtsUnitController.DragSelectUnit(unit);
            }
        }
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
        else if (UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally)
        {
            movePointer.SetActive(true);
            attackPointer.SetActive(false);

            movePointer.transform.position = hit.point;
        }
        else
        {

        }
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