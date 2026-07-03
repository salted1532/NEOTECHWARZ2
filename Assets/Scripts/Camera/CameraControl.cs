using UnityEngine;

// RTS 스타일 탑다운 카메라 컨트롤러.
// 방향키/화면 가장자리 이동, 마우스 휠 줌, 맵 경계 제한, Space로 본진 복귀 기능을 제공한다.
public class CameraControl : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 30f;
    [SerializeField] private float edgeSize = 10f;
    [SerializeField] private float smoothTime = 8f;

    [Header("Map Limit")]
    [SerializeField] private float minX = -130f;
    [SerializeField] private float maxX = 40f;
    [SerializeField] private float minZ = -130f;
    [SerializeField] private float maxZ = 40f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 20f;

    private Camera cam;

    private Vector3 targetPosition;
    private Vector3 mainBasePosition;

    private void Start()
    {
        cam = GetComponent<Camera>();

        // 시작 위치를 목표 위치이자 "본진(홈) 위치"로 저장해 Space 키로 되돌아올 수 있게 한다.
        targetPosition = transform.position;
        mainBasePosition = transform.position;

        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
    }

    // 실제 이동은 LateUpdate에서 목표 위치로 보간(Lerp)하여 카메라가 부드럽게 따라가도록 한다.
    private void LateUpdate()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * smoothTime
        );
    }

    // 방향키 입력 + 화면 가장자리 마우스 위치 + Space(본진 복귀)를 종합해 목표 위치(targetPosition)를 갱신한다.
    private void HandleMovement()
    {
        Vector3 moveDir = Vector3.zero;

        // 방향키 이동
        if (Input.GetKey(KeyCode.UpArrow))
            moveDir += Vector3.forward;

        if (Input.GetKey(KeyCode.DownArrow))
            moveDir += Vector3.back;

        if (Input.GetKey(KeyCode.LeftArrow))
            moveDir += Vector3.left;

        if (Input.GetKey(KeyCode.RightArrow))
            moveDir += Vector3.right;

        // 화면 가장자리로 마우스를 이동하면 그쪽으로 스크롤
        if (Input.mousePosition.y >= Screen.height - edgeSize)
            moveDir += Vector3.forward;

        if (Input.mousePosition.y <= edgeSize)
            moveDir += Vector3.back;

        if (Input.mousePosition.x >= Screen.width - edgeSize)
            moveDir += Vector3.right;

        if (Input.mousePosition.x <= edgeSize)
            moveDir += Vector3.left;

        // Space: 본진(시작 위치)으로 즉시 복귀
        if (Input.GetKeyDown(KeyCode.Space))
        {
            targetPosition = mainBasePosition;
            return;
        }

        moveDir.Normalize();

        targetPosition += moveDir * moveSpeed * Time.deltaTime;

        // 맵 경계값(min/max X,Z) 밖으로 나가지 않도록 위치 고정
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
    }

    // 마우스 휠 입력으로 orthographic 카메라의 확대/축소 크기를 조절한다.
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        cam.orthographicSize -= scroll * zoomSpeed;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize,
            minZoom,
            maxZoom
        );
    }
}