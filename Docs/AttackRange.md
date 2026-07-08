# AttackRange

`Assets/Scripts/Unit/AttackRange.cs`

## 개요

유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 적 감지 및 자동 공격/추격을 담당한다. 공격력 수치 자체는 더 이상 이 컴포넌트가 갖고 있지 않고 `UnitController.attackDamage`로 옮겨졌다 — 이 컴포넌트는 오직 "누구를 때릴지/쫓을지" 판단만 담당한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `UnitRange` | `int` | 공격 사거리 |
| `unitController` | `UnitController` | 부모 오브젝트의 유닛 컨트롤러 |
| `enemiesInRange` | `List<GameObject>` | 트리거 범위 안에 들어와 있는 "Enemy" 태그 오브젝트 목록 |
| `HasEnemyInRange` | `bool` (프로퍼티) | 파괴되지 않은 적이 범위 안에 하나라도 있는지 매번 실시간 계산 — `UnitController.AttackOrderTick`/`FriendlyAttackTick`/`FollowTick`이 "지금 교전 중이라 정지된 상태인지" 판단할 때 조회 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 이 콜라이더는 유닛의 자식이므로 부모(`transform.parent`)에서 `UnitController`를 찾아 캐싱 |
| `OnTriggerEnter(other)` | 적("Enemy" 태그)이 감지 범위(트리거)에 들어오면 `enemiesInRange`에 추가 |
| `OnTriggerExit(other)` | 적이 감지 범위를 벗어나면 목록에서 제거 |
| `Update()` | 매 프레임 이미 파괴된 대상을 정리하고, `GetPreferredTarget()`으로 대상을 찾아 사거리 안이면 공격(`unitController.Attack`), 범위 밖이지만 Idle 상태면 추격(`unitController.ChaseTarget`) |
| `GetPreferredTarget()` (private) | `unitController.GetOrderedTarget()`으로 명시 지정된 추격 대상이 있으면(우클릭/A모드) 다른 적은 전부 무시하고 그 대상만 선택(범위 안에 없으면 이번 프레임엔 대상 없음). 지정 대상이 없을 때만 `GetClosestEnemy()` 사용 |
| `GetClosestEnemy()` (private) | 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환 |

## 동작 조건

- 공격/추격은 `unitController.IsAttack()` 또는 `IsIdle()` 상태일 때만 트리거됨 (이동/채취/건설 등 다른 명령 수행 중이면 자동 개입하지 않음)
- `Move` 상태에서는 개입하지 않으므로, "이동하면서 적을 만나면 자동 교전"하려면 호출측이 `Idle` 상태를 유지하는 명령(`AttackMoveTo`, `FollowUnit`, `ChaseTarget`)을 써야 한다

## 연관 컴포넌트

- **UnitController**: `Attack(pos, enemy)`, `ChaseTarget(pos)`, `IsAttack()`, `IsIdle()`, `GetOrderedTarget()` 호출/참조
- **EnemyController**: `GetOrderedTarget()`이 반환하는 지정 추격 대상의 타입
