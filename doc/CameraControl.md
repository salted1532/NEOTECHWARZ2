# CameraControl

`Assets/Scripts/Camera/CameraControl.cs`

## 개요

RTS 스타일 탑다운 카메라 컨트롤러. 방향키/화면 가장자리 이동, 마우스 휠 줌, 맵 경계 제한, Space로 본진 복귀 기능을 제공한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `moveSpeed`, `edgeSize`, `smoothTime` | `float` | 이동 속도, 화면 가장자리 감지 두께, 이동 보간 속도 |
| `minX/maxX/minZ/maxZ` | `float` | 맵 경계값 |
| `zoomSpeed`, `minZoom`, `maxZoom` | `float` | 줌 속도 및 orthographic 크기 제한 |
| `cam` | `Camera` | 이 오브젝트의 카메라 컴포넌트 |
| `targetPosition` | `Vector3` | 카메라가 보간으로 따라갈 목표 위치 |
| `mainBasePosition` | `Vector3` | 시작 위치(=본진 위치). Space 키로 복귀할 좌표 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 카메라 컴포넌트 캐싱, 시작 위치를 `targetPosition`이자 `mainBasePosition`으로 저장, 커서를 화면 내로 고정(`CursorLockMode.Confined`) |
| `Update()` | 매 프레임 `HandleMovement()` + `HandleZoom()` 호출 |
| `LateUpdate()` | 실제 이동은 여기서 `targetPosition`으로 `Vector3.Lerp` 보간해 카메라가 부드럽게 따라가도록 함 |
| `HandleMovement()` (private) | 방향키 입력 + 화면 가장자리 마우스 위치 + Space(본진 복귀)를 종합해 `targetPosition`을 갱신하고 맵 경계값 안으로 클램프 |
| `HandleZoom()` (private) | 마우스 휠 입력으로 orthographic 카메라의 확대/축소 크기를 조절하고 `minZoom`~`maxZoom` 범위로 클램프 |

## 연관 컴포넌트

- 독립적으로 동작하며 다른 스크립트와 직접적인 의존 관계는 없음 (입력을 직접 폴링)
