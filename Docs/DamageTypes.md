# DamageTypes

`Assets/Scripts/Unit/DamageTypes.cs`

## 개요

전투 관련 공용 열거형을 모아둔 파일(클래스가 아니라 파일 하나에 열거형 3개, 네임스페이스 없음 — 프로젝트 전역에서 바로 참조 가능).

## 열거형

| 열거형 | 값 | 설명 |
|---|---|---|
| `ArmorType` | `Light, Heavy` | 장갑 타입(경장갑: 보병/경차량, 중장갑: 전차/대형유닛). 특정 유닛의 고유 추가 데미지가 어느 쪽을 노리는지 판정하는 데 쓰인다 |
| `SizeType` | `Small, Medium, Large` | 크기 타입. 공격 방식(`AttackEffectType`)에 따른 데미지 배율(`DamageMultiplierTableSO`)을 조회하는 키로 쓰인다 |
| `AttackDeliveryType` | `Hitscan, Projectile` | 공격 전달 방식(doc/0290). `Hitscan`은 즉시 명중(기존 동작), `Projectile`은 투사체가 날아가 명중해야 데미지가 적용됨(`ProjectileAttack` 필요) |

> `AttackEffectType`(`Bullet, Explosive, Laser, Flame`)은 이 파일이 아니라 `UnitController.cs`에 정의되어 있다(공격 수단 — 피격 이펙트 선택/데미지 배율 조회에 사용).

## 연관 컴포넌트

- **UnitController / EnemyUnitController**: `armorType`/`sizeType`/`attackDelivery` 필드로 사용, `UnitDataSO`에서 값을 받아옴
- **UnitDataSO**: `UnitData.armorType`/`sizeType`/`attackDelivery` 필드
- **DamageMultiplierTableSO**: `AttackEffectType × SizeType` 데미지 배율표 조회 키
- **ProjectileAttack**: `attackDelivery == Projectile`일 때만 관여
