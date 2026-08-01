using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 씬에 배치된 MinimapObjectiveMarker들을 미니맵 위에 아이콘으로 표시한다 (doc/0349).
// 월드 좌표 -> 미니맵 UI 로컬 좌표 변환은 MinimapViewIndicator와 동일한 공식을 쓴다.
// 아이콘은 프리팹 없이 코드에서 바로 만든다(스프라이트 없는 Image는 단색 사각형으로 렌더링됨) - 프로토타입
// 단계에서 에셋 없이도 바로 동작하게 하기 위함. 나중에 전용 아이콘 스프라이트가 생기면 프리팹으로 교체 가능.
public class MinimapObjectiveOverlay : MonoBehaviour
{
    public static MinimapObjectiveOverlay Instance { get; private set; }

    [SerializeField] private RectTransform minimapRect; // 미니맵 RawImage의 RectTransform
    [SerializeField] private Camera minimapCamera;

    private readonly Dictionary<MinimapObjectiveMarker, RectTransform> icons = new Dictionary<MinimapObjectiveMarker, RectTransform>();

    private void Awake()
    {
        Instance = this;
    }

    // 씬 시작 시 이미 배치돼 있던 마커는 OnEnable 시점에 Instance가 아직 안 잡혀있었을 수 있으므로
    // (스크립트 실행 순서 무관하게) Start()에서 한 번 전수 등록한다. 이후 동적으로 켜지는 마커는
    // MinimapObjectiveMarker.OnEnable()이 알아서 Register()를 부른다.
    private void Start()
    {
        foreach (MinimapObjectiveMarker marker in FindObjectsByType<MinimapObjectiveMarker>(FindObjectsSortMode.None))
            Register(marker);
    }

    public void Register(MinimapObjectiveMarker marker)
    {
        if (marker == null || icons.ContainsKey(marker))
            return;

        GameObject iconObj = new GameObject("ObjectiveIcon", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)iconObj.transform;
        rect.SetParent(minimapRect, false);
        rect.sizeDelta = new Vector2(marker.IconSize, marker.IconSize);

        Image image = iconObj.GetComponent<Image>();
        image.color = marker.IconColor;
        image.raycastTarget = false; // 미니맵 클릭(명령/카메라 이동)을 가로채지 않도록

        icons[marker] = rect;
    }

    public void Unregister(MinimapObjectiveMarker marker)
    {
        if (marker == null || !icons.TryGetValue(marker, out RectTransform rect))
            return;

        if (rect != null)
            Destroy(rect.gameObject);

        icons.Remove(marker);
    }

    private void Update()
    {
        if (icons.Count == 0)
            return;

        Rect rect2D = minimapRect.rect;

        foreach (var pair in icons)
        {
            if (pair.Key == null || pair.Value == null)
                continue;

            Vector3 viewportPoint = minimapCamera.WorldToViewportPoint(pair.Key.transform.position);
            pair.Value.anchoredPosition = new Vector2(
                rect2D.xMin + viewportPoint.x * rect2D.width,
                rect2D.yMin + viewportPoint.y * rect2D.height);
        }
    }
}
