using System.Collections.Generic;
using UnityEngine;

// 유닛 프리팹에 UnitController/HealthManager와 같이 부착하는 이펙트 전담 컴포넌트.
// 공격/이동/피격/사망 이펙트를 담당하며, 상태머신(UnitController)이나 체력 관리(HealthManager) 코드에는
// 최소한의 게터/이벤트만 추가하고 실제 재생 로직은 전부 이곳에 모아둔다(doc/0105 3.3절).
public class UnitEffects : MonoBehaviour
{
    [Header("공격 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private List<Transform> firePoints = new(); // 총구/무기 끝 - 다연장 무기면 여러 개 추가

    [Header("이동 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject moveTrailPrefab;
    [SerializeField] private List<Transform> moveTrailPoints = new(); // 발/바퀴/추진구 등 - 여러 개면 각 지점에 트레일이 붙어 따라다님
    private List<GameObject> activeTrails = new();

    [Header("피격 (위치는 동적 계산 - 콜라이더 기준, 리스트 아님)")]
    [SerializeField] private GameObject hitPrefab;

    [Header("사망 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject deathPrefab;
    [SerializeField] private List<Transform> deathPoints = new(); // 큰 유닛의 다중 폭발 지점(선택)

    private UnitController unitController;
    private HealthManager healthManager;
    private Collider bodyCollider; // 피격 이펙트 위치 계산용 (AttackRange의 트리거 콜라이더가 아니라 유닛 본체 콜라이더)

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        healthManager = GetComponent<HealthManager>();
        bodyCollider = GetComponent<Collider>(); // 클릭 판정(UserControl의 layerUnit 레이캐스트)에 쓰는 것과 동일한 콜라이더
    }

    private void OnEnable()
    {
        if (healthManager != null)
        {
            healthManager.OnDamaged += HandleDamaged;
            healthManager.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDamaged -= HandleDamaged;
            healthManager.OnDeath -= HandleDeath;
        }
    }

    // 이동 중 여부는 상태머신을 건드리지 않고 매 프레임 폴링으로 판단한다.
    private void Update()
    {
        bool moving = unitController != null && unitController.IsCurrentlyMoving();
        SetMoveTrail(moving);
    }

    private void SetMoveTrail(bool moving)
    {
        if (moving && activeTrails.Count == 0 && moveTrailPrefab != null)
        {
            activeTrails = EffectPlayer.SpawnPersistentAtPoints(moveTrailPrefab, moveTrailPoints, transform);
        }
        else if (!moving && activeTrails.Count > 0)
        {
            foreach (GameObject trail in activeTrails)
                if (trail != null) Destroy(trail);
            activeTrails.Clear();
        }
    }

    // UnitController.Attack()에서 데미지 적용 직후 호출된다. 대상 쪽 피격 이펙트는 HandleDamaged가
    // 별도로(콜라이더 기준 정밀 위치) 처리하므로, 여기서는 발사 쪽(총구) 이펙트만 다룬다.
    public void PlayAttack()
    {
        EffectPlayer.SpawnAtPoints(muzzlePrefab, firePoints, transform);
    }

    // attackerPosition: 데미지를 준 유닛의 위치 (HealthManager.OnDamaged가 넘겨줌).
    // bodyCollider.ClosestPoint(attackerPosition)이 "공격자 쪽을 향한 콜라이더 표면의 가장 가까운 점"을
    // 그대로 계산해준다 - 방향 벡터를 직접 구해서 레이캐스트로 테두리를 찾을 필요가 없다. 피격 위치는 맞을
    // 때마다 방향이 달라지므로 다른 이펙트처럼 고정 리스트로 미리 지정할 수 없어 매번 동적으로 계산한다.
    private void HandleDamaged(int amount, Vector3 attackerPosition)
    {
        Vector3 hitPoint = bodyCollider != null
            ? bodyCollider.ClosestPoint(attackerPosition)
            : transform.position; // 콜라이더가 없는 예외 상황 fallback

        Vector3 outward = hitPoint - transform.position;
        Quaternion rot = outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward.normalized) : Quaternion.identity;

        EffectPlayer.Spawn(hitPrefab, hitPoint, rot);
    }

    private void HandleDeath()
    {
        EffectPlayer.SpawnAtPoints(deathPrefab, deathPoints, transform);
        // 래그돌/사망모션이 필요해지면 Die() 계열의 즉시 Destroy 구조 자체를 바꿔야 한다(doc/0105 3.5절 옵션 B).
    }
}
