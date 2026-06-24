using UnityEngine;
using UnityEngine.EventSystems;


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
            SelectUnits();

            start = Vector2.zero;
            end = Vector2.zero;

            DrawDragRectangle();
        }

        // 우클릭 시
        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            // 유닛 오브젝트(layerGround)를 클릭했을 때
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerGround))
            {
                rtsUnitController.MoveSelectedUnits(hit.point);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = hit.point;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;
            }
        }
    }

    /// <summary>
    /// 클릭 선택
    /// </summary>
    private void HandleLeftClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit unitHit;
        RaycastHit groundHit;
        RaycastHit enemyHit;
        RaycastHit BuildingHit;
        RaycastHit OreHit;

        bool clickedUnit = Physics.Raycast(ray, out unitHit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out groundHit, Mathf.Infinity, layerGround);
        bool clickedEnemy = Physics.Raycast(ray, out enemyHit, Mathf.Infinity, layerEnemy);
        bool clickedBuilding = Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding);
        bool clickedOre = Physics.Raycast(ray, out OreHit, Mathf.Infinity, layerOre);

        // 1. 유닛 클릭
        if (clickedUnit)
        {
            UnitController unit = unitHit.transform.GetComponent<UnitController>();

            if (unit != null)
            {
                if (Input.GetKey(KeyCode.LeftShift))        
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                    rtsUnitController.ClickSelectUnit(unit);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
        }

        // 2. 땅 클릭 = 명령 처리
        if (clickedGround)  
        {
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
        }

        // 3. 적 클릭 = 명령 처리
        if (clickedEnemy)
        {

        }

        // 4. 건물 클릭 = 명령 처리
        if (clickedBuilding)
        {

        }

        // 5. 광물 클릭 = 명령 처리
        if (clickedOre)
        {

        }

        // 6. 아무것도 아닌 곳 클릭 = 선택 해제
        rtsUnitController.DeselectAll();
    }
    private void HandlekeyBoard()
    {

        if(rtsUnitController.IsUnitSelect())
        {
            // 공격모드 변화
            if (Input.GetKeyDown(KeyCode.A))
            {
                UsercurrentState = OrderState.Attack;
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                rtsUnitController.StopSelectedUnits();
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                rtsUnitController.HoldSelectedUnits();
            }
            // 순찰모드 변화
            if (Input.GetKeyDown(KeyCode.P))
            {
                UsercurrentState = OrderState.Patrol;
            }
            // 이동모드 변화
            if (Input.GetKeyDown(KeyCode.M))
            {
                UsercurrentState = OrderState.Move;
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
    /// 드래그 범위 내 유닛 선택
    /// </summary>
    private void SelectUnits()
    {
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
}