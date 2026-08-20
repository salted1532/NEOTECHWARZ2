using TMPro;
using UnityEngine;

// 1스테이지("국경 분쟁") 임무 목표 체크리스트. Stage0Objectives와 동일한 패턴 - 완료 조건은 매 프레임
// 다시 평가해 취소선을 표시하고, 주목표(OC 전초기지 파괴)가 완료되면 StageManager.ReportVictory()를
// 호출한다. 서브목표(광물/레이더 기지/적 건물 전멸)는 체크리스트 표시만 하고 승리 조건에는
// 포함하지 않는다 (Docs/Campaign.md 미션 1).
//
// "적 건물 모두 파괴"만 예외적으로 매 프레임 스캔하지 않는다 - EnemyBuildingController.ActiveBuildings가
// 등록/파괴될 때만 이벤트를 쏘므로, 그 이벤트가 올 때만 다시 계산한다(요청사항).
public class Stage1Objectives : MonoBehaviour
{
    private const int RequiredOre = 2000;

    [Header("주목표")]
    [SerializeField] private GameObject ocMainBase; // OC 전초기지(메인기지) - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyMainBaseText;

    [Header("서브목표")]
    [SerializeField] private TerritoryZone radarBaseZone; // 점령해야 할 레이더 기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI secureOreText;
    [SerializeField] private TextMeshProUGUI captureRadarBaseText;
    [SerializeField] private TextMeshProUGUI destroyAllEnemyBuildingsText;

    private RTSUnitController rtsController;
    private bool ocMainBaseAssigned;
    private bool allEnemyBuildingsDestroyed;

    // 서브목표 성공 사운드 - 조건이 나중에 다시 깨져도 최초 달성 순간에만 1회 재생한다(doc/0643).
    private bool oreSecuredSfxPlayed;
    private bool radarCapturedSfxPlayed;
    private bool allEnemyBuildingsDestroyedSfxPlayed;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
        ocMainBaseAssigned = ocMainBase != null;
    }

    private void OnEnable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged += RefreshAllEnemyBuildingsDestroyed;
        RefreshAllEnemyBuildingsDestroyed();
    }

    private void OnDisable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged -= RefreshAllEnemyBuildingsDestroyed;
    }

    private void RefreshAllEnemyBuildingsDestroyed()
    {
        allEnemyBuildingsDestroyed = EnemyBuildingController.ActiveBuildings.Count == 0;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool mainBaseDestroyed = ocMainBaseAssigned && ocMainBase == null;
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;
        bool oreSecured = oreAmount >= RequiredOre;
        bool radarCaptured = radarBaseZone != null && radarBaseZone.Owner == CaptureOwner.Ally;

        ObjectiveTextUtil.SetObjectiveText(destroyMainBaseText, LocalizationManager.GetText("objective.stage1.main1"), mainBaseDestroyed);
        ObjectiveTextUtil.SetObjectiveText(secureOreText, LocalizationManager.GetText("objective.stage1.sub1"), oreAmount, RequiredOre);
        ObjectiveTextUtil.SetObjectiveText(captureRadarBaseText, LocalizationManager.GetText("objective.stage1.sub2"), radarCaptured);
        ObjectiveTextUtil.SetObjectiveText(destroyAllEnemyBuildingsText, LocalizationManager.GetText("objective.stage1.sub3"), allEnemyBuildingsDestroyed);

        if (mainBaseDestroyed)
            StageManager.Instance?.ReportVictory();

        PlayMissionSuccessSfxOnce(oreSecured, ref oreSecuredSfxPlayed);
        PlayMissionSuccessSfxOnce(radarCaptured, ref radarCapturedSfxPlayed);
        PlayMissionSuccessSfxOnce(allEnemyBuildingsDestroyed, ref allEnemyBuildingsDestroyedSfxPlayed);
    }

    private void PlayMissionSuccessSfxOnce(bool objectiveComplete, ref bool alreadyPlayed)
    {
        if (!objectiveComplete || alreadyPlayed)
            return;

        alreadyPlayed = true;
        SoundManager.Instance?.PlayMissionSuccessVoice();
    }
}
