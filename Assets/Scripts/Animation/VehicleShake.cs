using UnityEngine;
using DG.Tweening;

// 지상 차량 유닛의 비주얼(메쉬) 자식 오브젝트에 부착한다(루트가 아님) - HoverBob과 동일한 이유로,
// 루트 트랜스폼은 UnitController가 이동 중 매 프레임 좌표를 직접 갱신하므로 같이 건드리면 이동 로직과
// 충돌한다. 이동 중일 때만 짧은 DOShakePosition을 계속 이어붙여서 "덜덜덜" 떨리는 느낌을 낸다(doc/0120).
public class VehicleShake : MonoBehaviour
{
    [SerializeField] private float shakeStrength = 0.03f;   // 흔들리는 폭
    [SerializeField] private int vibrato = 15;               // 흔들림 빈도 - 높을수록 더 잘게 떪
    [SerializeField] private float shakeCycleDuration = 0.2f; // 셰이크 한 사이클 길이 - 짧을수록 반응이 빠름

    private UnitController unitController;               // 아군 유닛에 붙어있으면 세팅
    private EnemyUnitController enemyUnitController;      // 적 유닛에 붙어있으면 세팅 (doc/0242)
    private AllyController allyController;                // 아군 OC에 붙어있으면 세팅 (doc/0469)
    private Vector3 basePosition;
    private Tween shakeTween;
    private bool shaking;

    // HoverBob/VehicleIdleAnimation과 동일한 이유(doc/0466/0468)로 Awake가 아니라 Start에서 조회한다 -
    // 이 오브젝트는 상위 프리팹 안에 중첩된 프리팹 인스턴스(차량 메쉬)의 자식으로 추가되는 경우가 많아서,
    // Awake() 시점엔 아직 바깥쪽 루트에 재부모되기 전이라 GetComponentInParent가 못 찾는 문제가 있었다.
    private void Start()
    {
        unitController = GetComponentInParent<UnitController>();
        enemyUnitController = GetComponentInParent<EnemyUnitController>();
        allyController = GetComponentInParent<AllyController>();
        basePosition = transform.localPosition;
    }

    // UnitEffects/HoverBob과 동일한 폴링 패턴(doc/0105) - 상태머신을 직접 건드리지 않는다.
    private void Update()
    {
        bool shouldShake = (unitController != null && unitController.IsCurrentlyMoving())
            || (enemyUnitController != null && enemyUnitController.IsCurrentlyMoving())
            || (allyController != null && allyController.IsCurrentlyMoving());

        if (shouldShake && !shaking)
            StartShake();
        else if (!shouldShake && shaking)
            StopShake();
    }

    private void StartShake()
    {
        shaking = true;
        PlayShakeCycle();
    }

    // fadeOut: true로 매 사이클 끝에 basePosition으로 정확히 돌아오게 한 뒤, 이동 중이면 곧바로 다음
    // 사이클을 이어붙인다 - SetLoops로 반복시키는 대신 이렇게 체이닝하면 각 사이클이 항상 basePosition
    // 기준으로 새로 시작해서 여러 번 반복해도 위치가 누적 오차로 흐트러지지 않는다.
    private void PlayShakeCycle()
    {
        shakeTween = transform.DOShakePosition(shakeCycleDuration, shakeStrength, vibrato, 90f, false, true)
            .OnComplete(() =>
            {
                if (shaking)
                    PlayShakeCycle();
            });
    }

    private void StopShake()
    {
        shaking = false;
        shakeTween?.Kill();
        shakeTween = transform.DOLocalMove(basePosition, 0.15f).SetEase(Ease.OutSine);
    }

    private void OnDestroy() => shakeTween?.Kill();
}
