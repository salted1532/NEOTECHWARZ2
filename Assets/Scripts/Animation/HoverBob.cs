using UnityEngine;
using DG.Tweening;

// 공중 유닛 / 리프트 중인 건물의 비주얼(메쉬) 자식 오브젝트에 부착한다(루트가 아님).
// 루트 트랜스폼은 UnitController(공중유닛 이동)나 BuildingController(리프트 이동)가 매 프레임 좌표를
// 직접 갱신하므로, 같은 트랜스폼을 건드리면 이동 로직과 충돌한다 - 그래서 자식의 localPosition.y만
// DOTween으로 오프셋을 더해 둥실거리게 한다(doc/0119).
public class HoverBob : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.25f;  // 위/아래 각각 이동하는 폭
    [SerializeField] private float bobDuration = 1.4f; // 한쪽 방향 이동에 걸리는 시간
    [SerializeField] private Ease bobEase = Ease.InOutSine;

    private UnitController unitController;         // 있으면 "공중유닛" - 항상 공중이므로 항상 재생
    private BuildingController buildingController;  // 있으면 "리프트 건물" - IsLifted()일 때만 재생

    private float baseY;
    private Tween bobTween;
    private bool bobbing;

    private void Awake()
    {
        unitController = GetComponentInParent<UnitController>();
        buildingController = GetComponentInParent<BuildingController>();
        baseY = transform.localPosition.y;
    }

    // UnitEffects가 이동 여부를 폴링하는 것과 동일한 패턴(doc/0105) - 상태머신을 직접 건드리지 않는다.
    private void Update()
    {
        bool shouldBob = (unitController != null && unitController.IsAirUnit())
            || (buildingController != null && buildingController.IsLifted());

        if (shouldBob && !bobbing)
            StartBob();
        else if (!shouldBob && bobbing)
            StopBob();
    }

    private void StartBob()
    {
        bobbing = true;
        bobTween = transform.DOLocalMoveY(baseY + bobHeight, bobDuration)
            .SetEase(bobEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopBob()
    {
        bobbing = false;
        bobTween?.Kill();
        transform.DOLocalMoveY(baseY, 0.3f).SetEase(Ease.OutSine); // 착륙 시 원래 높이로 부드럽게 복귀
    }

    private void OnDestroy() => bobTween?.Kill();
}
