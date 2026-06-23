using UnityEngine;

public class AttackRange : MonoBehaviour
{
    public int UnitRange;
    public int AttackDamage;

    private UnitController unitController;

    private void Awake()
    {
        unitController = transform.parent.GetComponent<UnitController>();
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Vector3 enemyPos = other.transform.position;

        float sqrDist = (transform.position - enemyPos).sqrMagnitude;
        float range = UnitRange * UnitRange;

        // 🔥 핵심: 상태 기반 제어 (명령 1회만)
        if (sqrDist > range)
        {
            if (!unitController.IsChasing())
                unitController.ChaseTarget(enemyPos);
        }
        else
        {
            if (!unitController.IsAttacking())
                unitController.Attack(enemyPos, AttackDamage);
        }
    }
}