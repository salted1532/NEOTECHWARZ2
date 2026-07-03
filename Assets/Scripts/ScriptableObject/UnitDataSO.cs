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

    // 코드에서 유닛을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }

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
}
