# 0155. 미니맵 시야 표시 사각형(줌 연동) 추가

## 날짜
2026-07-16

## 요청
"줌했을시 미니맵에서 어느 화면을 보고 있는지를 알려주는 Square의 크기도 조정되도록 해줘 / 확대 했을때 비율에 맞춰서 쭉 크기가 작아지다가 다시 줌 확대하면 원래 크기로 돌아오도록"

## 조사
`TestScene.unity`를 확인한 결과, 메인 카메라의 시야 영역을 미니맵에 표시하는 사각형(Square)이 **아직 존재하지 않았다** — `MiniMap` GameObject 아래에는 `MiniMap_image`(RawImage, `MinimapController` 부착) 하나뿐이고, 뷰포트 표시용 오브젝트/스크립트는 없었다. 따라서 기존 기능 수정이 아니라 신규 기능 구현.

사용자에게 스타일 확인(반투명 채워진 사각형 vs 테두리만) → "반투명 채워진 사각형" 선택.

## 코드 변경

### 1) 신규 스크립트 (`Assets/Scripts/Camera/MinimapViewIndicator.cs`)

**기존 코드**
없음 (신규 파일)

**변경 코드**
```csharp
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MinimapViewIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform viewIndicator;
    [SerializeField] private RectTransform minimapRect;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private Camera mainCamera;

    private static readonly Vector2[] ViewportCorners =
    {
        new Vector2(0f, 0f), new Vector2(1f, 0f),
        new Vector2(0f, 1f), new Vector2(1f, 1f),
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
                return;

            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 viewportPoint = minimapCamera.WorldToViewportPoint(worldPoint);

            float localX = rect.xMin + viewportPoint.x * rect.width;
            float localY = rect.yMin + viewportPoint.y * rect.height;

            minX = Mathf.Min(minX, localX); maxX = Mathf.Max(maxX, localX);
            minY = Mathf.Min(minY, localY); maxY = Mathf.Max(maxY, localY);
        }

        viewIndicator.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        viewIndicator.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }
}
```

메인 카메라 화면의 네 꼭짓점을 지면(Y=0)에 투영해 실제 시야 영역(사다리꼴, 원근 때문)을 구하고, 그 지면 좌표들을 `minimapCamera.WorldToViewportPoint`로 미니맵 카메라 기준으로 역투영한 뒤 `minimapRect.rect` 로컬 좌표로 변환, 4점의 axis-aligned 바운딩 박스를 사각형의 `sizeDelta`/`anchoredPosition`으로 사용한다. 줌으로 카메라 높이가 바뀌면 지면에 투영되는 시야 면적 자체가 커지거나 작아지므로, 이 계산이 매 프레임 다시 이루어지면서 사각형 크기가 자동으로 따라간다(줌인 → 작아짐, 줌아웃 → 원래 크기로 커짐). Q/E 회전에도 동일한 원리로 자동 대응.

기존 `MinimapController.MoveCameraToPointer`가 쓰는 "화면 좌표 → Ray → 지면 교차점 → 미니맵 카메라 좌표" 매핑을 반대 방향으로 재사용한 것이라 별도의 보정 상수 없이 정확하다.

### 2) `TestScene.unity`에 배치
`MiniMap_image`의 자식으로 `ViewSquare` GameObject 추가:
- `RectTransform`: `MiniMap_image`(=`minimapRect`)의 자식, 앵커/피벗 (0.5, 0.5) — `Update()`가 매 프레임 `sizeDelta`/`anchoredPosition`을 덮어씀
- `Image`: `color: {r:1, g:1, b:1, a:0.18}` (흰색 반투명 채움), `sprite: none`, **`raycastTarget: 0`** — 미니맵 클릭(`MinimapController.OnPointerClick`)을 가로채지 않도록 꺼둠
- `MinimapViewIndicator`: `minimapRect`/`minimapCamera`는 `MinimapController`와 동일한 참조, `mainCamera`는 Main Camera의 `Camera` 컴포넌트

## 요약 / 영향받는 파일
- `Assets/Scripts/Camera/MinimapViewIndicator.cs` (신규)
- `Assets/Scripts/Camera/MinimapViewIndicator.cs.meta` (신규)
- `Docs/MinimapViewIndicator.md` (신규, 레퍼런스 문서)
- `Assets/Scenes/TestScene.unity` — `MiniMap_image` 자식으로 `ViewSquare`(RectTransform/Image/CanvasRenderer/MinimapViewIndicator) 추가

## 비고
[[confirm_before_implementing]] — 사용자가 스타일(반투명 채워진 사각형)을 확인해준 뒤 바로 구현. 씬 파일은 에디터가 아니라 YAML을 직접 편집해 추가했으므로, 유니티 에디터에서 열어 `ViewSquare`가 인스펙터에 정상적으로 잡히는지(특히 스크립트 참조 4개) 한 번 확인 필요.
