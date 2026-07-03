using UnityEngine;

public interface IDestructible
{
    /// HealthManager가 사망 판정을 내렸을 때 호출한다.
    void Die();
}

public class HealthManager : MonoBehaviour
{
    [SerializeField]
    private int maxHealth = 100;

    private int currentHp;
    private bool isDead;

    // 체력 변화 시 UI(체력바 등)가 구독할 수 있도록 이벤트로 노출
    public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
    public event System.Action OnDeath;

    private void Awake()
    {
        currentHp = maxHealth;
    }

    public int GetHealth() => currentHp;
    public int GetMaxHealth() => maxHealth;
    public bool IsDead() => isDead;

    public void GetDamage(int damage)
    {
        if (isDead || damage <= 0)
            return;

        currentHp = Mathf.Max(0, currentHp - damage);
        OnHealthChanged?.Invoke(currentHp, maxHealth);

        Debug.Log($"{gameObject.name} 체력: {currentHp}/{maxHealth}");

        if (currentHp <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
            return;

        currentHp = Mathf.Min(maxHealth, currentHp + amount);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        OnDeath?.Invoke();

        // 이 오브젝트가 유닛이든 건물이든 상관없이 동일하게 통지
        if (TryGetComponent<IDestructible>(out var destructible))
        {
            destructible.Die();
        }
        else
        {
            Debug.LogWarning($"{name}: IDestructible을 구현한 컨트롤러가 없어 기본 Destroy로 처리합니다.");
            Destroy(gameObject);
        }
    }
}
