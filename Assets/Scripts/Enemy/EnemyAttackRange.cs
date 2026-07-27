using System.Collections.Generic;
using UnityEngine;

// 적 유닛(EnemyUnitController)의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 상대 감지 및
// 자동 공격/추격을 담당한다. 플레이어 쪽 AttackRange.cs를 반대 방향(플레이어 유닛/건물을 감지)으로 뒤집은
// 축소판 - 지정 대상 강제 추격 개념은 없고, 항상 "사거리 안의 가장 가까운 대상"만 본다 (doc/0231).
public class EnemyAttackRange : MonoBehaviour
{
    public int UnitRange;

    private EnemyUnitController enemyUnit;

    // 감지 대상: 플레이어 유닛(Worker/AttackUnit) + 플레이어 건물(MainBase/Tier1~3/SupplyDepot/Lab)
    private static readonly string[] TargetTags =
        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab" };

    private readonly List<GameObject> targetsInRange = new List<GameObject>();

    // 지정 공격 명령이 없는 이 컨트롤러에서는 AttackMoveTick이 "교전 중이라 정지된 것인지" 판단할 때 조회한다.
    public bool HasTargetInRange
    {
        get
        {
            foreach (GameObject target in targetsInRange)
            {
                if (target != null)
                    return true;
            }

            return false;
        }
    }

    private void Awake()
    {
        enemyUnit = transform.parent.GetComponent<EnemyUnitController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other))
            return;

        if (!targetsInRange.Contains(other.gameObject))
            targetsInRange.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTarget(other))
            return;

        targetsInRange.Remove(other.gameObject);
    }

    private static bool IsValidTarget(Collider other)
    {
        foreach (string tag in TargetTags)
        {
            if (other.CompareTag(tag))
                return true;
        }

        return false;
    }

    // 매 프레임 가장 가까운 대상을 찾아, 사거리 안이면 공격하고 범위 밖이지만 Idle 상태면 추격한다.
    private void Update()
    {
        targetsInRange.RemoveAll(target => target == null);

        GameObject target = GetClosestTarget();
        if (target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (enemyUnit.IsAttack() || enemyUnit.IsIdle())
        {
            if (distance <= UnitRange)
            {
                enemyUnit.Attack(target.transform.position, target);
            }
            else if (enemyUnit.IsIdle())
            {
                enemyUnit.ChaseTarget(target.transform.position);
            }
        }
    }

    private GameObject GetClosestTarget()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject target in targetsInRange)
        {
            if (target == null)
                continue;

            float sqrDist = (target.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = target;
            }
        }

        return closest;
    }
}
