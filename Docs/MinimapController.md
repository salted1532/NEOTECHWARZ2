# MinimapController

`Assets/Scripts/Camera/MinimapController.cs`

## 개요

미니맵 이미지(RawImage)를 클릭/드래그하면 그 지점의 월드 좌표를 계산해 메인 카메라를 이동시킨다. 미니맵 카메라가 실제로 그 픽셀에 무엇을 그렸는지 `ViewportPointToRay`로 그대로 역산하므로, 미니맵 카메라의 위치/각도/투영 방식이 바뀌어도 별도 보정 없이 항상 정확하다. `IPointerClickHandler`/`IDragHandler`를 구현해 uGUI 이벤트로 클릭/드래그를 모두 처리한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `minimapRect` | `RectTransform` (SerializeField) | RawImage의 RectTransform (보통 자기 자신) |
| `minimapCamera` | `Camera` (SerializeField) | 미니맵을 렌더링하는 카메라 |
| `mainCameraControl` | `CameraControl` (SerializeField) | 실제로 이동시킬 메인 카메라 컨트롤러 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnPointerClick(eventData)` / `OnDrag(eventData)` | 둘 다 `MoveCameraToPointer`로 위임 (클릭과 드래그를 동일하게 취급) |
| `MoveCameraToPointer(eventData)` (private) | 화면 좌표를 미니맵 RectTransform의 로컬 좌표로 변환(Screen Space - Overlay 캔버스 기준, 카메라 인자는 null) → 0~1 정규화된 UV(u, v) 계산 → 미니맵 카메라의 `ViewportPointToRay(u, v)`로 지면(Y=0) 평면과의 교차점을 구함 → `groundPoint.z -= 20f` 보정(미니맵 카메라 위치/각도로 인한 오차 보정으로 보임) 후 `mainCameraControl.JumpToWorldXZ(groundPoint)` 호출 |

## 연관 컴포넌트

- **CameraControl**: `JumpToWorldXZ(worldPoint)`를 호출해 실제 카메라 이동을 위임받음
