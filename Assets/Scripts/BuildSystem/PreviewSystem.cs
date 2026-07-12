using UnityEngine;

// 건물 배치 시 마우스 위치에 반투명 프리뷰(고스트 오브젝트)와 셀 커서를 보여주는 시스템.
// 배치 가능 여부(validity)에 따라 흰색/빨간색으로 피드백을 준다.
public class PreviewSystem : MonoBehaviour
{
    [SerializeField]
    private float previewYOffset = 0.06f;

    [SerializeField]
    private float cellIndicatorYOffset = 0.02f;

    [SerializeField]
    private GameObject cellIndicator;
    private GameObject previewObject;

    [SerializeField]
    private Grid grid;

    [SerializeField]
    private Material previewMaterialPrefab;
    private Material previewMaterialInstance;

    private Renderer cellIndicatorRenderer;

    // 포스트프로세싱(Bloom/Tonemapping/Color Adjustments)과 SSAO의 영향을 받지 않도록,
    // Main Camera는 그리지 않고 별도 Overlay 카메라만 그리는 전용 레이어(Indicators)에 프리뷰 고스트를 배치한다.
    private int indicatorsLayer;

    private void Start()
    {
        previewMaterialInstance = new Material(previewMaterialPrefab);
        cellIndicator.SetActive(false);
        cellIndicatorRenderer = cellIndicator.GetComponentInChildren<Renderer>();
        indicatorsLayer = LayerMask.NameToLayer("Indicators");
    }

    // 오브젝트와 그 자식 전체의 레이어를 재귀적으로 바꾼다 (previewObject는 매번 다른 건물 프리팹을
    // 그대로 Instantiate하므로, 프리팹 자체의 레이어를 건드리지 않고 런타임에만 전용 레이어로 옮긴다).
    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // 배치할 프리팹의 고스트(프리뷰) 오브젝트를 생성하고 셀 커서 크기를 맞춰 표시한다.
    public void StartShowingPlacementPreview(GameObject prefab, Vector2Int size)
    {
        previewObject = Instantiate(prefab);
        PreparePreview(previewObject);
        PrepareCursor(size);
        cellIndicator.SetActive(true);
    }

    // 프리뷰 오브젝트 없이 1x1 셀 커서만 표시 (건물 철거 등 커서만 필요한 모드용)
    public void StartBuildModeCursor()
    {
        cellIndicator.SetActive(true);

        PrepareCursor(Vector2Int.one);
    }

    // 셀 커서 오브젝트의 크기와 텍스처 스케일을 배치 대상 크기에 맞게 조정한다.
    private void PrepareCursor(Vector2Int size)
    {
        if (size.x > 0 || size.y > 0)
        {
            Vector3 cellSize = grid.cellSize;
            cellIndicator.transform.localScale = new Vector3(size.x * cellSize.x, 1, size.y * cellSize.z);
            cellIndicatorRenderer.material.mainTextureScale = size;
        }
    }

    // 생성된 프리뷰 오브젝트를 "허상"처럼 만든다: 지정한 머티리얼로 전부 교체하고,
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void PreparePreview(GameObject previewObject)
    {
        ApplyGhostMaterial(previewObject, previewMaterialInstance);
        DisableGameplayComponents(previewObject);
        SetLayerRecursively(previewObject, indicatorsLayer);
    }

