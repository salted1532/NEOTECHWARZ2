using UnityEngine;

// 건물 배치 시 마우스 위치에 반투명 프리뷰(고스트 오브젝트)와 셀 커서를 보여주는 시스템.
// 배치 가능 여부(validity)에 따라 흰색/빨간색으로 피드백을 준다.
public class PreviewSystem : MonoBehaviour
{
    [SerializeField]
    private float previewYOffset = 0.06f;

    [SerializeField]
    private GameObject cellIndicator;
    private GameObject previewObject;

    [SerializeField]
    private Material previewMaterialPrefab;
    private Material previewMaterialInstance;

    private Renderer cellIndicatorRenderer;

    private void Start()
    {
        previewMaterialInstance = new Material(previewMaterialPrefab);
        cellIndicator.SetActive(false);
        cellIndicatorRenderer = cellIndicator.GetComponentInChildren<Renderer>();
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
            cellIndicator.transform.localScale = new Vector3(size.x, 1, size.y);
            cellIndicatorRenderer.material.mainTextureScale = size;
        }
    }

    // 생성된 프리뷰 오브젝트를 "허상"처럼 만든다: 모든 머티리얼을 반투명 프리뷰 머티리얼로 교체하고,
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void PreparePreview(GameObject previewObject)
    {
        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = previewMaterialInstance;
            }

            renderer.materials = materials;
        }

        // Collider OFF
        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Rigidbody OFF
        Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // ⭐ NavMeshObstacle OFF (핵심 추가)
        UnityEngine.AI.NavMeshObstacle[] obstacles =
            previewObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();

        foreach (var obs in obstacles)
        {
            obs.enabled = false;
        }
    }

    // 프리뷰 표시 종료: 셀 커서를 숨기고 프리뷰 오브젝트를 파괴한다.
    public void StopShowingPreview()
    {
        cellIndicator.SetActive(false);
        if (previewObject != null)
            Destroy(previewObject);
    }

    // 매 프레임 마우스 위치를 받아 프리뷰/커서 위치와 배치 가능 여부에 따른 색상 피드백을 갱신한다.
    public void UpdatePosition(Vector3 position, bool validity)
    {
        if (previewObject != null)
        {
            MovePreview(position);
            ApplyFeedbackToPreview(validity);

        }

        MoveCursor(position);
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

    // 셀 커서를 지면 살짝 아래(y -0.9)로 내려서 배치하여 바닥에 밀착된 것처럼 보이게 한다.
    private void MoveCursor(Vector3 position)
    {
        Vector3 cellPosition = position;
        cellPosition.y -= 0.9f;

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
