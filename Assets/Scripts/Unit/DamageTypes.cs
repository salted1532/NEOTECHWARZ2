// 장갑 타입: 경장갑(보병/경차량) vs 중장갑(전차/대형유닛). 특정 유닛의 고유 추가 데미지가 어느 쪽을 노리는지 판정하는 데 쓰인다.
public enum ArmorType { Light, Heavy }

// 크기 타입: 공격 방식(AttackEffectType)에 따른 데미지 배율(DamageMultiplierTableSO)을 조회하는 키로 쓰인다.
public enum SizeType { Small, Medium, Large }

// 공격 전달 방식: Hitscan(즉시 명중, 기존 동작) vs Projectile(투사체가 날아가 명중해야 데미지 적용, doc/0290).
public enum AttackDeliveryType { Hitscan, Projectile }
