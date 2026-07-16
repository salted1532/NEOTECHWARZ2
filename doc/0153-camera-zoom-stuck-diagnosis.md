# 0153. Main Camera 줌이 안 되는 원인 진단

## 날짜
2026-07-16

## 요청
"현재 Testscene에서 main카메라 x 로테이션을 50정도로 하고 줌값도 조금 조정했는데 아예 줌이 안되는데 왜그런거야?"

## 조사
`Assets/Scripts/Camera/CameraControl.cs`의 `HandleZoom()`(라인 122-137)은 마우스 휠 입력으로 계산한 다음 높이(`nextY`)가 `minZoom~maxZoom` 범위를 벗어나면 이동 자체를 **취소(return)**하는 방식으로 구현되어 있다 (clamp가 아니라 reject).

`Assets/Scenes/TestScene.unity`에서 확인한 실제 값:
- Main Camera Transform: `m_LocalEulerAnglesHint: {x: 50, y: 0, z: 0}`, `m_LocalPosition.y: 30`
- `CameraControl` 컴포넌트: `minZoom: 8`, `maxZoom: 20`, `zoomSpeed: 25`

즉 X 로테이션을 50°로 조정하면서 카메라 Y 위치도 30으로 옮겼는데, `maxZoom`(20)은 그대로 두어 **현재 위치 자체가 이미 허용 범위 밖**에 있다. `HandleZoom()`은 범위를 벗어나는 이동을 전부 거부하기 때문에, 시작 지점이 이미 범위 밖이면 어느 방향으로 스크롤해도 계산되는 `nextY`가 계속 범위 밖이라 한 걸음도 움직이지 못하고 영구히 멈춘 것처럼 보인다.

## 결론 / 처리
사용자에게 두 가지 옵션을 제시:
1. `HandleZoom()`을 clamp 방식으로 고치고 `maxZoom`을 30 이상으로 올리는 근본 수정
2. 그대로 둠

**사용자가 "줌은 해결해서 조정하지 않아도 된다"고 답변** — 코드/씬 변경 없음. 순수 진단만 하고 종료.

## 영향받는 파일
없음 (코드 변경 없음)

## 비고
[[confirm_before_implementing]] — 사용자가 수정을 원치 않아 제안 단계에서 종료. 참고로 이 근본 원인(범위 밖 시작 위치에서 reject 방식 때문에 영구히 막히는 구조)은 나중에 카메라를 다시 수동으로 옮기면 동일하게 재발할 수 있음.
