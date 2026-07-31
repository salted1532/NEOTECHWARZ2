using System.Collections;
using UnityEngine;

// 대상에게 붙여서 일정 시간 동안 주기적으로 데미지를 주는 범용 도트 컴포넌트 (doc/0323).
// 이미 붙어있는 상태에서 다시 요청하면(재공격) 지속시간만 새로 갱신한다 - 스택은 쌓이지 않는다.
public class DamageOverTimeEffect : MonoBehaviour
{
    private HealthManager targetHealth;
    private int damagePerTick;
    private float tickInterval;
    private float remainingDuration;
    private AttackEffectType attackType;
    private bool isEnemyAttacker;
    private Coroutine routine;

    public static void ApplyOrRefresh(GameObject target, int damagePerTick, float tickInterval, float duration,
        AttackEffectType attackType, bool isEnemyAttacker)
    {
        DamageOverTimeEffect effect = target.GetComponent<DamageOverTimeEffect>();
        if (effect == null)
            effect = target.AddComponent<DamageOverTimeEffect>();

        effect.Setup(damagePerTick, tickInterval, duration, attackType, isEnemyAttacker);
    }

    private void Setup(int damagePerTick, float tickInterval, float duration, AttackEffectType attackType, bool isEnemyAttacker)
    {
        targetHealth = GetComponent<HealthManager>();
        this.damagePerTick = damagePerTick;
        this.tickInterval = tickInterval;
        this.attackType = attackType;
        this.isEnemyAttacker = isEnemyAttacker;
        remainingDuration = duration; // 이미 돌고 있었다면 지속시간만 새로 덮어씀(재공격 시 갱신)

        if (routine == null)
            routine = StartCoroutine(TickRoutine());
    }

    private IEnumerator TickRoutine()
    {
        while (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(tickInterval);
            remainingDuration -= tickInterval;
            targetHealth?.GetDamage(damagePerTick, transform.position, attackType, isEnemyAttacker);
        }

        Destroy(this);
    }
}
