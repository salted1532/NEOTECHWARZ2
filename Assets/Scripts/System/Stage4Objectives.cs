using TMPro;
using UnityEngine;

// 4스테이지("공동 전선") 임무 목표 체크리스트. 주목표(외계 사령기지 파괴)가 완료되면 승리 처리한다.
// 서브목표(OC 사령부 생존)는 살아있는 동안 계속 완료 상태로 표시되다가, 파괴되는 순간부터는
// 다시 살아나지 않으므로 그 이후로는 영구히 미완료로 고정한다(요청사항).
public class Stage4Objectives : MonoBehaviour
{
    [Header("주목표")]
    [SerializeField] private GameObject alienCommandBase; // 외계 사령기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyCommandBaseText;

    [Header("서브목표")]
    [SerializeField] private GameObject ocCommandCenter; // OC 사령부 - 직접 연결
    [SerializeField] private TextMeshProUGUI survivalOcCommandText;

    private bool alienCommandBaseAssigned;
    private bool ocCommandCenterAssigned;
    private bool ocCommandCenterDestroyedPermanently;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        alienCommandBaseAssigned = alienCommandBase != null;
        ocCommandCenterAssigned = ocCommandCenter != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool commandBaseDestroyed = alienCommandBaseAssigned && alienCommandBase == null;

        if (!ocCommandCenterDestroyedPermanently && ocCommandCenterAssigned && ocCommandCenter == null)
            ocCommandCenterDestroyedPermanently = true;

        ObjectiveTextUtil.SetObjectiveText(destroyCommandBaseText, "(주목표) 외계 사령기지 파괴", commandBaseDestroyed);
        ObjectiveTextUtil.SetSurvivalObjectiveText(survivalOcCommandText, "(서브) OC 사령부 생존", ocCommandCenterDestroyedPermanently);

        if (commandBaseDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
