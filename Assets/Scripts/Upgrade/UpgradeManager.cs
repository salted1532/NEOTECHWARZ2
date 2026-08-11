using UnityEngine;

// 연구(공격력/방어력) 종류. Upgrade 시스템과 연구소 큐(ResearchQueue) 양쪽에서 공유하는 타입.
public enum ResearchType
{
    Attack,
    Armor
}

// 연구로 얻은 전역 공격력/방어력 보너스를 저장하는 컴포넌트.
// 이 컴포넌트는 RTSUnitController에서만 참조한다 - ResearchQueue(연구소)나 UnitController(유닛)가
// 직접 이 컴포넌트를 찾거나 호출하지 않는다. 항상 RTSUnitController.AddGlobalBonus/GlobalAttackBonus/
// GlobalArmorBonus를 거쳐서만 값이 오가도록 해서, 연구소 큐 시스템과 유닛 시스템이 서로 독립적으로 유지된다.
public class UpgradeManager : MonoBehaviour
{
    private int attackBonus;
    private int armorBonus;

    public int GetBonus(ResearchType type) => type == ResearchType.Attack ? attackBonus : armorBonus;

    public void AddBonus(ResearchType type, int amount)
    {
        if (type == ResearchType.Attack)
            attackBonus += amount;
        else
            armorBonus += amount;
    }

    // 연구소가 여러 개여도 레벨/진행 여부는 전역으로 하나만 있어야 한다 (doc/0518) - 위 bonus와 동일하게
    // 이 컴포넌트가 유일한 소유자이고, ResearchQueue(연구소)는 RTSUnitController를 거쳐서만 접근한다.
    private int attackLevel;
    private int armorLevel;
    private bool attackInProgress;
    private bool armorInProgress;

    public int GetLevel(ResearchType type) => type == ResearchType.Attack ? attackLevel : armorLevel;

    public void AddLevel(ResearchType type)
    {
        if (type == ResearchType.Attack)
            attackLevel++;
        else
            armorLevel++;
    }

    public bool IsInProgress(ResearchType type) => type == ResearchType.Attack ? attackInProgress : armorInProgress;

    public void SetInProgress(ResearchType type, bool value)
    {
        if (type == ResearchType.Attack)
            attackInProgress = value;
        else
            armorInProgress = value;
    }
}
