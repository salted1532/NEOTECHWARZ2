# 미니맵 클릭 → 메인 카메라 이동 설계

작성일: 2026-07-04
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

미니맵 카메라의 렌더 결과를 보여주는 `MiniMap_image`를 클릭(혹은 드래그)하면, 그 클릭 지점이
미니맵 카메라 기준으로 어느 월드 좌표에 해당하는지 계산해서 `CameraControl`(메인 카메라)을
그 위치로 이동시킨다.

## 2. 현재 상태 확인

- 씬에 `EventSystem`과 `GraphicRaycaster`가 이미 있고, Canvas는 `m_RenderMode: 0`
  (**Screen Space - Overlay**)로 설정돼 있다. → UI 클릭 좌표 변환 시 카메라 인자를 `null`로
  넘겨야 하는 조합이다 (Overlay 모드에서는 `RectTransformUtility` 계열 API에 카메라를 넘기면 안 됨).
- 씬에 원래 있던 `MiniMap`이라는 오브젝트는 `UnityEngine.UI.Image` + 고정 스프라이트로 돼 있던
  자리표시자(placeholder)로 보인다. 미니맵 카메라의 RenderTexture(`Assets/images/MiniMap/MiniMap.renderTexture`)를
  실시간으로 띄우려면 `Image`가 아니라 **`RawImage`**여야 한다 (`Image`는 `Sprite`만 표시 가능하고
  `RenderTexture`를 직접 못 띄움). 방금 만든 `MiniMap_image`가 `RawImage` + 해당 `RenderTexture`
  조합인지 먼저 확인 필요.
- `CameraControl.cs`(메인 카메라)는 이미 `targetPosition`(private)을 향해 `LateUpdate()`에서
  매 프레임 `Vector3.Lerp`로 부드럽게 따라가는 구조이고, `minX/maxX/minZ/maxZ`로 맵 경계 밖으로
  못 나가게 클램프하고 있다. Space 키로 본진 복귀할 때도 이 `targetPosition`만 바꿔주면 나머지는
  알아서 처리되는 구조라, 미니맵 클릭도 같은 방식(=`targetPosition`만 갱신)으로 얹으면 기존 이동/줌/회전과
  자연스럽게 공존한다.

## 3. 좌표 변환 방법 두 가지

### 방법 A — 미니맵 카메라로 실제 레이를 쏴서 지면과 교차시키기 (권장)

미니맵 카메라가 "실제로 그 픽셀에 무엇을 그렸는지"를 그대로 역산하는 방법. 미니맵 카메라의
위치/회전/투영(Orthographic 여부, 크기)이 나중에 바뀌어도 코드 변경 없이 항상 정확하다.

1. 클릭된 화면 좌표를 `RawImage`의 RectTransform 로컬 좌표로 변환
   (`RectTransformUtility.ScreenPointToLocalPointInRectangle`, Overlay 캔버스라 camera 인자는 `null`).
2. 로컬 좌표를 그 RectTransform의 `rect` 크기 기준으로 0~1 정규화 (u, v).
3. `minimapCamera.ViewportPointToRay(new Vector3(u, v, 0))`로 레이를 생성.
4. 그 레이를 지면(`Plane(Vector3.up, Vector3.zero)`, `CameraControl.GetScreenCenterGroundPoint()`에서
   이미 쓰던 것과 동일한 방식)과 교차시켜 월드 좌표(X, Z)를 얻음.
5. `CameraControl`에 새로 추가한 공개 메서드(`JumpToWorldXZ(Vector3 point)` 등)를 호출해
   `targetPosition.x/z`만 그 값으로 바꾸고 `y`(줌 높이)는 그대로 둔다.

장점: 미니맵 카메라의 실제 시야를 그대로 반영하므로 미니맵 카메라가 완전한 정면 직교(Orthographic
Top-Down)가 아니어도(약간 기울어져 있어도) 항상 클릭한 지점과 정확히 일치한다.

### 방법 B — 맵 경계값으로 선형 매핑 (더 간단하지만 조건부)

`CameraControl.minX/maxX/minZ/maxZ`(이미 존재하는 맵 경계 상수)와 미니맵 이미지의 UI 사각형을
1:1로 대응시켜 (u, v) → (world X, world Z)를 단순 보간으로 계산.

```
worldX = Mathf.Lerp(minX, maxX, u)
worldZ = Mathf.Lerp(minZ, maxZ, v)
```

