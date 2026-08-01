using UnityEngine;
using UnityEngine.EventSystems;

// 미니맵 이미지(RawImage) 클릭/드래그를 처리한다.
// 미니맵 카메라가 실제로 그 픽셀에 무엇을 그렸는지 ViewportPointToRay로 그대로 역산하므로,
// 미니맵 카메라의 위치/각도/투영 방식이 바뀌어도 별도 보정 없이 항상 정확하다.
// 좌클릭: 대기 중인 명령(A공격 등)이 있으면 그 지점에 확정, 없으면 카메라 이동(기존 동작).
// 우클릭: 선택된 유닛/건물에 "그냥 우클릭"과 동일한 명령(이동/랠리)을 내린다 (doc/0349).
public class MinimapController : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    [SerializeField] private RectTransform minimapRect; // RawImage의 RectTransform (보통 자기 자신)
    [SerializeField] private Camera minimapCamera;       // 미니맵을 렌더링하는 카메라
    [SerializeField] private CameraControl mainCameraControl;
    [SerializeField] private UserControl userControl;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!TryGetGroundPoint(eventData, out Vector3 groundPoint))
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            userControl.IssueRightClickMoveAt(groundPoint);
            return;
        }

        // 좌클릭: 명령 대기 중이면(A공격 등) 카메라는 그대로 두고 그 자리에 명령만 확정한다 -
        // 미니맵으로 명령을 내릴 때마다 화면이 그쪽으로 튀면 오히려 불편하다.
        if (userControl.HasPendingGroundOrder())
        {
            userControl.ConfirmPendingOrderAt(groundPoint);
            return;
        }

        groundPoint.z -= 30f;
        mainCameraControl.JumpToWorldXZ(groundPoint);
    }

    // 드래그는 좌클릭으로 카메라를 계속 따라가게 하는 기존 동작만 유지한다 - 드래그 중에는 명령을 확정하지 않는다
    // (실수로 드래그하다 명령이 나가는 것 방지).
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            return;

        if (!TryGetGroundPoint(eventData, out Vector3 groundPoint))
            return;

        groundPoint.z -= 30f;
        mainCameraControl.JumpToWorldXZ(groundPoint);
    }

    private bool TryGetGroundPoint(PointerEventData eventData, out Vector3 groundPoint)
    {
        groundPoint = default;

        // Screen Space - Overlay 캔버스이므로 카메라 인자는 null이어야 한다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect, eventData.position, null, out Vector2 localPoint))
            return false;

        Rect rect = minimapRect.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        Ray ray = minimapCamera.ViewportPointToRay(new Vector3(u, v, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (!groundPlane.Raycast(ray, out float distance))
            return false;

        groundPoint = ray.GetPoint(distance);
        return true;
    }
}
