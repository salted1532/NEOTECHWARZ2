# 0319. 발사자(스카이랜서)가 죽어도 투사체가 계속 날아가도록 수정 (제안)

날짜: 2026-07-31

## 요청 내용

> skylancer가 공격하는 도중에 죽으면 투사체가 중간에 멈춘 상태로 아무일도 안일어나는데 스카이랜서가
> 죽으면 투사체가 사라지던지 아니면 끝까지 적한테 날라가도록 해줘

## 원인 조사

`Assets/Scripts/Unit/ProjectileAttack.cs`의 설계(`doc/0290`, `doc/0291`)상, 투사체 인스턴스 자체에는
아무 스크립트도 없고 **이동/명중 로직 전부가 발사자(스카이랜서)에 붙은 `ProjectileAttack` 컴포넌트의
코루틴(`FlyRoutine`)**으로 처리된다:

```csharp
private void FireFromPoint(...)
{
    ...
    GameObject instance = Instantiate(projectilePrefab, point.position, Quaternion.LookRotation(toTarget));
    StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType, isEnemyAttacker)); // <- 발사자 자신에서 시작
}
```

유니티 코루틴은 그걸 시작한 GameObject/컴포넌트에 종속된다. `UnitController.Die()`가
`Destroy(gameObject)`로 스카이랜서를 즉시 파괴하면, 그 위에서 돌고 있던 `FlyRoutine` 코루틴도
그 순간 아무 예외/정리 코드 없이 그냥 끊긴다. 이미 `Instantiate`된 투사체 오브젝트 자체는 스카이랜서의
자식이 아니라서 파괴되지 않고 씬에 남지만, 더 이상 아무도 움직이거나 `Destroy`해주지 않아서 그 자리에
영원히 멈춰버린다 - 정확히 이번 버그 증상.

(참고: `target`이 비행 중 파괴되는 경우는 `while (target != null)` 체크로 이미 처리되어 있음 - 이번
문제는 그것과 다른 케이스, "발사자"가 죽는 경우.)

## 해결 방향 (요청의 "끝까지 적한테 날라가도록"을 채택)

두 옵션(사라짐 vs 끝까지 비행) 중, **근본 원인을 고치는 방향으로 "끝까지 비행"을 채택**: 이동/명중
로직을 발사자가 아니라 **투사체 인스턴스 자기 자신**이 담당하도록 옮긴다. 그러면 발사자의 생존 여부와
완전히 무관해지고(이미 `target != null` 체크로 대상 파괴는 처리되는 것과 동일한 원리), 발사자를 죽여도
막 하늘을 날아가던 투사체가 물리적으로 계속 날아가 명중/소멸하는 게 자연스럽다. 발사자 쪽 죽음 처리
코드(`UnitController.Die()`)는 건드리지 않는다 - 모든 발사자(스카이랜서 외 다른 투사체 유닛 포함)에
공통 적용되는 근본 수정.

## 코드 변경

### `Assets/Scripts/Unit/Projectile.cs` (신규)

투사체 프리팹에 자동으로 붙는(코드에서 `AddComponent`, 프리팹 에셋을 직접 수정할 필요 없음) 이동/명중
전담 컴포넌트.

```csharp
using UnityEngine;

// 투사체 인스턴스 자신에게 붙어서 이동/명중을 처리한다 - 예전엔 발사자(ProjectileAttack)의 코루틴이
// 담당했는데, 발사자가 비행 중 죽으면(Destroy(gameObject)) 그 코루틴도 같이 끊겨서 투사체가 허공에
// 멈춰버리는 문제가 있었다(doc/0319). 발사자 생존 여부와 완전히 무관하게 동작하도록 소유권을 옮김.
public class Projectile : MonoBehaviour
{
    private Transform target;
    private HealthManager targetHealth;
    private int damage;
    private AttackEffectType attackType;
    private bool isEnemyAttacker;
    private float speed;
    private float hitDistance;

    public void Launch(Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType,
        bool isEnemyAttacker, float speed, float hitDistance)
    {
        this.target = target;
        this.targetHealth = targetHealth;
        this.damage = damage;
        this.attackType = attackType;
        this.isEnemyAttacker = isEnemyAttacker;
        this.speed = speed;
        this.hitDistance = hitDistance;
    }

    private void Update()
    {
        if (target == null) // 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸
        {
            Destroy(gameObject);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.magnitude <= hitDistance)
        {
            targetHealth?.GetDamage(damage, transform.position, attackType, isEnemyAttacker); // 명중 - 여기서 처음 데미지 적용
            Destroy(gameObject);
            return;
        }

        transform.position += toTarget.normalized * speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(toTarget);
    }
}
```

