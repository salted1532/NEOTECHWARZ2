using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 투사체 공격 유닛에 붙이는 옵셔널 컴포넌트(UnitEffects/LaserBeamAttack과 동일하게 없으면 그냥 무시됨, doc/0290).
// UnitController/EnemyUnitController.Attack()이 attackDelivery == Projectile일 때 즉시 데미지를 넣는 대신
// Fire()를 호출한다 - 데미지는 여기서 투사체가 대상에 명중한 순간 처음 적용된다.
//
// LaserBeamAttack과 달리 인스턴스를 재사용(풀링)하지 않고 발사마다 새로 Instantiate/Destroy한다 - 공격속도가
// 빠른 유닛은 이전 투사체가 아직 날아가는 중에 다음 발사가 나갈 수 있어서, 여러 발이 동시에 공중에 떠
// 있어야 하기 때문(doc/0290).
public class ProjectileAttack : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    // 다연장 무기용 - UnitEffects.firePoints와 동일한 패턴(doc/0291). 비워두면 유닛 자신의 위치에서 1발
    // 발사, 여러 개 채우면 공격 1회당 각 지점에서 동시에 1발씩(총 지점 수만큼) 발사되고 각각 명중 시
    // 데미지가 따로 들어간다 - 지점 수만큼 데미지가 곱연산되는 효과이므로 다연장 무기 의도가 아니면 1개만.
    [SerializeField] private List<Transform> firePoints = new();
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float hitDistance = 0.5f; // 이 거리 안으로 들어오면 명중 처리 (물리 충돌 안 씀)

    // UnitController/EnemyUnitController.Attack()이 호출. targetHealth/damage/attackType은 발사 시점 기준으로
    // 미리 계산되어 넘어온다 - 장갑/배율은 명중 시점이 아니라 발사 시점 기준(비행 중 대상 장갑이 바뀌는
    // 경우는 없다고 가정, 데미지 계산 로직을 여기서 중복하지 않기 위함).
    public void Fire(Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
    {
        if (projectilePrefab == null || target == null)
            return;

        if (firePoints == null || firePoints.Count == 0)
        {
            FireFromPoint(transform, target, targetHealth, damage, attackType);
            return;
        }

        foreach (Transform point in firePoints)
        {
            if (point != null)
                FireFromPoint(point, target, targetHealth, damage, attackType);
        }
    }

    private void FireFromPoint(Transform point, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
    {
        Vector3 toTarget = target.position - point.position;
        GameObject instance = Instantiate(projectilePrefab, point.position, Quaternion.LookRotation(toTarget));
        StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType));
    }

    private IEnumerator FlyRoutine(GameObject instance, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
    {
        while (target != null) // 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸
        {
            Vector3 toTarget = target.position - instance.transform.position;
            if (toTarget.magnitude <= hitDistance)
            {
                targetHealth?.GetDamage(damage, instance.transform.position, attackType); // 명중 - 여기서 처음 데미지 적용
                break;
            }

            instance.transform.position += toTarget.normalized * projectileSpeed * Time.deltaTime;
            instance.transform.rotation = Quaternion.LookRotation(toTarget);
            yield return null;
        }

        Destroy(instance);
    }
}
