using System.Collections.Generic;
using UnityEngine;

// BaseStructure(건설중 파운데이션)/HealthManager와 같이 부착하는 이펙트 전담 컴포넌트.
// 건설 진행 중 지속 이펙트, 완공 순간 이펙트, 피격/파괴 이펙트를 담당한다
// (doc/0105 3.8절, doc/0108 피격 타입별 확장, doc/0117 파괴 이펙트 추가).
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

    [Header("파괴 (비워두면 건물 중심 1곳) - 전투로 파괴됐을 때만, 건설 취소 버튼으로는 재생 안 됨")]
    [SerializeField] private GameObject destroyPrefab;
    [SerializeField] private List<Transform> destroyPoints = new();

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
        {
            healthManager.OnDamaged += HandleDamaged;
            healthManager.OnDeath += HandleDestroyed;
        }
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDamaged -= HandleDamaged;
            healthManager.OnDeath -= HandleDestroyed;
        }
    }

    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType)
    {
        EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType));
    }

    // HealthManager.OnDeath는 체력이 0이 돼서 BaseStructure.Die() -> CancelConstruction()으로 이어질 때만
    // 발생한다. 플레이어가 "건설 취소" 버튼을 눌러 CancelConstruction()을 직접 호출하는 경로는 HealthManager를
    // 거치지 않으므로 이 이벤트가 안 나가고, 자연스럽게 "취소"와 "전투로 파괴"가 구분된다.
    // attachToPoint: false - 이 BaseStructure는 곧바로 Destroy(gameObject)될 예정이라, 부모로 붙이면 이펙트가
    // 재생을 채 끝내기도 전에 같이 파괴돼버린다.
    private void HandleDestroyed()
    {
        EffectPlayer.SpawnAtPoints(destroyPrefab, destroyPoints, transform, attachToPoint: false);
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
