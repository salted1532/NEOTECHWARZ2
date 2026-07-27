# 0246 - 건물이 시작 시 지면에 자동으로 붙도록 수정 (아군/적 둘 다)

## 질문/요청

아군 건물 스크립트에 시작할 때 지면에 딱 붙어서 위치하도록 하는 코드가 있는지 확인, 있으면 적 건물에도
적용, 없으면 둘 다 적용해달라는 요청.

## 확인 결과 - 없었음

[[0040]]에서 만든 "프리팹 높이 기반 자동 지면 정렬"(`PlacementSystem.GetGroundOffsetY`)은
**건설(PlacementSystem을 거친 정상 건설 흐름)** 시점에만 적용되는 계산이었음 - 플레이어가 실제로
건물을 지을 때 스폰 좌표를 "지면 좌표 + 프리팹 높이 오프셋"으로 정확히 계산해주는 것뿐, **씬에 이미
배치돼 있는 건물**(시작 건물, 테스트용으로 에디터에 직접 끌어다 놓은 건물)에는 이 보정이 전혀 적용되지
않았음. `BuildingController.Start()`는 `groundOffset` 값만 계산해두고(나중에 리프트/이착륙 계산에
사용) `transform.position`은 전혀 건드리지 않았음.

즉 씬에 미리 배치해둔 건물이 지형과 딱 안 맞으면(살짝 파묻히거나 뜬 채로) 그 상태 그대로 시작됨 -
질문하신 그 기능은 없었음. 적 건물(`EnemyBuildingController`)도 마찬가지로 이런 보정이 전혀 없었음.

## 수정 내용

**`Assets/Scripts/Building/BuildingController.cs`**
- `SnapToGround()` 추가 - 기존 `SampleGroundHeight()`(지면 레이캐스트)를 재사용해서, 현재 XZ 위치의
  지면 높이 + `groundOffset`(메쉬 피벗-지면 거리)으로 `transform.position.y`를 다시 맞춤
- `Start()`에서 `groundOffset` 계산 직후 호출

**`Assets/Scripts/Enemy/EnemyBuildingController.cs`**
- `groundLayer` 필드, `groundOffset` 필드, `SnapToGround()`를 새로 추가 (`BuildingController`와 동일한
  패턴 - 원래 이 스크립트엔 지면 관련 로직 자체가 없었음)
- `Start()`에서 `PlacementSystem.GetGroundOffsetY(gameObject)`로 오프셋을 계산하고 `SnapToGround()` 호출
  (`ApplyBuildingData()` 호출보다 먼저 - 위치 보정과 데이터 적용은 서로 무관해서 순서는 상관없지만
  자연스러운 흐름상 위치부터 잡음)

두 스크립트 다 `groundLayer`가 비어있거나(Nothing) 그 지점에서 레이캐스트가 지면을 못 잡으면 원래
위치를 그대로 유지한다 - 안전한 폴백이라 `groundLayer`를 실수로 안 채워도 기존 동작을 깨지 않는다.
정상적으로 `PlacementSystem`을 거쳐 건설된 아군 건물은 이미 정확한 지면 좌표로 스폰되므로 이 보정으로
값이 거의 안 바뀐다 - 오직 씬에 직접 배치해둔 건물에만 실질적인 효과가 있음.

## 변경 파일

- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/Enemy/EnemyBuildingController.cs`
