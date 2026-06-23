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

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Vector3 enemyPos = other.transform.position;
        GameObject enemyObject = other.gameObject;

        float distance = Vector3.Distance(transform.position, enemyPos);

        if (unitController.IsIdle() == true || unitController.IsAttack() == true)
        {
            Debug.Log("추척중");
            unitController.ChaseTarget(enemyPos);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Vector3 enemyPos = other.transform.position;
        GameObject enemyObject = other.gameObject;
    
        float distance = Vector3.Distance(transform.position, enemyPos);
        if(unitController.IsIdle() == true || unitController.IsAttack() == true)
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(enemyPos, AttackDamage, enemyObject);
            }
        }

    }
}