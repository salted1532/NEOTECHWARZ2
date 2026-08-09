# 0506 - 16:9 외 화면 비율에서 UI가 조금씩 깨지는 문제 조사

## 날짜
2026-08-09

## 요청 내용
"화면 비율이 16:9일때만 정상적으로 작동하고 나머지 비율일땐 조금씩 깨지는데 이걸 해결하려면
어떤식으로 하는게 좋을까?" — 코드 변경 요청이 아니라 해결 방향에 대한 자문 요청.

## 조사 내용
`CanvasScaler`, 카메라, 미니맵 관련 설정을 프로젝트 전역에서 확인.

1. **CanvasScaler는 이미 대부분 정리돼 있음** — `MainScene.unity`, `Missions/MissionSelect.unity`,
   `prefabs/Game/GameManager.prefab`(인게임 HUD) 세 곳 모두 `Scale With Screen Size`,
   기준 해상도 1920x1080, `Match Width Or Height 0.5`로 통일(doc/0500). 즉 화면 비율이 달라져도
   UI 전체가 균일하게 확대/축소되는 기반 자체는 이미 갖춰져 있어 원인이 아님.
   - 예외: `prefabs/UI/HealthBar.prefab`, `prefabs/Capture_Point/Capture_Point.prefab`는
     `Constant Pixel Size`(800x600)로 남아있지만 두 캔버스 모두 `m_RenderMode: 2`(World Space)라서
     CanvasScaler의 UI Scale Mode 자체가 적용되지 않음 — 화면 비율 문제와 무관.

2. **메인 게임 카메라**(`CameraControl.cs` 부착) — Perspective, `field of view: 60`(수직 기준),
   `m_NormalizedViewPortRect`는 0~1 정규화값. Unity 기본 동작상 화면이 가로로 넓어지면(예: 21:9)
   수직 FOV는 그대로 두고 좌우로 더 넓게 보이고, 좁아지면(예: 4:3) 좌우가 덜 보임 — 이 자체는
   버그가 아니라 원근 카메라의 정상 특성. 다만 맵 이동 경계(`minX/maxX/minZ/maxZ`, 16:9 기준으로
   튜닝된 값)를 벗어난 영역까지 보이게 되면 초광각 비율에서 맵 바깥(빈 공간)이 드러나 보일 수 있음
   — 이건 실제로 "깨져 보이는" 원인이 될 수 있는 부분.

3. **개별 HUD 요소의 Anchor 설정 — 가장 유력한 원인**: CanvasScaler가 화면 전체 스케일은 맞춰줘도,
   각 UI 요소가 화면의 어느 지점에 "붙어야" 하는지는 RectTransform의 Anchor가 결정한다. 에디터
   Game 뷰가 보통 16:9로 맞춰진 상태에서 눈대중으로 위치를 잡은 요소는, Anchor가 실제 배치 의도
   (예: 화면 우측 하단 고정)와 다르게 중앙 등으로 남아있으면 16:9에서는 우연히 맞아 보이다가 다른
   비율에서 좌우/상하로 밀려 보인다. "16:9만 정상, 나머지는 조금씩 깨짐" 증상과 가장 일치.

이번 세션에서는 Unity 에디터가 열려있는 것을 확인(`MainScene`)했으나, 실제로 깨지는 요소를 특정하려면
Game 뷰에서 여러 비율로 전환해 스크린샷을 비교하는 과정이 필요해 이번 조사에서는 원인 후보를
좁히는 선까지만 진행함.

## 권장 해결 순서 (코드 변경 없음, 자문)
1. Unity Game 뷰의 해상도 드롭다운에 4:3 / 16:10 / 21:9 등 커스텀 비율을 추가해서 실제로 어떤
   요소가 어떻게 밀리는지 먼저 스크린샷으로 재현 — 감으로 고치면 다른 요소를 놓치기 쉬움.
2. 깨지는 요소마다 RectTransform Anchor를 "화면 어디에 붙어야 하는가"에 맞게 재설정
   (구석 고정 요소는 해당 구석 Anchor + `anchoredPosition`을 오프셋으로, 중앙 요소는 (0.5, 0.5)).
   CanvasScaler를 더 손대는 방향이 아니라 이쪽이 실제 수정 지점일 가능성이 높음.
3. 위 조치 후에도 초광각 비율에서 맵 바깥이 보이는 문제가 남으면, 그때 `CameraControl`의 맵 경계값을
   `cam.aspect` 기준으로 살짝 보정하는 것을 검토(현재는 불필요할 수도 있어 선행 확인 필요).

## 요약/영향받는 파일
- 코드 변경 없음 (조사 및 자문만 진행).
- 참고한 파일: `Assets/Scenes/MainScene/MainScene.unity`, `Assets/Scenes/Missions/MissionSelect.unity`,
  `Assets/prefabs/Game/GameManager.prefab`, `Assets/prefabs/UI/HealthBar.prefab`,
  `Assets/prefabs/Capture_Point/Capture_Point.prefab`, `Assets/Scripts/Camera/CameraControl.cs`,
  `Assets/Scripts/Camera/MinimapController.cs`.
- 다음 단계(사용자 확인 시): 실제로 깨지는 요소 스크린샷 확보 → 구체적 Anchor 수정안을 doc/NNNN으로
  제안 후 승인 받아 적용.
