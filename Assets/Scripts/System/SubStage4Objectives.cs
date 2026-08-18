using TMPro;
using UnityEngine;

// 서브미션 4("최후의 저지선") 임무 목표 체크리스트. 고정 배치된 혼성 방어부대로 시작해 정해진
// 시간(기본 20분) 동안 방어선을 사수하면 승리, 그 전에 부대가 전멸하면 패배한다(Docs/Campaign.md
// 서브미션 4). 서브목표(최소 병력 이상 생존)는 승리/패배와 무관하게 완료 시점의 생존 인원만 표시하는
// 체크리스트용이라 승리 조건에는 포함하지 않는다.
public class SubStage4Objectives : MonoBehaviour
{
    [Header("주목표 - 방어선 사수")]
    [SerializeField] private float defenseDurationSeconds = 20f * 60f; // 기본 20분
    [SerializeField] private TextMeshProUGUI holdTheLineText;

    [Header("서브목표 - 최소 병력 생존")]
    [SerializeField] private int minimumSurvivingUnits = 5; // 인스펙터에서 조정
    [SerializeField] private TextMeshProUGUI minimumSurvivorsText;

    private RTSUnitController rtsController;
    private float elapsedSeconds;
    private bool squadDeployed;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
        squadDeployed = rtsController != null && rtsController.UnitList.Count > 0;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int aliveCount = rtsController != null ? rtsController.UnitList.Count : 0;
        bool squadWiped = squadDeployed && aliveCount == 0;

        if (squadWiped)
        {
            StageManager.Instance?.ReportDefeat();
            return;
        }

        elapsedSeconds += Time.deltaTime;
        bool timeSurvived = elapsedSeconds >= defenseDurationSeconds;
        bool minimumMet = aliveCount >= minimumSurvivingUnits;

        ObjectiveTextUtil.SetObjectiveText(holdTheLineText, LocalizationManager.GetText("objective.substage4.main1"), Mathf.FloorToInt(elapsedSeconds), Mathf.FloorToInt(defenseDurationSeconds));
        ObjectiveTextUtil.SetObjectiveText(minimumSurvivorsText, LocalizationManager.GetText("objective.substage4.sub1"), minimumMet);

        if (timeSurvived)
            StageManager.Instance?.ReportVictory();
    }
}
