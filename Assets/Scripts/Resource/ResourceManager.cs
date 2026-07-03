using UnityEngine;

// 플레이어(팀)의 자원(광물/가스)과 인구수(보급)를 관리하는 중앙 저장소.
// RTSUnitController가 이 컴포넌트를 통해 자원 조회/소모/증가를 처리한다.
public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int startOre = 50;
    [SerializeField] private int startGas = 0;
    [SerializeField] private int startMaxPopulation = 10;

    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int maxPopulation;       // 인구수 한도 (보급고 등 건물로 증가)

    // 자원/인구가 변경될 때마다 발생하는 이벤트 (UI 갱신용)
    public event System.Action OnResourceChanged;

    private void Awake()
    {
        currentOre = startOre;
        currentGas = startGas;
        maxPopulation = startMaxPopulation;
    }

    // ===== Get =====
    public int GetOre() => currentOre;
    public int GetGas() => currentGas;
    public int GetPopulation() => currentPopulation;
    public int GetMaxPopulation() => maxPopulation;

    // ===== 채취(일꾼)로 인한 자원 획득 =====
    public void AddOre(int amount)
    {
        if (amount <= 0) return;
        currentOre += amount;
        OnResourceChanged?.Invoke();
    }

    public void AddGas(int amount)
    {
        if (amount <= 0) return;
        currentGas += amount;
        OnResourceChanged?.Invoke();
    }

    // ===== 인구수 한도 변경 (보급고 건설/파괴 시) =====
    public void AddMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation += amount;
        OnResourceChanged?.Invoke();
    }

    public void RemoveMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation = Mathf.Max(0, maxPopulation - amount);
        OnResourceChanged?.Invoke();
    }

    // ===== 생산/건설 가능 여부를 확인 (소모 없이) =====
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;
        if (currentPopulation + populationCost > maxPopulation) return false;

        return true;
    }

    // ===== 확인 + 실제 자원을 소모 (요청을 "받아들여도 될지" 판정) =====
    public bool TrySpend(int oreCost, int gasCost, int populationCost = 0)
    {
        if (!CanAfford(oreCost, gasCost, populationCost))
            return false;

        currentOre -= oreCost;
        currentGas -= gasCost;
        currentPopulation += populationCost;

        OnResourceChanged?.Invoke();
        return true;
    }

    // ===== 유닛 사망/건물 파괴 시 인구수 반환 =====
    public void ReleasePopulation(int amount)
    {
        if (amount <= 0) return;
        currentPopulation = Mathf.Max(0, currentPopulation - amount);
        OnResourceChanged?.Invoke();
    }
}