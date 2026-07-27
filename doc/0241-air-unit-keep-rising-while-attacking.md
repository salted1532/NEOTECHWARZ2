# 0241 - 공격 중에도 공중유닛이 목표 고도까지 계속 상승하도록 수정

## 요청

이전 대화에서 확인된 "아군 공중유닛이 시작할 때 안 뜨는" 문제의 진짜 원인은: 우주공항에서 막 생성되자마자
근처에 적이 있으면, 뜨기도 전에 바로 공격을 시작해버려서 그 자리(바닥)에 눌러붙은 채로 싸우는 것이었음
(유닛 본인이 직접 진단). 공격 중이어도 `airCruiseAltitude`만큼 위로 계속 올라가는 동작은 계속돼야 함.
아군/적 둘 다 적용.

## 원인

`UnitController.Attack()` / `EnemyUnitController.Attack()`이 공중 유닛일 때 이렇게 처리하고 있었음:

```csharp
targetPosition = AirTargetPosition(transform.position, true); // 제자리 정지 - 현재 고도를 그대로 유지
isMovingAirUnit = false;
```

`destinationIsAirborne: true`로 넘겨서 "지금 위치를 그대로 목표로 고정"하고, `isMovingAirUnit`을 꺼서
`Update()`의 공중 이동 블록 자체를 완전히 멈춰버림. 막 생성된 직후(아직 지면 높이)에 공격이 시작되면
"지금 위치"가 곧 바닥이라, 그 고도 그대로 영구히 고정되어 버렸음 - 즉 뜨는 도중에 적을 만나면 상승이
그 자리에서 끊겨버리는 구조였음.

## 수정 내용

`UnitController.Attack()`, `EnemyUnitController.Attack()`의 공중 유닛 분기를 수정:

```csharp
float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
targetPosition = new Vector3(transform.position.x, groundBelow + airCruiseAltitude, transform.position.z);
isMovingAirUnit = true;
```

- `isMovingAirUnit`을 끄지 않고 계속 켜둠 → `Update()`의 공중 이동 블록이 공격 중에도 계속 실행됨
- 목표 지점을 "현재 XZ + 그 자리 지면 높이 + airCruiseAltitude"로 매 공격 프레임마다 다시 계산 → 수평
  이동은 없이(제자리 유지) 수직으로만 목표 고도까지 계속 수렴
- `Attack()`은 상대가 사거리 안에 있는 한 매 프레임 호출되므로, 고도에 도달해서 `Update()`가
  `isMovingAirUnit = false`로 되돌려도 다음 프레임 `Attack()`이 다시 켜서 계속 유지됨 (사실상 "도달할
  때까지 상승, 도달하면 그 고도에서 호버링"하는 모양이 됨)
- 수평 목표가 항상 현재 위치와 같으므로 `Update()`의 회전 로직(`dir.sqrMagnitude > 0.001f`)은 사실상
  발동하지 않고, 적을 향한 회전은 기존처럼 `RotateYOnly(end)`가 전담

## 변경 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Enemy/EnemyUnitController.cs`
