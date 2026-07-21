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
    // 이동 중 실시간으로 자기 발밑 지면 높이를 알아내기 위한 레이어(지형/땅). 비워두면(Nothing) 지면 높이 추적
    // 없이 목적지 고도로 곧장 직선 이동한다(이 경우 언덕을 완전히 넘기 전에 미리 하강할 수 있음).
    [SerializeField] private LayerMask groundLayer;

    private NavMeshObstacle navMeshObstacle;
    private PlacementSystem placementSystem;

    private bool isLifted;             // 현재 공중에 떠 있는지(상승 중/대기 중/이동 중/하강 중 모두 포함)
    private bool isAscending;          // 이륙 중(상승 중)
    private bool isFlyingToDestination;// 착륙 위치로 수평 이동 중
    private bool isDescending;         // 착륙 위치 위에서 하강 중

    private Vector3 verticalTarget;    // 상승 목표(현재 위치 기준 + liftHeight)
    // 수평 비행 목표(목적지 좌표 + liftHeight 고도) - 자유이동/착륙 비행 공통.
    // 목적지의 지면 높이(Y)를 기준으로 매번 다시 계산한다 - 언덕/저지대처럼 지형 높이가 다른 곳으로 이동해도
    // 항상 "그 지점 + liftHeight"가 되도록. 건물 이동 목적지는 항상 지면 레이캐스트로만 들어오므로(다른 유닛/건물의
    // 이미 공중에 뜬 좌표를 목적지로 받는 경우가 없음) 매번 다시 더해도 고도가 중첩될 일은 없다.
    private Vector3 flightDestination;
    private Vector3 landingGroundDestination; // 착륙 최종 지면 좌표 (pendingLanding일 때만 유효, 하강 단계 전용)

    private Vector3Int gridPosition;   // 현재 자신이 점유 중인 그리드 셀 좌표
    private bool hasGridPosition;      // gridPosition이 유효한지 (에디터에 미리 배치된 시작 건물은 처음엔 false)

    private Vector3Int pendingGridPosition;      // 착륙 예정 위치의 그리드 좌표 (비행 중에만 유효)
    private System.Action onRelocationLanded;    // 착륙 완료 시(Land) 호출 - PlacementSystem이 고스트 제거
    private System.Action onRelocationCancelled; // 착륙 전에 파괴되는 등 비행이 중단될 때 호출 - 그리드 예약 해제 + 고스트 제거

    private bool pendingLanding; // true면 현재 수평이동이 "공식 착륙 비행"(도착 시 하강→착륙까지 이어짐), false면 우클릭/Move버튼 자유이동(도착 시 그 자리에서 계속 공중 대기)

    // 이 건물의 메쉬 피벗이 바닥(지면)에서 얼마나 떨어져 있는지(PlacementSystem.GetGroundOffsetY와 동일한 계산).
    // 건물이 지면에 서 있을 때의 transform.position.y = (그 지점 지면 높이) + groundOffset이다.
    // SampleGroundHeight()는 순수 지면 높이(레이캐스트로 잰 지형 표면)만 돌려주므로, 비행 중 고도를 계산할 때
    // 이 오프셋을 더해줘야 이륙 시(자기 transform.position 기준으로 상승) 도달한 고도와 이동 중 고도가 일치한다.
    // 안 더하면 건물 메쉬 크기만큼(피벗-지면 거리) 이동 중 고도가 이륙 때보다 낮게 계산된다.
    private float groundOffset;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();

        groundOffset = PlacementSystem.GetGroundOffsetY(gameObject);

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
            // 수평(X/Z)과 수직(Y)을 각각 독립적으로 목표에 수렴시킨다. 예전엔 Vector3.MoveTowards로 목적지(수평+수직)를
            // 한 번에 보간했는데, 그러면 수평 이동거리가 liftHeight보다 훨씬 크면(흔한 경우) 방향 벡터의 수직 성분이
            // 희석돼서 "목적지에 거의 다 왔을 때가 돼서야 살짝 상승"하는 것처럼 보였다 - 이륙 직후(아직 목표 고도에
            // 도달 못한 상태)에 바로 이동 명령을 내리면 특히 두드러짐(수평 이동은 정상인데 고도는 안 오르는 것처럼 보임).
            // 이제는 X/Z는 목적지를 향해 수렴하고, Y는 목적지 고도로 곧장 보간하는 대신 매 프레임 "지금 발밑 지면 +
            // liftHeight"를 다시 재서 그쪽으로 수렴한다 - 그래야 언덕 위를 지나는 동안은 그만큼 떠 있다가, 언덕을
            // 실제로 벗어나 발밑 지형이 낮아지는 순간에 맞춰 고도도 자연스럽게 낮아진다(공중 유닛과 동일한 방식,
            // [[0084-air-unit-terrain-hugging-altitude]] 참고).
            Vector3 pos = transform.position;

            Vector3 horizontalTarget = new Vector3(flightDestination.x, pos.y, flightDestination.z);
            pos = Vector3.MoveTowards(pos, horizontalTarget, liftMoveSpeed * Time.deltaTime);

            // 도착 판정도 미리 계산해둔 flightDestination.y가 아니라 "지금 이 프레임에 실제로 향하고 있는" 고도
            // (desiredY)와 비교해야 한다. flightDestination.y는 그리드 기준 지면 좌표로 미리 계산된 값이라 실제
            // 지형(레이캐스트로 잰 값)과 완전히 일치한다는 보장이 없는데, 예전처럼 flightDestination.y와 비교하면
            // 그 미세한 차이 때문에 도착 판정이 영원히 안 나서(수직 오차가 0.05를 못 좁힘) 착륙까지 못 이어지는
            // 문제가 있었다.
            // groundOffset(메쉬 피벗-지면 거리)을 반드시 더해야 한다 - SampleGroundHeight는 순수 지면 표면 높이만
            // 돌려주는데, 이륙 시 도달한 고도(transform.position + liftHeight)에는 이 피벗 오프셋이 이미 포함돼
            // 있어서, 여기서 안 더하면 이동 중엔 이륙 때보다 딱 그 오프셋만큼 낮게 계산돼버린다.
            float groundBelow = SampleGroundHeight(pos, flightDestination.y - liftHeight - groundOffset);
            float desiredY = groundBelow + groundOffset + liftHeight;
            pos.y = Mathf.MoveTowards(pos.y, desiredY, liftMoveSpeed * Time.deltaTime);

            transform.position = pos;

            bool arrivedHorizontally = Mathf.Abs(pos.x - flightDestination.x) < 0.05f && Mathf.Abs(pos.z - flightDestination.z) < 0.05f;
            bool arrivedVertically = Mathf.Abs(pos.y - desiredY) < 0.05f;

            if (arrivedHorizontally && arrivedVertically)
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

    // xzPosition의 X/Z 바로 아래에 있는 지면(groundLayer) 높이를 레이캐스트로 알아낸다. 못 찾으면 fallback을 쓴다.
    // UnitController.SampleGroundHeight와 동일한 패턴.
    private float SampleGroundHeight(Vector3 xzPosition, float fallback)
    {
        if (groundLayer == 0)
            return fallback;

        Vector3 rayOrigin = new Vector3(xzPosition.x, 1000f, xzPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
            return hit.point.y;

        return fallback;
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

        GetComponent<BuildingEffects>()?.PlayTakeoff();
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
        // 수평 비행 중엔 착륙지점 상공까지만 이동. destination(PlacementSystem이 계산)은 이미
        // GetGroundOffsetY(피벗 오프셋)가 반영된 좌표라 여기서 groundOffset을 또 더하지 않는다
        // (MoveWhileLifted의 groundDestination과 달리 이쪽은 이미 포함돼 있음).
        flightDestination = new Vector3(destination.x, destination.y + liftHeight, destination.z);
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
        // 목적지의 지면 높이(Y) 기준으로 (피벗 오프셋 + liftHeight)만큼 띄운 고도까지 이동 - 언덕 위/아래로
        // 이동해도 항상 "그 지점 + groundOffset + liftHeight"가 되도록 목적지 Y를 그대로 반영한다.
        // groundDestination.y는 우클릭/Move 지점의 순수 지면 raycast 값(피벗 보정 없음)이라 groundOffset을
        // 직접 더해줘야 한다(BeginRelocationFlight의 destination과 달리 여기엔 애초에 피벗 보정이 안 들어있음).
        flightDestination = new Vector3(groundDestination.x, groundDestination.y + groundOffset + liftHeight, groundDestination.z);
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

        GetComponent<BuildingEffects>()?.PlayLanding();
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

    // 프리뷰/고스트용: PreviewSystem이 이 컴포넌트를 비활성화(Start() 자체가 안 돎)하기 직전에 호출해,
    // 마커(및 그 자식의 상시 재생 파티클인 Circle Select 등)가 켜진 채로 노출되지 않도록 미리 숨긴다.
    public void HideMarkerForGhost()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
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
