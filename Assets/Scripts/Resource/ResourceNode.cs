using System.Collections.Generic;
using UnityEngine;

public enum ResourceType { Ore, Gas }

// 광물/가스 채취 지점. 여러 일꾼이 동시에 채취하지 못하도록 대기열(줄서기) 방식을 사용하며,
// 남은 양에 따라 오브젝트 크기가 단계적으로 줄어들고 고갈되면 스스로 파괴된다.
public class ResourceNode : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int remainingAmount;

    // 대기열이 이 인원 이상이면 "혼잡"으로 보고, 새로 오는 일꾼은 우선 다른 자원을 찾아보게 한다.
    // 하드 캡이 아니라 임계값일 뿐이므로, 대체 자원이 없으면 이 값을 넘겨서도 계속 줄을 설 수 있다.
    [SerializeField] private int waitWorkerCount = 2;

    private const float ShrinkStepPerQuarter = 0.2f;

    private int initialAmount;
    private int consumedQuarters; // 지금까지 줄어든 구간 수 (0~4)

    // 대기열(큐): 맨 앞(index 0)의 일꾼만 실제로 채취하고, 나머지는 자기 차례를 기다린다. 인원 제한 없음
    private readonly List<UnitController> workerQueue = new List<UnitController>();

    public ResourceType Type => resourceType;
    public bool IsDepleted => remainingAmount <= 0;

    // 대기열이 혼잡한지(= 새로 오는 일꾼이 다른 자원을 먼저 찾아봐야 하는지) 여부. 줄서기 자체를 막지는 않는다
    public bool IsCrowded => workerQueue.Count >= waitWorkerCount;

    // 대기열 등록 (인원 제한 없이 항상 성공, 이미 등록돼 있으면 그대로 유지)
    public void JoinQueue(UnitController worker)
    {
        if (!workerQueue.Contains(worker))
            workerQueue.Add(worker);
    }

    public void LeaveQueue(UnitController worker)
    {
        workerQueue.Remove(worker);
    }

    // 대기열 맨 앞(=현재 채취할 차례)인지 여부
    public bool IsTurnToGather(UnitController worker)
    {
        return workerQueue.Count > 0 && workerQueue[0] == worker;
    }

    private void Awake()
    {
        initialAmount = remainingAmount;
    }

    private void Start()
    {
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.ResourceNodeList.Add(this);
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