### `Assets/Scripts/Unit/ProjectileAttack.cs` (수정)

**기존 코드**:
```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
```
```csharp
    private void FireFromPoint(Transform point, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType, bool isEnemyAttacker)
    {
        Vector3 toTarget = target.position - point.position;
        GameObject instance = Instantiate(projectilePrefab, point.position, Quaternion.LookRotation(toTarget));
        StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType, isEnemyAttacker));
    }

    private IEnumerator FlyRoutine(GameObject instance, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType, bool isEnemyAttacker)
    {
        while (target != null) // 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸
        {
            Vector3 toTarget = target.position - instance.transform.position;
            if (toTarget.magnitude <= hitDistance)
            {
                targetHealth?.GetDamage(damage, instance.transform.position, attackType, isEnemyAttacker); // 명중 - 여기서 처음 데미지 적용
                break;
            }

            instance.transform.position += toTarget.normalized * projectileSpeed * Time.deltaTime;
            instance.transform.rotation = Quaternion.LookRotation(toTarget);
            yield return null;
        }

        Destroy(instance);
    }
}
```

**변경 코드**:
```csharp
using System.Collections.Generic;
using UnityEngine;
```
```csharp
    // 이동/명중은 이제 투사체 인스턴스 자신(Projectile 컴포넌트)이 담당한다 - 발사자가 비행 중 죽어도
    // (doc/0319) 계속 날아가도록. 프리팹에 이미 Projectile이 붙어있으면 그걸 쓰고, 없으면 자동으로
    // 추가한다(프리팹 에셋을 직접 수정할 필요 없음).
    private void FireFromPoint(Transform point, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType, bool isEnemyAttacker)
    {
        Vector3 toTarget = target.position - point.position;
        GameObject instance = Instantiate(projectilePrefab, point.position, Quaternion.LookRotation(toTarget));

        Projectile projectile = instance.GetComponent<Projectile>();
        if (projectile == null)
            projectile = instance.AddComponent<Projectile>();

        projectile.Launch(target, targetHealth, damage, attackType, isEnemyAttacker, projectileSpeed, hitDistance);
    }
}
```

## 요약

- 신규 `Projectile.cs`가 투사체 이동/명중을 전담 - 발사자(스카이랜서 등)가 죽어도 영향받지 않음.
- `ProjectileAttack.cs`에서 `FlyRoutine` 코루틴 제거, `FireFromPoint`가 `Projectile.Launch(...)`를
  호출하도록 변경. `using System.Collections;`(코루틴용)도 더 이상 필요 없어 제거.
- 동작/데미지 판정 로직 자체는 그대로 - 코루틴이던 걸 `Update()`로 옮긴 것뿐이라 명중 판정, 대상 파괴
  처리 등은 기존과 동일하게 작동.
- 스카이랜서뿐 아니라 `ProjectileAttack`을 쓰는 모든 투사체 유닛(아군/적 공통)에 적용되는 근본 수정.

## 영향받는 파일

- `Assets/Scripts/Unit/Projectile.cs` (신규)
- `Assets/Scripts/Unit/ProjectileAttack.cs` (수정)

## 다음 단계

이대로 수정해도 될지 확인 부탁드립니다.
