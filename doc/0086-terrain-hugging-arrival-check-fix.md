# 0086 — 지형 추적 비행 도입 후 착륙(도착 판정)이 안 되던 문제

## 질문
"언덕이동은 자연스럽게 잘 되는데 이젠 착륙이 안돼네 확인좀 부탁해"

## 원인

[[0084-air-unit-terrain-hugging-altitude]]/[[0085-building-terrain-hugging-altitude]]에서 Y(고도)를 "목적지 좌표에
미리 계산해둔 고도(`targetPosition.y`/`flightDestination.y`)로 직선 보간"하던 방식에서 "매 프레임 발밑 지형을
다시 재서(`SampleGroundHeight`) 그 값 + 고도값으로 수렴"하는 방식으로 바꿨는데, **도착 판정(`arrivedVertically`)은
그대로 옛날 방식대로 미리 계산해둔 `targetPosition.y`/`flightDestination.y`와 비교**하고 있었음.

문제는 `targetPosition.y`/`flightDestination.y`가 그리드 기준으로 미리 계산된 값이라, 실제 레이캐스트로 잰
지형 높이(`SampleGroundHeight`가 매 프레임 실제로 수렴시키는 목표)와 완전히 똑같다는 보장이 없다는 것 - 아주
작은 차이(부동소수점, 메시 표면과 그리드 기준점의 미세한 차이 등)만 있어도 실제 위치(`pos.y`)는
"라이브로 잰 목표"에는 정확히 도달하지만 "미리 계산해둔 옛 목표"와는 그 미세한 차이만큼 영원히 안 좁혀짐 -
그래서 도착 판정(`< 0.05f`/`< 0.1f`)이 절대 통과하지 못하고, 수평 이동으로 만든 언덕 오르내리기는 잘 되는데
"도착 → 착륙 단계 진입" 전환만 영원히 안 일어남.

## 수정

도착 판정을 "미리 계산해둔 값"이 아니라 **"이번 프레임에 실제로 수렴시키고 있는 값(desiredY)"과 비교**하도록 통일.

### `Assets/Scripts/Unit/UnitController.cs`
- `Update()`의 공중 이동 블록: `arrivedVertically`를 `targetPosition.y` 대신 그 프레임에 계산한
  `desiredY(= groundBelow + airCruiseAltitude)`와 비교하도록 수정.
- `PatrolTick()`의 `arrivedAir`도 같은 문제가 있어서 함께 수정 - 3D 거리 대신 **수평(X/Z) 거리만** 비교하도록 변경
  (고도는 어차피 Update()에서 계속 지형을 따라 조정되는 값이라 순찰 웨이포인트 전환 판정에 넣을 필요가 없음).

### `Assets/Scripts/Building/BuildingController.cs`
- `UpdateLiftedMovement()`의 `isFlyingToDestination` 단계: `arrivedVertically`를 `flightDestination.y` 대신
  그 프레임에 계산한 `desiredY(= groundBelow + liftHeight)`와 비교하도록 수정. 이게 착륙(`isDescending` 진입)이
  안 되던 직접적인 원인이었음 - 도착 판정이 안 나니 `pendingLanding` 체크 자체에 도달하지 못했음.
- 하강 단계(`isDescending`, 착륙 최종 지면으로 곧장 내려가는 부분)는 애초에 지형 추적을 안 쓰는 별개 로직이라
  이번 버그와 무관 - 손대지 않음.

## 확인 필요 사항
건물을 이륙시켜서 착륙 위치를 지정했을 때 정상적으로 하강해서 착륙까지 완료되는지, 평지/언덕 양쪽에서 확인 부탁.
공중 유닛의 순찰(Patrol) 명령도 웨이포인트 사이를 왕복하며 잘 전환되는지 같이 확인.
