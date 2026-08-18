using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 서브미션 3("구조대 파견") 임무 목표 체크리스트. Stage3Objectives의 구조 판정(비콘 트리거 접촉 +
// 위장 OC 유닛 억제 해제)을 그대로 쓰되, 서로 떨어진 여러 지점을 전부 구조해야 하므로 지점을
// RescuePoint 리스트로 늘렸다(Docs/Campaign.md 서브미션 3). 적 기지/건물 파괴는 목표에 없어
// 주목표가 이것 하나뿐이다 - 전원 구조되면 바로 승리 처리한다.
public class SubStage3Objectives : MonoBehaviour
{
    [System.Serializable]
    public class RescuePoint
    {
        public Collider beacon; // 구조 지점의 트리거 콜라이더 - 직접 연결
        public List<UnitController> rescuedUnits; // 이 지점의 위장 OC(조종 가능한 아군 유닛) 목록 - 직접 연결
        [System.NonSerialized] public bool rescued;
    }

    [Header("주목표 - 생존자 구조")]
    [SerializeField] private List<RescuePoint> rescuePoints;
    [SerializeField] private TextMeshProUGUI rescueSurvivorsText;
    // 한 지점 안에서 rescuedUnits를 전부 같은 프레임에 Rescue()하면 마커 깜빡임/SFX가 겹친다
    // (Stage3Objectives와 동일한 이유) - 리스트 순서대로 이 간격만큼 텀을 두고 한 마리씩 처리한다.
    [SerializeField] private float rescueStaggerInterval = 0.1f;

    private RTSUnitController rtsController;
    private int rescuedPointCount;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        if (rtsController != null)
        {
            foreach (RescuePoint point in rescuePoints)
            {
                if (point.rescued || point.beacon == null)
                    continue;

                if (IsAnyUnitTouchingBeacon(point))
                {
                    point.rescued = true;
                    rescuedPointCount++;
                    StartCoroutine(RescueSequence(point));
                }
            }
        }

        bool allRescued = rescuePoints.Count > 0 && rescuedPointCount >= rescuePoints.Count;

        ObjectiveTextUtil.SetObjectiveText(rescueSurvivorsText, LocalizationManager.GetText("objective.substage3.main1"), rescuedPointCount, rescuePoints.Count);

        if (allRescued)
            StageManager.Instance?.ReportVictory();
    }

    private IEnumerator RescueSequence(RescuePoint point)
    {
        foreach (UnitController unit in point.rescuedUnits)
        {
            unit?.Rescue();
            yield return new WaitForSeconds(rescueStaggerInterval);
        }
    }

    // 구조 대상 자신(위장 OC)은 판정에서 제외한다 - 처음부터 비콘 근처에 배치돼 있어서 그것만으로
    // 즉시 완료돼버리면 안 된다(Stage3Objectives와 동일한 이유).
    private bool IsAnyUnitTouchingBeacon(RescuePoint point)
    {
        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null || point.rescuedUnits.Contains(unit))
                continue;

            if (unit.IsTouching(point.beacon))
                return true;
        }
        return false;
    }
}
