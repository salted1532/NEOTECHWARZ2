using UnityEngine;
using UnityEngine.UI;
using FischlWorks_FogWar;

// 유닛/건물마다 다른 사망 처리(Destroy 방식, 이펙트 등)를 구현하기 위한 인터페이스.
public interface IDestructible
{
    /// HealthManager가 체력이 0 이하가 됐을 때 호출한다.
    void Die();
}

// 체력(HP) 관리 공용 컴포넌트. 유닛/건물 등 어디에나 부착해서 데미지/힐/사망 처리를 담당한다.
// 실제 사망 시 처리(파괴 방식)는 같은 오브젝트의 IDestructible 구현체에 위임한다.
public class HealthManager : MonoBehaviour
{
    [SerializeField]
    private int maxHealth = 100;

    [SerializeField] private Slider healthSlider; // 체력바 UI (프리팹에서 직접 연결) - 체력 변화에 맞춰 값만 자동 갱신됨

    private int currentHp;
    private bool isDead;
    private int armor; // 받는 피해를 고정으로 깎는 값 - UnitData.armor를 ApplyUnitData()가 SetArmor()로 반영(doc/0556)

    // 체력바가 안개(Y≈1의 물리적 Plane)보다 훨씬 높이 떠 있어서 깊이 테스트로는 절대 안 가려지는 문제의
    // 대안 - 안개 상태를 직접 조회해서 Update()에서 렌더러를 켜고 끈다 (doc/0358).
    private csFogWar fogWar;
    // 적 유닛이면 발견 여부(discovered)까지 반영한 판정을 우선 쓴다 (doc/0567) - 그 외(아군/건물 등)는 null로 남아
    // 기존 위치 기반 안개 판정을 그대로 쓴다.
    private EnemyUnitController enemyUnitController;

    // 체력 변화 시 UI(체력바 등)가 갱신될 수 있도록 이벤트로 알림
    public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
    public event System.Action OnDeath;
    // 피격 이펙트 전용 이벤트. OnHealthChanged는 Heal()에서도 발생해 "회복"과 구분이 안 되므로 별도로 둔다.
    // attackerPosition은 피격 이펙트를 콜라이더 표면의 "공격자 쪽" 지점에 스폰하는 데 쓰이고,
    // attackType은 총기/폭발형/레이저/화염 중 어떤 피격 이펙트를 재생할지 UnitEffects가 고르는 데 쓰인다.
    // isEnemyAttacker: 공격자가 적 진영인지(true) 아군의 아군사격인지(false) - "적에게 공격받음" 경고음이
    // 아군사격에는 울리지 않도록 UnitAudio/BuildingAudio가 이 값으로 판별한다 (doc/0292).
    public event System.Action<int, Vector3, AttackEffectType, bool> OnDamaged;

    private void Awake()
    {
        currentHp = maxHealth;
        fogWar = EffectPlayer.GetFogWar(); // 스폰마다 씬 전체를 재탐색하지 않도록 EffectPlayer의 캐시 재사용 (doc/0498 Tier 4-1)
        enemyUnitController = GetComponent<EnemyUnitController>();

        OnHealthChanged += UpdateHealthSlider;
        UpdateHealthSlider(currentHp, maxHealth);
    }

    // 체력 변화가 없어도 안개 상태는 계속 바뀌므로(유닛이 안 움직여도 다른 유닛이 시야를 뺏어가는 등)
    // 매 프레임 다시 확인한다 - 풀피면 애초에 숨김 조건이라 안개 체크할 필요도 없음 (doc/0358).
    private void Update()
    {
        if (healthSlider == null || currentHp >= maxHealth)
            return;

        bool visible = enemyUnitController != null
            ? enemyUnitController.IsEffectivelyVisible()
            : FogVisibility.IsRevealed(fogWar, transform.position);
        healthSlider.gameObject.SetActive(visible);
    }

    // 체력이 바뀔 때마다(OnHealthChanged) 체력바 슬라이더 값을 함께 갱신한다. 슬라이더가 연결 안 돼 있으면 아무 것도 안 함.
    // 만피 상태에서는 체력바를 숨기고, 조금이라도 깎이면 다시 보여준다(회복해서 만피로 돌아가면 다시 숨김).
    private void UpdateHealthSlider(int current, int max)
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = max;
        healthSlider.value = current;
        healthSlider.gameObject.SetActive(current < max);
    }

    // 체력바 UI 자체를 켜고 끈다 (건설 프리뷰/고스트처럼 체력 표시가 필요 없는 경우 PreviewSystem이 호출).
    public void SetHealthBarVisible(bool visible)
    {
        if (healthSlider != null)
            healthSlider.gameObject.SetActive(visible);
    }

    public int GetHealth() => currentHp;
    public int GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;

    // 데미지를 적용한다. 이미 죽었거나 데미지가 0 이하면 무시하고, 체력이 0 이하가 되면 Die()를 호출한다.
    // attackerPosition: 데미지를 준 유닛의 위치 (피격 이펙트 방향 계산용, UnitEffects 참고).
    // attackType: 공격 수단 (피격 이펙트 종류 선택용, UnitEffects 참고).
    // isEnemyAttacker: 공격자가 적 진영인지 - "적에게 공격받음" 경고음이 아군사격에는 울리지 않게 하는 데 쓰인다(doc/0292).
    public void GetDamage(int damage, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isDead || damage <= 0)
            return;

        // 방어력만큼 경감하되, 공격 1회당 최소 1피해는 항상 들어간다(방어력이 아무리 높아도 완전 무적은 없음, doc/0556).
        int mitigated = Mathf.Max(1, damage - armor);

        currentHp = Mathf.Max(0, currentHp - mitigated);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
        OnDamaged?.Invoke(mitigated, attackerPosition, attackType, isEnemyAttacker);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    // 유닛 스폰 시 UnitData.armor 값을 반영한다(UnitController/EnemyUnitController/AllyController.ApplyUnitData 공통, doc/0556).
    public void SetArmor(int value) => armor = Mathf.Max(0, value);

    // 체력을 회복시킨다 (최대 체력을 넘지 않도록 제한).
    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
            return;

        currentHp = Mathf.Min(maxHealth, currentHp + amount);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 최대 체력을 동적으로 재설정한다 (예: BaseStructure가 어떤 건물을 지을지에 따라 최대체력을 다시 지정할 때).
    // 현재 체력이 새 최대치를 넘지 않도록 클램프한다.
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 유닛이 생산되어 스폰되는 시점 전용: 최대 체력을 새로 지정하는 동시에 그만큼 꽉 채운다.
    // 건설 중 서서히 차오르게 하는 SetMaxHealth와 달리, 방금 생산된 유닛은 항상 풀피로 시작해야 하므로 currentHp도 함께 채운다.
    public void InitializeHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = maxHealth;
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 현재 체력을 절대값으로 지정한다 (데미지/회복처럼 상대적 증감이 아니라 특정 값으로 강제 설정).
    public void SetHealth(int newCurrent)
    {
        if (isDead)
            return;

        currentHp = Mathf.Clamp(newCurrent, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 사망 처리: 중복 실행을 막고, OnDeath 이벤트를 발생시킨 뒤 실제 파괴는 IDestructible에 위임한다.
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        OnDeath?.Invoke();

        // 이 오브젝트가 유닛이든 건물이든 동일한 방식으로 사망 처리 위임
        if (TryGetComponent<IDestructible>(out var destructible))
        {
            destructible.Die();
        }
        else
        {
            Debug.LogWarning($"{name}: No IDestructible controller found. Falling back to default Destroy().");
            Destroy(gameObject);
        }
    }
}
