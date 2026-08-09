# DamageOverTimeEffect

`Assets/Scripts/Unit/DamageOverTimeEffect.cs`

## 개요

대상 오브젝트에 붙여서 일정 시간 동안 주기적으로 데미지를 주는 범용 도트(DoT) 컴포넌트(doc/0323). 이미 붙어있는 상태에서 다시 요청(재공격)이 들어오면 지속시간만 새로 갱신한다 — 스택은 쌓이지 않는다. 스카이 랜서의 화염 도트 등 여러 스킬이 공용으로 사용한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `targetHealth` | 데미지를 적용할 대상의 `HealthManager` (같은 오브젝트에서 조회) |
| `damagePerTick` / `tickInterval` / `remainingDuration` | 틱당 데미지, 틱 간격, 남은 지속시간 — `Setup()`으로 갱신됨 |
| `attackType` / `isEnemyAttacker` | 피격 이펙트 종류와 공격자 진영 — `HealthManager.GetDamage()` 호출에 그대로 전달 |
| `routine` | 진행 중인 틱 코루틴 (이미 돌고 있으면 재시작하지 않음) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `ApplyOrRefresh(target, damagePerTick, tickInterval, duration, attackType, isEnemyAttacker)` (static) | 대상에 컴포넌트가 없으면 추가하고, `Setup()`을 호출해 새로 걸거나 지속시간을 갱신 |
| `Setup(...)` (private) | 필드 세팅, `remainingDuration`을 새 값으로 덮어씀(재공격 시 갱신), 코루틴이 없으면 `TickRoutine()` 시작 |
| `TickRoutine()` (private) | `tickInterval`마다 `remainingDuration`을 줄이며 `targetHealth.GetDamage()` 호출, 0 이하가 되면 자기 자신을 `Destroy` |

## 연관 컴포넌트

- **HealthManager**: 실제 데미지 적용 대상
- **SkyLancerSkill**: 공중 강화 패시브에서 화염 도트를 걸 때 `ApplyOrRefresh` 호출
