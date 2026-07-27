# 0235 - 공중 적 유닛이 시작 시 떠오르지 않던 문제 수정

## 요청

공중 유닛(Raven/Strike Drone 등, `isAirUnit: true`)을 테스트로 씬 바닥에 배치해두면 시작해도 공중으로
뜨질 않음. 시작할 때 "자기 위치로 이동" 명령을 내린 것처럼 처리해서, 그 지점의 지면 높이 기준으로
`airCruiseAltitude`만큼 자동으로 떠오르게 해달라는 요청.

## 원인

`EnemyUnitController.Awake()`가 공중 유닛 초기화 시:

```csharp
targetPosition = AirTargetPosition(transform.position, true); // destinationIsAirborne: true
```

`destinationIsAirborne: true`를 넘기고 있었는데, 이건 "이 좌표는 이미 공중에 떠 있는 대상의 좌표다(고도를
또 더하지 마라)"는 뜻(`AirTargetPosition` 참고, [[0231]]). 게다가 `isMovingAirUnit`을 `true`로 켜지도
않아서, 애초에 `Update()`의 공중 이동 분기 자체가 실행되지 않았음. 결과적으로 시작 위치(씬에 배치해둔
그대로, 즉 바닥)에 계속 머무름.

플레이어 쪽 `UnitController.Awake()`는 반대로 되어 있었음:

```csharp
targetPosition = AirTargetPosition(transform.position); // destinationIsAirborne 기본값 false
isMovingAirUnit = true;
```

`destinationIsAirborne`을 기본값(false)으로 둬서 "이 좌표는 지면 좌표"로 취급해 그 지점의 지면 높이 +
`airCruiseAltitude`로 목적지를 계산하고, `isMovingAirUnit = true`로 실제 이동(상승)을 시작시킴 - 즉
시작하자마자 "자기 위치로 이동" 명령을 낸 것과 동일한 효과. `EnemyUnitController`를 만들 때 이 부분을
반대로 잘못 이식했던 것.

## 수정 내용

`Assets/Scripts/Enemy/EnemyUnitController.cs`의 `Awake()`를 `UnitController.Awake()`와 동일하게 맞춤:

```csharp
targetPosition = AirTargetPosition(transform.position);
isMovingAirUnit = true;
```

이제 공중 유닛 프리팹을 씬 아무 위치(바닥 포함)에 놓아도 시작하자마자 그 XZ 위치의 지면 높이 +
`airCruiseAltitude`까지 자동으로 떠오른다.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
