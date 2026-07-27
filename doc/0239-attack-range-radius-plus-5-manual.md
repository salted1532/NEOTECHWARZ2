# 0239 - AttackRange 감지 콜라이더 반경 = 사거리 + 5로 전체 프리팹 직접 수정

## 요청

[[0237]]에서 진단한 "감지 반경(고정값) vs 사거리(UnitRange) 불일치" 문제를, 런타임 코드로 자동 보정하는
대신([[0237]]에서 이미 되돌림) 프리팹의 콜라이더 반경 자체를 `UnitRange + 5`로 직접 맞춰달라는 요청.
아군/적 구분 없이 전체 유닛(`AttackRange`를 쓰는 NTA 9종 + `EnemyAttackRange`를 쓰는 OC 9종)에 적용.

## 수정 내용

각 프리팹의 "AttackRange" 자식 오브젝트에 있는 `CapsuleCollider.m_Radius` 값을 그 유닛의 `UnitRange`
값 + 5로 직접 수정함 (스크립트는 안 건드리고 프리팹 데이터만 수정).

### NTA (`AttackRange`)

| 유닛 | UnitRange | 콜라이더 Radius |
|---|---|---|
| Worker Drone | 2 | 7 |
| Assault Trooper | 12 | 17 |
| Scout Drone | 14 | 19 |
| Sharpshooter | 20 | 25 |
| Ranger Infantry Fighting Vehicle | 14 | 19 |
| Pulsar Tank | 20 | 25 |
| SkyLancer | 18 | 23 |
| Firehawk | 18 | 23 |
| Guardian Drone | 20 | 25 |

### OC (`EnemyAttackRange`)

| 유닛 | UnitRange | 콜라이더 Radius |
|---|---|---|
| Nanobot Repair | 4 | 9 |
| Cyborg Soldier | 12 | 17 |
| Striker | 14 | 19 |
| Railgunner | 20 | 25 |
| Brute Mech | 2 | 7 |
| Heavy Assault Tank | 20 | 25 |
| Ironhawk | 18 | 23 |
| Raven | 18 | 23 |
| Strike Drone | 20 | 25 (이미 사용자가 25로 맞춰둔 상태 - 변경 없음) |

모두 [[0238]]에서 추가한 씬 뷰 디버그 선(청록/빨강)으로 실제 사거리와 비교하며 확인 가능.

## 추가: 코드 안전장치

프리팹 값을 손으로 다 맞춰뒀지만, 나중에 새 유닛을 추가하거나 사거리를 바꾸면서 콜라이더 반경 맞추는 걸
깜빡할 수 있으니 코드 차원의 최소 보장 장치를 추가함. [[0237]]에서 추가했다가 되돌렸던 것과 비슷한
메커니즘이지만, 이번엔 "런타임에 항상 다시 계산"이 아니라 "부족할 때만 채워주는 보험" 성격:

- `AttackRange.cs`(아군), `EnemyAttackRange.cs`(적)에 `EnsureDetectionRadius()` 추가:
  `콜라이더 반경 = Mathf.Max(현재 반경, UnitRange + 5)` - 이미 충분히 크면 그대로 두고(줄이지 않음),
  부족할 때만 넓힌다.
- 호출 시점 두 곳:
  1. `Awake()`에서 한 번 (프리팹 자체 값 기준 최소 보장)
  2. `UnitController.ApplyUnitData()` / `EnemyUnitController.ApplyUnitData()`에서 `UnitRange`를 SO 값으로
     덮어쓴 직후 한 번 더 (런타임에 사거리가 바뀌어도 계속 보장되도록)

이번엔 [[0237]]과 달리 되돌리지 않고 유지 - "매번 다시 계산해서 강제로 맞추는" 대신 "부족한 경우에만
안전하게 채워주는" 방향이라 문제였던 지점(항상 자동으로 크기를 조정)과는 성격이 다름.

## 변경 파일

NTA: `Assault Trooper.prefab`, `Sharpshooter.prefab`, `Scout Drone.prefab`,
`Ranger Infantry Fighting Vehicle.prefab`, `Pulsar Tank.prefab`, `SkyLancer.prefab`, `Firehawk.prefab`,
`Guardian Drone.prefab`, `Worker Drone.prefab`

OC: `Nanobot Repair.prefab`, `Cyborg Soldier .prefab`, `Striker.prefab`, `Railgunner.prefab`,
`Brute Mech.prefab`, `Heavy Assault Tank.prefab`, `Ironhawk.prefab`, `Raven.prefab`
(`Strike Drone.prefab`은 이미 일치해서 변경 없음)

코드: `Assets/Scripts/Unit/AttackRange.cs`, `Assets/Scripts/Unit/UnitController.cs`,
`Assets/Scripts/Enemy/EnemyAttackRange.cs`, `Assets/Scripts/Enemy/EnemyUnitController.cs`
