using System.Collections.Generic;
using UnityEngine;

// 치유 유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 다친 아군 자동 감지/회복을 담당한다.
// AttackRange.cs의 거울상(doc/0661) - "Enemy" 태그가 아닌 대상을 감지해서, 적 대신 다친 아군을 자동으로
// 치유한다. AttackRange의 "이미 물던 대상 유지(hysteresis)" 등 세밀한 로직은 가져오지 않고 매 프레임
// 가장 가까운 대상을 다시 고르는 단순한 버전으로 둔다 - 치유는 공격과 달리 대상을 자주 놓쳐도(다른
// 다친 아군으로 바뀌어도) 전투처럼 손해가 나지 않아 하이스테리시스가 굳이 필요하지 않다.
public class HealRange : MonoBehaviour
{
    public int UnitRange;

    private const float DetectionRangeMargin = 5f; // AttackRange와 동일한 안전장치

    private UnitController unitController;
    private CapsuleCollider detectionCollider;
    private readonly List<GameObject> targetsInRange = new List<GameObject>();

    private void Awake()
    {
        unitController = transform.parent.GetComponent<UnitController>();
        detectionCollider = GetComponent<CapsuleCollider>();
        EnsureDetectionRadius();
    }

    public void EnsureDetectionRadius()
    {
        if (detectionCollider != null)
            detectionCollider.radius = Mathf.Max(detectionCollider.radius, UnitRange + DetectionRangeMargin);
    }

    // 다친 아군 유닛만 담는다 - 적("Enemy" 태그)과 건물(BuildingController, 기존 일꾼 수리 시스템 전담)은 제외.
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
            return;

        if (!other.TryGetComponent<HealthManager>(out _))
            return;

        if (other.GetComponent<BuildingController>() != null)
            return;

        if (!targetsInRange.Contains(other.gameObject))
            targetsInRange.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        targetsInRange.Remove(other.gameObject);
    }

    // 매 프레임 사거리 내에서 가장 가까운 다친 아군을 찾아 사거리 안이면 치유하고, 밖이지만 Idle 상태면 접근한다.
    // 홀드(제자리 대기, UnitState.Attack)일 때도 사거리 안이면 치유는 하되 쫓아가진 않는다 -
    // AttackRange.Update()와 동일한 패턴(IsAttackOrderState() || IsIdle() 게이트).
    private void Update()
    {
        targetsInRange.RemoveAll(target => target == null);

        if (unitController.IsConstructing() || unitController.HasPendingSkillOrder)
            return;

        if (!unitController.IsAttackOrderState() && !unitController.IsIdle())
            return;

        GameObject target = GetPreferredTarget();
        if (target == null)
        {
            unitController.StopHeal();
            return;
        }

        float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;
        if (sqrDistance <= UnitRange * UnitRange)
        {
            unitController.BeginHeal(target);
        }
        else
        {
            // 감지 콜라이더(UnitRange + margin)는 여전히 물고 있어도 실제 치유 사거리(UnitRange)를
            // 벗어났으면 치유부터 끊는다 - 안 그러면 추격을 시작해도 isHealing이 계속 true로 남아
            // 대상이 멀어지는 동안에도 치유가 이어졌다.
            unitController.StopHeal();
            if (unitController.IsIdle())
                unitController.ChaseTarget(target.transform.position); // 홀드 중엔 쫓아가지 않는다
        }
    }

    // 명시적으로 지정된 치유 대상(우클릭, doc/0666)이 있으면 다른 다친 아군은 무시하고 그 대상만
    // 선택한다 - AttackRange.GetPreferredTarget()과 동일한 패턴. 아직 감지 트리거 안에 들어오지
    // 않았으면 이번 프레임엔 대상 없음(다른 대상으로 대체하지 않음 - HealOrderTick이 계속 접근시킨다).
    // 지정 대상이 없을 때만(패시브 대기 상태) 사거리 내 최근접 다친 아군을 자동으로 고른다.
    private GameObject GetPreferredTarget()
    {
        UnitController ordered = unitController.GetOrderedHealTarget();
        if (ordered != null)
            return targetsInRange.Contains(ordered.gameObject) ? ordered.gameObject : null;

        return GetClosestDamagedAlly();
    }

    private GameObject GetClosestDamagedAlly()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject candidate in targetsInRange)
        {
            if (candidate == null)
                continue;

            if (!candidate.TryGetComponent<HealthManager>(out var health) || health.IsDead() || health.GetHealth() >= health.GetMaxHealth())
                continue;

            float sqrDist = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = candidate;
            }
        }

        return closest;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * UnitRange);
    }
}
