using UnityEngine;

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

    private int currentHp;
    private bool isDead;

    // 체력 변화 시 UI(체력바 등)가 갱신될 수 있도록 이벤트로 알림
    public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
    public event System.Action OnDeath;

    private void Awake()
    {
        currentHp = maxHealth;
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
