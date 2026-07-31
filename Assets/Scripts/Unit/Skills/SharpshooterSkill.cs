using System.Collections;
using UnityEngine;

// 샤프슈터의 액티브 스킬 2종 (doc/0323).
// - 저격(단일 유닛 지정형): 즉시 고정 데미지.
// - 은신(자기 자신 지정형): 일정 시간 동안 EnemyAttackRange의 감지 대상에서 제외 + 반투명 흰색 표시.
//   지속시간이 끝날 때만 풀리고, 그동안 공격을 해도 풀리지 않는다(doc/0323 확정 사항).
// 두 스킬은 targetType이 서로 달라(SingleUnit/None) context.unitTarget 유무만으로 구분되므로, 어느 쪽이
// traitA/traitB로 배정되든(에디터에서 자유롭게 설정) 이 스크립트는 신경 쓰지 않는다.
[RequireComponent(typeof(UnitController))]
public class SharpshooterSkill : MonoBehaviour, IUnitSkill
{
    [SerializeField] private int sniperDamage = 40;
    [SerializeField] private float stealthDuration = 15f;
    [SerializeField] private AudioClip sniperShotSfx;

    private Coroutine stealthRoutine;

    public void Activate(UnitController unit, RTSUnitController.TraitChoice trait, UnitTraitOption traitData, SkillActivationContext context)
    {
        if (context.unitTarget != null)
            Sniper(unit, context.unitTarget);
        else
            EnterStealth(unit);
    }

    private void Sniper(UnitController unit, GameObject target)
    {
        if (target.TryGetComponent(out HealthManager targetHealth))
            targetHealth.GetDamage(sniperDamage, unit.transform.position, AttackEffectType.Bullet, isEnemyAttacker: false);

        if (sniperShotSfx != null)
            AudioSource.PlayClipAtPoint(sniperShotSfx, unit.transform.position);
    }

    private void EnterStealth(UnitController unit)
    {
        if (stealthRoutine != null)
            StopCoroutine(stealthRoutine);

        stealthRoutine = StartCoroutine(StealthRoutine(unit));
    }

    private IEnumerator StealthRoutine(UnitController unit)
    {
        unit.SetStealthed(true);
        unit.GetComponent<StealthVisual>()?.EnterStealth();

        yield return new WaitForSeconds(stealthDuration);

        unit.SetStealthed(false);
        unit.GetComponent<StealthVisual>()?.ExitStealth();
        stealthRoutine = null;
    }
}
