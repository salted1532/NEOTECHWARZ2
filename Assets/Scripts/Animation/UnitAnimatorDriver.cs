using UnityEngine;

// 유닛의 이동/공격 상태를 Animator 파라미터(IsMoving/Fire)에 반영한다.
// 비주얼 모델에 Animator가 없는 유닛(정적 메쉬만 쓰는 유닛 등)도 있으므로, Animator를 못 찾으면
// 아무 동작도 하지 않고 조용히 넘어간다 - 모든 유닛이 애니메이션을 갖는 것은 아니기 때문.
public class UnitAnimatorDriver : MonoBehaviour
{
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int FireParam = Animator.StringToHash("Fire");

    private UnitController unitController;
    private Animator animator;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        animator = GetComponentInChildren<Animator>(); // 비주얼 모델 자식에 붙어있는 Animator
    }

    private void Update()
    {
        if (animator == null || unitController == null)
            return;

        animator.SetBool(IsMovingParam, unitController.IsCurrentlyMoving());
    }

    // UnitController.Attack()이 실제로 공격에 성공했을 때 호출한다 (doc/0222).
    public void PlayFire()
    {
        if (animator == null)
            return;

        animator.SetTrigger(FireParam);
    }
}
