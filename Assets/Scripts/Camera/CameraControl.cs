using UnityEngine;

// RTS 스타일 탑다운 카메라 컨트롤러.
// 방향키/화면 가장자리 이동, 마우스 휠 줌, Q/E 화면 중앙 기준 궤도 회전, 맵 경계 제한, Space로 본진 복귀 기능을 제공한다.
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
    [SerializeField] private float zoomSpeed = 25f;
    [SerializeField] private float minZoom = 8f;  // 카메라 높이(Y) 하한 - 너무 가깝게 못 들어가게 (기준 지형고도 0 기준)
    [SerializeField] private float maxZoom = 35f; // 카메라 높이(Y) 상한 - 너무 멀리 못 나가게 (기준 지형고도 0 기준)
    [SerializeField] private LayerMask groundLayer; // 화면 중앙 지형 높이 샘플링용 레이어 (지형/Ground)

    [Header("Rotate")]
    [SerializeField] private float rotateSpeed = 120f;     // Q/E 회전 속도 (초당 각도)
    [SerializeField] private float maxRotationAngle = 60f; // 기준 각도에서 좌우로 돌 수 있는 최대 각도
    [SerializeField] private float returnSpeed = 200f;     // Q/E를 떼었을 때 원래 각도로 되돌아가는 속도 (초당 각도)

    private Camera cam;

    private Vector3 targetPosition;
    private Vector3 mainBasePosition;

    private float currentRotationAngle = 0f; // 기준(정면) 각도로부터 현재까지 회전한 누적 각도

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
        HandleRotate();
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
        // Q/E로 화면을 돌린 상태에서도 "위/오른쪽"이 항상 화면상 위/오른쪽을 향하도록,
        // 고정된 월드축이 아니라 카메라의 현재 회전을 수평면(Y=0)에 투영한 축을 기준으로 이동한다.
        Vector3 camForward = transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDir = Vector3.zero;

        // 방향키 이동
        if (Input.GetKey(KeyCode.UpArrow))
            moveDir += camForward;

        if (Input.GetKey(KeyCode.DownArrow))
            moveDir -= camForward;

        if (Input.GetKey(KeyCode.LeftArrow))
            moveDir -= camRight;

        if (Input.GetKey(KeyCode.RightArrow))
            moveDir += camRight;

        // 화면 가장자리로 마우스를 이동하면 그쪽으로 스크롤
        if (Input.mousePosition.y >= Screen.height - edgeSize)
            moveDir += camForward;

        if (Input.mousePosition.y <= edgeSize)
            moveDir -= camForward;

        if (Input.mousePosition.x >= Screen.width - edgeSize)
            moveDir += camRight;

        if (Input.mousePosition.x <= edgeSize)
            moveDir -= camRight;

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

    // 마우스 휠 입력으로 카메라를 자신이 바라보는 방향(forward)으로 앞뒤로 이동시켜 확대/축소한다.
    // (이 카메라는 Perspective라 orthographicSize는 아무 효과가 없어서 위치를 직접 옮기는 방식으로 줌을 구현한다)
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector3 zoomStep = transform.forward * scroll * zoomSpeed;
        float nextY = targetPosition.y + zoomStep.y;

        // 화면 중앙이 보고 있는 지형의 높이만큼 줌 범위 자체를 같이 올려서, 언덕 위/아래 어디를 보든
        // "지형으로부터의 거리감"이 평지에서와 동일하게 느껴지도록 한다.
        float groundHeight = GetGroundHeightAtScreenCenter();

        if (nextY < minZoom + groundHeight || nextY > maxZoom + groundHeight)
            return;

        targetPosition += zoomStep;
    }

    // 화면 정중앙이 바라보는 지점의 실제 지형(groundLayer) 높이를 레이캐스트로 알아낸다.
    // 레이어 미설정이거나 아무것도 맞지 않으면(허공 등) 0(기준 평지)을 반환.
    private float GetGroundHeightAtScreenCenter()
    {
        if (groundLayer == 0)
            return 0f;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            return hit.point.y;

        return 0f;
    }

    // Q/E 입력으로 화면 중앙이 바라보는 지점을 기준으로 카메라를 좌우로 궤도 회전시킨다.
    // (달이 지구를 중심으로 도는 것처럼, pivot을 축으로 위치와 시선 방향을 함께 회전)
    // 기준 각도에서 ±maxRotationAngle까지만 돌 수 있고, Q/E를 떼면 다시 기준 각도(0)로 서서히 되돌아간다.
    private void HandleRotate()
    {
        float rotateInput = 0f;

        if (Input.GetKey(KeyCode.Q))
            rotateInput += 1f;

        if (Input.GetKey(KeyCode.E))
            rotateInput -= 1f;

        float angleStep;

        if (!Mathf.Approximately(rotateInput, 0f))
        {
            float desiredAngle = Mathf.Clamp(currentRotationAngle + rotateInput * rotateSpeed * Time.deltaTime, -maxRotationAngle, maxRotationAngle);
            angleStep = desiredAngle - currentRotationAngle;
        }
        else if (!Mathf.Approximately(currentRotationAngle, 0f))
        {
            angleStep = Mathf.MoveTowards(currentRotationAngle, 0f, returnSpeed * Time.deltaTime) - currentRotationAngle;
        }
        else
        {
            return; // 입력도 없고 이미 기준 각도인 상태
        }

        if (Mathf.Approximately(angleStep, 0f))
            return;

        currentRotationAngle += angleStep;

        Vector3 pivot = GetScreenCenterGroundPoint();
        Quaternion delta = Quaternion.AngleAxis(angleStep, Vector3.up);

        targetPosition = pivot + delta * (targetPosition - pivot);
        transform.rotation = delta * transform.rotation;
    }

    // 미니맵 클릭 등 외부에서 특정 지면 좌표로 카메라를 이동시킬 때 사용.
    // 높이(Y, 줌 상태)와 회전은 그대로 유지하고 X/Z만 바꾸되, LateUpdate의 Lerp 보간 없이 즉시 순간이동한다.
    public void JumpToWorldXZ(Vector3 worldPoint)
    {
        targetPosition.x = Mathf.Clamp(worldPoint.x, minX, maxX);
        targetPosition.z = Mathf.Clamp(worldPoint.z, minZ, maxZ);

        transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
    }

    // 화면 정중앙 레이가 지면(Y = 0 평면)과 만나는 지점을 구한다 (회전 축 pivot으로 사용).
    private Vector3 GetScreenCenterGroundPoint()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);

        return targetPosition; // 평면과 만나지 않는 예외적인 경우 안전하게 현재 목표 위치를 축으로 사용
    }
}