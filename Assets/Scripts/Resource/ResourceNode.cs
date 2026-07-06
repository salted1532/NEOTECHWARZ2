using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceType { Ore, Gas }

// 광물/가스 채취 지점. 여러 일꾼이 동시에 채취하지 못하도록 대기열(줄서기) 방식을 사용하며,
// 남은 양에 따라 오브젝트 크기가 단계적으로 줄어들고 고갈되면 스스로 파괴된다.
public class ResourceNode : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int remainingAmount;

    [SerializeField] private GameObject resourceMarker; // 선택 시 표시할 마커 (Unit/BuildingController와 동일한 패턴)
    [SerializeField] private Sprite icon; // Info_panel에 표시할 아이콘

    [SerializeField] private float flashInterval = 0.3f; // 채취 명령(우클릭) 피드백 깜빡임 간격
    [SerializeField] private int flashCount = 3;          // 깜빡이는 횟수

    private Coroutine flashRoutine;
    private RTSUnitController rtsController;

    // 대기열이 이 인원 이상이면 "혼잡"으로 보고, 새로 오는 일꾼은 우선 다른 자원을 찾아보게 한다.
    // 하드 캡이 아니라 임계값일 뿐이므로, 대체 자원이 없으면 이 값을 넘겨서도 계속 줄을 설 수 있다.
    [SerializeField] private int waitWorkerCount = 2;

    private const float ShrinkStepPerQuarter = 0.2f;
    private const float MinScale = 0.1f; // 스케일이 0 이하로 내려가 메시가 뒤집히는 것 방지

    private int initialAmount;
    private int consumedQuarters; // 지금까지 줄어든 구간 수 (0~4)

    private CapsuleCollider nodeCollider;
    private float colliderBaseRadius;
    private float colliderBaseHeight;
    private Vector3 colliderBaseCenter;

    // 대기열(큐): 맨 앞(index 0)의 일꾼만 실제로 채취하고, 나머지는 자기 차례를 기다린다. 인원 제한 없음
    private readonly List<UnitController> workerQueue = new List<UnitController>();

    public ResourceType Type => resourceType;
    public bool IsDepleted => remainingAmount <= 0;
    public int RemainingAmount => remainingAmount;

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

        nodeCollider = GetComponent<CapsuleCollider>();
        if (nodeCollider != null)
        {
            colliderBaseRadius = nodeCollider.radius;
            colliderBaseHeight = nodeCollider.height;
            colliderBaseCenter = nodeCollider.center;
        }
    }

    private void Start()
    {
        if (resourceMarker != null)
            resourceMarker.SetActive(false);

        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController?.ResourceNodeList.Add(this);
    }

    // 자원 노드 선택 시 마커(테두리 등 표시)를 활성화한다.
    public void SelectResource()
    {
        if (resourceMarker != null)
            resourceMarker.SetActive(true);
    }

    // 자원 노드 선택 해제 시 마커를 비활성화한다.
    public void DeselectResource()
    {
        if (resourceMarker != null)
            resourceMarker.SetActive(false);
    }

    public Sprite GetIcon() => icon;

    // 채취 명령(우클릭)을 받았을 때 "어느 자원이 대상인지" 피드백으로 마커를 짧게 깜빡인다.
    // 좌클릭 선택 마커와 같은 오브젝트를 사용하므로, 끝나면 실제 선택 상태에 맞춰 복원한다.
    public void FlashMarker()
    {
        if (resourceMarker == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(flashInterval);

        for (int i = 0; i < flashCount; i++)
        {
            resourceMarker.SetActive(true);
            yield return wait;
            resourceMarker.SetActive(false);
            yield return wait;
        }

        // 깜빡이는 도중 좌클릭으로 선택된 상태였다면(드문 경우) 꺼진 채로 두지 않고 선택 마커 상태로 복원
        resourceMarker.SetActive(rtsController != null && rtsController.selectedResourceNode == this);

        flashRoutine = null;
    }

    /// 채취 시도 시 실제로 얼마나 캐갈 수 있는지 (고갈 임박 시 amountPerTrip보다 적게 줄 수도 있음)
    public int Extract(int amountPerTrip)
    {
        int taken = Mathf.Min(amountPerTrip, remainingAmount);
        remainingAmount -= taken;

        ShrinkByRemainingRatio();

        if (remainingAmount <= 0)
        {
            // 채취 중이던(선택된) 채로 고갈된 경우 UI(Info_panel)가 유령 참조를 들고 있지 않도록 정리
            rtsController?.ClearSelectedResourceIfMatches(this);

            Destroy(gameObject);
        }

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

            Vector3 scale = transform.localScale;
            float newY = Mathf.Max(scale.y - ShrinkStepPerQuarter, MinScale);
            float appliedYShrink = scale.y - newY; // 최소 스케일에 걸리면 실제로는 덜 줄어들 수 있음

            transform.localScale = new Vector3(
                Mathf.Max(scale.x - ShrinkStepPerQuarter, MinScale),
                newY,
                Mathf.Max(scale.z - ShrinkStepPerQuarter, MinScale));

            // 크기(Y)가 줄어드는 만큼(0.2씩) 위치도 그대로 아래로 내림
            transform.position -= new Vector3(0f, appliedYShrink, 0f);
        }

        ApplyColliderSizeCompensation();
    }

    // transform.localScale이 작아져도 콜라이더의 실제(월드) 크기는 최초 그대로 유지되도록,
    // Unity가 스케일을 곱해서 계산하는 만큼을 미리 나눠서 상쇄한다.
    private void ApplyColliderSizeCompensation()
    {
        if (nodeCollider == null)
            return;

        Vector3 scale = transform.localScale;
        float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z), 0.0001f);
        float verticalScale = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);

        nodeCollider.radius = colliderBaseRadius / radialScale;
        nodeCollider.height = colliderBaseHeight / verticalScale;
        nodeCollider.center = new Vector3(
            colliderBaseCenter.x / radialScale,
            colliderBaseCenter.y / verticalScale,
            colliderBaseCenter.z / radialScale);
    }
}
