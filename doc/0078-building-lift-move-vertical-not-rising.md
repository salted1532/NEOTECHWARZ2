# 0078 — 건물 이륙 직후 이동 명령 시 고도가 제대로 안 오르던 문제

## 질문
"건물 이륙후 이동중에 y값 5까지 올라가도록 하는 코드좀 확인해줘 이륙하자마자 이동하면 제대로 위로 올라가지 않고
움직이는데 이동중에도 위로 상승하지 않네"

## 원인

`Assets/Scripts/Building/BuildingController.cs`의 `UpdateLiftedMovement()` 중 `isFlyingToDestination`(수평 이동) 단계가
원인. 기존 코드:

```csharp
transform.position = Vector3.MoveTowards(transform.position, flightDestination, liftMoveSpeed * Time.deltaTime);
```

`flightDestination`은 "목적지 좌표(X,Z) + liftHeight(Y)"로 미리 계산된 값인데, `Vector3.MoveTowards`는 현재 위치에서
목적지까지의 **3D 직선 방향**으로 고정 속도만큼 이동시킨다. 문제는 이 방향 벡터의 수직(Y) 성분이 수평(X/Z) 거리에 비해
훨씬 작을 때(건물을 조금만 옮겨도 이동거리가 liftHeight(5)보다 훨씬 큰 게 보통) 한 프레임당 실제로 오르는 높이가
아주 적다는 것 — 즉 목적지에 거의 다 도착할 때가 돼서야 방향 벡터의 수직 비중이 커지면서 급격히 상승하는 것처럼
보임. 게다가 이륙 버튼을 누르자마자(`isAscending`이 아직 `true`인 상태에서, 즉 고도가 거의 안 오른 상태에서) 바로
이동 명령을 내리면 시작 Y가 지면에 가까운 채로 이 계산이 시작되므로 증상이 더 두드러짐 — 사용자가 관찰한 정확히 그 현상.

## 수정

수평(X/Z)과 수직(Y)을 **완전히 독립적으로** 목표에 수렴시키도록 변경. 매 프레임:
- Y는 `Mathf.MoveTowards(현재Y, flightDestination.y, liftMoveSpeed * Time.deltaTime)`로 목표 고도까지 단독으로 상승.
- X/Z는 (Y를 그대로 둔 채) `Vector3.MoveTowards`로 목적지의 수평 좌표까지 단독으로 수렴.
- 두 값을 합쳐서 `transform.position`에 반영.
- 도착 판정도 "수평 도착"과 "수직 도착"을 각각 따로 검사해서 둘 다 만족해야 다음 단계(대기 또는 착륙 하강)로 넘어가게 함.

이제 이동 명령이 이륙 도중에 들어와도 Y가 항상 `liftHeight/liftMoveSpeed`초(기본 5/5=1초) 안에 목표 고도까지 확실히
오르고, 그 이후엔 목표 고도를 유지한 채로 수평 이동만 계속됨. 목적지가 아무리 멀어도 고도 상승이 희석되지 않음.

## 확인 필요 사항
Unity 에디터에서 건물을 이륙시키자마자(고도가 채 오르기 전에) 바로 먼 지점으로 이동 명령을 내려서, 고도가
곧바로 목표 높이(지면 + liftHeight)까지 오른 뒤 그 높이를 유지한 채 수평으로 날아가는지 확인 부탁.

## 참고 (이번엔 손대지 않음)
`MoveWhileLifted()`가 받는 `groundDestination`은 우클릭/Move 버튼 클릭 시의 순수 레이캐스트 히트 지점(지면 raw Y)이고,
`BeginRelocationFlight()`(착륙 예약)가 받는 `destination`은 `PlacementSystem`에서 `GetGroundOffsetY`로 보정된 좌표라서,
자유 이동(Move) 때와 착륙 예약 때의 실제 순항 고도가 건물 피벗 오프셋만큼(대개 1 미만) 미세하게 다를 수 있음.
이번에 보고된 증상과는 무관해서 손대지 않았음 — 필요하면 별도로 요청.
