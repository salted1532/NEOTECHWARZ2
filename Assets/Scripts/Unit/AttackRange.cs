using System.Collections.Generic;
using UnityEngine;

// 유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 적 감지 및 자동 공격/추격을 담당한다.
public class AttackRange : MonoBehaviour
{
    public int UnitRange;
    public int AttackDamage;

    private UnitController unitController;
    // 트리거 범위 안에 들어와 있는 "Enemy" 태그 오브젝트 목록
    private readonly List<GameObject> enemiesInRange = new List<GameObject>();

    private void Awake()
    {
        // 이 콜라이더는 유닛의 자식이므로 부모에서 UnitController를 찾는다.
        unitController = transform.parent.GetComponent<UnitController>();
    }

    // 적이 감지 범위(트리거)에 들어오면 목록에 추가
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        if (!enemiesInRange.Contains(other.gameObject))
            enemiesInRange.Add(other.gameObject);
    }

    // 적이 감지 범위를 벗어나면 목록에서 제거
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        enemiesInRange.Remove(other.gameObject);
    }

    // 매 프레임 가장 가까운 적을 찾아, 사거리 안이면 공격하고 범위 밖이지만 Idle 상태면 추격한다.
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

    // 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환한다.
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