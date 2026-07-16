# MinimapViewIndicator

`Assets/Scripts/Camera/MinimapViewIndicator.cs`

## 개요

미니맵 위에 메인 카메라가 실제로 보고 있는 지면 영역을 반투명 사각형으로 표시한다. 메인 카메라 화면의 네 꼭짓점을 지면(Y=0)에 투영해 실제 시야 영역을 구하고, 그 지점들을 미니맵 카메라 기준으로 역투영해서 미니맵 UI 위의 위치/크기로 변환한다. 줌(카메라 높이)이나 Q/E 회전이 바뀌어도 매 프레임 실제 시야를 그대로 반영하므로 별도 보정이 필요 없다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `viewIndicator` | `RectTransform` (SerializeField) | 크기/위치를 갱신할 사각형의 RectTransform (보통 자기 자신) |
| `minimapRect` | `RectTransform` (SerializeField) | 미니맵 RawImage의 RectTransform. `MinimapController.minimapRect`와 동일한 오브젝트를 참조해야 좌표계가 맞음 |
| `minimapCamera` | `Camera` (SerializeField) | 미니맵을 렌더링하는 카메라 |
| `mainCamera` | `Camera` (SerializeField) | 시야를 표시할 대상 카메라 (Main Camera) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Update()` (private) | 메인 카메라의 화면 네 꼭짓점(0,0)/(1,0)/(0,1)/(1,1)을 `ViewportPointToRay`로 지면(Y=0) 평면과 교차시켜 실제 시야의 지면 좌표 4개를 구함 → 각 지점을 `minimapCamera.WorldToViewportPoint`로 미니맵 카메라 기준 뷰포트 좌표로 역투영 → `minimapRect.rect` 기준 로컬 좌표로 변환 → 4점의 axis-aligned 바운딩 박스를 `viewIndicator.sizeDelta`/`anchoredPosition`에 반영. 네 꼭짓점 중 하나라도 지면과 만나지 않으면(카메라가 하늘 쪽을 보는 극단적인 각도 등) 그 프레임은 갱신을 건너뜀 |

## 연관 컴포넌트

- **MinimapController**: 같은 `minimapRect`/`minimapCamera`를 공유. `MinimapController`는 미니맵 클릭 → 월드 좌표 변환(정방향의 반대 방향 매핑)을 담당하고, `MinimapViewIndicator`는 메인 카메라 시야 → 미니맵 좌표 변환(같은 매핑을 반대로 사용)을 담당
- **CameraControl**: 줌/회전으로 메인 카메라의 위치·각도가 바뀌면 `Update()`가 매 프레임 이를 반영해 사각형 크기/위치가 자동으로 따라감 (별도 이벤트 연동 불필요)

## TestScene 배치

`MiniMap_image`(RawImage)의 자식 `ViewSquare`에 `Image`(흰색, alpha 0.18, `raycastTarget: 0`)와 함께 붙어 있다. `raycastTarget`을 꺼둔 이유는, 이 사각형이 `MiniMap_image` 위에 겹쳐 있어 켜두면 미니맵 클릭(`MinimapController.OnPointerClick`)을 가로채 클릭 이동이 먹통이 되기 때문이다.
