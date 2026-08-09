# Projectile

`Assets/Scripts/Unit/Projectile.cs`

## 개요

투사체 인스턴스 자신에게 붙어서 이동/명중을 처리한다. 예전엔 발사자(`ProjectileAttack`)의 코루틴이 담당했는데, 발사자가 비행 중 죽으면(`Destroy(gameObject)`) 그 코루틴도 같이 끊겨서 투사체가 허공에 멈춰버리는 문제가 있었다(doc/0319). 이 스크립트로 소유권을 옮겨 발사자 생존 여부와 완전히 무관하게 동작하도록 했다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `target` | 추적할 대상 트랜스폼 |
| `targetHealth` | 명중 시 데미지를 적용할 `HealthManager` |
| `damage` / `attackType` / `isEnemyAttacker` | 데미지량, 피격 이펙트 종류, 공격자 진영 |
| `speed` / `hitDistance` | 이동 속도, 명중으로 판정할 거리 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Launch(target, targetHealth, damage, attackType, isEnemyAttacker, speed, hitDistance)` | 발사 시 필요한 값을 전부 세팅 |
| `Update()` | 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸. 대상과의 거리가 `hitDistance` 이내면 데미지 적용 후 소멸. 그 외에는 대상 방향으로 이동 + 회전 |

## 연관 컴포넌트

- **ProjectileAttack**: `Launch()`로 이 컴포넌트를 초기화해 발사자로부터 소유권을 넘김
- **HealthManager**: 명중 시 `GetDamage()` 호출 대상
