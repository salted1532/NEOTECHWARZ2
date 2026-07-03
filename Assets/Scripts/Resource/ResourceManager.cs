using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int startOre = 50;
    [SerializeField] private int startGas = 0;
    [SerializeField] private int startMaxPopulation = 10;

    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int maxPopulation;       // 인구수 한도 (보급고 등으로 증가)

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

    // ===== 채집(일꾼)으로 자원 획득 =====
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

    // ===== 인구수 한도 변경 (보급고 건설/파괴) =====
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

    // ===== 생산/건설 가능 여부만 확인 (차감 없음) =====
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;
        if (currentPopulation + populationCost > maxPopulation) return false;

        return true;
    }

    // ===== 확인 + 실제 차감을 한 번에 (요청이 "받아들여지는" 시점) =====
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