using UnityEngine;
using DG.Tweening;

// 차량형 유닛의 포탑 오브젝트에 직접 부착한다(예: unit_Tank_Heavy_A_Turret_yup).
// 몸체(UnitController.RotateYOnly)와 별개로 AttackRange가 감지한 대상을 향해 몸체보다 빠르게 Y축 회전하고,
// 이동 명령 중이라 자동 공격이 안 나가는 상태(UnitState.Move)에도 상관없이 계속 조준만 한다(doc/0219) -
// AttackRange.GetTrackingTarget()이 공격 가능 여부와 무관하게 사거리 내 우선순위 대상을 그대로 돌려주기 때문.
//
// UnitController.Attack()은 이 컴포넌트가 붙어있는 유닛에 한해 몸체 RotateYOnly를 건너뛴다 - "공격 중엔
// 몸체가 안 돌고 포탑만 돈다"는 요구사항 때문. 이동 중 몸체 회전은 NavMeshAgent가 원래 자동으로 처리하므로
// 별도 코드가 필요 없다.
public class TurretController : MonoBehaviour
{
    [Header("조준")]
    [SerializeField] private float rotationSpeed = 360f; // 초당 회전각(도) - 몸체보다 빠르게

    [Header("반동 (DOTween)")]
    [SerializeField] private Transform recoilPart;              // 뒤로 빠질 파츠(포신). 비우면 이 오브젝트 자신
    [SerializeField] private Vector3 recoilLocalOffset = new Vector3(0f, 0f, -0.3f);
    [SerializeField] private float recoilDuration = 0.08f;      // 뒤로 빠지는 시간
    [SerializeField] private float recoilReturnDuration = 0.18f; // 원위치 복귀 시간
    [SerializeField] private Ease recoilEase = Ease.OutQuad;
    [SerializeField] private Ease recoilReturnEase = Ease.OutBack;

    private AttackRange attackRange;
    private Vector3 recoilRestLocalPosition;
    private Quaternion restLocalRotation; // 조준 대상이 없을 때 되돌아갈 "정면" 로컬 회전값 (보통 몸체 정면)
    private Tween recoilTween;

    private void Awake()
    {
        if (recoilPart == null)
            recoilPart = transform;

        recoilRestLocalPosition = recoilPart.localPosition;
        restLocalRotation = transform.localRotation;
    }

    // AttackRange 조회는 Awake가 아니라 Start에서 한다 - Unity는 서로 다른 GameObject에 붙은 컴포넌트들의
    // Awake() 호출 순서를 보장하지 않아서, Turret(자식)의 Awake가 몸체(루트) UnitController.Awake보다 먼저
    // 실행되면 그 시점엔 UnitController.attackRange가 아직 비어있어 null을 그대로 캐싱해버리는 문제가 있었다
    // (공격/이동 상태와 무관하게 포탑이 계속 조준을 못 하던 원인). Start()는 씬의 모든 Awake가 끝난 뒤에만
    // 호출되는 게 보장되므로 이 시점엔 항상 채워져 있다.
    private void Start()
    {
        UnitController unitController = GetComponentInParent<UnitController>();
        attackRange = unitController != null ? unitController.GetAttackRange() : null;
    }

    // 조준할 대상이 있으면 그쪽을, 없으면(사거리 이탈 등으로 target == null) 몸체 기준 원래 정면으로
    // 같은 속도로 자연스럽게 되돌아간다 - 매 프레임 목표 회전값만 다시 계산해서 RotateTowards로 수렴시키는
    // 방식이라 별도의 "복귀 중" 상태를 관리할 필요 없이 그냥 끊김 없이 이어진다.
    private void Update()
    {
        GameObject target = attackRange != null ? attackRange.GetTrackingTarget() : null;
        Quaternion desiredRotation;

        if (target != null)
        {
            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0;

            if (dir.sqrMagnitude < 0.001f)
                return; // 대상이 자기 자신과 같은 수평 위치 - 이번 프레임은 그대로 유지

            desiredRotation = Quaternion.LookRotation(dir);
        }
        else
        {
            // restLocalRotation(포탑이 처음 놓여있던 로컬 회전)을 부모(몸체)의 현재 월드 회전에 다시 얹어서
            // "몸체 기준 정면"을 구한다 - 몸체가 이동하며 방향을 틀어도 그에 맞춰 정면 기준이 같이 갱신된다.
            desiredRotation = transform.parent != null
                ? transform.parent.rotation * restLocalRotation
                : restLocalRotation;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
    }

    // UnitController.Attack()이 데미지를 실제로 입힌 순간 호출한다 (UnitEffects.PlayAttack와 동일한 훅 지점).
    public void FireRecoil()
    {
        recoilTween?.Kill();
        recoilPart.localPosition = recoilRestLocalPosition;

        recoilTween = recoilPart.DOLocalMove(recoilRestLocalPosition + recoilLocalOffset, recoilDuration)
            .SetEase(recoilEase)
            .OnComplete(() => recoilTween = recoilPart
                .DOLocalMove(recoilRestLocalPosition, recoilReturnDuration)
                .SetEase(recoilReturnEase));
    }

    private void OnDestroy() => recoilTween?.Kill();
}
