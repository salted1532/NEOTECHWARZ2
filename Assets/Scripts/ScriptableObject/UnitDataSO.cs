using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class UnitDataSO : ScriptableObject
{
    [SerializeField]
    public List<UnitData> unitData = new List<UnitData>();
}

[System.Serializable]
public class UnitData
{
    [field: SerializeField]
    public string unitName { get; private set; }

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
    [field: SerializeField]
    public int productionTime { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }
}
