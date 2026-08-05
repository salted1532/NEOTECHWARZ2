using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 5스테이지("최후의 원정") 임무 목표 체크리스트. 주목표 2개(에너지 코어 3개 파괴 + 외계 지휘 코어
// 제거)가 모두 완료되면 승리 처리한다. 서브목표(OC 사령부 생존)는 Stage4Objectives와 동일한 규칙
// (파괴되면 영구히 미완료로 고정)을 따른다.
public class Stage5Objectives : MonoBehaviour
{
    [Header("주목표 - 에너지 코어")]
    [SerializeField] private List<GameObject> energyCores; // 파괴해야 할 에너지 코어들 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyEnergyCoresText;

    [Header("주목표 - 외계 지휘 코어")]
    [SerializeField] private GameObject alienCommandCore; // 외계 지휘 코어 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyCommandCoreText;

    [Header("서브목표")]
    [SerializeField] private GameObject ocCommandCenter; // OC 사령부 - 직접 연결
    [SerializeField] private TextMeshProUGUI survivalOcCommandText;

    private List<GameObject> trackedEnergyCores;
    private bool alienCommandCoreAssigned;
    private bool ocCommandCenterAssigned;
    private bool ocCommandCenterDestroyedPermanently;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        trackedEnergyCores = energyCores.FindAll(core => core != null);
        alienCommandCoreAssigned = alienCommandCore != null;
        ocCommandCenterAssigned = ocCommandCenter != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
        bool allCoresDestroyed = trackedEnergyCores.Count > 0 && destroyedCoreCount == trackedEnergyCores.Count;

        bool commandCoreDestroyed = alienCommandCoreAssigned && alienCommandCore == null;

        if (!ocCommandCenterDestroyedPermanently && ocCommandCenterAssigned && ocCommandCenter == null)
            ocCommandCenterDestroyedPermanently = true;

        ObjectiveTextUtil.SetObjectiveText(destroyEnergyCoresText, "(주목표) 에너지 코어 파괴", destroyedCoreCount, trackedEnergyCores.Count);
        ObjectiveTextUtil.SetObjectiveText(destroyCommandCoreText, "(주목표) 외계 지휘 코어 제거", commandCoreDestroyed);
        ObjectiveTextUtil.SetSurvivalObjectiveText(survivalOcCommandText, "(서브) OC 사령부 생존", ocCommandCenterDestroyedPermanently);

        if (allCoresDestroyed && commandCoreDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
