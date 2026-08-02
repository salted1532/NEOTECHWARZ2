using System.Collections.Generic;
using UnityEngine;

// 유닛의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 적 감지 및 자동 공격/추격을 담당한다.
public class AttackRange : MonoBehaviour
{
    public int UnitRange;

    // 감지 콜라이더 반경이 최소 이 값(UnitRange + margin) 이상이 되도록 보장하는 안전장치 (doc/0239 참고 -
    // 프리팹 값은 이제 전부 UnitRange+5로 손으로 맞춰뒀지만, 나중에 새 유닛을 추가하거나 사거리를 바꾸면서
    // 깜빡 안 맞추는 실수를 방지하기 위한 보험). 절대 줄이지는 않고(Mathf.Max) 부족할 때만 넓힌다.
    private const float DetectionRangeMargin = 5f;

    private UnitController unitController;
    private CapsuleCollider detectionCollider;
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
        detectionCollider = GetComponent<CapsuleCollider>();
        EnsureDetectionRadius();
    }

    // UnitRange가 바뀔 때마다(UnitController.ApplyUnitData) 함께 호출해서, 감지 콜라이더가 최소
    // UnitRange + DetectionRangeMargin 이상을 유지하도록 보장한다.
    public void EnsureDetectionRadius()
    {
        if (detectionCollider != null)
            detectionCollider.radius = Mathf.Max(detectionCollider.radius, UnitRange + DetectionRangeMargin);
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

        if (unitController.HasPendingSkillOrder)
            return; // 스킬 사용 명령이 우선 - 사거리 밖 적 자동교전으로 이동을 가로채지 않는다 (doc/0383)

        GameObject target = GetPreferredTarget();
        if (target == null)
            return;

        float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;

        if (unitController.IsAttack() || unitController.IsIdle())
        {
            if (sqrDistance <= UnitRange * UnitRange)
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

        EnemyUnitController ordered = unitController.GetOrderedTarget();
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

        EnemyUnitController ordered = unitController.GetOrderedTarget();

        if (ordered != null)
            return enemiesInRange.Contains(ordered.gameObject) ? ordered.gameObject : null;

        return GetClosestEnemy();
    }

    // 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환한다. 이 유닛이 공격할 수 없는
    // 도메인(지상 전용 유닛에게 공중 적, 혹은 그 반대)의 대상은 아예 후보에서 제외한다 - 그래야 공격-이동
    // 중에 상대하지 못할 적을 스쳐 지나가도 멈추거나 쫓아가지 않고 원래 목적지로 계속 이동한다.
    private GameObject GetClosestEnemy()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject enemy in enemiesInRange)
        {
            if (enemy == null)
                continue;

            if (!CanEngage(enemy))
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

    // 대상이 이 유닛의 공격 도메인(지상/공중)에 해당하는지 확인한다.
    private bool CanEngage(GameObject enemy)
    {
        bool targetIsAir = enemy.TryGetComponent<EnemyUnitController>(out var enemyUnit) && enemyUnit.IsAirUnit();
        return unitController.CanAttackDomain(targetIsAir);
    }

    // 사거리 확인용 디버그 선 (씬 뷰 전용 - Gizmos는 빌드에는 포함되지 않음). 유닛 정면 방향으로 UnitRange
    // 길이만큼 그어서, 실제 감지 콜라이더 크기와 사거리 값이 얼마나 차이나는지 눈으로 바로 비교할 수 있다.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * UnitRange);
    }
}