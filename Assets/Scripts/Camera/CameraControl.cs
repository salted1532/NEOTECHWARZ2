using UnityEngine;

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

        targetPosition = transform.position;
        mainBasePosition = transform.position;

        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        HandleMovement();
        HandleZoom();
    }

    private void LateUpdate()
    {
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * smoothTime
        );
    }

    private void HandleMovement()
    {
        Vector3 moveDir = Vector3.zero;

        // 방향키
        if (Input.GetKey(KeyCode.UpArrow))
            moveDir += Vector3.forward;

        if (Input.GetKey(KeyCode.DownArrow))
            moveDir += Vector3.back;

        if (Input.GetKey(KeyCode.LeftArrow))
            moveDir += Vector3.left;

        if (Input.GetKey(KeyCode.RightArrow))
            moveDir += Vector3.right;

        // 화면 가장자리 이동
        if (Input.mousePosition.y >= Screen.height - edgeSize)
            moveDir += Vector3.forward;

        if (Input.mousePosition.y <= edgeSize)
            moveDir += Vector3.back;

        if (Input.mousePosition.x >= Screen.width - edgeSize)
            moveDir += Vector3.right;

        if (Input.mousePosition.x <= edgeSize)
            moveDir += Vector3.left;

        // 본진으로 이동
        if (Input.GetKeyDown(KeyCode.Space))
        {
            targetPosition = mainBasePosition;
            return;
        }

        moveDir.Normalize();

        targetPosition += moveDir * moveSpeed * Time.deltaTime;

        // 맵 경계 제한
        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(targetPosition.z, minZ, maxZ);
    }

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