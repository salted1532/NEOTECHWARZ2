using System.Collections.Generic;
using UnityEngine;

// BaseStructure(건설중 파운데이션)/HealthManager와 같이 부착하는 이펙트 전담 컴포넌트.
// 건설 진행 중 지속 이펙트, 완공 순간 이펙트, 피격 이펙트를 담당한다(doc/0105 3.8절, doc/0108 피격 타입별 확장).
public class ConstructionEffects : MonoBehaviour
{
    [Header("건설 중 지속 (비워두면 건물 중심 1곳)")]
    [SerializeField] private GameObject constructionLoopPrefab;
    [SerializeField] private List<Transform> constructionPoints = new(); // 비계/모서리 등 여러 지점
    private List<GameObject> activeLoops = new();

    [Header("완공 순간")]
    [SerializeField] private GameObject completePrefab;
    [SerializeField] private List<Transform> completePoints = new();

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

    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType)
    {
        EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType));
    }

    public void StartLoop()
    {
        if (activeLoops.Count > 0)
            return; // 이미 재생 중

        activeLoops = EffectPlayer.SpawnPersistentAtPoints(constructionLoopPrefab, constructionPoints, transform);
    }

    public void StopLoopAndPlayComplete()
    {
        foreach (GameObject loop in activeLoops)
            if (loop != null) Destroy(loop);
        activeLoops.Clear();

        // attachToPoint: false - 이 BaseStructure는 CompleteConstruction() 끝에서 곧바로 Destroy(gameObject)될
        // 예정이라, 부모로 붙이면 완공 이펙트가 재생을 채 끝내기도 전에 같이 파괴돼버린다.
        EffectPlayer.SpawnAtPoints(completePrefab, completePoints, transform, attachToPoint: false);
    }
}
