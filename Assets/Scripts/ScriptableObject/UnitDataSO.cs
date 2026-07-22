using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 게임 내 모든 유닛 종류의 데이터를 담는 ScriptableObject 데이터베이스 (에디터에서 에셋으로 생성해 관리).
[CreateAssetMenu]
public class UnitDataSO : ScriptableObject
{
    [SerializeField]
    public List<UnitData> unitData = new List<UnitData>();
}

// 유닛 하나의 스펙(체력/공격력/비용/생산시간/프리팹 등)을 정의하는 데이터 항목.
[System.Serializable]
public class UnitData
{
    [field: SerializeField]
    public string unitName { get; private set; }

    // 툴팁에 표시할 설명(역할 등). 비워두면 기본 문구가 대신 표시된다.
    [field: SerializeField, TextArea(2, 5)]
    public string description { get; private set; }

    // 코드에서 유닛을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }

    // 이 유닛을 생산할 수 있는 건물 종류: 0=본진(MainBase), 1=병영(Tier1), 2=공장(Tier2), 3=우주공항(Tier3).
    // 이 값만 지정하면 코드 수정 없이 해당 건물의 생산 패널에 자동으로 나타난다.
    [field: SerializeField]
    public int tier { get; private set; }

    // 장갑 타입(경장갑/중장갑)과 크기 타입(소형/중형/대형). 실제 전투 계산에는 유닛 프리팹의 UnitController 값이
    // 쓰이고(기존 attackDamage/armor/attackType과 동일한 방식), 여기 값은 생산 패널 등 표시용.
    [field: SerializeField]
    public ArmorType armorType { get; private set; }
    [field: SerializeField]
    public SizeType sizeType { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }

    [field: SerializeField]
    public int attackDamge { get; private set; }

    [field: SerializeField]
    public int attackRange { get; private set; }

    [field: SerializeField]
    public int mineral { get; private set; }
    [field: SerializeField]
    public int gas { get; private set; }
    // 이 유닛을 생산하는 데 필요한 인구수(보급) 비용
    [field: SerializeField]
    public int population { get; private set; }
    [field: SerializeField]
    public int productionTime { get; private set; }
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }

    // 이 유닛의 생산 버튼을 대신 누르는 키보드 단축키 (없으면 KeyCode.None)
    [field: SerializeField]
    public KeyCode shortcutKey { get; private set; }
}
