using System.Collections.Generic;
using UnityEngine;

// 건물 오브젝트에 부착되는 컨트롤러.
// 선택 표시, 랠리 포인트(집결지) 관리, 자식의 UnitSpawner를 통한 유닛 생산 위임, 사망 처리를 담당한다.
public class BuildingController : MonoBehaviour
{
    [SerializeField]
    private GameObject buildingMarker;

    // 이 건물의 유닛 생산 큐를 실제로 관리하는 자식 컴포넌트
    private UnitSpawner UnitSpawner;

    // 생산된 유닛이 스폰 후 이동할 집결 지점
    private Vector3 RallyPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();

        controller.BuildingList.Add(this);

        UnitSpawner = GetComponentInChildren<UnitSpawner>();

        // 기본 랠리 포인트는 건물 앞쪽(약간 -Z 방향)으로 설정
        RallyPosition = transform.position + new Vector3(0, 0, -2f);
    }

    // Update is called once per frame
    void Update()
    {

    }

    // 건물 선택 시 마커(테두리 등 표시)를 활성화한다.
    public void SelectBuilding()
    {
        //Debug.Log(name + " ????");
        buildingMarker.SetActive(true);
    }

    // 건물 선택 해제 시 마커를 비활성화한다.
    public void DeselecBuilding()
    {
        //Debug.Log(name + " ???? ????");
        buildingMarker.SetActive(false);
    }

    // 랠리 포인트(신규 생산 유닛의 집결지)를 지정 위치로 변경한다.
    public void SetRallyPosition(Vector3 position)
    {
        RallyPosition = position;
    }

    // 지정 유닛 ID를 생산 대기열에 추가하도록 UnitSpawner에 위임한다.
    public void SpawnUnit(int unitID)
    {
        UnitSpawner.Enqueue(unitID);
    }

    public Vector3 GetRallyPos()
    {
        return RallyPosition;
    }

    // 현재 생산 대기열 목록을 반환 (UI 표시용, UnitSpawner에 위임)
    public IReadOnlyList<ProductionData> GetProductionQueue()
    {
        return UnitSpawner.GetProductionQueue();
    }

    // 현재 생산 중인 항목의 진행률(0~1) 반환 (UnitSpawner에 위임)
    public float GetProductionProgress()
    {
        return UnitSpawner.GetProductionProgress();
    }

    // 대기열의 특정 항목 생산을 취소한다 (UnitSpawner에 위임)
    public void CancelProduction(int index)
    {
        UnitSpawner.Cancel(index);
    }

    // 건물 파괴 처리: RTSUnitController의 관리 목록에서 제거하고 게임오브젝트를 파괴한다.
    // (HealthManager의 IDestructible 구현체로 호출됨)
    public void Die()
    {
        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.BuildingList.Remove(this);

        Destroy(gameObject);
    }
}
