using System.Collections.Generic;
using UnityEngine;

// EnemyAttackRange가 부모 유닛에게 요구하는 상태 조회/명령 계약. EnemyUnitController(적)와
// AllyController(아군 OC, doc/0452 - EnemyUnitController를 상속하지 않는 완전 독립 클래스)가 각자
// 구현해서, 하나의 EnemyAttackRange/AllyAttackRange 감지 로직을 서로 다른(비상속) 두 컨트롤러 타입에
// 모두 재사용할 수 있게 한다.
public interface IAttackRangeUnit
{
    bool IsAttack();
    bool IsIdle();
    void Attack(Vector3 end, GameObject target);
    bool ChaseTarget(Vector3 pos);
    bool CanAttackDomain(bool targetIsAirUnit);
}

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

    // 한 번 교전(추격/공격)을 시작한 대상은, 이 여유 거리(감지 반경 + 추가 완충) 밖으로 완전히
    // 벗어나기 전까지는 계속 우선시한다. 도달 불가능한 대상 근처에 정착할 때 유닛 자신의 미세한 위치
    // 흔들림만으로 감지 트리거 콜라이더 경계를 들락날락하면서, 매 프레임 "지금 이 순간의 최근접
    // 대상"만 다시 뽑아 추격/공격이 계속 처음부터 다시 시작되는 것처럼 멈칫거리는 문제가 있었다
    // (doc/0388).
    private const float EngagedTargetLoseSightMargin = 3f;
    private GameObject engagedTarget;

    // ChaseTarget()이 "도달 불가"로 판정해 포기한 대상. 감지 범위를 완전히 벗어나기(OnTriggerExit) 전까지는
    // GetClosestTarget() 후보에서 제외해서, 매번 같은 도달 불가 대상을 다시 골랐다가 다시 포기하는 것을
    // 반복하지 않는다 - 그래야 다른(도달 가능한) 대상이 있으면 그쪽으로 넘어간다 (doc/0398).
    private GameObject unreachableTarget;

    // EnemyUnitController 또는 AllyController(doc/0452, 서로 비상속) - IAttackRangeUnit 계약으로 통일해서 다룬다.
    private IAttackRangeUnit enemyUnit;
    private CapsuleCollider detectionCollider;

    // 감지 대상 Tag 목록 - 기본값은 플레이어 진영(Worker/AttackUnit/MainBase/Tier1~3/SupplyDepot/Lab) +
    // 아군 OC("AllyOC" Tag, doc/0452 - 적이 아군 OC도 자동교전 대상으로 삼는다). protected 인스턴스
    // 필드라서 하위 클래스가 기본값을 바꿀 수 있다 - AllyAttackRange(doc/0448)가 이걸 ["Enemy"]로 바꿔서
    // 플레이어/아군 대신 외계종족을 자동교전 대상으로 삼는 용도로 상속해서 쓴다.
    [SerializeField]
    protected string[] targetTags =
        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab", "AllyOC" };

    private readonly List<GameObject> targetsInRange = new List<GameObject>();

    // 지정 공격 명령이 없는 이 컨트롤러에서는 AttackMoveTick이 "교전 중이라 정지된 것인지" 판단할 때 조회한다.
    public bool HasTargetInRange
    {
        get
        {
            foreach (GameObject target in targetsInRange)
            {
                // 도달 불가로 이미 포기한 대상은 "교전 중"으로 치지 않는다 - 안 그러면 그 대상이 넓은
                // 감지 콜라이더 안에 계속 머무는 동안 AttackMoveTick()이 교전 중으로 착각해서 공격-이동
                // 재개를 영원히 막는다 (doc/0398).
                if (target != null && target != unreachableTarget)
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

            return (transform.position - target.transform.position).sqrMagnitude <= UnitRange * UnitRange;
        }
    }

    private void Awake()
    {
        enemyUnit = transform.parent.GetComponent<IAttackRangeUnit>();
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

        if (unreachableTarget == other.gameObject)
            unreachableTarget = null; // 감지 범위를 완전히 벗어났다 다시 들어오면 다시 시도해볼 기회를 준다
    }

    private bool IsValidTarget(Collider other)
    {
        foreach (string tag in targetTags)
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

        GameObject target = GetEngagedOrClosestTarget();
        if (target == null)
            return;

        float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;

        if (enemyUnit.IsAttack() || enemyUnit.IsIdle())
        {
            if (sqrDistance <= UnitRange * UnitRange)
            {
                enemyUnit.Attack(target.transform.position, target);
            }
            else if (enemyUnit.IsIdle())
            {
                if (enemyUnit.ChaseTarget(target.transform.position))
                {
                    // 도달 불가로 최종 판정 - 이 대상은 포기하고 다음 프레임부터 다른 대상을 찾는다 (doc/0398)
                    unreachableTarget = target;
                    if (engagedTarget == target)
                        engagedTarget = null;
                }
            }
        }
    }

    // 포탑(TurretController)이 조준 대상을 물어볼 때 쓴다. 플레이어 쪽 AttackRange.GetTrackingTarget()과
    // 달리 지정 대상/아군 강제공격 개념이 없어서, 그냥 GetClosestTarget()과 동일하게 사거리 내 가장 가까운
    // (도메인이 맞는) 대상을 그대로 돌려준다.
    public GameObject GetTrackingTarget() => GetEngagedOrClosestTarget();

    // targetsInRange 멤버십(트리거 콜라이더 기준)만으로 매 프레임 새로 뽑으면, 도달 불가능한 대상
    // 근처에서 정착할 때 경계를 들락날락하며 대상을 놓쳤다 다시 잡았다 반복하게 된다 - 이미 물고
    // 있던 대상은 완전히 멀어지기 전까지는 그대로 우선시한다(doc/0388).
    private GameObject GetEngagedOrClosestTarget()
    {
        if (engagedTarget != null && CanEngage(engagedTarget))
        {
            float loseSightRange = UnitRange + DetectionRangeMargin + EngagedTargetLoseSightMargin;
            float sqrDist = (transform.position - engagedTarget.transform.position).sqrMagnitude;
            if (sqrDist <= loseSightRange * loseSightRange)
                return engagedTarget;
        }

        return engagedTarget = GetClosestTarget();
    }

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

            if (target == unreachableTarget) // 도달 불가로 이미 포기한 대상은 다시 뽑지 않는다 (doc/0398)
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
        else if (target.TryGetComponent<AllyController>(out var allyUnit)) // 아군 OC 유닛 (doc/0452)
            targetIsAir = allyUnit.IsAirUnit();
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
