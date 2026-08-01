using UnityEngine;

// 플레이어(팀)의 자원(광물/가스)과 인구수(보급)를 관리하는 중앙 저장소.
// RTSUnitController가 이 컴포넌트를 통해 자원 조회/소모/증가를 처리한다.
public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int startOre = 50;
    [SerializeField] private int startGas = 0;
    [SerializeField] private int startMaxPopulation = 10;
    [SerializeField] private int maxPopulationCap = 200; // 인구수 한도가 아무리 늘어도 이 값을 넘지 않음

    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int rawMaxPopulation;    // 인구수 한도 누적치 (200 캡 적용 전 실제 합계)

    // 자원/인구가 변경될 때마다 발생하는 이벤트 (UI 갱신용)
    public event System.Action OnResourceChanged;

    private void Awake()
    {
        currentOre = startOre;
        currentGas = startGas;
        rawMaxPopulation = startMaxPopulation;
    }

    // ===== Get =====
    public int GetOre() => currentOre;
    public int GetGas() => currentGas;
    public int GetPopulation() => currentPopulation;
    // 표시/판정용 한도는 항상 여기서 캡을 씌운다. 누적치(rawMaxPopulation) 자체는 캡을 넘어도 그대로 보존.
    public int GetMaxPopulation() => Mathf.Min(maxPopulationCap, rawMaxPopulation);

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
    // 누적치(rawMaxPopulation)에는 캡을 씌우지 않는다 — 캡을 넘겨 지은 뒤 일부가 파괴돼도
    // 남은 누적치가 여전히 캡을 넘으면 표시 한도(GetMaxPopulation)는 캡값 그대로 유지되어야 하기 때문.
    public void AddMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        rawMaxPopulation += amount;
        OnResourceChanged?.Invoke();
    }

    public void RemoveMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        rawMaxPopulation = Mathf.Max(0, rawMaxPopulation - amount);
        OnResourceChanged?.Invoke();
    }

    // ===== 생산/건설 가능 여부를 확인 (소모 없이) =====
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;

        // populationCost가 0인 요청(건물 건설 등 인구수를 아예 쓰지 않는 항목)은 인구수 여유와 무관하게
        // 항상 허용한다 - 이 체크가 populationCost와 무관하게 "현재 인구수(currentPopulation)가 이미
        // 한도를 넘었는지"만 봐서, 인구수가 초과된 상태(예: 유닛을 많이 잃어서 22/10)에서는 인구수를
        // 전혀 쓰지 않는 건물 건설까지 "자원 부족"으로 막혀버리는 버그가 있었다.
        if (populationCost > 0 && currentPopulation + populationCost > GetMaxPopulation())
            return false;

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

    // ===== 생산 큐를 거치지 않고 씬에 이미 배치된 유닛의 인구수를 반영 (자원 소모/한도 검사 없이 직접 가산) =====
    // TrySpend와 달리 CanAfford 판정을 하지 않는다 - 이미 맵에 존재하는 유닛이라 "지금 지을 수 있는지"를
    // 물을 필요가 없고, AddMaxPopulation처럼 그냥 현재 상태에 그대로 반영만 하면 된다.
    public void AddPopulationDirect(int amount)
    {
        if (amount <= 0) return;
        currentPopulation += amount;
        OnResourceChanged?.Invoke();
    }
}