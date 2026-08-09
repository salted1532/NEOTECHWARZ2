# SkyLancerSkill

`Assets/Scripts/Unit/Skills/SkyLancerSkill.cs`

## 개요

스카이 랜서의 스킬 2종을 구현한다(doc/0323). 공중 강화는 패시브(논타겟)로 이 유닛이 붙어있는 한 항상 활성 상태이며, 공격이 공중 유닛에 명중할 때마다 화염 도트를 걸거나 갱신한다 — `Activate()`가 아니라 `UnitController.OnAttackHit` 이벤트로 동작한다. 지상 폭격은 액티브(범위 지정형)로 사거리까지 접근 후 지정 지점 반경 안의 모든 유닛에게(아군/시전자 포함, 태그 구분 없음) 고정 데미지를 입힌다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `airBurnDamagePerTick` / `airBurnTickInterval` / `airBurnDuration` | 공중 강화 화염 도트의 틱당 데미지, 간격, 지속시간 |
| `bombardDamage` | 지상 폭격 데미지 |
| `bombardEffectPrefab` | 폭격 지점에 재생할 이펙트 프리팹 (null이면 조용히 무시, doc/0293 패턴) |
| `bombardSfx` | 폭격 SFX |
| `radiusIndicatorDuration` | 폭격 범위를 눈으로 확인할 수 있도록 `RadiusIndicator`를 보여주는 시간 |
| `unit` | 이 스킬이 붙은 `UnitController` (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `UnitController` 캐싱, `OnAttackHit` 이벤트 구독 |
| `OnDestroy()` | 이벤트 구독 해제 |
| `HandleAttackHit(target)` (private) | 대상이 공중 유닛(아군/적 컨트롤러 어느 쪽이든)이면 `DamageOverTimeEffect.ApplyOrRefresh()`로 화염 도트를 걸거나 갱신 |
| `Activate(unit, trait, traitData, context)` | 지상 폭격 실행 — 이펙트/SFX 재생, `RadiusIndicator.Show()`로 범위 표시, `Physics.OverlapSphere`로 범위 내 유닛을 찾아 (콜라이더 중복 방지 후) 전부 데미지 적용. `UnitController.SkillOrderTick`이 사거리 안에 들어온 뒤에만 호출해줌 |

## 연관 컴포넌트

- **UnitController**: `[RequireComponent]`로 강제됨, `OnAttackHit` 이벤트 발행처
- **DamageOverTimeEffect**: 공중 강화 화염 도트 적용에 사용
- **RadiusIndicator**: 지상 폭격 범위 시각화
- **EffectPlayer**: 폭격 이펙트 스폰
