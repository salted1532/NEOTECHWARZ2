using UnityEngine;
using UnityEngine.EventSystems;

// 미니맵 이미지(RawImage)를 클릭/드래그하면 그 지점의 월드 좌표를 계산해 메인 카메라를 이동시킨다.
// 미니맵 카메라가 실제로 그 픽셀에 무엇을 그렸는지 ViewportPointToRay로 그대로 역산하므로,
// 미니맵 카메라의 위치/각도/투영 방식이 바뀌어도 별도 보정 없이 항상 정확하다.
public class MinimapController : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    [SerializeField] private RectTransform minimapRect; // RawImage의 RectTransform (보통 자기 자신)
    [SerializeField] private Camera minimapCamera;       // 미니맵을 렌더링하는 카메라
    [SerializeField] private CameraControl mainCameraControl;

    public void OnPointerClick(PointerEventData eventData) => MoveCameraToPointer(eventData);
    public void OnDrag(PointerEventData eventData) => MoveCameraToPointer(eventData);

    private void MoveCameraToPointer(PointerEventData eventData)
    {
        // Screen Space - Overlay 캔버스이므로 카메라 인자는 null이어야 한다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect, eventData.position, null, out Vector2 localPoint))
            return;

        Rect rect = minimapRect.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        Ray ray = minimapCamera.ViewportPointToRay(new Vector3(u, v, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 groundPoint = ray.GetPoint(distance);
            groundPoint.z -= 20f;
            mainCameraControl.JumpToWorldXZ(groundPoint);
        }
    }
}
