using System.Collections.Generic;
using UnityEngine;

// 건물 프리팹에 BuildingController/HealthManager와 같이 부착하는 이펙트 전담 컴포넌트.
// 이륙/착륙/피격 이펙트를 담당한다(doc/0105 3.8절, doc/0108 피격 타입별 확장).
public class BuildingEffects : MonoBehaviour
{
    [Header("이륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject takeoffPrefab;
    [SerializeField] private List<Transform> takeoffPoints = new(); // 추진구/랜딩기어 등 여러 지점

    [Header("착륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject landingPrefab;
    [SerializeField] private List<Transform> landingPoints = new();

    [Header("피격 (공격 타입별로 다른 이펙트, 위치는 동적 계산 - 콜라이더 기준)")]
    [SerializeField] private HitEffectSet hitEffects = new();

    private HealthManager healthManager;
    private Collider bodyCollider; // 클릭 판정(UserControl의 layerBuilding 레이캐스트)에 쓰는 것과 동일한 콜라이더

    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        bodyCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (healthManager != null)
            healthManager.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (healthManager != null)
            healthManager.OnDamaged -= HandleDamaged;
    }

    public void PlayTakeoff() => EffectPlayer.SpawnAtPoints(takeoffPrefab, takeoffPoints, transform);
    public void PlayLanding() => EffectPlayer.SpawnAtPoints(landingPrefab, landingPoints, transform);

    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType)
    {
        EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType));
    }
}
