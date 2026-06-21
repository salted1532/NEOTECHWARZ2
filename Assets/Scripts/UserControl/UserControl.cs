using UnityEngine;
using UnityEngine.EventSystems;

public class UserControl : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private LayerMask layerUnit;
    [SerializeField] private Camera mainCamera;

    [Header("Drag UI")]
    [SerializeField] private RectTransform dragRectangle;

    private RTSUnitController rtsUnitController;

    private Vector2 start;
    private Vector2 end;
    private Rect dragRect;

    private void Awake()
    {
        mainCamera = Camera.main;
        rtsUnitController = GetComponent<RTSUnitController>();

        start = Vector2.zero;
        end = Vector2.zero;

        DrawDragRectangle();
    }

    private void Update()
    {
        HandleMouseSelection();
    }

    private void HandleMouseSelection()
    {
        // 드래그 시작
        if (Input.GetMouseButtonDown(0))
        {
            start = Input.mousePosition;
            dragRect = new Rect();

            if (EventSystem.current.IsPointerOverGameObject())
                return;

            TryClickSelectUnit();
            Debug.Log("드래그 시작");
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

            Debug.Log("드래그 종료");
            start = Vector2.zero;
            end = Vector2.zero;

            DrawDragRectangle();
        }
    }

    /// <summary>
    /// 클릭 선택
    /// </summary>
    private void TryClickSelectUnit()
    {
        RaycastHit hit;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out  hit, Mathf.Infinity, layerUnit))
        {
            UnitController unit = hit.transform.GetComponent<UnitController>();

            if (unit == null)
            {
                return;
            }

            if (Input.GetKey(KeyCode.LeftShift))
            {
                rtsUnitController.ShiftClickSelectUnit(unit);
            }
            else
            {
                rtsUnitController.ClickSelectUnit(unit);
            }
        }
        else
        {
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                rtsUnitController.DeselectAll();
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
        Debug.Log("SelectUnits 실행");

        Debug.Log("UnitList 개수 : " + rtsUnitController.UnitList.Count);

        foreach (UnitController unit in rtsUnitController.UnitList)
        {
            Vector3 screenPos =
                mainCamera.WorldToScreenPoint(unit.transform.position);

            Debug.Log(unit.name);
            Debug.Log(screenPos);
            Debug.Log(dragRect);

            if (dragRect.Contains(screenPos))
            {
                Debug.Log(unit.name + " 선택범위 포함");

                rtsUnitController.DragSelectUnit(unit);
            }
        }
    }
}