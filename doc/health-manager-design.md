# 유닛/건물 공용 HealthManager 설계

작성일: 2026-07-03
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

유닛(`UnitController`)과 건물(`BuildingController`)이 각각 `HealthManager` 컴포넌트를
같은 GameObject에 붙여서 사용한다.

- `UnitController` / `BuildingController` → `HealthManager.GetDamage(damage)` 호출로 데미지 전달
- `UnitController` / `BuildingController` → `HealthManager.GetHealth()` 등으로 현재 체력 조회
- `HealthManager`는 체력이 0 이하가 되면 "사망 판정"을 내리고, 자신을 destroy하는 대신
  **같은 오브젝트에 붙어있는 컨트롤러에게 죽음을 통지**한다.

## 2. 문제: HealthManager가 Unit인지 Building인지 몰라야 함

`HealthManager` 하나로 유닛/건물을 모두 커버하려면, `HealthManager`가
`UnitController`인지 `BuildingController`인지 타입을 직접 알면 안 된다
(`if (TryGetComponent<UnitController>...) else if (TryGetComponent<BuildingController>...)` 식은
새로운 파괴 가능 타입이 추가될 때마다 `HealthManager`를 계속 고쳐야 해서 나쁨).

**해결책**: 공통 인터페이스 `IDestructible`를 하나 두고, `UnitController`와
`BuildingController`가 각각 이 인터페이스를 구현한다. `HealthManager`는 죽었을 때
`GetComponent<IDestructible>()`만 호출하면 된다.

```
GameObject (유닛 또는 건물)
 ├─ HealthManager        (체력 관리, 데미지 처리, 사망 판정)
 └─ UnitController        : IDestructible  (Die()에서 실제 파괴 처리)
    또는 BuildingController : IDestructible
```

## 3. IDestructible 인터페이스

새 파일: `Assets/Scripts/Common/IDestructible.cs`

```csharp
public interface IDestructible
{
    /// HealthManager가 사망 판정을 내렸을 때 호출한다.
    void Die();
}
```

## 4. HealthManager 완성 코드

`Assets/Scripts/Unit/HealthManager.cs` 교체안:

```csharp
using UnityEngine;

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
```

### 설계 포인트

- `GetDamage`/`GetHealth`는 요청하신 대로 유지. `Heal`, `IsDead`, `OnHealthChanged`,
  `OnDeath`는 체력바 UI나 회복 아이템 등 나중에 필요해질 확률이 높아 최소한으로 추가.
  당장 안 쓸 거면 `Heal`/이벤트 2종은 빼도 무방.
- `isDead` 가드: `GetDamage`가 사망 후에도 여러 번 호출돼서 `Die()`가 중복 실행되는 것을 방지.
- `TryGetComponent<IDestructible>`이 실패하는 경우(둘 다 안 붙어있는 경우)를 대비해
  기본 fallback으로 `Destroy(gameObject)`를 넣어둠 — 없어도 되지만 조용히 아무 일도
  안 일어나는 것보단 나음.

## 5. UnitController / BuildingController 쪽 변경 (필요한 부분만)

### UnitController.cs

```csharp
public class UnitController : MonoBehaviour, IDestructible
{
    // ... 기존 필드 동일 ...

    public void Die()
    {
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);

        Destroy(gameObject);
    }
}
```

### BuildingController.cs

```csharp
public class BuildingController : MonoBehaviour, IDestructible
{
    // ... 기존 필드 동일 ...

    public void Die()
    {
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.BuildingList.Remove(this);

        Destroy(gameObject);
    }
}
```

`Start()`에서 각각 `controller.UnitList.Add(this)` / `controller.BuildingList.Add(this)`를
하고 있으므로 (`UnitController.cs:73`, `BuildingController.cs:20`), 죽을 때 리스트에서
제거해주지 않으면 파괴된 오브젝트 참조가 리스트에 남아 `NullReferenceException`의
원인이 됩니다. `Die()`에서 반드시 같이 제거해야 합니다.

### 데미지를 주는 쪽 (예: UnitController.Attack)

```csharp
public void Attack(Vector3 end, int damage, GameObject enemy)
{
    ...
    if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
    {
        targetHealth.GetDamage(damage);
    }
}
```

## 6. 적용 순서 제안

1. `IDestructible.cs` 추가
2. `HealthManager.cs` 교체
3. `UnitController`, `BuildingController`에 `: IDestructible` + `Die()` 추가
   (리스트 제거 포함)
4. 각 프리팹에 `HealthManager` 컴포넌트 부착 + `maxHealth` 값 설정
5. 데미지를 주는 코드(현재 `UnitController.Attack`은 `Debug.Log`만 찍고 실제 데미지
   적용이 없음)에서 `HealthManager.GetDamage()` 연결
