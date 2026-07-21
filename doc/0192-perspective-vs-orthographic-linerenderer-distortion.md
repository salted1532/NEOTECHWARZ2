# 0192 - Perspective→Orthographic 전환 시 LineRenderer 왜곡 원인 질문

## 요청

"카메라 projection을 perspective에서 Orthographic으로 바꾸면 어떤게 다른것이며 왜 라인렌더러 같은 것들을
왜곡되서 보이게 되는거야?" — 코드 변경 없이 개념 설명을 요청한 순수 Q&A.

## 조사 내용

- `Assets/Scripts/Camera/CameraControl.cs`를 확인: 현재 씬 카메라는 Perspective로 고정되어 있고,
  `HandleZoom()` 주석에도 "이 카메라는 Perspective라 orthographicSize는 아무 효과가 없어서 위치를
  직접 옮기는 방식으로 줌을 구현한다"고 명시되어 있음. 즉 프로젝트는 현재 Orthographic을 쓰고 있지
  않고, 전환을 검토/이해하려는 목적의 질문으로 보임.
- 프로젝트 내 `LineRenderer`를 사용하는 스크립트는 `Assets/Scripts/CaptureSystem/TerritoryZone.cs`
  1곳뿐 (거점 경계선). 이전 세션([[territory-outline-hidden-in-fog-design]] 0177,
  [[fogplane-overlay-render-queue-fix]] 0179, 0180에서 되돌림)에서 이 LineRenderer가 안개를 뚫고
  보이는 문제를 다룬 적이 있어, 이번 질문이 그 맥락과 연결될 가능성이 있다고 보고 참고용으로 확인함.
  다만 이번 질문 자체는 안개 문제가 아니라 투영 방식 차이에 대한 일반 개념 질문.
- `TerritoryZone.cs`에는 `alignment`/`widthMultiplier` 등을 코드로 설정하는 부분이 없어, LineRenderer
  설정(Alignment 등)은 인스펙터/프리팹에 있을 것으로 추정(기본값이면 View 정렬).

## 답변 요약

**Perspective vs Orthographic 차이**
- Perspective: 절두체(frustum) 뷰 볼륨, 소실점 존재, 투영행렬에 z 기반 perspective divide가 있어
  거리가 멀수록 화면상 크기가 작아짐.
- Orthographic: 직육면체 뷰 볼륨, 소실점 없음, z divide가 없어 거리와 무관하게 월드 크기가 화면 크기에
  그대로 비례. 평행선은 화면에서도 항상 평행 유지.

**왜 LineRenderer가 왜곡되어 보이는가**
- 일반 정적 메쉬는 정점이 고정돼 있고 투영행렬만 적용되므로 Perspective/Orthographic 전환에 안전함.
- LineRenderer(및 TrailRenderer, 카메라 페이싱 파티클 등)는 매 프레임 CPU에서 "카메라를 향해 폭을
  펼치는" 리본 메쉬를 새로 생성함(기본 Alignment = View). 이때 보통 `(점 → 카메라 위치)` 벡터를 그
  점에서의 시선 방향으로 취급하는데, 이 가정은 Perspective(모든 시선이 카메라라는 한 점에서 퍼짐)에서만
  정확히 성립함.
- Orthographic은 모든 시선 광선이 카메라 forward 방향으로 평행하므로, 카메라 정면 축 위에 있지 않은
  점(화면 가장자리, 비스듬한 라인 등)에서는 "점→카메라위치" 벡터가 실제 평행한 시선 방향과 어긋나게
  되고, 그 결과 리본의 폭 평면이 틀어져 선이 비틀리거나 두께가 불균일하게 보이는 왜곡이 발생함.
- Unity의 LineRenderer는 비교적 최신 버전에서 `camera.orthographic`을 감지해 forward 벡터 기반으로
  보정하기도 하므로, 실제로 왜곡이 보인다면 흔한 원인은: `Camera.main`과 실제 렌더링 카메라 불일치,
  Alignment가 `Transform Z`로 되어있는 경우, 커스텀 셰이더/스크립트가 orthographic 분기 없이 폭을
  직접 계산하는 경우.

코드 변경 없음(순수 설명 질문). 필요하면 다음에 실제 Orthographic 전환 작업 시 `TerritoryZone.cs`의
LineRenderer Alignment 설정과 렌더링 카메라를 확인하기로.

## 변경된 파일

없음.
