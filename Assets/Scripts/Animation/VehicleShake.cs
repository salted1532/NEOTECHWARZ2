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

    private UnitController unitController;
    private Vector3 basePosition;
    private Tween shakeTween;
    private bool shaking;

    private void Awake()
    {
        unitController = GetComponentInParent<UnitController>();
        basePosition = transform.localPosition;
    }

    // UnitEffects/HoverBob과 동일한 폴링 패턴(doc/0105) - 상태머신을 직접 건드리지 않는다.
    private void Update()
    {
        bool shouldShake = unitController != null && unitController.IsCurrentlyMoving();

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
        transform.DOLocalMove(basePosition, 0.15f).SetEase(Ease.OutSine);
    }

    private void OnDestroy() => shakeTween?.Kill();
}
