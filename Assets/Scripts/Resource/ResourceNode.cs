using UnityEngine;

public enum ResourceType { Ore, Gas }

public class ResourceNode : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int remainingAmount;

    private const float ShrinkStepPerQuarter = 0.2f;

    private int initialAmount;
    private int consumedQuarters; // 지금까지 줄어든 구간 수 (0~4)

    public ResourceType Type => resourceType;
    public bool IsDepleted => remainingAmount <= 0;

    private void Awake()
    {
        initialAmount = remainingAmount;
    }

    /// 채취 시도 시 실제로 얼마나 캐갈 수 있는지 (고갈 임박 시 amountPerTrip보다 적게 줄 수도 있음)
    public int Extract(int amountPerTrip)
    {
        int taken = Mathf.Min(amountPerTrip, remainingAmount);
        remainingAmount -= taken;

        ShrinkByRemainingRatio();

        if (remainingAmount <= 0)
            Destroy(gameObject);

        return taken;
    }

    // 초기량의 1/4씩 줄어들 때마다 크기를 0.2씩 축소
    private void ShrinkByRemainingRatio()
    {
        if (initialAmount <= 0)
            return;

        float quarterAmount = initialAmount / 4f;
        int targetQuarters = Mathf.Min(4, Mathf.FloorToInt((initialAmount - remainingAmount) / quarterAmount));

        while (consumedQuarters < targetQuarters)
        {
            consumedQuarters++;
            transform.localScale -= Vector3.one * ShrinkStepPerQuarter;
        }
    }
}