장점: 레이캐스트/카메라 계산이 필요 없어 구현이 매우 짧다.
단점: **미니맵 카메라가 정확히 orthographic + 완전 수직(Top-Down)이고, 그 시야 범위가
`minX~maxX, minZ~maxZ`와 정확히 일치할 때만** 정확하다. 미니맵 카메라 시야를 나중에 조금만
조정해도(예: 여백을 더 준다거나) 클릭 위치가 어긋나기 시작하고, 이 어긋남을 코드가 아니라
"미니맵 카메라 세팅"으로 계속 맞춰줘야 해서 두 시스템이 암묵적으로 묶여버린다.

→ **방법 A를 권장**. 코드량 차이가 크지 않고, 미니맵 카메라를 이후에 자유롭게 조정해도 안전하다.

## 4. 구체적인 변경 계획

### 4-1. `CameraControl.cs`에 이동 진입점 추가

`targetPosition`이 private이라 외부에서 직접 못 건드리므로, 아래처럼 공개 메서드 하나만 추가한다
(맵 경계 클램프도 기존 `HandleMovement()`와 동일한 로직을 재사용해서 안전하게 처리).

```csharp
// 미니맵 클릭 등 외부에서 특정 지면 좌표로 카메라를 이동시킬 때 사용.
// 높이(Y, 줌 상태)와 회전은 그대로 유지하고 X/Z만 바꾼다.
public void JumpToWorldXZ(Vector3 worldPoint)
{
    targetPosition.x = Mathf.Clamp(worldPoint.x, minX, maxX);
    targetPosition.z = Mathf.Clamp(worldPoint.z, minZ, maxZ);
}
```

### 4-2. 새 스크립트 `MinimapController.cs` (예: `Assets/Scripts/UI/MinimapController.cs`)

`MiniMap_image`(RawImage) 오브젝트에 붙여서 클릭/드래그를 직접 받는다.

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

- `OnPointerClick` = 한 번 클릭 시 즉시 이동. `OnDrag` = 누른 채로 끌면 계속 따라가며 스크럽
  (RTS에서 흔한 미니맵 조작 방식). 둘 다 같은 계산을 타므로 한 메서드로 묶음.
- Overlay 캔버스이므로 `ScreenPointToLocalPointInRectangle`의 카메라 인자는 반드시 `null`.

## 5. 씬/에디터 설정 체크리스트

- [ ] `MiniMap_image`가 `Image`가 아니라 **`RawImage`**이고, `texture`에 미니맵 카메라의
      `RenderTexture`가 연결돼 있는지 확인.
- [ ] `RawImage`의 `Raycast Target`이 켜져 있는지 확인 (꺼져 있으면 클릭 이벤트 자체가 안 옴).
- [ ] `MinimapController` 컴포넌트를 `MiniMap_image`에 붙이고 `minimapRect`(보통 자기 자신),
      `minimapCamera`, `mainCameraControl`(메인 카메라의 `CameraControl`) 3개를 인스펙터에서 연결.
- [ ] `EventSystem`/`GraphicRaycaster`는 이미 씬에 있으므로 추가 작업 불필요.
- [ ] 미니맵 카메라가 지면을 완전히 덮도록 배치돼 있는지 확인 (안 덮는 영역을 클릭하면
      레이가 지면과 안 만나거나 맵 밖 좌표가 나올 수 있음 → `JumpToWorldXZ`의 클램프로 방어는 됨).

## 6. 열린 질문 / 가정

- **클릭 vs 드래그**: 위 설계는 좌클릭 클릭·드래그 모두 카메라 이동으로 가정했다. 일부 RTS는
  미니맵 좌클릭 = 카메라 이동, 미니맵 우클릭(드래그) = 선택 유닛 이동 명령으로 나누기도 하는데,
  이번 요청 범위는 "카메라 이동"만이므로 우클릭 명령 쪽은 다루지 않았다.
- **줌/회전 유지**: `JumpToWorldXZ`는 X/Z만 바꾸고 현재 카메라 높이(줌)와 Q/E 회전 상태는 그대로
  유지한다고 가정했다. 미니맵 클릭 시 항상 기본 줌/회전으로 리셋하고 싶다면 별도로 알려주면
  `Space`(본진 복귀)처럼 전체 리셋하는 버전으로 바꿀 수 있다.
- **미니맵 카메라 방식**: 미니맵 카메라가 완전한 Orthographic Top-Down이 아니라 약간의 원근/각도가
  있어도 방법 A는 그대로 동작한다. 다만 지면이 평평(Y=0 평면)하다는 전제이므로, 언덕/고저차가
  있는 지형이라면 `Plane(Vector3.up, Vector3.zero)` 대신 실제 지형 콜라이더에 레이캐스트하는 편이
  더 정확하다 (`UserControl.cs`에 이미 있는 `layerGround` 레이캐스트 패턴 재사용 가능).
