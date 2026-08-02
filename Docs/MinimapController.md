# MinimapController

`Assets/Scripts/Camera/MinimapController.cs`

## 개요

미니맵 이미지(RawImage) 클릭/드래그를 처리한다. `IPointerClickHandler`/`IDragHandler`를 구현해 uGUI
이벤트로 클릭/드래그를 모두 처리하며, 미니맵 카메라가 실제로 그 픽셀에 무엇을 그렸는지
`ViewportPointToRay`로 그대로 역산하므로 미니맵 카메라의 위치/각도/투영 방식이 바뀌어도 별도 보정
없이 항상 정확하다.

- **좌클릭**: 대기 중인 명령(A공격 등)이 있으면 그 지점에 확정, 없으면 메인 카메라를 그 지점으로 이동
  (기존 동작).
- **우클릭**: 선택된 유닛/건물에 "그냥 우클릭"(메인 화면 우클릭)과 동일한 명령(이동/랠리)을 내린다(doc/0349).
- **드래그**: 좌클릭으로 카메라를 계속 따라가게 함(대기 중인 명령은 확정하지 않음 — 실수로 드래그하다
  명령이 나가는 것 방지).

지형 높이 계산은 실제 지형 콜라이더에 레이캐스트해서 구한다(메인 화면 클릭과 동일한 방식, `layerGround`
공유) — 예전엔 Y=0 평면과의 수학적 교차점만 썼는데, 지형이 정확히 Y=0이 아닌 경사로/언덕에서는 마커가
땅속에 파묻히거나 허공에 뜨는 버그가 있어서 수정됨(doc/0355). 지형 콜라이더를 못 맞춘 경우(맵 밖 등)엔
기존 Y=0 평면 계산으로 폴백.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `minimapRect` | `RectTransform` (SerializeField) | RawImage의 RectTransform (보통 자기 자신) |
| `minimapCamera` | `Camera` (SerializeField) | 미니맵을 렌더링하는 카메라 |
| `mainCameraControl` | `CameraControl` (SerializeField) | 실제로 이동시킬 메인 카메라 컨트롤러 |
| `userControl` | `UserControl` (SerializeField) | 대기 중인 명령 확인/확정, 우클릭 명령 위임, 지형 레이어마스크(`GroundLayerMask`) 조회 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnPointerClick(eventData)` | 좌클릭: 대기 중인 명령이 있으면 `userControl.ConfirmPendingOrderAt(groundPoint)`, 없으면 `mainCameraControl.JumpToWorldXZ`. 우클릭: `userControl.IssueRightClickMoveAt(groundPoint)` |
| `OnDrag(eventData)` | 좌클릭 드래그만 처리(카메라 계속 이동), 우클릭 드래그는 무시 |
| `TryGetGroundPoint(eventData, out groundPoint)` (private) | 화면 좌표 → 미니맵 로컬 좌표(Screen Space - Overlay 기준, 카메라 인자 null) → 0~1 UV → 미니맵 카메라의 `ViewportPointToRay` → 실제 지형 콜라이더(`userControl.GroundLayerMask`)에 레이캐스트, 실패 시 Y=0 평면 교차로 폴백 |

## 연관 컴포넌트

- **CameraControl**: `JumpToWorldXZ(worldPoint)`를 호출해 실제 카메라 이동을 위임받음
- **UserControl**: `HasPendingGroundOrder()`/`ConfirmPendingOrderAt()`/`IssueRightClickMoveAt()`/`GroundLayerMask`로 명령 확정·실행·지형 레이어를 공유
- **MinimapAlertController**: 별개 컴포넌트지만 같은 미니맵 RawImage 오브젝트(`MiniMap_image`)에 부착됨
