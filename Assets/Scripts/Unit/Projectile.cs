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
