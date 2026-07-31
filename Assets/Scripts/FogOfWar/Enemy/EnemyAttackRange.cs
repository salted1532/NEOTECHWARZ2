using System.Collections.Generic;
using UnityEngine;

// 적 유닛(EnemyUnitController)의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 상대 감지 및
// 자동 공격/추격을 담당한다. 플레이어 쪽 AttackRange.cs를 반대 방향(플레이어 유닛/건물을 감지)으로 뒤집은
// 축소판 - 지정 대상 강제 추격 개념은 없고, 항상 "사거리 안의 가장 가까운 대상"만 본다 (doc/0231).
public class EnemyAttackRange : MonoBehaviour
{
    public int UnitRange;

    // 감지 콜라이더 반경이 최소 이 값(UnitRange + margin) 이상이 되도록 보장하는 안전장치 (doc/0239 참고 -
    // 프리팹 값은 이제 전부 UnitRange+5로 손으로 맞춰뒀지만, 나중에 새 유닛을 추가하거나 사거리를 바꾸면서
    // 깜빡 안 맞추는 실수를 방지하기 위한 보험). 절대 줄이지는 않고(Mathf.Max) 부족할 때만 넓힌다.
    private const float DetectionRangeMargin = 5f;

    private EnemyUnitController enemyUnit;
    private CapsuleCollider detectionCollider;

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

    // 지금 이 순간 실제 공격 사거리(UnitRange) 안에 유효한 대상이 있는지. HasTargetInRange는 더 넓은
    // 감지 콜라이더(UnitRange+margin) 기준이라 "지금 공격 중인가"를 나타내기엔 부적합해서 따로 둔다
    // (doc/0253, UnitAnimatorDriver의 Fire 파라미터 판정용).
    public bool HasTargetInAttackRange
    {
        get
        {
            GameObject target = GetClosestTarget();
            if (target == null)
                return false;

            return Vector3.Distance(transform.position, target.transform.position) <= UnitRange;
        }
    }

    private void Awake()
    {
        enemyUnit = transform.parent.GetComponent<EnemyUnitController>();
        detectionCollider = GetComponent<CapsuleCollider>();
        EnsureDetectionRadius();
    }

    // UnitRange가 바뀔 때마다(EnemyUnitController.ApplyUnitData) 함께 호출해서, 감지 콜라이더가 최소
    // UnitRange + DetectionRangeMargin 이상을 유지하도록 보장한다.
    public void EnsureDetectionRadius()
    {
        if (detectionCollider != null)
            detectionCollider.radius = Mathf.Max(detectionCollider.radius, UnitRange + DetectionRangeMargin);
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

    // 포탑(TurretController)이 조준 대상을 물어볼 때 쓴다. 플레이어 쪽 AttackRange.GetTrackingTarget()과
    // 달리 지정 대상/아군 강제공격 개념이 없어서, 그냥 GetClosestTarget()과 동일하게 사거리 내 가장 가까운
    // (도메인이 맞는) 대상을 그대로 돌려준다.
    public GameObject GetTrackingTarget() => GetClosestTarget();

    // 감지된 대상 중 자신과의 거리(제곱 거리)가 가장 짧은 대상을 찾아 반환한다. 이 유닛이 공격할 수 없는
    // 도메인(지상 전용 유닛에게 공중 대상, 혹은 그 반대)의 대상은 아예 후보에서 제외한다 - 그래야 공격-이동
    // 중에 상대하지 못할 대상을 스쳐 지나가도 멈추거나 쫓아가지 않고 원래 목적지로 계속 이동한다.
    private GameObject GetClosestTarget()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject target in targetsInRange)
        {
            if (target == null)
                continue;

            if (!CanEngage(target))
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

    // 대상이 이 유닛의 공격 도메인(지상/공중)에 해당하는지 확인한다. 플레이어 유닛이면 그 유닛의
    // IsAirUnit(), 건물이면 이/착륙 상태(IsLifted)를 기준으로 판정한다 (EnemyUnitController.IsAirborne와
    // 동일한 판정 방식).
    private bool CanEngage(GameObject target)
    {
        bool targetIsAir;

        if (target.TryGetComponent<UnitController>(out var playerUnit))
        {
            if (playerUnit.IsStealthed()) // 은신 유닛은 감지 자체를 못 한다 (doc/0323) - 포탑 조준도 GetTrackingTarget()을 거쳐 이 필터를 그대로 탄다
                return false;

            targetIsAir = playerUnit.IsAirUnit();
        }
        else if (target.TryGetComponent<BuildingController>(out var building))
            targetIsAir = building.IsLifted();
        else
            targetIsAir = false;

        return enemyUnit.CanAttackDomain(targetIsAir);
    }

    // 사거리 확인용 디버그 선 (씬 뷰 전용 - Gizmos는 빌드에는 포함되지 않음). 유닛 정면 방향으로 UnitRange
    // 길이만큼 그어서, 실제 감지 콜라이더 크기와 사거리 값이 얼마나 차이나는지 눈으로 바로 비교할 수 있다.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * UnitRange);
    }
}
