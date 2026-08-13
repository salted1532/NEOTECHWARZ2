using UnityEngine;
using DG.Tweening;

// 근접 유닛(예: Ripfang)의 몸 모델 오브젝트에 붙인다. 공격 판정 순간 몸이 대상 쪽(로컬 정면)으로 짧게
// 튀어나갔다가 되돌아와서 "몸통박치기" 느낌을 낸다. EnemyUnitController.Attack()이 데미지를 입히는
// 순간 호출한다 (TurretController.FireRecoil()과 동일한 훅 지점/구조).
public class MeleeBodySlamAttack : MonoBehaviour
{
    [Header("몸통박치기 (DOTween)")]
    [SerializeField] private Transform bodyPart; // 튀어나갈 파츠. 비우면 이 오브젝트 자신
    [SerializeField] private float lungeDistance = 0.6f; // 로컬 정면으로 튀어나가는 거리
    [SerializeField] private float lungeDuration = 0.08f;
    [SerializeField] private float lungeReturnDuration = 0.15f;
    [SerializeField] private Ease lungeEase = Ease.OutQuad;
    [SerializeField] private Ease lungeReturnEase = Ease.OutBack;

    private Vector3 restLocalPosition;
    private Tween lungeTween;

    private void Awake()
    {
        if (bodyPart == null)
            bodyPart = transform;

        restLocalPosition = bodyPart.localPosition;
    }

    // 몸은 이미 대상을 향해 회전한 뒤 이 훅이 불리므로(EnemyUnitController.Attack()의 RotateYOnly가
    // 먼저 실행됨), 로컬 +Z(정면)로 튀어나갔다 돌아오기만 해도 그대로 몸통박치기로 보인다.
    public void Slam()
    {
        lungeTween?.Kill();
        bodyPart.localPosition = restLocalPosition;

        lungeTween = bodyPart.DOLocalMove(restLocalPosition + Vector3.forward * lungeDistance, lungeDuration)
            .SetEase(lungeEase)
            .OnComplete(() => lungeTween = bodyPart
                .DOLocalMove(restLocalPosition, lungeReturnDuration)
                .SetEase(lungeReturnEase));
    }

    private void OnDestroy() => lungeTween?.Kill();
}
