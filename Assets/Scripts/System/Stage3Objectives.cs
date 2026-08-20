using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 3스테이지("침공") 임무 목표 체크리스트. 주목표(외계 전초기지 제거)가 완료되면 승리 처리한다.
// 서브목표(생존자 구조)는 맵의 구조 비콘 트리거 콜라이더에 (구조 대상 자신을 제외한) 아무 아군
// 유닛이나 닿으면 완료로 처리하고, 한 번 완료되면 되돌리지 않는다("구조했다"는 사실은 유닛이
// 다시 벗어나도 취소되지 않아야 하므로 - Stage0/1의 "재평가" 목표들과 다름). 완료되는 순간 미리
// 배치해둔 위장 OC(실제로는 UnitController.isRescueUnit이 붙은 조종 가능한 아군 유닛, doc/0458)의
// 억제도 함께 풀어준다.
public class Stage3Objectives : MonoBehaviour
{
    [Header("주목표")]
    [SerializeField] private GameObject alienOutpost; // 외계 전초기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyOutpostText;

    [Header("서브목표")]
    [SerializeField] private Collider rescueBeacon; // 생존자 구조 지점의 트리거 콜라이더 - 직접 연결 (doc/0459 후속)
    [SerializeField] private List<UnitController> rescuedUnits; // 위장 OC(실제로는 조종 가능한 아군 유닛) 목록 - 직접 연결 (doc/0458)
    [SerializeField] private TextMeshProUGUI rescueSurvivorsText;
    // rescuedUnits를 전부 같은 프레임에 Rescue()하면 각자의 마커 깜빡임/SFX(doc/0465)가 겹쳐 보이고
    // 들린다 - 리스트 순서대로 이 간격만큼 텀을 두고 한 마리씩 구조 처리한다 (doc/0466).
    [SerializeField] private float rescueStaggerInterval = 0.1f;

    private RTSUnitController rtsController;
    private bool alienOutpostAssigned;
    private bool survivorsRescued;

    // 서브목표 성공 사운드 - 최초 달성 순간 1회만 재생한다(doc/0643).
    private bool survivorsRescuedSfxPlayed;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
        alienOutpostAssigned = alienOutpost != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool outpostDestroyed = alienOutpostAssigned && alienOutpost == null;

        if (!survivorsRescued && rescueBeacon != null && rtsController != null)
        {
            survivorsRescued = IsAnyUnitTouchingBeacon();
            if (survivorsRescued)
                StartCoroutine(RescueSequence());
        }

        ObjectiveTextUtil.SetObjectiveText(destroyOutpostText, LocalizationManager.GetText("objective.stage3.main1"), outpostDestroyed);
        ObjectiveTextUtil.SetObjectiveText(rescueSurvivorsText, LocalizationManager.GetText("objective.stage3.sub1"), survivorsRescued);

        if (outpostDestroyed)
            StageManager.Instance?.ReportVictory();

        PlayMissionSuccessSfxOnce(survivorsRescued, ref survivorsRescuedSfxPlayed);
    }

    private void PlayMissionSuccessSfxOnce(bool objectiveComplete, ref bool alreadyPlayed)
    {
        if (!objectiveComplete || alreadyPlayed)
            return;

        alreadyPlayed = true;
        SoundManager.Instance?.PlayMissionSuccessVoice();
    }

    private IEnumerator RescueSequence()
    {
        foreach (UnitController unit in rescuedUnits)
        {
            unit?.Rescue();
            yield return new WaitForSeconds(rescueStaggerInterval);
        }
    }

    // 비콘의 실제 트리거 콜라이더에 닿았는지로 판정한다(UnitController.IsTouching - MissionItem/doc/0456과
    // 동일한 패턴, doc/0459 후속 - 거리/반경 대신 실제 물리 트리거 접촉으로 변경).
    private bool IsAnyUnitTouchingBeacon()
    {
        foreach (UnitController unit in rtsController.UnitList)
        {
            // 구조 대상 유닛 자기 자신은 제외한다 - 처음부터 비콘 근처에 배치돼 있어서 그것만으로
            // 서브목표가 즉시 완료돼버리면 안 된다(doc/0458 - Layer/Tag를 처음부터 정상 Unit으로 두기로
            // 하면서 이 유닛들도 UnitList에 등록되므로 명시적으로 걸러내야 함).
            if (unit == null || rescuedUnits.Contains(unit))
                continue;

            if (unit.IsTouching(rescueBeacon))
                return true;
        }
        return false;
    }
}
