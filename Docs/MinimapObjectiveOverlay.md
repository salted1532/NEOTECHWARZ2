# MinimapObjectiveOverlay

`Assets/Scripts/Camera/MinimapObjectiveOverlay.cs`

## 개요

씬에 배치된 `MinimapObjectiveMarker`들을 미니맵 위에 아이콘으로 표시하는 싱글턴(doc/0349). 월드 좌표 → 미니맵 UI 로컬 좌표 변환은 `MinimapViewIndicator`와 동일한 공식을 쓴다. 아이콘은 프리팹 없이 코드에서 바로 만든다(스프라이트 없는 Image는 단색 사각형으로 렌더링됨) — 프로토타입 단계에서 에셋 없이도 바로 동작하게 하기 위함이며, 나중에 전용 아이콘 스프라이트가 생기면 프리팹으로 교체 가능하다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `Instance` | 정적 싱글턴 인스턴스 |
| `minimapRect` | 미니맵 RawImage의 RectTransform |
| `minimapCamera` | 미니맵을 렌더링하는 카메라 (뷰포트 좌표 계산용) |
| `icons` | 마커→생성된 아이콘 RectTransform 매핑 (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 싱글턴 등록 |
| `Start()` | 씬 시작 시 이미 배치돼 있던 마커를 전수 등록 — `OnEnable` 시점엔 `Instance`가 아직 안 잡혀있을 수 있으므로(스크립트 실행 순서 무관) `FindObjectsByType`로 한 번 훑는다. 이후 동적으로 켜지는 마커는 `MinimapObjectiveMarker.OnEnable()`이 알아서 `Register()`를 호출 |
| `Register(marker)` | 단색 사각형 Image 아이콘을 생성해 `minimapRect`의 자식으로 붙임 (raycastTarget은 꺼서 미니맵 클릭을 가로채지 않음) |
| `Unregister(marker)` | 아이콘 파괴 및 딕셔너리에서 제거 |
| `Update()` | 매 프레임 각 아이콘을 `minimapCamera.WorldToViewportPoint`로 미니맵 로컬 좌표로 변환해 위치 갱신 |

## 연관 컴포넌트

- **MinimapObjectiveMarker**: `OnEnable`/`OnDisable`에서 이 오버레이에 자신을 등록/해제
- **MinimapViewIndicator**: 월드→미니맵 좌표 변환 공식을 공유
