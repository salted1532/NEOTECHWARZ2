using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    // ===== 건물 이동(리프트) =====
    [Header("건물 이동(리프트)")]
    [SerializeField] private bool canLift = true; // 이 건물이 리프트 이륙 가능한지 (건물별로 인스펙터에서 끌 수 있음)
    [SerializeField] private float liftHeight = 5f;   // 이륙 시 상승할 높이
    [SerializeField] private float liftMoveSpeed = 5f; // 상승/수평이동/하강 공통 속도

    private NavMeshObstacle navMeshObstacle;
    private PlacementSystem placementSystem;

    private bool isLifted;             // 현재 공중에 떠 있는지(상승 중/대기 중/이동 중/하강 중 모두 포함)
    private bool isAscending;          // 이륙 중(상승 중)
    private bool isFlyingToDestination;// 착륙 위치로 수평 이동 중
    private bool isDescending;         // 착륙 위치 위에서 하강 중

    private Vector3 verticalTarget;    // 상승 목표(현재 위치 기준 + liftHeight)
    private Vector3 flightDestination; // 수평 비행 목표(목적지 좌표 + liftHeight 고도) - 자유이동/착륙 비행 공통
    private Vector3 landingGroundDestination; // 착륙 최종 지면 좌표 (pendingLanding일 때만 유효, 하강 단계 전용)

    private Vector3Int gridPosition;   // 현재 자신이 점유 중인 그리드 셀 좌표
    private bool hasGridPosition;      // gridPosition이 유효한지 (에디터에 미리 배치된 시작 건물은 처음엔 false)

    private Vector3Int pendingGridPosition;      // 착륙 예정 위치의 그리드 좌표 (비행 중에만 유효)
    private System.Action onRelocationLanded;    // 착륙 완료 시(Land) 호출 - PlacementSystem이 고스트 제거
    private System.Action onRelocationCancelled; // 착륙 전에 파괴되는 등 비행이 중단될 때 호출 - 그리드 예약 해제 + 고스트 제거

    private bool pendingLanding; // true면 현재 수평이동이 "공식 착륙 비행"(도착 시 하강→착륙까지 이어짐), false면 우클릭/Move버튼 자유이동(도착 시 그 자리에서 계속 공중 대기)

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();

        rtsController.BuildingList.Add(this);

        navMeshObstacle = GetComponent<NavMeshObstacle>();

        UnitSpawner = GetComponentInChildren<UnitSpawner>();

        // 기본 랠리 포인트는 건물 앞쪽(약간 -Z 방향)으로 설정
        RallyPosition = transform.position + new Vector3(0, 0, -2f);
    }

    // Update is called once per frame
    void Update()
    {
        if (isLifted)
            UpdateLiftedMovement();
    }

    // 이륙(수직 상승) → [대기: 착륙 위치 선택 전] → 수평 이동 → 착륙(수직 하강) 순서로 진행한다.
    // 공중유닛(UnitController)과 동일하게 MoveTowards 기반 직접 좌표 보간을 사용한다.
    private void UpdateLiftedMovement()
    {
        if (isAscending)
        {
            transform.position = Vector3.MoveTowards(transform.position, verticalTarget, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, verticalTarget) < 0.05f)
                isAscending = false; // 목표 고도 도달 - 착륙 위치를 정할 때까지 공중에서 대기

            return;
        }

        if (isFlyingToDestination)
        {
            // flightDestination은 항상 "목적지 좌표 + liftHeight"로 미리 계산돼 있으므로, 그대로 목표로 삼으면
            // 이륙 도중(고도가 덜 오른 상태)에 새 이동 명령이 들어와도 비행하면서 자연스럽게 목표 고도까지 수렴한다.
            transform.position = Vector3.MoveTowards(transform.position, flightDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, flightDestination) < 0.05f)
            {
                isFlyingToDestination = false;

                if (pendingLanding)
                    isDescending = true;
                // else: 우클릭/Move버튼 자유이동 도착 - 착륙하지 않고 이 고도(목적지 + liftHeight)에서 계속 공중에 떠 있는다
            }

            return;
        }

        if (isDescending)
        {
            transform.position = Vector3.MoveTowards(transform.position, landingGroundDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, landingGroundDestination) < 0.05f)
            {
                transform.position = landingGroundDestination;
                Land();
            }
        }
    }

    public bool CanLift() => canLift;
    public bool IsLifted() => isLifted;

    // 완공 시(BaseStructure.CompleteConstruction) 또는 착륙 완료 시(Land) 자신이 점유한 그리드 좌표를 기록한다.
    public void SetGridInfo(Vector3Int gridPos)
    {
        gridPosition = gridPos;
        hasGridPosition = true;
    }

    // 생산 대기열에 하나라도 남아있는지 (UnitSpawner가 없는 건물은 항상 false). LiftOff() 가드용.
    private bool HasActiveProductionQueue()
    {
        return UnitSpawner != null && UnitSpawner.GetProductionQueue().Count > 0;
    }

    // "리프트" 버튼: 공중으로 떠오르며 그리드에서 자신의 위치를 지운다(공중 유닛처럼 그리드/NavMesh 영향을 받지 않음).
    public void LiftOff()
    {
        if (!canLift || isLifted)
            return;

        if (HasActiveProductionQueue()) // 생산 대기열에 뭔가 있으면 이륙 불가(공중에서 생산이 계속되는 것 방지)
            return;

        if (hasGridPosition)
        {
            placementSystem?.ReleaseBuildingGrid(gridPosition);
            hasGridPosition = false;
        }

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = false;

        isLifted = true;
        isAscending = true;
        verticalTarget = transform.position + Vector3.up * liftHeight;
    }

    // "착륙" 버튼: 완전히 떠 있는 상태(상승/이동/하강 중이 아님)에서만 착륙 위치 선택 모드로 진입한다.
    public void BeginLanding()
    {
        if (!isLifted || isAscending || isDescending || (isFlyingToDestination && pendingLanding))
            return;

        placementSystem?.StartBuildingRelocation(this);
    }

    // PlacementSystem이 착륙 위치 클릭을 확정했을 때 호출: 그 위치로 수평 이동을 시작한다(자유 비행 중이었다면 그 상태를 덮어쓰고 전환).
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        isAscending = false;
        isDescending = false;
        pendingLanding = true;
        landingGroundDestination = destination; // 최종 착륙 지면 좌표 (하강 단계에서 사용)
        flightDestination = destination + Vector3.up * liftHeight; // 수평 비행 중엔 착륙지점 상공(liftHeight 고도)까지만 이동
        isFlyingToDestination = true;
    }

    // "우클릭 이동" / "이동" 버튼(M): 공중유닛처럼 그 지점 위 상공(목적지 좌표 + liftHeight 고도)으로 이동한다 - 착륙하지 않고 계속 공중에 떠 있는다.
    // 착륙 위치로 비행 중(공식 착륙 예약)이었다면 그 예약을 취소하고 자유 비행으로 전환한다.
    public void MoveWhileLifted(Vector3 groundDestination)
    {
        if (!isLifted)
            return;

        CancelPendingLandingFlight();

        isAscending = false;
        isDescending = false;
        pendingLanding = false;
        flightDestination = groundDestination + Vector3.up * liftHeight; // 공중유닛과 동일한 패턴: 목적지 + 이륙 높이
        isFlyingToDestination = true;
    }

    // 착륙 예약(공식 착륙 비행) 중이었다면 취소하고 예약해둔 그리드 셀/고스트를 정리한다.
    // (비행 중 파괴되거나, 우클릭/Move버튼으로 새 자유이동 명령을 받아 착륙 예약이 무효화될 때 호출)
    private void CancelPendingLandingFlight()
    {
        if (onRelocationCancelled == null)
            return;

        System.Action cancelled = onRelocationCancelled;
        onRelocationCancelled = null;
        onRelocationLanded = null;
        pendingLanding = false;

        cancelled.Invoke();
    }

    // 착륙 완료: 그리드에 새 위치를 등록하고 지상 상태로 복귀한다.
    private void Land()
    {
        isLifted = false;
        isAscending = false;
        isFlyingToDestination = false;
        isDescending = false; // 착륙 후에도 이 플래그가 남아있으면 다음 이륙 시 목표 고도 도달과 동시에
                               // 바로 이 하강 분기로 다시 진입해버려서(옛 flightDestination으로 즉시 재착륙),
                               // 실제로는 그리드에 등록되지 않은 자리를 등록된 것처럼 표시하는 버그가 생긴다.
        pendingLanding = false;

        gridPosition = pendingGridPosition;
        hasGridPosition = true;

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = true;

        System.Action landed = onRelocationLanded;
        onRelocationLanded = null;
        onRelocationCancelled = null;

        landed?.Invoke();
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

    // 생산 대기열이 가득 찼는지 (UnitSpawner에 위임) - 자원을 소모하기 전에 먼저 확인하기 위함
    public bool IsProductionQueueFull()
    {
        return UnitSpawner != null && UnitSpawner.IsQueueFull();
    }

    // 대기열의 특정 항목 생산을 취소한다 (UnitSpawner에 위임) - 환불을 위해 취소된 유닛ID를 반환한다.
    public int CancelProduction(int index)
    {
        return UnitSpawner.Cancel(index);
    }

    // 파괴 시 대기열에 남아있던 항목들을 반환(제거)한다 - UnitSpawner가 없는 건물(생산 불가 건물)은 null.
    public IReadOnlyList<ProductionData> ClearProductionQueue()
    {
        return UnitSpawner != null ? UnitSpawner.ClearQueue() : null;
    }

    // 건물 파괴 처리: 대기열 환불, RTSUnitController의 관리 목록에서 제거, 인구수 반환 후 게임오브젝트를 파괴한다.
    // (HealthManager의 IDestructible 구현체로 호출됨)
    public void Die()
    {
        CancelPendingLandingFlight(); // 착륙 위치로 비행 중(또는 착륙 직전)에 파괴되면 예약해둔 그리드 셀/고스트를 정리

        rtsController?.RefundProductionQueue(ClearProductionQueue()); // 대기열에 남아있던 유닛들 환불
        rtsController?.BuildingList.Remove(this);
        rtsController?.selectedBuildingList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel 등)가 유령 참조를 들고 있지 않도록
        rtsController?.RemoveMaxPopulationForBuilding(buildingID); // 이 건물이 제공하던 인구수 한도를 반환

        Destroy(gameObject);
    }
}
