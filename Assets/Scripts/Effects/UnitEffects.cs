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
    // 아직 자기 수명대로 재생 중인 공격 이펙트 인스턴스들 - 공격이 취소되면(StopAttackEffects) 자동 파괴
    // 타이머를 기다리지 않고 즉시 정리한다. 매 PlayAttack()마다 이미 파괴된(=null) 항목을 걸러내 무한정
    // 커지지 않게 한다.
    private List<GameObject> activeAttackEffects = new();

    [Header("이동 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject moveTrailPrefab;
    [SerializeField] private List<Transform> moveTrailPoints = new(); // 발/바퀴/추진구 등 - 여러 개면 각 지점에 트레일이 붙어 따라다님
    [SerializeField] private float moveTrailRotationFollowSpeed = 6f; // 회전 추적 속도 - 낮을수록 더 느리게 따라감(관성감 커짐), 0이면 유닛에 즉시 부착(doc/0118)
    [SerializeField] private float moveTrailFastRotationThreshold = 90f; // 이 각속도(도/초)를 넘는 급회전 중엔 트레일 축소
    [SerializeField] private float moveTrailShrinkScale = 0.4f; // 급회전 중 목표 크기/방출량 배율 - 1이면 축소 없음
    [SerializeField] private float moveTrailShrinkLerpSpeed = 8f;
    private List<GameObject> activeTrails = new();

    [Header("피격 (공격 타입별로 다른 이펙트, 위치는 동적 계산 - 콜라이더 기준)")]
    [SerializeField] private HitEffectSet hitEffects = new();

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
            activeTrails = EffectPlayer.SpawnPersistentAtPoints(
                moveTrailPrefab, moveTrailPoints, transform,
                moveTrailRotationFollowSpeed, moveTrailFastRotationThreshold, moveTrailShrinkScale, moveTrailShrinkLerpSpeed);
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
        activeAttackEffects.RemoveAll(effect => effect == null); // 이미 자기 수명 다해 파괴된 이전 인스턴스 정리
        activeAttackEffects.AddRange(EffectPlayer.SpawnAtPoints(muzzlePrefab, firePoints, transform));
    }

    // UnitController.CancelAttackOrder()가 이동/정지/순찰 등 다른 명령으로 공격이 취소될 때 호출한다.
    // 자동 파괴 타이머가 아직 안 끝났어도 지금 재생 중인 공격 이펙트를 즉시 정지시킨다.
    public void StopAttackEffects()
    {
        foreach (GameObject effect in activeAttackEffects)
            if (effect != null) Destroy(effect);

        activeAttackEffects.Clear();
    }

    // attackerPosition/attackType: HealthManager.OnDamaged가 넘겨줌 (EffectPlayer.PlayHit 참고).
    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType)
    {
        EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType));
    }

    private void HandleDeath()
    {
        // attachToPoint: false - 이 유닛은 곧바로 Destroy(gameObject)될 예정이라, 부모로 붙이면 이펙트가
        // 재생을 채 끝내기도 전에 같이 파괴돼버린다.
        EffectPlayer.SpawnAtPoints(deathPrefab, deathPoints, transform, attachToPoint: false);
        // 래그돌/사망모션이 필요해지면 Die() 계열의 즉시 Destroy 구조 자체를 바꿔야 한다(doc/0105 3.5절 옵션 B).
    }
}
