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
}
