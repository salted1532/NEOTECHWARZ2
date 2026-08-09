# GuardianDroneSkill

`Assets/Scripts/Unit/Skills/GuardianDroneSkill.cs`

## 개요

가디언 드론의 액티브 스킬 2종을 구현한다(doc/0323). 집중 포화는 단일 유닛 지정형으로 강화 폭격 투사체를 일정 간격으로 3회 발사하며(기존 `ProjectileAttack`/`Projectile` 재사용), 쉴드 전개는 자기 자신 지정형으로 일정 시간 동안 최대체력을 임시로 올려주고 그만큼 데미지를 먼저 받거나 시간이 다 되면 즉시 원래대로 되돌린다. `IUnitSkill`을 구현해 `RTSUnitController`의 특성(trait) 시스템에 연결된다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `barrageDamagePerShot` / `barrageShotCount` / `barrageShotInterval` | 집중 포화 발당 데미지, 발사 횟수(기본 3), 발사 간격 |
| `barrageShotSfx` | 발사마다 재생할 SFX (비워두면 조용히 무시) |
| `shieldBonusHealth` / `shieldDuration` | 쉴드로 늘어나는 최대체력량과 지속시간 |
| `shieldActivateSfx` | 쉴드 전개 시작 시 재생할 SFX |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Activate(unit, trait, traitData, context)` | `context.unitTarget`이 있으면 `BarrageRoutine`(집중 포화), 없으면 `ShieldRoutine`(쉴드 전개) 실행 |
| `BarrageRoutine(unit, target)` (private) | `barrageShotInterval` 간격으로 `barrageShotCount`회 반복 발사. 대상이 중간에 파괴되면(`target == null`) 남은 발사를 취소 |
| `ShieldRoutine(healthManager)` (private) | 최대체력을 `shieldBonusHealth`만큼 올리고 그만큼 즉시 회복(최대치만 올리면 자동 회복이 안 되므로), `OnDamaged` 이벤트로 누적 피해량을 추적하다가 지속시간이 끝나거나 버프량만큼 피해를 받으면 최대체력을 원래대로 되돌림(현재체력도 자동 clamp됨) |

## 연관 컴포넌트

- **ProjectileAttack / Projectile**: 집중 포화 발사에 재사용
- **HealthManager**: 쉴드 전개의 최대체력 증감 및 `OnDamaged` 이벤트 구독 대상
- **UnitController**: `[RequireComponent]`로 강제됨, 스킬이 부착되는 유닛 본체
