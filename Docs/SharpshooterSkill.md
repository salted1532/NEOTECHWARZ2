# SharpshooterSkill

`Assets/Scripts/Unit/Skills/SharpshooterSkill.cs`

## 개요

샤프슈터의 액티브 스킬 2종을 구현한다(doc/0323). 저격은 단일 유닛 지정형으로 즉시 고정 데미지를 입히고, 은신은 자기 자신 지정형으로 일정 시간 동안 `EnemyAttackRange`의 감지 대상에서 제외되며 반투명 흰색으로 표시된다 — 지속시간이 끝날 때만 풀리고 그동안 공격을 해도 풀리지 않는다(doc/0323 확정 사항). 두 스킬은 `targetType`이 서로 달라(`SingleUnit`/`None`) `context.unitTarget` 유무만으로 구분되므로, 에디터에서 traitA/traitB 어느 쪽으로 배정되든 이 스크립트는 신경 쓰지 않는다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `sniperDamage` | 저격 데미지 |
| `stealthDuration` | 은신 지속시간 |
| `sniperShotSfx` / `stealthSfx` | 각각 저격 발사 / 은신 시작 시 재생할 SFX |
| `stealthRoutine` | 진행 중인 은신 코루틴 (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Activate(unit, trait, traitData, context)` | `context.unitTarget`이 있으면 `Sniper()`, 없으면 `EnterStealth()` |
| `Sniper(unit, target)` (private) | 대상의 `HealthManager.GetDamage()`를 즉시 호출, SFX 재생 |
| `EnterStealth(unit)` (private) | 진행 중인 은신 코루틴이 있으면 중지 후 `StealthRoutine()` 재시작 |
| `StealthRoutine(unit)` (private) | `unit.SetStealthed(true)` + `StealthVisual.EnterStealth()` 적용 → `stealthDuration` 대기 → 원상복구 |

## 연관 컴포넌트

- **HealthManager**: 저격 데미지 적용 대상
- **StealthVisual**: 은신 중 반투명 흰색 표시를 실제로 담당
- **UnitController**: `[RequireComponent]`로 강제됨, `SetStealthed()`로 감지 제외 상태 설정
