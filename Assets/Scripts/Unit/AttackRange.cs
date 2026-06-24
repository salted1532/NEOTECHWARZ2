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
        GameObject enemyObject = other.gameObject;
    
        float distance = Vector3.Distance(transform.position, enemyPos);

        if (unitController.IsAttack() == true)
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(enemyPos, AttackDamage, enemyObject);
            }
        }

        if (unitController.IsIdle() == true)
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(enemyPos, AttackDamage, enemyObject);
            }
            else
            {
                unitController.ChaseTarget(enemyPos);
            }
        }

    }
}