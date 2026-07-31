using System.Collections;
using UnityEngine;

// 가디언 드론의 액티브 스킬 2종 (doc/0323).
// - 집중 포화(단일 유닛 지정형): 강화 폭격 투사체를 일정 간격으로 3회 발사(기존 ProjectileAttack/Projectile 재사용).
// - 쉴드 전개(자기 자신 지정형): 일정 시간 동안 최대체력을 임시로 올려주고, 그만큼 데미지를 먼저 받거나
//   시간이 다 되면 즉시 원래대로 되돌린다(doc/0323 확정 사항).
[RequireComponent(typeof(UnitController))]
public class GuardianDroneSkill : MonoBehaviour, IUnitSkill
{
    [SerializeField] private int barrageDamagePerShot = 100;
    [SerializeField] private int barrageShotCount = 3;
    [SerializeField] private float barrageShotInterval = 0.25f;

    [SerializeField] private int shieldBonusHealth = 150;
    [SerializeField] private float shieldDuration = 20f;

    public void Activate(UnitController unit, RTSUnitController.TraitChoice trait, UnitTraitOption traitData, SkillActivationContext context)
    {
        if (context.unitTarget != null)
            StartCoroutine(BarrageRoutine(unit, context.unitTarget));
        else if (unit.TryGetComponent(out HealthManager healthManager))
            StartCoroutine(ShieldRoutine(healthManager));
    }

    private IEnumerator BarrageRoutine(UnitController unit, GameObject target)
    {
        ProjectileAttack projectileAttack = unit.GetComponent<ProjectileAttack>();

        for (int i = 0; i < barrageShotCount; i++)
        {
            if (target == null || projectileAttack == null)
                yield break; // 대상이 중간에 파괴되면 남은 발사를 취소

            if (target.TryGetComponent(out HealthManager targetHealth))
                projectileAttack.Fire(target.transform, targetHealth, barrageDamagePerShot, AttackEffectType.Explosive, isEnemyAttacker: false);

            yield return new WaitForSeconds(barrageShotInterval);
        }
    }

    private IEnumerator ShieldRoutine(HealthManager healthManager)
    {
        int damageTaken = 0;
        void OnDamaged(int amount, Vector3 pos, AttackEffectType type, bool isEnemyAttacker) => damageTaken += amount;

        healthManager.SetMaxHealth(healthManager.GetMaxHealth() + shieldBonusHealth);
        healthManager.Heal(shieldBonusHealth); // 최대치만 올리면 회복이 안 되므로 버프량만큼 즉시 채워줌
        healthManager.OnDamaged += OnDamaged;

        float elapsed = 0f;
        while (elapsed < shieldDuration && damageTaken < shieldBonusHealth)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        healthManager.OnDamaged -= OnDamaged;
        healthManager.SetMaxHealth(healthManager.GetMaxHealth() - shieldBonusHealth); // 현재체력도 자동으로 clamp됨
    }
}
