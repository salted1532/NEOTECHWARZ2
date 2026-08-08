using UnityEngine;

// 유닛의 이동/공격 상태를 Animator 파라미터(IsMoving/Fire)에 반영한다.
// 비주얼 모델에 Animator가 없는 유닛(정적 메쉬만 쓰는 유닛 등)도 있으므로, Animator를 못 찾으면
// 아무 동작도 하지 않고 조용히 넘어간다 - 모든 유닛이 애니메이션을 갖는 것은 아니기 때문.
public class UnitAnimatorDriver : MonoBehaviour
{
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int FireParam = Animator.StringToHash("Fire");

    private UnitController unitController;             // 아군 유닛에 붙어있으면 세팅
    private EnemyUnitController enemyUnitController;    // 적 유닛에 붙어있으면 세팅 (doc/0242)
    private AllyController allyController;              // 아군 OC에 붙어있으면 세팅 (doc/0469)
    private Animator animator;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
        allyController = GetComponent<AllyController>();
        animator = GetComponentInChildren<Animator>(); // 비주얼 모델 자식에 붙어있는 Animator
    }

    private void Update()
    {
        if (animator == null || (unitController == null && enemyUnitController == null && allyController == null))
            return;

        bool isMoving = unitController != null ? unitController.IsCurrentlyMoving()
            : enemyUnitController != null ? enemyUnitController.IsCurrentlyMoving()
            : allyController.IsCurrentlyMoving();
        bool isAttacking = unitController != null ? unitController.IsAttack()
            : enemyUnitController != null ? enemyUnitController.IsAttack()
            : allyController.IsAttack();

        animator.SetBool(IsMovingParam, isMoving);
        // 공격 중인 동안은 계속 true를 흘려보내 Fire 상태에 머무르게 한다 (doc/0225).
        // 공격이 끝나면 false가 되어 애니메이터가 자체적으로 idle로 돌아간다.
        animator.SetBool(FireParam, isAttacking);
    }
}
