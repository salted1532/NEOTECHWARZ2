# ProjectileAttack

`Assets/Scripts/Unit/ProjectileAttack.cs`

## 개요

투사체 공격 유닛에 붙이는 옵셔널 컴포넌트(`UnitEffects`/`LaserBeamAttack`과 동일하게 없으면 그냥 무시됨, doc/0290). `UnitController`/`EnemyUnitController.Attack()`이 `attackDelivery == AttackDeliveryType.Projectile`일 때 즉시 데미지를 넣는 대신 이 컴포넌트의 `Fire()`를 호출한다 — 데미지는 투사체가 대상에 명중한 순간 처음 적용된다(Hitscan과 가장 큰 차이).

`LaserBeamAttack`과 달리 인스턴스를 재사용(풀링)하지 않고 발사마다 새로 `Instantiate`/`Destroy`한다 — 공격속도가 빠른 유닛은 이전 투사체가 아직 날아가는 중에 다음 발사가 나갈 수 있어서, 여러 발이 동시에 공중에 떠 있어야 하기 때문(doc/0290).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `projectilePrefab` | 투사체 3D 모델 프리팹 |
| `firePoints` | 발사 지점 `List<Transform>` — 다연장 무기용(`UnitEffects.firePoints`와 동일 패턴, doc/0291). 비워두면 유닛 자신의 위치에서 1발, 여러 개 채우면 공격 1회당 각 지점에서 동시에 1발씩(지점 수만큼) 발사되고 각각 명중 시 데미지가 따로 들어감 — 실질적으로 지점 수만큼 데미지가 곱연산되므로 다연장 의도가 아니면 1개만 넣을 것 |
| `projectileSpeed`(30) | 투사체 이동 속도(유닛/초) |
| `hitDistance`(0.5) | 이 거리 안으로 들어오면 명중 처리(물리 충돌 안 씀, 순수 거리 비교) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Fire(target, targetHealth, damage, attackType, isEnemyAttacker)` | `UnitController`/`EnemyUnitController.Attack()`이 호출. `damage`/`attackType`은 발사 시점 기준으로 미리 계산되어 넘어옴(장갑/배율은 명중 시점이 아니라 발사 시점 기준). `firePoints`가 비어있으면 유닛 자신 위치에서 1발, 있으면 전부에서 동시 발사 |
| `GetFirePointCount()` | 공격 1회당 동시에 발사되는 투사체 개수(`firePoints`가 비어있으면 1) — 정보 패널 툴팁의 "공격력 x2" 같은 배수 표기에 사용(doc/0293) |
| `FireFromPoint(point, target, targetHealth, damage, attackType, isEnemyAttacker)` (private) | 지점 하나에서 실제 `Instantiate` + 비행 코루틴 시작 |
| `FlyRoutine(instance, target, targetHealth, damage, attackType, isEnemyAttacker)` (private) | 매 프레임 대상 쪽으로 이동/회전, `hitDistance` 안으로 들어오면 `HealthManager.GetDamage()` 호출 후 소멸. 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController**: `attackDelivery`가 `Projectile`이고 이 컴포넌트가 붙어있으면 `Attack()`에서 즉시 데미지 대신 `Fire()` 호출(없으면 Hitscan으로 자동 폴백)
- **HealthManager**: 명중 시점에 `GetDamage()` 호출 — `isEnemyAttacker`를 그대로 전달해 아군사격 여부를 유지(doc/0292)
- **DamageTypes (AttackDeliveryType)**: `Hitscan`/`Projectile` 선택을 정의하는 열거형
- **UIController**: `GetFirePointCount()`(→ `UnitController.GetShotCount()`)를 Info Panel 툴팁의 배수 표기에 사용
