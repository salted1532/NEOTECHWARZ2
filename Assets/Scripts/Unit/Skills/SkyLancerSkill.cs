using System.Collections.Generic;
using UnityEngine;

// 스카이 랜서의 스킬 2종 (doc/0323).
// - 공중 강화(패시브, 논타겟): 이 유닛이 붙어있는 한 항상 활성. 공격이 공중 유닛에 명중할 때마다
//   화염 도트를 걸거나 갱신한다. Activate()가 아니라 UnitController.OnAttackHit 이벤트로 동작한다.
// - 지상 폭격(액티브, 범위 지정형): 사거리까지 접근 후 지정 지점 반경 안의 모든 유닛에게(아군/시전자 포함,
//   태그 구분 없음) 고정 데미지.
[RequireComponent(typeof(UnitController))]
public class SkyLancerSkill : MonoBehaviour, IUnitSkill
{
    [SerializeField] private int airBurnDamagePerTick = 2;
    [SerializeField] private float airBurnTickInterval = 1f;
    [SerializeField] private float airBurnDuration = 7f;

    [SerializeField] private int bombardDamage = 20;
    [SerializeField] private GameObject bombardEffectPrefab;
    [SerializeField] private AudioClip bombardSfx;
    [SerializeField] private float radiusIndicatorDuration = 2f; // 폭격 범위를 눈으로 확인할 수 있도록 보여주는 시간(초)

    private UnitController unit;

    private void Awake()
    {
        unit = GetComponent<UnitController>();
        unit.OnAttackHit += HandleAttackHit;
    }

    private void OnDestroy()
    {
        if (unit != null)
            unit.OnAttackHit -= HandleAttackHit;
    }

    // 공중 강화 - 패시브라 order panel 버튼이 없고, 이 유닛이 실제 공격을 명중시킬 때마다 자동으로 반응한다.
    private void HandleAttackHit(GameObject target)
    {
        bool targetIsAir =
            (target.TryGetComponent(out UnitController friendlyUnit) && friendlyUnit.IsAirUnit()) ||
            (target.TryGetComponent(out EnemyUnitController enemyUnit) && enemyUnit.IsAirUnit());

        if (!targetIsAir)
            return;

        DamageOverTimeEffect.ApplyOrRefresh(target, airBurnDamagePerTick, airBurnTickInterval, airBurnDuration,
            AttackEffectType.Flame, isEnemyAttacker: false);
    }

    // 지상 폭격 - 액티브(범위 지정형). UnitController.SkillOrderTick이 사거리 안에 들어온 뒤에만 호출해준다.
    public void Activate(UnitController unit, RTSUnitController.TraitChoice trait, UnitTraitOption traitData, SkillActivationContext context)
    {
        EffectPlayer.Spawn(bombardEffectPrefab, context.groundPoint, Quaternion.identity); // null이면 조용히 무시됨(doc/0293 패턴)

        if (bombardSfx != null)
            AudioSource.PlayClipAtPoint(bombardSfx, context.groundPoint);

        RadiusIndicator.Show(context.groundPoint, traitData.areaRadius, radiusIndicatorDuration); // 피해 범위를 눈으로 확인 가능하게

        Collider[] hits = Physics.OverlapSphere(context.groundPoint, traitData.areaRadius);
        HashSet<HealthManager> alreadyHit = new HashSet<HealthManager>();

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent(out HealthManager health) || !alreadyHit.Add(health))
                continue; // 겹치는 콜라이더가 여러 개인 유닛에게 중복 데미지가 들어가지 않도록 방지

            // 아군/적/시전자 본인 구분 없이 전부 적용 (doc/0323 확정 사항)
            health.GetDamage(bombardDamage, unit.transform.position, AttackEffectType.Explosive, isEnemyAttacker: false);
        }
    }
}
