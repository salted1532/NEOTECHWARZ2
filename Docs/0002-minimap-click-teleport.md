# 0002. 미니맵 클릭 → 카메라 순간이동

## 날짜
2026-07-06

## 요청
지금 미니맵 클릭이 카메라 위치 이동하는게 아니라 바로 그 위치로 순간이동 하는거로 바꿔줘

## 답변 / 변경사항
- `Assets/Scripts/Camera/CameraControl.cs`의 `JumpToWorldXZ(Vector3 worldPoint)`는 원래 `targetPosition`만 갱신해서 `LateUpdate()`의 `Vector3.Lerp` 보간을 통해 서서히 이동하는 구조였음.
- `targetPosition`을 갱신하는 것과 동시에 `transform.position`의 X/Z도 즉시 대입하도록 수정해서, 미니맵 클릭 시 카메라가 그 자리로 바로 순간이동하도록 변경.
- 높이(Y, 줌 상태)와 회전은 그대로 유지.
- 방향키 이동, Space(본진 복귀) 등 다른 카메라 이동은 기존처럼 부드러운 보간(Lerp) 그대로 유지 — `JumpToWorldXZ` 호출 경로(미니맵 클릭)에만 영향.

## 변경 파일
- `Assets/Scripts/Camera/CameraControl.cs`
