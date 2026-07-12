# 0084 — 공중 유닛이 언덕을 벗어나는 순간에 맞춰 고도가 바뀌도록(지형 추적 비행)

## 질문
"만약 언덕위에 있는데 언덕아래로 클릭해서 좌표 지정했어 그렇게 했을때 언덕에서 벗어날떄 그때 y축이 변화했으면
좋겠는데 가능할까?"

## 문제

[[0083-air-altitude-relative-to-terrain]]까지 수정한 결과, 목적지 고도는 "그 지점 지면 + 5"로 정확히 계산되지만,
실제 이동은 여전히 출발 위치에서 그 목적지 고도까지 **한 번에 직선(MoveTowards)으로 보간**하는 방식이었음. 그래서
언덕 위(예: 고도 8 = 지면 3 + 5)에서 저지대(예: 고도 3 = 지면 -2 + 5)로 이동 명령을 내리면, 수평 이동과 동시에
고도도 그 즉시 "목표 고도"를 향해 점진적으로 낮아지기 시작함 - 아직 언덕 위를 지나는 중인데도 미리 하강해버려서
언덕 지형에 파묻히듯 스칠 수 있음. 사용자가 원한 건 "언덕을 실제로 벗어나는 순간부터" 고도가 바뀌는 것.

## 해결 방법: 목적지 고도로 직선 보간 → 매 프레임 "발밑 지면" 재측정

목적지 고도를 미리 계산해서 그쪽으로 곧장 이동하는 대신, **매 프레임 자기 발밑(현재 X/Z) 바로 아래의 지면 높이를
레이캐스트로 다시 재서 그 값 + `airCruiseAltitude`를 목표 고도로 삼도록** 변경. 수평 이동은 목적지를 향해
그대로 진행하되, 수직 이동은 완전히 독립적으로 "지금 서 있는 자리의 지형"을 따라간다.

이렇게 하면:
- 언덕 위를 지나는 동안엔 발밑이 계속 높은 지형이므로 고도도 계속 높게 유지됨(파묻히지 않음).
- 수평 이동으로 언덕 능선을 실제로 벗어나 발밑 지형이 낮아지는 그 순간부터 고도도 자연스럽게 따라 낮아짐 -
  정확히 사용자가 원한 동작.

## 수정

### `Assets/Scripts/Unit/UnitController.cs`
- 새 필드 `[SerializeField] private LayerMask airGroundLayer;` 추가 - 지면 높이를 잴 때 쓸 레이어(지형/Ground).
  비워두면(0) 예전처럼 목적지 고도로 곧장 직선 이동(안전한 기본 동작 유지, 하위 호환).
- 새 헬퍼 `SampleGroundHeight(Vector3 xzPosition, float fallback)`: `xzPosition`의 X/Z 바로 위 1000에서 아래로
  레이캐스트를 쏴서 `airGroundLayer`에 맞는 지면의 Y를 반환. 못 찾으면 `fallback` 반환.
- `Update()`의 공중 유닛 이동 블록을 수평/수직 독립 보간으로 재작성:
  ```csharp
  Vector3 horizontalTarget = new Vector3(targetPosition.x, pos.y, targetPosition.z);
  pos = Vector3.MoveTowards(pos, horizontalTarget, moveSpeed * Time.deltaTime);

  float groundBelow = SampleGroundHeight(pos, targetPosition.y - airCruiseAltitude);
  pos.y = Mathf.MoveTowards(pos.y, groundBelow + airCruiseAltitude, moveSpeed * Time.deltaTime);
  ```
  도착 판정도 "수평 도착"과 "수직 도착"을 각각 따로 확인해서 둘 다 만족해야 정지하도록 변경([[0078-building-lift-move-vertical-not-rising]]에서
  건물에 적용했던 것과 동일한 패턴을 공중 유닛에도 적용).

### 프리팹
`airGroundLayer`는 새 필드라 기존 프리팹엔 값이 없어서(0 = 비어있음) 그대로 두면 기능이 꺼진 채로 동작함.
실제 공중 유닛 프리팹 3개에 `Ground` 레이어(레이어 인덱스 7, 비트값 128)를 채워 넣어 기능을 켬:
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
- `Assets/prefabs/Test/TestAirUnit.prefab`

씬(`SampleScene.unity`)에 실제로 `Ground` 레이어를 쓰는 지형 오브젝트(콜라이더 포함) 3개가 있는 것도 확인함.

## 확인 필요 사항
언덕 위에 공중유닛을 두고 언덕 아래(저지대)를 클릭해서 이동시켜서, 언덕 능선 위를 지나는 동안은 높이 유지하다가
능선을 벗어나는 시점에 맞춰 고도가 내려가는지 확인 부탁. 반대로 저지대에서 언덕 위로 이동할 때도 언덕 사면에
가까워지면서 자연스럽게 고도가 올라가는지 확인. `airGroundLayer`가 다른 유닛 프리팹(향후 추가되는 공중 유닛
등)에도 `Ground`로 채워져 있는지 새로 만들 때 챙겨야 함.
