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

    private UnitController unitController;         // 있으면 아군 "공중유닛" - 항상 공중이므로 항상 재생
    private EnemyUnitController enemyUnitController; // 있으면 적 "공중유닛" - 위와 동일한 판정 (doc/0242)
    private AllyController allyController;         // 있으면 "아군 OC(AllyController)" 공중유닛 - 위와 동일한 판정 (doc/0467)
    private BuildingController buildingController;  // 있으면 "리프트 건물" - IsLifted()일 때만 재생

    private float baseY;
    private Tween bobTween;
    private bool bobbing;

    // 이 오브젝트는 상위 프리팹(예: Strike Drone) 안에 중첩된 프리팹 인스턴스(예: Ornithopter 메쉬)의
    // 자식으로 추가되는 경우가 많다 - 중첩 프리팹은 Awake() 시점엔 아직 바깥쪽 루트에 재부모(reparent)되기
    // 전이라 GetComponentInParent가 루트의 UnitController/EnemyUnitController를 못 찾고 null로 캐싱되는
    // 문제가 있었다(doc/0466). Start()는 전체 계층이 다 붙은 뒤(다음 프레임) 호출되므로 여기서 조회한다.
    private void Start()
    {
        unitController = GetComponentInParent<UnitController>();
        enemyUnitController = GetComponentInParent<EnemyUnitController>();
        allyController = GetComponentInParent<AllyController>();
        buildingController = GetComponentInParent<BuildingController>();
        baseY = transform.localPosition.y;
    }

    // UnitEffects가 이동 여부를 폴링하는 것과 동일한 패턴(doc/0105) - 상태머신을 직접 건드리지 않는다.
    private void Update()
    {
        bool shouldBob = (unitController != null && unitController.IsAirUnit())
            || (enemyUnitController != null && enemyUnitController.IsAirUnit())
            || (allyController != null && allyController.IsAirUnit())
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
        bobTween = transform.DOLocalMoveY(baseY, 0.3f).SetEase(Ease.OutSine); // 착륙 시 원래 높이로 부드럽게 복귀
    }

    private void OnDestroy() => bobTween?.Kill();
}
