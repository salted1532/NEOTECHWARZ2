using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 건물 기반(파운데이션) 오브젝트. PlacementSystem이 일꾼 도착 시 이 프리팹을 생성하고 Initialize()로
// 지어질 건물 종류/건설시간을 넘겨준다. 담당 일꾼(builder)이 붙어있는 동안에만 건설시간이 줄어들고,
// 담당 일꾼이 없으면(사망 등으로 Unity의 가짜 null이 되면) 건설이 자동으로 일시정지된다.
// 건설시간이 다 되면 해당 건물을 생성하고 자신은 파괴된다.
public class BaseStructure : MonoBehaviour, IDestructible
{
    [SerializeField] private BuildingDataSO buildingDatabase; // 완공 시 생성할 건물 프리팹을 buildingID로 조회

    [SerializeField] private GameObject buildingMarker; // 선택/우클릭 피드백용 마커(Marker 자식) - 평소엔 꺼져있음
    [SerializeField] private float markerFlashInterval = 0.3f;
    [SerializeField] private int markerFlashCount = 3;
    private Coroutine markerFlashRoutine;

    private int buildingID;
    private float remainingBuildTime;
    private Vector3 groundPosition; // 완공 시 실제 건물을 다시 배치할 지면 좌표(오프셋 없는 순수 지면 위치)
    private Vector3Int gridPosition; // 완공될 건물에 그대로 넘겨줄 그리드 좌표 (리프트 이동 시 자기 자리 해제용)

    private float healthPerSecond; // 건설 중 초당 채워지는 체력량 (완공될 건물의 최대체력 ÷ 건설시간)
    private float healAccumulator; // HealthManager.Heal()은 int만 받으므로 소수점 나머지를 누적해뒀다가 1 이상 모이면 반영
    private Sprite icon; // 완공될 건물의 아이콘(Info_panel 표시용, 완공될 건물 프리팹에서 미리 읽어옴)

    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)

    private void Awake()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    // 완공될 건물의 최대체력/아이콘을 프리팹에서 미리 읽어와 HealthManager와 Info_panel 표시에 반영한다.
    // buildingSize/cellSize: 프리팹 자체는 3x3(6x6 유닛) 기준으로 만들어져 있어서, 2x2처럼 더 작은/다른 크기의
    // 건물을 지을 땐 그 크기(그리드 칸 수 × 셀 크기)에 맞춰 가로/세로(X/Z) 스케일을 다시 계산해 덮어쓴다.
    // 세로(Y, 두께)는 프리팹에 원래 세팅된 값을 그대로 유지한다.
    public void Initialize(int buildingID, float buildTime, Vector3 groundPosition, Vector3Int gridPosition,
        Vector2Int buildingSize, Vector3 cellSize, System.Action onCancelledByPlayer)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
        this.groundPosition = groundPosition;
        this.gridPosition = gridPosition;
        this.onCancelledByPlayer = onCancelledByPlayer;

        Vector3 currentScale = transform.localScale;
        transform.localScale = new Vector3(buildingSize.x * cellSize.x, currentScale.y, buildingSize.y * cellSize.z);

        int finalMaxHealth = 0;

        BuildingData data = buildingDatabase != null
            ? buildingDatabase.buildingData.Find(d => d.ID == buildingID)
            : null;

        if (data != null && data.Prefab != null)
        {
            if (data.Prefab.TryGetComponent<HealthManager>(out var finishedHealth))
                finalMaxHealth = finishedHealth.GetMaxHealth();

            if (data.Prefab.TryGetComponent<BuildingController>(out var controller))
                icon = controller.GetIcon();
        }

        healthPerSecond = buildTime > 0f ? finalMaxHealth / buildTime : finalMaxHealth;

        if (healthManager != null)
        {
            healthManager.SetMaxHealth(finalMaxHealth);
            healthManager.SetHealth(0); // 건설 시작 시점엔 0에서부터 진행률만큼 차오르게 함
        }
    }

    private void Update()
    {
        if (builder == null)
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지

        remainingBuildTime -= Time.deltaTime;

        if (healthManager != null)
        {
            healAccumulator += healthPerSecond * Time.deltaTime;

            if (healAccumulator >= 1f)
            {
                int wholeHeal = Mathf.FloorToInt(healAccumulator);
                healAccumulator -= wholeHeal;
                healthManager.Heal(wholeHeal);
            }
        }

        if (remainingBuildTime <= 0f)
            CompleteConstruction();
    }

    // 일꾼이 도착해서 건설을 시작(또는 재개)할 때 호출된다. 이미 다른 일꾼이 담당 중이었다면 그 일꾼의 건설 담당을 풀어준다.
    public void AttachBuilder(UnitController worker)
    {
        if (builder != null && builder != worker)
            builder.FinishConstruction();

        builder = worker;
    }

    public int GetBuildingID() => buildingID;
    public Sprite GetIcon() => icon;

    // 좌클릭 선택 시(RTSUnitController) 마커를 켠다. 우클릭 피드백 깜빡임(FlashMarker)과 같은 마커를 공유한다.
    public void SelectStructure()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(true);
    }

    public void DeselectStructure()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }

    // 다른 일꾼을 이 건물 기반에 우클릭했을 때(UserControl) 피드백으로 마커를 짧게 깜빡인다. (Enemy/Building 등과 동일한 패턴)
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

        // 깜빡이는 도중 선택된 상태였다면 꺼진 채로 두지 않고 선택 마커 상태로 복원
        bool isSelected = rtsController != null && rtsController.selectedBaseStructure == this;
        buildingMarker.SetActive(isSelected);

        markerFlashRoutine = null;
    }

    private void CompleteConstruction()
    {
        BuildingData data = buildingDatabase != null
            ? buildingDatabase.buildingData.Find(d => d.ID == buildingID)
            : null;

        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            // ⭐ 건설 중 입은 피해가 완공된 건물에도 그대로 이어지도록, BaseStructure의 최종 체력을 넘겨준다.
            // (BaseStructure의 HealthManager는 Initialize()에서 이미 완공될 건물과 같은 최대체력 척도로 맞춰져 있음)
            if (healthManager != null && obj.TryGetComponent<HealthManager>(out var finishedHealthManager))
                finishedHealthManager.SetHealth(healthManager.GetHealth());

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            if (obj.TryGetComponent<BuildingController>(out var builtController))
                builtController.SetGridInfo(gridPosition); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록 전달

            rtsController?.AddMaxPopulation(data.maxpopulationamount); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
        }

        if (builder != null)
            builder.FinishConstruction();

        rtsController?.ClearSelectedStructureIfMatches(this);

        Destroy(gameObject);
    }

    // 플레이어가 Info_panel의 취소 버튼/단축키로 건설을 직접 취소했을 때 호출된다.
    // 건물 가격 전액을 환불하고, 담당 일꾼을 해제하고, 그리드 예약을 풀어준 뒤 스스로 파괴된다.
    public void CancelConstruction()
    {
        rtsController?.RefundBuilding(buildingID);

        if (builder != null)
            builder.FinishConstruction();

        onCancelledByPlayer?.Invoke();

        rtsController?.ClearSelectedStructureIfMatches(this);

        Destroy(gameObject);
    }

    // HealthManager가 체력이 0 이하가 됐을 때 호출(IDestructible) - 취소와 동일하게 환불/정리한다.
    // (현재는 BaseStructure를 실제로 공격하는 경로가 없어 이론상의 대비이지만, "취소 or 파괴"를 모두 커버하기 위해 구현.)
    public void Die()
    {
        CancelConstruction();
    }
}
