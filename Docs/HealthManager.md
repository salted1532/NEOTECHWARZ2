# HealthManager

`Assets/Scripts/Unit/HealthManager.cs`

## 개요

체력(HP) 관리 공용 컴포넌트. 유닛/건물 등 어디에나 부착해서 데미지/힐/사망 처리를 담당한다. 실제 사망 시 처리(파괴 방식)는 같은 오브젝트의 `IDestructible` 구현체에 위임한다.

## IDestructible 인터페이스

유닛/건물마다 다른 사망 처리(Destroy 방식, 이펙트 등)를 구현하기 위한 인터페이스. `HealthManager`가 체력이 0 이하가 됐을 때 `Die()`를 호출한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `maxHealth` | `int` (SerializeField) | 최대 체력 |
| `currentHp` | `int` | 현재 체력 |
| `isDead` | `bool` | 사망 여부 (중복 사망 처리 방지) |
| `OnHealthChanged` | `event Action<int,int>` | 체력 변화 시 발생 (currentHp, maxHealth) — UI(체력바 등) 갱신용 |
| `OnDeath` | `event Action` | 사망 시 발생 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `currentHp`를 `maxHealth`로 초기화 |
| `GetHealth()` / `GetMaxHealth()` / `IsDead()` | 상태 조회 |
| `GetDamage(damage)` | 데미지를 적용. 이미 죽었거나 데미지가 0 이하면 무시. 체력이 0 이하가 되면 `Die()` 호출 |
| `Heal(amount)` | 체력을 회복 (최대 체력을 넘지 않도록 제한) |
| `SetMaxHealth(newMax)` | 최대 체력을 절대값으로 재설정(음수 방지 클램프). 현재 체력이 새 최대치를 넘으면 같이 줄임. `BaseStructure`가 건설할 건물 종류에 맞춰 최대체력을 동적으로 지정할 때 사용 |
| `SetHealth(newCurrent)` | 현재 체력을 절대값으로 지정(0~maxHealth 클램프) — `Heal`/`GetDamage`처럼 상대적 증감이 아니라 특정 값으로 강제 설정. 이미 죽었으면 무시 |
| `Die()` (private) | 사망 처리: 중복 실행 방지, `OnDeath` 이벤트 발생 후 같은 오브젝트의 `IDestructible` 구현체에 실제 파괴를 위임. 구현체가 없으면 기본 `Destroy(gameObject)` 처리 |

## 연관 컴포넌트

- **UnitController.Die() / BuildingController.Die() / BaseStructure.Die()**: `IDestructible` 구현체로서 `HealthManager.Die()`에서 호출됨
- **AttackRange**: `Attack()` 로직에서 대상의 `HealthManager.GetDamage()`를 호출해 데미지 적용
- **BaseStructure**: 건설 시작 시 `SetMaxHealth`/`SetHealth(0)`으로 초기화하고, 건설 진행에 따라 매 프레임 `Heal()`로 체력을 채워 "건설 중 체력이 차오르는" 연출을 구현
