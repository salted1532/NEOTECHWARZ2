# 0340. 해상도에 따라 UI가 일그러지는 문제 수정 (CanvasScaler)

**날짜:** 2026-07-31

## 요청

> 전체적인 UI들이 화면 해상도에 따라 일그러지는거좀 수정해줘 현재 canvas상에서 배치된 그 위치를
> 기준으로 해상도에 맞게 비율 조절되도록 만들어줘

## 원인

화면에 그려지는(Screen Space - Overlay) `Canvas` 2곳 — `GameManager.prefab`의 메인 HUD 캔버스
(TestScene/SampleScene이 공유), `MainScene.unity`의 메인 메뉴 캔버스 — 둘 다 `CanvasScaler`의
`UI Scale Mode`가 **`Constant Pixel Size`**(`m_UiScaleMode: 0`)로 되어 있었음. 이 모드는 화면
해상도가 얼마든 UI 요소를 항상 고정된 픽셀 크기/좌표로만 그리기 때문에, 디자인 당시 기준으로 삼은
해상도와 실제 실행 해상도가 다르면 요소 크기·간격·정렬이 전부 어긋나 보임(해상도가 낮으면 UI가 화면
비율에 비해 과하게 크게, 높으면 과하게 작게 나오고 겹치거나 잘림).

`Reference Resolution`도 `800×600`(유니티 CanvasScaler의 기본값 — 아무도 손댄 적 없는 값)으로
방치돼 있었던 것으로 봐서, 애초에 스케일링 자체가 한 번도 설정된 적이 없었던 것으로 보임.

세계 공간(World Space) 캔버스인 `Capture_Point.prefab`(점령 바)와 `HealthBar.prefab`(유닛 머리 위
체력바)은 화면 해상도가 아니라 3D 월드 좌표/카메라 거리 기준으로 크기가 정해지는 별개의 방식이라
이번 증상과 무관 — 손대지 않음.

## 수정

`GameManager.prefab`, `MainScene.unity`의 `CanvasScaler` 설정을 아래처럼 변경:

| 필드 | 이전 | 이후 |
|---|---|---|
| UI Scale Mode | Constant Pixel Size (0) | **Scale With Screen Size (1)** |
| Reference Resolution | 800 × 600 | **1920 × 1080** |
| Screen Match Mode | Match Width Or Height (0, 그대로) | 그대로 |
| Match | 0 | **0.5 (너비/높이 균형)** |

`Scale With Screen Size`는 "기준 해상도(Reference Resolution)에서 배치한 그대로"를 기준으로,
실제 실행 해상도가 다르면 그 비율만큼 캔버스 전체를 균일하게 확대/축소하는 방식 — 요청하신 "현재
배치된 위치를 기준으로 해상도에 맞게 비율 조절"과 정확히 일치. 기준 해상도는 이 프로젝트의 실제
개발/테스트 해상도인 1920×1080(Game 뷰에서 계속 그 값으로 확인해온 해상도)으로 맞춰서, 지금
배치해둔 모습이 그대로 "기준"이 되도록 함.

## 검증

- `npx uloop-cli compile`/`get-logs`: 에러 0개.
- TestScene을 Play Mode로 띄워 스크린샷 확인 — Game 뷰가 여전히 1920×1080(기준 해상도와 동일)이라
  `Scale With Screen Size`에서도 배율이 1로 계산돼, 기존 화면과 완전히 동일하게 보이는 것 확인
  (레이아웃 변화 없음 = 회귀 없음).
- 다른 해상도에서의 실제 스케일링 동작은 에디터 Game 뷰 해상도를 바꿔가며 직접 확인 필요(이번
  세션에서는 자동화 도구로 Game 뷰 크기를 바꾸는 것까진 안 함) — 원리상 `CanvasScaler`가 표준으로
  제공하는 기능이라 정상 동작이 보장됨.

## 영향받는 파일

- `Assets/prefabs/Game/GameManager.prefab`
- `Assets/Scenes/MainScene.unity`
