using System.Collections.Generic;
using UnityEngine;

public class AttackRange : MonoBehaviour
{
    public int UnitRange;
    public int AttackDamage;

    private UnitController unitController;
    private readonly List<GameObject> enemiesInRange = new List<GameObject>();

    private void Awake()
    {
        unitController = transform.parent.GetComponent<UnitController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        if (!enemiesInRange.Contains(other.gameObject))
            enemiesInRange.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        enemiesInRange.Remove(other.gameObject);
    }

    private void Update()
    {
        enemiesInRange.RemoveAll(enemy => enemy == null); // 이미 죽어서 destroy된 대상 정리

        GameObject target = GetClosestEnemy();
        if (target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (unitController.IsAttack() || unitController.IsIdle())
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(target.transform.position, AttackDamage, target);
            }
            else if (unitController.IsIdle())
            {
                unitController.ChaseTarget(target.transform.position);
            }
        }
    }

    private GameObject GetClosestEnemy()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject enemy in enemiesInRange)
        {
            if (enemy == null)
                continue;

            float sqrDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = enemy;
            }
        }

        return closest;
    }
}