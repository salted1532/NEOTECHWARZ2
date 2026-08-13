using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FischlWorks_FogWar;

// 적 건물 "껍데기". 캠페인은 정해진 스크립트/트리거로 적 유닛을 직접 배치·스폰할 예정이라, 적 건물이
// 실제로 생산 큐/자원 소모/건설 그리드 같은 걸 가질 필요가 없다 - 지금은 체력만 갖고 있다가 파괴되면
// 사라지는 오브젝트로만 동작한다 ("적 전초기지 파괴" 같은 미션 목표의 대상이 되는 정도). `HealthManager`가
// 데미지/사망을 처리하고, 이 컴포넌트는 그 사망 처리(`IDestructible`)만 구현한다.
//
// 나중에 스커미시에서 OC를 실제로 플레이 가능한 진영으로 만들 때(doc/0243) 생산 큐 등 실제 기능이
// 필요해지면, `BuildingController`를 참고해서 이 클래스를 확장하면 된다 - 클래스 이름을 미리 맞춰둬서
// 나중에 갈아엎지 않아도 되게 함.
public class EnemyBuildingController : MonoBehaviour, IDestructible
{
    // 씬에 존재하는 모든 적 건물 목록. 등록/해제(=파괴) 시에만 갱신되고 이벤트로 알리므로, "적 건물
    // 모두 파괴" 같은 스테이지 목표는 매 프레임 스캔하지 않고 이 이벤트를 구독해서 필요할 때만
    // 다시 계산하면 된다.
    public static readonly List<EnemyBuildingController> ActiveBuildings = new List<EnemyBuildingController>();
    public static event System.Action OnActiveBuildingsChanged;

    [SerializeField] private GameObject buildingMarker; // 선택 표시 (EnemyUnitController.enemyMarker와 동일한 패턴)
    [SerializeField] private string buildingName; // Info_panel에 표시할 이름
    [SerializeField] private Sprite icon;          // Info_panel에 표시할 아이콘
    private string infoDescription;                // Info_panel에 표시할 설명 (BuildingData.infoDescription, doc/0476)

    // 미니맵에 표시하는 y20대 스프라이트 마커(자식 오브젝트, 인스펙터에서 연결). EnemyUnitController.minimapIcon과
    // 동일한 이유(안개가 실제 3D Plane이라 Y가 높은 오브젝트는 깊이 테스트로 안 가려짐)로 Update()에서
    // 안개 상태를 직접 조회해 켜고 끈다 (doc/0356/0361).
    [SerializeField] private SpriteRenderer minimapIcon;

    // 공격 명령(우클릭/A 모드) 피드백 마커 깜빡임 (EnemyUnitController.flashInterval/flashCount와 동일한 패턴, doc/0248)
    [SerializeField] private float flashInterval = 0.3f;
    [SerializeField] private int flashCount = 3;
    private Coroutine flashRoutine;

    // OC Building Data SO(EnemyBuildingDataSO)의 BuildingData.ID와 매칭되는 값 - Start()에서 이 ID로
    // 이름/체력을 조회해 ApplyBuildingData()로 덮어쓴다 (EnemyUnitController.enemyUnitID와 동일한 패턴, doc/0245).
    [SerializeField] private int enemyBuildingID;

    // 시작 시 지면 높이를 재는 레이어 (BuildingController.groundLayer와 동일한 용도, doc/0246)
    [SerializeField] private LayerMask groundLayer;

    private RTSUnitController rtsController;
    private PlacementSystem placementSystem;
    private float groundOffset; // 이 건물의 메쉬 피벗이 지면에서 얼마나 떨어져 있는지 (PlacementSystem.GetGroundOffsetY와 동일한 계산)
    private HealthManager healthManager; // Info_panel 표시용 - Start에서 한 번만 캐싱

    // 선택 중인 건물이 안개 속으로 들어가면 선택을 풀어준다 (EnemyUnitController.fogWar와 동일한 패턴, doc/0360).
    private csFogWar fogWar;

    private void Start()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
        fogWar = FindFirstObjectByType<csFogWar>();

        // 씬에 직접 배치해둔 적 건물(캠페인은 전부 이 방식 - 정상 건설 흐름 자체가 없음)도 시작하자마자
        // 정확히 지면에 붙도록 위치를 보정한다 (BuildingController와 동일한 패턴, doc/0246).
        groundOffset = PlacementSystem.GetGroundOffsetY(gameObject);
        SnapToGround();

        // 씬에 직접 배치해둔 적 건물도 자기 크기(BuildingData.Size)에 맞춰 그리드 셀 중앙으로 위치를 맞추고,
        // PlacementSystem의 그리드 점유 정보에 스스로 등록한다 (BuildingController와 동일한 패턴, doc/0247).
        RegisterToGridIfPossible();

        // 씬에 직접 배치됐든 나중에 스크립트로 생성됐든, 항상 자기 enemyBuildingID로 OC Building Data SO를
        // 조회해서 스스로 이름/체력을 적용한다 (EnemyUnitController.Start()와 동일한 패턴).
        ApplyBuildingData(rtsController != null ? rtsController.GetEnemyBuildingData(enemyBuildingID) : null);

