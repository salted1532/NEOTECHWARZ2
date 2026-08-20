using TMPro;
using UnityEngine;

// 서브미션 2("잔해 수색") 임무 목표 체크리스트. Stage2Objectives와 동일한 "줍기 → 따라가기 → 반납"
// 패턴을 유물 파편 하나에만 적용한다(기지 없이 전투 유닛 + 파편을 옮길 일꾼 한 기로 구성된 부대라
// 본체/파편 두 아이템을 동시에 굴리는 본편과 달리 파편 하나뿐, Docs/Campaign.md 서브미션 2).
// 서브목표(OC 회수팀 전멸)는 Stage0Objectives와 동일하게 0.5초마다 씬을 다시 스캔해 판정한다.
public class SubStage2Objectives : MonoBehaviour
{
    [Header("주목표 - 유물 파편")]
    [SerializeField] private MissionItem fragment; // 유물 파편 오브젝트 - 직접 연결
    [SerializeField] private Collider fragmentBeacon; // 파편 반납 지점의 트리거 콜라이더 - 직접 연결
    [SerializeField] private TextMeshProUGUI collectFragmentText;

    [Header("서브목표 - OC 회수팀 전멸")]
    [SerializeField] private TextMeshProUGUI eliminateRecoveryTeamText;

    [Header("판정 설정")]
    [SerializeField] private float pickupRadius = 3f;  // 일꾼이 이 범위 안에 들어오면 자동으로 듦
    [SerializeField] private Vector3 carryOffset = Vector3.up; // 들린 동안 일꾼 기준 오프셋

    private RTSUnitController rtsController;
    private UnitController fragmentCarrier;
    private bool fragmentDelivered;

    private float recoveryTeamScanTimer;
    private bool recoveryTeamEliminated;

    // 서브목표 성공 사운드 - 최초 달성 순간 1회만 재생한다(doc/0643).
    private bool recoveryTeamEliminatedSfxPlayed;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        UpdateCarry();

        recoveryTeamScanTimer -= Time.deltaTime;
        if (recoveryTeamScanTimer <= 0f)
        {
            recoveryTeamScanTimer = 0.5f;
            recoveryTeamEliminated = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        }

        ObjectiveTextUtil.SetObjectiveText(collectFragmentText, LocalizationManager.GetText("objective.substage2.main1"), fragmentDelivered);
        ObjectiveTextUtil.SetObjectiveText(eliminateRecoveryTeamText, LocalizationManager.GetText("objective.substage2.sub1"), recoveryTeamEliminated);

        if (fragmentDelivered)
            StageManager.Instance?.ReportVictory();

        PlayMissionSuccessSfxOnce(recoveryTeamEliminated, ref recoveryTeamEliminatedSfxPlayed);
    }

    private void PlayMissionSuccessSfxOnce(bool objectiveComplete, ref bool alreadyPlayed)
    {
        if (!objectiveComplete || alreadyPlayed)
            return;

        alreadyPlayed = true;
        SoundManager.Instance?.PlayMissionSuccessVoice();
    }

    private void UpdateCarry()
    {
        if (fragmentDelivered || fragment == null || rtsController == null)
            return;

        if (fragmentCarrier == null)
            fragmentCarrier = FindNearestWorkerInRange(fragment.transform.position, pickupRadius);

        if (fragmentCarrier == null)
            return;

        fragment.transform.position = fragmentCarrier.transform.position + carryOffset;

        if (fragmentBeacon != null && fragment.IsTouching(fragmentBeacon))
        {
            fragmentDelivered = true;
            fragment.gameObject.SetActive(false);
            fragmentCarrier = null;
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
