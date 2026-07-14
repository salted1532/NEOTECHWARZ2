using UnityEngine;

// 공격 타입(총기/폭발형/레이저/화염)별 피격 이펙트 프리팹 묶음.
// UnitEffects/BuildingEffects/ConstructionEffects가 전부 똑같은 4개 슬롯을 필요로 해서 하나로 모아뒀다 -
// 인스펙터에서도 항상 같은 구조로 보인다.
[System.Serializable]
public class HitEffectSet
{
    [SerializeField] private GameObject bulletHitPrefab;
    [SerializeField] private GameObject explosiveHitPrefab;
    [SerializeField] private GameObject laserHitPrefab;
    [SerializeField] private GameObject flameHitPrefab;

    public GameObject GetPrefab(AttackEffectType attackType)
    {
        switch (attackType)
        {
            case AttackEffectType.Explosive: return explosiveHitPrefab;
            case AttackEffectType.Laser: return laserHitPrefab;
            case AttackEffectType.Flame: return flameHitPrefab;
            default: return bulletHitPrefab;
        }
    }
}
