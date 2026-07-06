using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 건물 오브젝트에 부착되는 컨트롤러.
// 선택 표시, 랠리 포인트(집결지) 관리, 자식의 UnitSpawner를 통한 유닛 생산 위임, 사망 처리를 담당한다.
public class BuildingController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject buildingMarker;

    [SerializeField]
    private Sprite icon; // Info_panel 등 선택 UI에 표시할 아이콘

    // BuildingDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetBuildingName(buildingID)로 조회)
    [SerializeField]
    private int buildingID;

    [SerializeField] private float markerFlashInterval = 0.3f; // 공격 대상 지정 피드백 깜빡임 간격
    [SerializeField] private int markerFlashCount = 3;          // 깜빡이는 횟수

    // 이 건물의 유닛 생산 큐를 실제로 관리하는 자식 컴포넌트
    private UnitSpawner UnitSpawner;

    // 생산된 유닛이 스폰 후 이동할 집결 지점
    private Vector3 RallyPosition;

    private RTSUnitController rtsController;
    private Coroutine markerFlashRoutine;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();

        rtsController.BuildingList.Add(this);

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

    // 공격 대상(아군 강제 공격 등)으로 지정됐을 때 "이 건물이 대상"임을 피드백으로 마커를 짧게 깜빡인다.
    // 좌클릭 선택 마커와 같은 오브젝트를 사용하므로, 끝나면 실제 선택 상태에 맞춰 복원한다.
    public void FlashMarker()
    {
        if (buildingMarker == null)
            return;

        if (markerFlashRoutine != null)
            StopCoroutine(markerFlashRoutine);

        markerFlashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(markerFlashInterval);

        for (int i = 0; i < markerFlashCount; i++)
        {
            buildingMarker.SetActive(true);
            yield return wait;
            buildingMarker.SetActive(false);
            yield return wait;
        }

        bool isSelected = rtsController != null && rtsController.selectedBuildingList.Contains(this);
        buildingMarker.SetActive(isSelected);

        markerFlashRoutine = null;
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

    public Sprite GetIcon() => icon;
    public int GetBuildingID() => buildingID;

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
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록

        Destroy(gameObject);
    }
}
