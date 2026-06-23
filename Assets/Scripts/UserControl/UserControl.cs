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

    [SerializeField]
    private bool attackMode = false;
    [SerializeField]
    private bool moveMode = false;
    [SerializeField]
    private bool patrolMode = false;
    [SerializeField]
    private bool rallyMode = false;
    [SerializeField]
    private bool modeOn = false;

    private enum PointerType
    {
        None,
        Attack,
        Basic
    }

    private PointerType currentPointer = PointerType.None;

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
                
                moveModeOn();
                UpdatePointer();
                movePointer.transform.position = hit.point;
                movePointer.SetActive(true);

                AllModeOff();
            }
        }
    }

    private void HandlekeyBoard()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            attackModeOn();
        }
    }

    /// <summary>
    /// 클릭 선택
    /// </summary>
    private void HandleLeftClick()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitUnit;
        RaycastHit hitGround;

        bool clickedUnit = Physics.Raycast(ray, out hitUnit, Mathf.Infinity, layerUnit);
        bool clickedGround = Physics.Raycast(ray, out hitGround, Mathf.Infinity, layerGround);

        // 1. 유닛 클릭
        if (clickedUnit)
        {
            UnitController unit = hitUnit.transform.GetComponent<UnitController>();

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
            if (attackMode)
            {
                rtsUnitController.AttackGroundSelectedUnits(hitGround.point);

                attackPointer.transform.position = hitGround.point;
                attackPointer.SetActive(true);

                AllModeOff();

                return;
            }
        }

       // 3. 아무것도 아닌 곳 클릭 = 선택 해제
        rtsUnitController.DeselectAll();
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

    private void AllModeOff()
    {
        attackMode = false;
        moveMode = false;
        patrolMode = false;
        rallyMode = false;
    }

    //입력 상태 변화
    private void attackModeOn()
    {
        attackMode = true;
        moveMode = false;
        patrolMode = false;
        rallyMode = false;
    }
    private void moveModeOn()
    {
        attackMode = false;
        moveMode = true;
        patrolMode = false;
        rallyMode = false;
    }
    private void patrolModeOn()
    {
        attackMode = false;
        moveMode = false;
        patrolMode = true;
        rallyMode = false;
    }
    private void rallyModeOn()
    {
        attackMode = false;
        moveMode = false;
        patrolMode = false;
        rallyMode = true;
    }
    private void UpdatePointer()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerGround))
            return;

        if (attackMode)
        {
            attackPointer.SetActive(true);
            movePointer.SetActive(false);

            attackPointer.transform.position = hit.point;
        }
        else if (moveMode || patrolMode || rallyMode)
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