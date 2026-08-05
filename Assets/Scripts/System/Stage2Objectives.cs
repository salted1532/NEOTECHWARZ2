using TMPro;
using UnityEngine;

// 2스테이지("미지의 신호") 임무 목표 체크리스트. 외계 유물/OC 연구 데이터는 트리거 콜라이더를 따로
// 붙이지 않고, 이 스크립트가 매 프레임 거리 판정으로 "줍기 → 따라가기 → 반납"을 전부 처리한다
// (스테이지당 스크립트 1개로 완결시키기 위함, 요청사항).
//
// 로직: item을 아직 아무도 안 들었으면 pickupRadius 안의 가장 가까운 일꾼을 찾아 든 상태로 만든다.
// 든 동안은 item이 매 프레임 그 일꾼 위치(+carryOffset)를 따라가고, item이 비콘 deliverRadius
// 안에 들어오면 반납 완료 처리 후 item을 비활성화한다. 든 일꾼이 죽으면(참조 null) 자동으로
// 다시 주울 수 있는 상태로 돌아간다. 유물 확보 = 주목표(승리), 연구 데이터 확보 = 서브목표.
public class Stage2Objectives : MonoBehaviour
{
    [Header("주목표 - 외계 유물")]
    [SerializeField] private Transform artifact; // 유물 오브젝트 - 직접 연결
    [SerializeField] private Transform artifactBeacon; // 유물 반납 지점 - 직접 연결
    [SerializeField] private TextMeshProUGUI collectArtifactText;

    [Header("서브목표 - OC 연구 데이터")]
    [SerializeField] private Transform researchData; // 연구 데이터 오브젝트 - 직접 연결
    [SerializeField] private Transform researchDataBeacon; // 데이터 반납 지점 - 직접 연결
    [SerializeField] private TextMeshProUGUI collectResearchDataText;

    [Header("판정 설정")]
    [SerializeField] private float pickupRadius = 3f;  // 일꾼이 이 범위 안에 들어오면 자동으로 듦
    [SerializeField] private float deliverRadius = 2f; // 비콘까지 이 범위 안이면 반납 완료
    [SerializeField] private Vector3 carryOffset = Vector3.up; // 들린 동안 일꾼 기준 오프셋

    private RTSUnitController rtsController;

    private UnitController artifactCarrier;
    private bool artifactDelivered;

    private UnitController dataCarrier;
    private bool dataDelivered;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        UpdateCarry(artifact, artifactBeacon, ref artifactCarrier, ref artifactDelivered);
        UpdateCarry(researchData, researchDataBeacon, ref dataCarrier, ref dataDelivered);

        ObjectiveTextUtil.SetObjectiveText(collectArtifactText, "(주목표) 외계 유물 확보", artifactDelivered);
        ObjectiveTextUtil.SetObjectiveText(collectResearchDataText, "(서브) OC 연구 데이터 확보", dataDelivered);

        if (artifactDelivered)
            StageManager.Instance?.ReportVictory();
    }

    private void UpdateCarry(Transform item, Transform beacon, ref UnitController carrier, ref bool delivered)
    {
        if (delivered || item == null || rtsController == null)
            return;

        if (carrier == null)
            carrier = FindNearestWorkerInRange(item.position, pickupRadius);

        if (carrier == null)
            return;

        item.position = carrier.transform.position + carryOffset;

        if (beacon != null && Vector3.Distance(item.position, beacon.position) <= deliverRadius)
        {
            delivered = true;
            item.gameObject.SetActive(false);
            carrier = null;
        }
    }

    private UnitController FindNearestWorkerInRange(Vector3 position, float radius)
    {
        UnitController nearest = null;
        float nearestDistSqr = radius * radius;

        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null || !unit.CompareTag("Worker"))
                continue;

            float distSqr = (unit.transform.position - position).sqrMagnitude;
            if (distSqr <= nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = unit;
            }
        }

        return nearest;
    }
}
