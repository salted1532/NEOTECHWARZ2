# AttackRange

`Assets/Scripts/Unit/AttackRange.cs`

## 개요

유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 적 감지 및 자동 공격/추격을 담당한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `UnitRange` | `int` | 공격 사거리 |
| `AttackDamage` | `int` | 공격 데미지 |
| `unitController` | `UnitController` | 부모 오브젝트의 유닛 컨트롤러 |
| `enemiesInRange` | `List<GameObject>` | 트리거 범위 안에 들어와 있는 "Enemy" 태그 오브젝트 목록 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 이 콜라이더는 유닛의 자식이므로 부모(`transform.parent`)에서 `UnitController`를 찾아 캐싱 |
| `OnTriggerEnter(other)` | 적("Enemy" 태그)이 감지 범위(트리거)에 들어오면 `enemiesInRange`에 추가 |
| `OnTriggerExit(other)` | 적이 감지 범위를 벗어나면 목록에서 제거 |
| `Update()` | 매 프레임 이미 파괴된 대상을 정리하고, 가장 가까운 적을 찾아 사거리 안이면 공격(`unitController.Attack`), 범위 밖이지만 Idle 상태면 추격(`unitController.ChaseTarget`) |
| `GetClosestEnemy()` (private) | 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환 |

## 동작 조건

- 공격/추격은 `unitController.IsAttack()` 또는 `IsIdle()` 상태일 때만 트리거됨 (이동/채취 등 다른 명령 수행 중이면 자동 개입하지 않음)

## 연관 컴포넌트

- **UnitController**: `Attack(pos, damage, target)`, `ChaseTarget(pos)`, `IsAttack()`, `IsIdle()` 호출/참조