    // 오브젝트의 모든 렌더러 머티리얼을 지정한 머티리얼 인스턴스로 교체한다.
    private void ApplyGhostMaterial(GameObject obj, Material material)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.materials = materials;
        }
    }

    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void DisableGameplayComponents(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        UnityEngine.AI.NavMeshObstacle[] obstacles =
            obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();

        foreach (var obs in obstacles)
        {
            obs.enabled = false;
        }

        // 프리뷰/고스트는 실제 체력이 없으므로(항상 풀피로 초기화됨) 체력바를 표시하면 오해를 일으킨다 - 숨긴다.
        HealthManager[] healthManagers = obj.GetComponentsInChildren<HealthManager>();
        foreach (HealthManager hm in healthManagers)
        {
            hm.SetHealthBarVisible(false);
        }
    }

    // 배치가 확정된 위치에 "일꾼이 도착할 때까지 남아있는" 정적 건설 고스트를 생성한다.
    // 마우스를 따라다니는 previewObject와는 완전히 별개의 오브젝트/머티리얼 인스턴스를 사용하므로
    // 이후 다른 건물을 미리보기해도 서로 색이 간섭하지 않는다. 항상 고정된 흰색(배치 가능 색)으로 표시.
    public GameObject SpawnConstructionGhost(GameObject prefab, Vector3 position)
    {
        GameObject ghost = Instantiate(prefab, position, Quaternion.identity);

        Material ghostMaterial = new Material(previewMaterialPrefab);
        Color c = Color.white;
        c.a = 0.5f;
        ghostMaterial.color = c;

        ApplyGhostMaterial(ghost, ghostMaterial);
        DisableGameplayComponents(ghost);

        return ghost;
    }

    // 프리뷰 표시 종료: 셀 커서를 숨기고 프리뷰 오브젝트를 파괴한다.
    public void StopShowingPreview()
    {
        cellIndicator.SetActive(false);
        if (previewObject != null)
            Destroy(previewObject);
    }

    // 매 프레임 마우스 위치를 받아 프리뷰/커서 위치와 배치 가능 여부에 따른 색상 피드백을 갱신한다.
    // previewPosition: 건물 프리뷰(고스트)용 위치 (건물 메쉬 바닥이 지면에 닿도록 이미 높이 보정됨)
    // groundPosition: 셀 커서(cellIndicator)용 위치 (건물 높이와 무관하게 항상 지면 기준)
    public void UpdatePosition(Vector3 previewPosition, Vector3 groundPosition, bool validity)
    {
        if (previewObject != null)
        {
            MovePreview(previewPosition);
            ApplyFeedbackToPreview(validity);

        }

        MoveCursor(groundPosition);
        ApplyFeedbackToCursor(validity);
    }

    // 배치 가능하면 흰색, 불가능하면 빨간색(반투명)으로 프리뷰 오브젝트 머티리얼을 물들인다.
    private void ApplyFeedbackToPreview(bool validity)
    {
        Color c = validity ? Color.white : Color.red;

        c.a = 0.5f;
        previewMaterialInstance.color = c;
    }

    // 배치 가능하면 흰색, 불가능하면 빨간색(반투명)으로 셀 커서 머티리얼을 물들인다.
    private void ApplyFeedbackToCursor(bool validity)
    {
        Color c = validity ? Color.white : Color.red;

        c.a = 0.5f;
        cellIndicatorRenderer.material.color = c;
    }

    // 셀 커서를 지면 기준 위치 + 살짝 띄운 오프셋(cellIndicatorYOffset)으로 배치해 바닥에 밀착된 것처럼 보이게 한다.
    // 건물 높이와 무관하게 항상 같은 높이(지면)에 그려져야 하므로, 건물 프리뷰 위치가 아닌 별도의 지면 위치를 받는다.
    private void MoveCursor(Vector3 position)
    {
        Vector3 cellPosition = position;
        cellPosition.y += cellIndicatorYOffset;

        cellIndicator.transform.position = cellPosition;
    }

    // 프리뷰 오브젝트를 지정 위치 + Y 오프셋(previewYOffset)만큼 띄워서 배치한다.
    private void MovePreview(Vector3 position)
    {
        previewObject.transform.position = new Vector3(
            position.x,
            position.y + previewYOffset,
            position.z);
    }

    // 건물 철거(제거) 모드용 커서 표시: 1x1 크기에 항상 "불가능(빨강)" 색으로 시작한다.
    internal void StartShowingRemovePreview()
    {
        cellIndicator.SetActive(true);
        PrepareCursor(Vector2Int.one);
        ApplyFeedbackToCursor(false);
    }
}
