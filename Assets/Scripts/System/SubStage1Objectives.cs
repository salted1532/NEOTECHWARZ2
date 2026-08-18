using TMPro;
using UnityEngine;

// 서브미션 1("측면 기습") 임무 목표 체크리스트. Stage1Objectives와 동일한 패턴이지만 기지 없이
// 주어진 전투부대만으로 진행하는 임무라 승리 조건 외에 패배 조건도 이 스크립트가 직접 판단한다
// (Docs/Campaign.md 서브미션 1) - 배치된 부대가 전멸하면 StageManager.ReportDefeat()를 호출한다.
public class SubStage1Objectives : MonoBehaviour
{
    [Header("주목표")]
    [SerializeField] private GameObject radarBase; // 파괴해야 할 레이더 기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyRadarBaseText;

    [Header("서브목표")]
    [SerializeField] private TextMeshProUGUI eliminateScoutsText;

    private RTSUnitController rtsController;
    private bool radarBaseAssigned;
    private bool squadDeployed;

    // 서브목표(승리 조건과 무관한 체크리스트 표시용)라 초당 갱신이 필요 없음 - Stage0Objectives와
    // 동일하게 0.5초마다만 다시 스캔.
    private float scoutScanTimer;
    private bool scoutsEliminated;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
        radarBaseAssigned = radarBase != null;
        squadDeployed = rtsController != null && rtsController.UnitList.Count > 0;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool radarBaseDestroyed = radarBaseAssigned && radarBase == null;

        scoutScanTimer -= Time.deltaTime;
        if (scoutScanTimer <= 0f)
        {
            scoutScanTimer = 0.5f;
            scoutsEliminated = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        }

        ObjectiveTextUtil.SetObjectiveText(destroyRadarBaseText, LocalizationManager.GetText("objective.substage1.main1"), radarBaseDestroyed);
        ObjectiveTextUtil.SetObjectiveText(eliminateScoutsText, LocalizationManager.GetText("objective.substage1.sub1"), scoutsEliminated);

        if (radarBaseDestroyed)
        {
            StageManager.Instance?.ReportVictory();
            return;
        }

        bool squadWiped = squadDeployed && rtsController != null && rtsController.UnitList.Count == 0;
        if (squadWiped)
            StageManager.Instance?.ReportDefeat();
    }
}