        ActiveBuildings.Add(this);
        OnActiveBuildingsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        ActiveBuildings.Remove(this);
        OnActiveBuildingsChanged?.Invoke();
    }

    // 건물은 안 움직이지만 플레이어 유닛이 이동하면서 안개가 계속 바뀌므로 매 프레임 다시 확인한다.
    // 미니맵 마커 토글(doc/0361)과 선택 해제(doc/0360)가 같은 안개 조회 결과를 공유한다.
    private void Update()
    {
        bool revealed = FogVisibility.IsRevealed(fogWar, transform.position);

        if (minimapIcon != null)
            minimapIcon.enabled = revealed;

        if (!revealed)
            rtsController?.ClearSelectedEnemyBuildingIfMatches(this);
    }

    // 그리드 셀 좌표를 역산해 자기 크기에 맞춰 위치를 정렬하고, PlacementSystem의 그리드 점유 정보에
    // 자신을 등록한다 (BuildingController.RegisterToGridIfPossible과 동일한 패턴, doc/0247).
    // enemyBuildingID로 조회한 BuildingData가 없거나 그 칸이 이미 점유돼 있으면 아무것도 하지 않는다.
    private void RegisterToGridIfPossible()
    {
        if (placementSystem == null || rtsController == null)
            return;

        BuildingData data = rtsController.GetEnemyBuildingData(enemyBuildingID);
        if (data == null)
            return;

        Vector3Int gridPos = placementSystem.WorldToGridCell(transform.position);

        if (!placementSystem.RegisterBuildingGrid(gameObject, gridPos, data.Size, enemyBuildingID))
            return;

        // 그리드 셀 크기에 맞춰 XZ만 중앙정렬한다. Y는 방금 SnapToGround()로 맞춘 실제 지면 좌표를 그대로
        // 넘겨서 유지한다 (그리드 셀 크기로 Y를 되돌리면 지형 높이와 어긋난다 - doc/0150과 동일한 이유).
        transform.position = placementSystem.GetGroundPosition(gridPos, data.Size, transform.position.y);
    }

    // 현재 XZ 위치의 지면 높이 + groundOffset(피벗-지면 거리)으로 transform.position.y를 다시 맞춘다
    // (BuildingController.SnapToGround와 동일한 패턴). groundLayer가 비어있거나 레이캐스트가 실패하면
    // 원래 위치 그대로 유지된다.
    private void SnapToGround()
    {
        if (groundLayer == 0)
            return;

        Vector3 rayOrigin = new Vector3(transform.position.x, 1000f, transform.position.z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
            return;

        Vector3 pos = transform.position;
        pos.y = hit.point.y + groundOffset;
        transform.position = pos;
    }

    // 이름/체력을 SO 값으로 덮어쓴다. 프리팹 자체에 미리 박아둔 값은 인스펙터 프리뷰용 기본값 역할만
    // 하고, 실제로는 이 메서드를 통해 OC Building Data SO 값이 반영된다 (EnemyUnitController.ApplyUnitData와 동일한 패턴).
    public void ApplyBuildingData(BuildingData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        infoDescription = LocalizationManager.GetTextOrFallback($"building.oc.{data.ID}.info", data.infoDescription);
        buildingName = LocalizationManager.GetTextOrFallback($"building.oc.{data.ID}.name", data.Name);

        healthManager?.InitializeHealth(data.hp);
    }

    public HealthManager GetHealthManager() => healthManager;

    public void SelectEnemyBuilding()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(true);
    }

    public void DeselectEnemyBuilding()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }

    public string GetBuildingName() => buildingName;
    public Sprite GetIcon() => icon;
    public string GetDescription() => infoDescription;

    // OC/Spore Brood Building Data SO의 ID 그대로 - AllyAIDirector가 특정 건물 종류(예: Hive Core)를
    // 우선 공격 대상으로 지목할 때 이름 대신 ID로 비교한다(doc/0543, GetEnemyUnitID()와 동일한 패턴).
    public int GetBuildingID() => enemyBuildingID;

    // 공격 명령(우클릭/A 모드)을 받았을 때 "어느 건물이 대상인지" 피드백으로 마커를 짧게 깜빡인다
    // (EnemyUnitController.FlashMarker/BuildingController.FlashMarker와 동일한 패턴, doc/0248).
    public void FlashMarker()
    {
        if (buildingMarker == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(flashInterval);

        for (int i = 0; i < flashCount; i++)
        {
            buildingMarker.SetActive(true);
            yield return wait;
            buildingMarker.SetActive(false);
            yield return wait;
        }

        bool isSelected = rtsController != null && rtsController.selectedEnemyBuilding == this;
        buildingMarker.SetActive(isSelected);

        flashRoutine = null;
    }

    // 사망 처리: 지금은 그냥 파괴만 한다 (인구수 반환/자원 환불/생산 대기열 정리 등 없음 - 애초에 그런
    // 기능이 없는 껍데기라서). 선택된 채로 죽었을 때 UI가 유령 참조를 들고 있지 않도록 정리만 해준다.
    public void Die()
    {
        rtsController?.ClearSelectedEnemyBuildingIfMatches(this);

        Destroy(gameObject);
    }
}
