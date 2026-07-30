using System.Collections;
using UnityEngine;
using DG.Tweening;

// 지상 차량 유닛의 비주얼(메쉬) 자식 오브젝트에 부착한다(VehicleShake와 같은 오브젝트) - 루트는
// UnitController/NavMeshAgent가 이동 중 매 프레임 좌표를 갱신하므로 피한다.
// 가만히 있을 때(IsCurrentlyMoving()==false && IsAttack()==false)만 동작한다:
//  1) 엔진 떨림: 아주 미세한 DOShakePosition을 계속 이어붙여 "덜덜거리는" 느낌을 낸다
//     (VehicleShake와 동일한 체이닝 패턴, 진폭만 훨씬 작음).
//  2) 포탑 방황: 랜덤 idleWaitMin~idleWaitMax초(기본 5~15초, 유닛마다/사이클마다 다시 뽑아서 한꺼번에
//     안 돈다)마다 포탑을 0~360도 범위의 랜덤 절대 각도로 돌렸다가 5~10초 대기 후, 나갈 때와 같은
//     duration으로 천천히 원위치로 복귀한다(TurretController의 기본 회전속도는 360도 범위에서는 너무
//     빠르게 홱 도는 것처럼 보여서 쓰지 않는다). 회전 중에는 TurretController를 잠시 꺼서 같은
//     트랜스폼을 두 스크립트가 동시에 건드리지 않게 한다. 대기/회전 중 실제 조준 대상이 잡히면
//     (AttackRange) 즉시 TurretController에 제어권을 돌려줘서 포탑이 눈멀지 않게 한다.
public class VehicleIdleAnimation : MonoBehaviour
{
    [Header("포탑 방황 (선택 - 포탑 없는 차량이면 비워도 자동으로 못 찾으면 스킵됨)")]
    [SerializeField] private TurretController turretController;
    [SerializeField] private float idleWaitMin = 5f;   // 다음 방황까지 대기하는 최소 시간
    [SerializeField] private float idleWaitMax = 15f;  // 다음 방황까지 대기하는 최대 시간 - 매번 다시 뽑아서 한꺼번에 안 돈다
    [SerializeField] private float turretWanderDuration = 1f; // 방황/복귀 둘 다 이 시간으로 돈다 - 같은 속도로 돌아오게
    [SerializeField] private float turretHoldMin = 5f;
    [SerializeField] private float turretHoldMax = 10f;

    [Header("엔진 떨림 (DOTween)")]
    [SerializeField] private float idleShakeStrength = 0.01f;
    [SerializeField] private int idleShakeVibrato = 10;
    [SerializeField] private float idleShakeCycleDuration = 0.15f;

    [Header("수동 제어 (UnitController/EnemyUnitController가 없을 때만 사용됨)")]
    [SerializeField] private bool manualIdle = false; // 인스펙터에서 직접 켜고 끌 수 있다 - SetManualIdle()로도 제어 가능

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private AttackRange attackRange;
    private EnemyAttackRange enemyAttackRange;

    private Vector3 basePosition;
    private Tween shakeTween;
    private Tween wanderTween;
    private Coroutine wanderRoutine;
    private bool isIdling;

    private void Awake()
    {
        unitController = GetComponentInParent<UnitController>();
        enemyUnitController = GetComponentInParent<EnemyUnitController>();
        basePosition = transform.localPosition;

        if (turretController == null)
            turretController = transform.root.GetComponentInChildren<TurretController>();
    }

    private void Start()
    {
        if (unitController != null)
            attackRange = unitController.GetAttackRange();
        else if (enemyUnitController != null)
            enemyAttackRange = enemyUnitController.GetAttackRange();
    }

    // VehicleShake/HoverBob과 동일한 폴링 토글 패턴(doc/0105).
    private void Update()
    {
        bool idle = IsIdle();

        if (idle && !isIdling)
        {
            isIdling = true;
            PlayIdleShakeCycle();
            if (turretController != null)
                wanderRoutine = StartCoroutine(IdleTurretWanderRoutine());
        }
        else if (!idle && isIdling)
        {
            isIdling = false;
            StopIdleShake();

            if (wanderRoutine != null)
            {
                StopCoroutine(wanderRoutine);
                wanderRoutine = null;
            }
            wanderTween?.Kill();

            if (turretController != null)
                turretController.enabled = true;
        }
    }

    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        return manualIdle; // 유닛 컨트롤러가 없는 오브젝트에서는 이 값으로 직접 idle 여부를 제어한다
    }

    // UnitController/EnemyUnitController가 없는 오브젝트(쇼케이스, 데모 씬 등)에서 외부 스크립트나
    // 이벤트로 idle 애니메이션을 직접 켜고 끌 때 쓴다.
    public void SetManualIdle(bool idle) => manualIdle = idle;

    private bool HasTrackingTarget()
    {
        if (attackRange != null) return attackRange.GetTrackingTarget() != null;
        if (enemyAttackRange != null) return enemyAttackRange.GetTrackingTarget() != null;
        return false;
    }

    private IEnumerator IdleTurretWanderRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleWaitMin, idleWaitMax));

            if (HasTrackingTarget())
                continue; // 이미 조준 중인 대상이 있으면 방황시키지 않는다

            turretController.enabled = false;

            Vector3 originalEuler = turretController.transform.localEulerAngles;
            Vector3 wanderEuler = new Vector3(originalEuler.x, Random.Range(0f, 360f), originalEuler.z);

            bool rotateDone = false;
            wanderTween = turretController.transform.DOLocalRotate(wanderEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => rotateDone = true);

            while (!rotateDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            float hold = Random.Range(turretHoldMin, turretHoldMax);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                if (HasTrackingTarget()) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            wanderTween?.Kill();

            if (HasTrackingTarget())
            {
                turretController.enabled = true; // 대상이 잡혔으면 복귀 트윈 없이 바로 조준 재개
                continue;
            }

            // 처음 돌아갈 때와 같은 duration으로 천천히 복귀 - TurretController의 기본 회전속도(초당 360도)에
            // 맡기면 360도 범위에서는 너무 빠르게 홱 돌아가 보이기 때문에 직접 트윈으로 속도를 맞춘다.
            bool returnDone = false;
            wanderTween = turretController.transform.DOLocalRotate(originalEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => returnDone = true);

            while (!returnDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            wanderTween?.Kill();
            turretController.enabled = true;
        }
    }

    // basePosition 기준으로 매 사이클 새로 시작해서 여러 번 반복해도 누적 오차 없이 위치가 흐트러지지 않는다
    // (VehicleShake.PlayShakeCycle과 동일한 체이닝 패턴).
    private void PlayIdleShakeCycle()
    {
        shakeTween = transform.DOShakePosition(idleShakeCycleDuration, idleShakeStrength, idleShakeVibrato, 90f, false, true)
            .OnComplete(() =>
            {
                if (isIdling)
                    PlayIdleShakeCycle();
            });
    }

    private void StopIdleShake()
    {
        shakeTween?.Kill();
        transform.DOLocalMove(basePosition, 0.15f).SetEase(Ease.OutSine);
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        wanderTween?.Kill();
    }
}
