# CameraControl

`Assets/Scripts/Camera/CameraControl.cs`

## 개요

RTS 스타일 탑다운 카메라 컨트롤러. 방향키/화면 가장자리 이동, 마우스 휠 줌, Q/E 화면 중앙 기준 궤도 회전, 맵 경계 제한, Space로 본진 복귀 기능을 제공한다. 미니맵 클릭으로 특정 좌표로 즉시 이동하는 진입점(`JumpToWorldXZ`)도 갖고 있다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `moveSpeed`, `edgeSize`, `smoothTime` | `float` | 이동 속도, 화면 가장자리 감지 두께, 이동 보간 속도 |
| `minX/maxX/minZ/maxZ` | `float` | 맵 경계값 |
| `zoomSpeed`, `minZoom`, `maxZoom` | `float` | 줌 속도, 카메라 높이(Y) 하한/상한 (너무 가깝게/멀리 못 가게) |
| `rotateSpeed` | `float` | Q/E 회전 속도(초당 각도) |
| `maxRotationAngle` | `float` | 기준(정면) 각도에서 좌우로 돌 수 있는 최대 각도 |
| `returnSpeed` | `float` | Q/E를 뗐을 때 기준 각도로 되돌아가는 속도(초당 각도) |
| `cam` | `Camera` | 이 오브젝트의 카메라 컴포넌트 |
| `targetPosition` | `Vector3` | 카메라가 보간으로 따라갈 목표 위치 |
| `mainBasePosition` | `Vector3` | 시작 위치(=본진 위치). Space 키로 복귀할 좌표 |
| `currentRotationAngle` | `float` | 기준(정면) 각도로부터 현재까지 회전한 누적 각도 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 카메라 컴포넌트 캐싱, 시작 위치를 `targetPosition`이자 `mainBasePosition`으로 저장, 커서를 화면 내로 고정(`CursorLockMode.Confined`) |
| `Update()` | 매 프레임 `HandleMovement()` + `HandleZoom()` + `HandleRotate()` 호출 |
| `LateUpdate()` | 실제 이동은 여기서 `targetPosition`으로 `Vector3.Lerp` 보간해 카메라가 부드럽게 따라가도록 함 |
| `HandleMovement()` (private) | 카메라의 현재 회전을 수평면에 투영한 축(고정 월드축이 아님 - Q/E로 화면을 돌려도 "위/오른쪽"이 항상 화면상 위/오른쪽) 기준으로, 방향키 입력 + 화면 가장자리 마우스 위치 + Space(본진 복귀)를 종합해 `targetPosition`을 갱신하고 맵 경계값 안으로 클램프 |
| `HandleZoom()` (private) | 이 카메라는 Perspective라 `orthographicSize`가 효과 없으므로, 마우스 휠 입력으로 카메라가 바라보는 방향(forward)을 따라 위치를 직접 앞뒤로 옮겨서 확대/축소를 구현. 높이(Y) 기준 `minZoom`~`maxZoom` 범위로 제한 |
| `HandleRotate()` (private) | Q/E 입력으로 화면 중앙이 바라보는 지점(`GetScreenCenterGroundPoint`)을 축으로 카메라를 좌우 궤도 회전시킨다(달이 지구를 도는 것처럼 위치와 시선 방향을 함께 회전). 기준 각도에서 ±`maxRotationAngle`까지만 돌 수 있고, 입력이 없으면 `returnSpeed`로 서서히 기준 각도(0)로 복귀 |
| `JumpToWorldXZ(worldPoint)` | 미니맵 클릭 등 외부에서 특정 지면 좌표로 카메라를 이동시킬 때 사용. 높이(Y, 줌 상태)와 회전은 그대로 유지하고 X/Z만 맵 경계로 클램프해 변경하되, `LateUpdate`의 Lerp 보간 없이 `transform.position`을 즉시 순간이동시킴 |
| `GetScreenCenterGroundPoint()` (private) | 화면 정중앙 레이가 지면(Y = 0 평면)과 만나는 지점을 구한다 (Q/E 회전 축 pivot으로 사용). 평면과 만나지 않으면 현재 `targetPosition`을 그대로 반환 |

## 연관 컴포넌트

- **MinimapController**: 미니맵 클릭/드래그 시 `JumpToWorldXZ(groundPoint)`를 호출해 카메라를 그 지점으로 이동시킴
