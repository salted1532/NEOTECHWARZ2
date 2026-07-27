using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// OC(오메가 코퍼레이션) 등 적 진영 유닛 데이터베이스. NTA와 동일한 UnitData 구조를 그대로 재사용해서
// 스탯 필드를 중복 정의하지 않고, 진영별로 SO 에셋만 따로 둔다 (doc/0230).
[CreateAssetMenu]
public class EnemyUnitDataSO : ScriptableObject
{
    [SerializeField]
    public List<UnitData> unitData = new List<UnitData>();
}
