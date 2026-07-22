using UnityEngine;

// 공격 방식(소총/폭발/레이저/화염) x 대상 크기(소형/중형/대형)별 데미지 배율표.
// 코드에 하드코딩하지 않고 별도 에셋으로 분리해서, 밸런스 수치를 인스펙터에서 언제든 조정할 수 있게 한다.
[CreateAssetMenu]
public class DamageMultiplierTableSO : ScriptableObject
{
    [System.Serializable]
    public class SizeMultiplier
    {
        [Tooltip("퍼센트 값. 100 = 기본 데미지 그대로, 130 = +30%, 60 = -40%")]
        public float smallPercent = 100f;
        public float mediumPercent = 100f;
        public float largePercent = 100f;

        public float GetPercent(SizeType size)
        {
            switch (size)
            {
                case SizeType.Small: return smallPercent;
                case SizeType.Medium: return mediumPercent;
                case SizeType.Large: return largePercent;
                default: return 100f;
            }
        }
    }

    public SizeMultiplier bullet = new SizeMultiplier { smallPercent = 100f, mediumPercent = 80f, largePercent = 60f };
    public SizeMultiplier explosive = new SizeMultiplier { smallPercent = 70f, mediumPercent = 100f, largePercent = 130f };
    public SizeMultiplier laser = new SizeMultiplier { smallPercent = 100f, mediumPercent = 100f, largePercent = 100f };
    public SizeMultiplier flame = new SizeMultiplier { smallPercent = 130f, mediumPercent = 100f, largePercent = 60f };

    // attackType/targetSize 조합에 해당하는 배율(1.0 = 100%)을 반환한다.
    public float GetMultiplier(AttackEffectType attackType, SizeType targetSize)
    {
        SizeMultiplier table = attackType switch
        {
            AttackEffectType.Bullet => bullet,
            AttackEffectType.Explosive => explosive,
            AttackEffectType.Laser => laser,
            AttackEffectType.Flame => flame,
            _ => null
        };

        return table != null ? table.GetPercent(targetSize) / 100f : 1f;
    }
}
