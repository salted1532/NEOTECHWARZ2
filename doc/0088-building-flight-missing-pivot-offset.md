# 0088 — 건물 이동 중 고도가 이륙 때보다 메쉬 피벗 오프셋만큼 낮게 계산되던 문제

## 질문
"y=2인 지형에서 liftHeight가 5라고 하면 이륙시 7로 되고 그 지형을 이동할떄도 7이여야하는데 5로 고정돼 만약
언덕아래 y=0인 지형으로 지정하면 언덕위 ground layer안에서는 5로 고정되고 언덕아래로 갈시 -2되서 3으로 이동돼
언덕 끝에서 y값이 변경되는건 좋은데 내가 원하는건 기존 땅좌표 y값에 +5 liftHeight하는걸 원해 아니면 고정될거면
처음에 이륙할떄도 고정된 값으로만 이륙했으면 좋겠어 아니면 지형에 맞게 갱신해줘"

## 원인

건물이 지면에 서 있을 때의 `transform.position.y`는 순수 지형 높이가 아니라
**"지형 높이 + 그 건물 메쉬의 피벗-지면 거리(`PlacementSystem.GetGroundOffsetY`가 계산하는 값)"**임 - 건물
메쉬는 보통 피벗이 바닥 정중앙이 아니라 다른 기준점에 있어서, 완공/스폰 시 항상 이 오프셋만큼 들어올려서 배치함
(`BaseStructure.CompleteConstruction()`, `PlacementSystem.PlaceRelocatedBuilding()` 등에서 전부 이렇게 함).

- **이륙(`LiftOff`)**: `verticalTarget = transform.position + Vector3.up * liftHeight` - 자기 자신의 현재
  `transform.position`을 기준으로 삼기 때문에, 그 안에 이미 포함된 피벗 오프셋이 자동으로 딸려 올라감. 그래서
  "지형(2) + 피벗오프셋(예: 2) = 4"에서 이륙하면 "4 + liftHeight(5) = 9" ... (실제 사용자 예시로는 지형2+피벗2=4가
  아니라 이미 "y=2"라고 관찰한 그 자체가 지형+피벗을 합친 관찰값이었을 가능성이 큼) - 어쨌든 **관찰되는
  transform.position.y 자체에 피벗 오프셋이 항상 녹아있어서 이륙 계산은 자동으로 맞았음**.
- **이동 중 지형 추적([[0084]]/[[0085]])**: `SampleGroundHeight()`는 레이캐스트로 잰 **순수 지형 표면 높이**만
  반환함(피벗 개념이 없음). 여기에 `+ liftHeight`만 더하고 피벗 오프셋을 안 더해서, 이동 중 목표 고도가
  이륙 때보다 정확히 "피벗 오프셋"만큼 낮게 계산되고 있었음 - 사용자가 관찰한 "7이어야 하는데 5로 고정"이
  바로 이 누락된 오프셋만큼의 차이.
- 언덕→저지대로 넘어갈 때 고도가 2만큼 변한 것 자체는 지형 추적이 정상 동작하고 있다는 뜻(사용자도 "언덕 끝에서
  변경되는건 좋다"고 확인) - 다만 그 기준선 전체가 피벗 오프셋만큼 낮게 깔려있었던 것.

## 수정

`Assets/Scripts/Building/BuildingController.cs`:
- `groundOffset` 필드 추가, `Start()`에서 `PlacementSystem.GetGroundOffsetY(gameObject)`로 1회 계산해 캐싱
  (건물 자신의 메쉬 기준 피벗-지면 거리).
- `UpdateLiftedMovement()`의 실시간 고도 계산에 이 오프셋을 반영:
  `desiredY = groundBelow + groundOffset + liftHeight` (전엔 `groundOffset` 없이 `groundBelow + liftHeight`였음).
- `MoveWhileLifted()`(우클릭/Move 자유이동): 목적지 Y가 순수 지면 클릭 좌표(피벗 미포함)라서
  `flightDestination.y = groundDestination.y + groundOffset + liftHeight`로 수정(`groundOffset` 추가).
- `BeginRelocationFlight()`(공식 착륙): 여기 들어오는 `destination`은 `PlacementSystem`이 이미
  `GetGroundOffsetY`를 반영해서 계산해준 좌표라 **원래부터 맞았음** - 그대로 둠(주석으로 이유 명시).

이제 이륙 시점 고도와 이동 중 고도 계산이 같은 기준(지형 + 피벗 오프셋 + liftHeight)을 쓰므로, 사용자가 원한
"기존 땅 좌표 Y값 + liftHeight"(피벗 보정 포함)가 이륙/이동/착륙 전 구간에서 일관되게 유지됨.

## 확인 필요 사항
건물을 이륙시켜서 도달한 고도를 확인한 뒤, 그 자리에서 (혹은 다른 평지로) 이동 명령을 내려도 고도가 그대로
유지되는지(더 이상 뚝 떨어지지 않는지) 확인 부탁. 언덕/저지대를 가로질러 이동시켰을 때도 "이륙 때와 같은 기준으로"
고도가 오르내리는지 함께 확인.
