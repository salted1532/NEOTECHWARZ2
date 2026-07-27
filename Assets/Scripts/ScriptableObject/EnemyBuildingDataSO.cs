using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// OC(오메가 코퍼레이션) 등 적 진영 건물 데이터베이스. NTA와 동일한 BuildingData 구조를 그대로 재사용해서
// 스탯 필드를 중복 정의하지 않고, 진영별로 SO 에셋만 따로 둔다 (doc/0230).
[CreateAssetMenu]
public class EnemyBuildingDataSO : ScriptableObject
{
    [field: SerializeField]
    public List<BuildingData> buildingData = new List<BuildingData>();
}
