# 0027 - 미니맵 클릭 → 메인 카메라 이동 (설계 → 실제 구현)

> **번호에 대한 메모**: 이 문서는 원래 번호 없이 `doc/minimap-click-to-move-design.md`로
> 2026-07-04에 작성된 "코드 수정 전, 설계만 정리한 검토 문서"다. `Docs/0001~`류 세션 로그 번호
> 규칙(0013에서 수립)보다 먼저 작성되어 원래 몇 번째 세션이었는지 알 수 없다 — 사용자 요청에 따라
> 세션 로그를 전부 `doc/` 폴더로 옮기며 임의로 0027번을 부여했다(실제 작성일은 0001~0024보다 이름).
>
> 이후 실제로 구현이 완료되었고, 그 중 "Lerp 보간 → 즉시 순간이동"으로 바뀐 부분은 정확히
> **[0002-minimap-click-teleport.md](0002-minimap-click-teleport.md)** 세션에서 이뤄진 변경과 일치한다.
> 아래는 **설계 당시 제안했던 코드(기존 코드)** → **실제로 구현된 현재 코드(변경 코드)** 형식으로
> 다시 정리했다.

## 1. `CameraControl.cs` — 외부에서 카메라를 특정 좌표로 이동시키는 진입점

**기존 코드 (설계 당시 제안)**
```csharp
// 미니맵 클릭 등 외부에서 특정 지면 좌표로 카메라를 이동시킬 때 사용.
// 높이(Y, 줌 상태)와 회전은 그대로 유지하고 X/Z만 바꾼다.
public void JumpToWorldXZ(Vector3 worldPoint)
{
    targetPosition.x = Mathf.Clamp(worldPoint.x, minX, maxX);
    targetPosition.z = Mathf.Clamp(worldPoint.z, minZ, maxZ);
}
```
(`targetPosition`만 바꾸고, 실제 이동은 기존 `LateUpdate()`의 `Vector3.Lerp` 보간에 맡기는 방식 — 즉 부드럽게 스크롤되며 도착)

**변경 코드 (실제 구현)**
```csharp
// 미니맵 클릭 등 외부에서 특정 지면 좌표로 카메라를 이동시킬 때 사용.
// 높이(Y, 줌 상태)와 회전은 그대로 유지하고 X/Z만 바꾸되, LateUpdate의 Lerp 보간 없이 즉시 순간이동한다.
public void JumpToWorldXZ(Vector3 worldPoint)
{
    targetPosition.x = Mathf.Clamp(worldPoint.x, minX, maxX);
    targetPosition.z = Mathf.Clamp(worldPoint.z, minZ, maxZ);

    transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
}
```

**차이점**
- 설계안은 `targetPosition`만 갱신해 `LateUpdate()`의 보간(Lerp)에 맡기려 했다. 처음엔 이 설계안 그대로 구현됐던 것으로 보이나, 이후 **[0002-minimap-click-teleport.md](0002-minimap-click-teleport.md)** 요청("미니맵 클릭이 서서히 이동하는게 아니라 바로 순간이동하도록 바꿔줘")으로 `transform.position`도 함께 즉시 갱신해 **순간이동**하도록 바뀌었다.

## 2. `MinimapController.cs` — 미니맵 클릭/드래그 처리

**기존 코드 (설계 당시 제안)**
```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 미니맵 이미지를 클릭/드래그하면 그 지점의 월드 좌표를 계산해 메인 카메라를 이동시킨다.
public class MinimapController : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    [SerializeField] private RectTransform minimapRect; // RawImage의 RectTransform (보통 자기 자신)
    [SerializeField] private Camera minimapCamera;       // 미니맵을 렌더링하는 카메라
    [SerializeField] private CameraControl mainCameraControl;

    public void OnPointerClick(PointerEventData eventData) => MoveCameraToPointer(eventData);
    public void OnDrag(PointerEventData eventData) => MoveCameraToPointer(eventData);

    private void MoveCameraToPointer(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect, eventData.position, null, out Vector2 localPoint))
            return;

        Rect rect = minimapRect.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        Ray ray = minimapCamera.ViewportPointToRay(new Vector3(u, v, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
            mainCameraControl.JumpToWorldXZ(ray.GetPoint(distance));
    }
}
```

**변경 코드 (실제 구현)**
```csharp
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
```

**차이점**
- `using UnityEngine.UI;`는 실제로 필요 없어 제거됨(`RawImage` 타입을 직접 참조하지 않고 `RectTransform`만 사용).
- 계산된 지면 좌표에 `groundPoint.z -= 20f` 보정이 추가됨 — 메인 카메라가 원근(Perspective)이라 카메라가 바라보는 지점과 카메라 자신의 X/Z 위치 사이에 오프셋이 있어서, 클릭한 지점이 화면 중앙에 오도록 보정한 것으로 보인다 (설계 당시에는 이 보정 필요성을 예상하지 못했음).
- 좌표 변환 방식은 설계안의 "방법 A(미니맵 카메라로 실제 레이를 쏴서 지면과 교차)"가 그대로 채택됨 — "방법 B(선형 매핑)"는 사용되지 않음.

## 3. 설계 문서가 남긴 열린 질문에 대한 실제 결론
- **클릭 vs 드래그**: 설계안대로 `OnPointerClick`/`OnDrag` 둘 다 카메라 이동으로 구현됨.
- **줌/회전 유지**: 설계안대로 `JumpToWorldXZ`는 X/Z만 바꾸고 카메라 높이(줌)·회전은 그대로 유지.
- **지형 고저차**: 여전히 `Plane(Vector3.up, Vector3.zero)`(평면 Y=0) 기준으로 계산 — 실제 지형 콜라이더 레이캐스트로 바꾸는 개선은 아직 적용되지 않음.
