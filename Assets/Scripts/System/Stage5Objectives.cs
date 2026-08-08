using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 5스테이지("최후의 원정") 임무 목표 체크리스트. 주목표 2개(에너지 코어 3개 파괴 + 외계 지휘 코어
// 제거)가 모두 완료되면 승리 처리한다. OC는 다른 전선에서 별도로 공격 중이라 이 전장에는 없음 -
// NTA 단독 작전이라 서브목표 없음.
public class Stage5Objectives : MonoBehaviour
{
    [Header("주목표 - 에너지 코어")]
    [SerializeField] private List<GameObject> energyCores; // 파괴해야 할 에너지 코어들 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyEnergyCoresText;

    [Header("주목표 - 외계 지휘 코어")]
    [SerializeField] private GameObject alienCommandCore; // 외계 지휘 코어 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyCommandCoreText;

    private List<GameObject> trackedEnergyCores;
    private bool alienCommandCoreAssigned;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        trackedEnergyCores = energyCores.FindAll(core => core != null);
        alienCommandCoreAssigned = alienCommandCore != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
        bool allCoresDestroyed = trackedEnergyCores.Count > 0 && destroyedCoreCount == trackedEnergyCores.Count;

        bool commandCoreDestroyed = alienCommandCoreAssigned && alienCommandCore == null;

        ObjectiveTextUtil.SetObjectiveText(destroyEnergyCoresText, LocalizationManager.GetText("objective.stage5.main1"), destroyedCoreCount, trackedEnergyCores.Count);
        ObjectiveTextUtil.SetObjectiveText(destroyCommandCoreText, LocalizationManager.GetText("objective.stage5.main2"), commandCoreDestroyed);

        if (allCoresDestroyed && commandCoreDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
