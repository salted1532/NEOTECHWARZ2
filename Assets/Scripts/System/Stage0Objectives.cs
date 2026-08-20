using TMPro;
using UnityEngine;

// 0스테이지(튜토리얼) 임무 목표 체크리스트.
// 목표별 완료 조건은 매 프레임 다시 평가한다 - 자원을 다시 쓰거나 유닛이 죽는 등으로 조건이
// 깨지면 취소선도 다시 사라져야 하므로(요청사항), "한 번 완료되면 고정"하지 않는다.
// 주목표(거점 점령/트루퍼 10기/병영 건설) 3개가 모두 완료되면 StageManager.ReportVictory()를 호출한다.
// 서브목표(적 전멸/광물 1000)는 체크리스트 표시만 하고 승리 조건에는 포함하지 않는다.
public class Stage0Objectives : MonoBehaviour
{
    private const int AssaultTrooperUnitID = RTSUnitController.UnitID.Marine; // 데이터상 표시명은 "Assault Trooper"
    private const int RequiredTrooperCount = 10;
    private const int BarracksBuildingID = RTSUnitController.BuildingID.Barracks;
    private const int RequiredOre = 1000;

    [Header("주목표")]
    [SerializeField] private TerritoryZone targetZone; // 점령해야 할 거점 (씬의 TerritoryZone 오브젝트를 연결)
    [SerializeField] private TextMeshProUGUI captureZoneText;
    [SerializeField] private TextMeshProUGUI produceTroopersText;
    [SerializeField] private TextMeshProUGUI buildBarracksText;

    [Header("서브목표")]
    [SerializeField] private TextMeshProUGUI clearEnemiesText;
    [SerializeField] private TextMeshProUGUI secureOreText;

    private RTSUnitController rtsController;

    // 서브목표(승리 조건과 무관한 체크리스트 표시용)라 초당 갱신이 필요 없음 - 0.5초마다만 다시 스캔.
    private float enemyScanTimer;
    private bool enemiesCleared;

    // 서브목표 성공 사운드 - 조건이 나중에 다시 깨져도(체크리스트는 재평가되지만) 최초 달성 순간에만
    // 1회 재생한다(doc/0643).
    private bool enemiesClearedSfxPlayed;
    private bool oreSecuredSfxPlayed;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return; // 이미 승패가 갈렸으면 더 이상 갱신하지 않음

        int trooperCount = CountAliveUnits(AssaultTrooperUnitID);
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;

        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = trooperCount >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);

        enemyScanTimer -= Time.deltaTime;
        if (enemyScanTimer <= 0f)
        {
            enemyScanTimer = 0.5f;
            enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        }

        bool oreSecured = oreAmount >= RequiredOre;

        ObjectiveTextUtil.SetObjectiveText(captureZoneText, LocalizationManager.GetText("objective.stage0.main1"), zoneCaptured);
        ObjectiveTextUtil.SetObjectiveText(produceTroopersText, LocalizationManager.GetText("objective.stage0.main2"), trooperCount, RequiredTrooperCount);
        ObjectiveTextUtil.SetObjectiveText(buildBarracksText, LocalizationManager.GetText("objective.stage0.main3"), barracksBuilt);
        ObjectiveTextUtil.SetObjectiveText(clearEnemiesText, LocalizationManager.GetText("objective.stage0.sub1"), enemiesCleared);
        ObjectiveTextUtil.SetObjectiveText(secureOreText, LocalizationManager.GetText("objective.stage0.sub2"), oreAmount, RequiredOre);

        if (zoneCaptured && troopersReady && barracksBuilt)
            StageManager.Instance?.ReportVictory();

        PlayMissionSuccessSfxOnce(enemiesCleared, ref enemiesClearedSfxPlayed);
        PlayMissionSuccessSfxOnce(oreSecured, ref oreSecuredSfxPlayed);
    }

    private void PlayMissionSuccessSfxOnce(bool objectiveComplete, ref bool alreadyPlayed)
    {
        if (!objectiveComplete || alreadyPlayed)
            return;

        alreadyPlayed = true;
        SoundManager.Instance?.PlayMissionSuccessVoice();
    }

    private int CountAliveUnits(int unitID)
    {
        if (rtsController == null) return 0;

        int count = 0;
        foreach (UnitController unit in rtsController.UnitList)
            if (unit != null && unit.GetUnitID() == unitID)
                count++;

        return count;
    }
}
