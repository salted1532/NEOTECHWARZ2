using UnityEngine;
using UnityEngine.UI;

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

    // 체력 변화 시 UI(체력바 등)가 갱신될 수 있도록 이벤트로 알림
    public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
    public event System.Action OnDeath;

    private void Awake()
    {
        currentHp = maxHealth;

        OnHealthChanged += UpdateHealthSlider;
        UpdateHealthSlider(currentHp, maxHealth);
    }

    // 체력이 바뀔 때마다(OnHealthChanged) 체력바 슬라이더 값을 함께 갱신한다. 슬라이더가 연결 안 돼 있으면 아무 것도 안 함.
    private void UpdateHealthSlider(int current, int max)
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = max;
        healthSlider.value = current;
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
    public void GetDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHp = Mathf.Max(0, currentHp - damage);
        OnHealthChanged?.Invoke(currentHp, maxHealth);

        Debug.Log($"{gameObject.name} HP: {currentHp}/{maxHealth}");

        if (currentHp <= 0)
        {
            Die();
        }
    }

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
