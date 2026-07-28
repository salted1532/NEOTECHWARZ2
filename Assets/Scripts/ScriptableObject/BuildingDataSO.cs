using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 게임 내 모든 건물 종류의 데이터를 담는 ScriptableObject 데이터베이스 (에디터에서 에셋으로 생성해 관리).
[CreateAssetMenu]
public class BuildingDataSO : ScriptableObject
{
    [field: SerializeField]
    public List<BuildingData> buildingData = new List<BuildingData>();
}

// 건물 하나의 스펙(비용/크기/생산시간/프리팹 등)을 정의하는 데이터 항목.
[System.Serializable]
public class BuildingData
{
    [field: SerializeField]
    public string Name { get; private set; }

    // 툴팁에 표시할 설명(역할 등). 비워두면 기본 문구가 대신 표시된다.
    [field: SerializeField, TextArea(2, 5)]
    public string description { get; private set; }
    // 코드에서 건물을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID처럼 별도 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }
    // 그리드 상에서 차지하는 칸 크기 (x, y)
    [field: SerializeField]
    public Vector2Int Size { get; private set; } = Vector2Int.one;
    [field: SerializeField]
    public int mineral { get; private set; }
    [field: SerializeField]
    public int gas { get; private set; }
    // 이 건물이 추가로 제공(또는 소모)하는 인구수(보급) 용량
    [field: SerializeField]
    public int population { get; private set; }
    [field: SerializeField]
    public int maxpopulationamount { get; private set; }
    [field: SerializeField]
    public int productionTime { get; private set; }
    // 이 건물을 짓기 전에 미리 완공되어 있어야 하는 건물의 ID (RTSUnitController.BuildingID 상수, 0이면 조건 없음)
    [field: SerializeField]
    public int requiredBuildingID { get; private set; }
    // 체력. 아군 건물(BuildingController)은 아직 이 값을 안 쓰고 프리팹의 HealthManager에 직접
    // 박아두지만, 적 건물(EnemyBuildingController)은 이 값으로 스스로 체력을 초기화한다 (doc/0245).
    [field: SerializeField]
    public int hp { get; private set; }
    // Info_panel에 표시할 아이콘. 아군 건물도 아직 안 쓰고 있음(마찬가지로 EnemyBuildingController 전용).
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }

    // 이 건물 종류의 건설/파괴 SFX + 선택 음성을 모아둔 에셋 (비워두면 조용함, doc/0255).
    [field: SerializeField]
    public BuildingSoundBankSO soundBank { get; private set; }
}