using System.Collections.Generic;
using UnityEngine;

// 유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 적 감지 및 자동 공격/추격을 담당한다.
public class AttackRange : MonoBehaviour
{
    public int UnitRange;

    private UnitController unitController;
    // 트리거 범위 안에 들어와 있는 "Enemy" 태그 오브젝트 목록
    private readonly List<GameObject> enemiesInRange = new List<GameObject>();

    // 지정 공격 명령(AttackOrderTick)이 "교전 중이라 정지된 것인지" 판단할 때 조회한다.
    // Update() 실행 순서에 의존하지 않도록 매번 실시간으로(파괴된 항목 제외) 계산한다.
    public bool HasEnemyInRange
    {
        get
        {
            foreach (GameObject enemy in enemiesInRange)
            {
                if (enemy != null)
                    return true;
            }

            return false;
        }
    }

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

        GameObject target = GetPreferredTarget();
        if (target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (unitController.IsAttack() || unitController.IsIdle())
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(target.transform.position, target);
            }
            else if (unitController.IsIdle())
            {
                unitController.ChaseTarget(target.transform.position);
            }
        }
    }

    // 포탑 등 "조준만 하고 공격 여부는 별개로 판단하는" 스크립트가 쓴다. GetPreferredTarget()과 달리 명시
    // 지정된 대상(아군 강제공격/적 추격 지정)이 있으면 사거리 트리거 안에 실제로 들어왔는지와 무관하게
    // 그 대상을 그대로 돌려준다 - 공격 명령을 내리는 순간 포탑이 바로 그쪽을 보게 하기 위해서(추격 중이라
    // 아직 사거리 밖이어도 미리 조준). 지정 대상이 아예 없을 때만(패시브 대기 상태) 사거리 내 최근접 적으로 대체한다.
    public GameObject GetTrackingTarget()
    {
        if (unitController.HasFriendlyOrder)
            return unitController.GetFriendlyTargetObject();

        EnemyController ordered = unitController.GetOrderedTarget();
        if (ordered != null)
            return ordered.gameObject;

        return GetClosestEnemy();
    }

    // 명시적으로 지정된 추격 대상(우클릭/A 모드)이 있으면 다른 적은 전부 무시하고 오직 그 대상만 선택한다
    // (트리거 안에 아직 없으면 이번 프레임엔 대상 없음 - 다른 적으로 대체하지 않는다).
    // 지정 대상이 아예 없을 때만(패시브 대기 상태) 가장 가까운 적을 선택한다.
    private GameObject GetPreferredTarget()
    {
        if (unitController.HasFriendlyOrder)
            return null; // 아군 강제 공격 중엔 다른 적을 무시한다 (FriendlyAttackTick이 전담 처리)

        EnemyController ordered = unitController.GetOrderedTarget();

        if (ordered != null)
            return enemiesInRange.Contains(ordered.gameObject) ? ordered.gameObject : null;

        return GetClosestEnemy();
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