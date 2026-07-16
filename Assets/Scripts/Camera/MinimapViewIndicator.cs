using UnityEngine;

// 미니맵 위에 메인 카메라가 실제로 보고 있는 지면 영역을 반투명 사각형으로 표시한다.
// 메인 카메라 화면의 네 꼭짓점을 지면(Y=0)에 투영해 실제 시야 영역을 구하고,
// 그 지점들을 미니맵 카메라 기준으로 역투영해서 미니맵 UI 위의 위치/크기로 변환한다.
// 줌(카메라 높이)이나 Q/E 회전이 바뀌어도 매 프레임 실제 시야를 그대로 반영하므로 별도 보정이 필요 없다.
[RequireComponent(typeof(RectTransform))]
public class MinimapViewIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform viewIndicator; // 크기/위치를 갱신할 사각형 (보통 자기 자신)
    [SerializeField] private RectTransform minimapRect;   // 미니맵 RawImage의 RectTransform
    [SerializeField] private Camera minimapCamera;         // 미니맵을 렌더링하는 카메라
    [SerializeField] private Camera mainCamera;             // 시야를 표시할 대상 카메라

    private static readonly Vector2[] ViewportCorners =
    {
        new Vector2(0f, 0f),
        new Vector2(1f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
    };

    private void Update()
    {
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Rect rect = minimapRect.rect;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int i = 0; i < ViewportCorners.Length; i++)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(ViewportCorners[i].x, ViewportCorners[i].y, 0f));

            if (!groundPlane.Raycast(ray, out float distance))
                return; // 지면과 만나지 않는 꼭짓점이 있으면(하늘 쪽을 향한 시야 등) 이번 프레임 갱신을 건너뜀

            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 viewportPoint = minimapCamera.WorldToViewportPoint(worldPoint);

            float localX = rect.xMin + viewportPoint.x * rect.width;
            float localY = rect.yMin + viewportPoint.y * rect.height;

            minX = Mathf.Min(minX, localX);
            maxX = Mathf.Max(maxX, localX);
            minY = Mathf.Min(minY, localY);
            maxY = Mathf.Max(maxY, localY);
        }

        // 미니맵 이미지 밖으로 나가는 부분은 그리지 않도록 사각형을 미니맵 rect 안으로 잘라낸다.
        minX = Mathf.Clamp(minX, rect.xMin, rect.xMax);
        maxX = Mathf.Clamp(maxX, rect.xMin, rect.xMax);
        minY = Mathf.Clamp(minY, rect.yMin, rect.yMax);
        maxY = Mathf.Clamp(maxY, rect.yMin, rect.yMax);

        viewIndicator.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        viewIndicator.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }
}
