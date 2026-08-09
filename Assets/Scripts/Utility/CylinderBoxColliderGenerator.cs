using UnityEngine;

// 실린더 형태의 콜리더를 BoxCollider 여러 개로 근사한다. Unity에는 CylinderCollider가 없어서
// 박스를 360도 방향으로 원형 배치해 대체한다. 인스펙터에서 컴포넌트 우클릭 →
// Generate Cylinder Collider 로 생성, Clear Cylinder Collider 로 제거.
public class CylinderBoxColliderGenerator : MonoBehaviour
{
    [Header("배치")]
    [SerializeField] private float radius = 1f;
    [SerializeField, Min(3)] private int boxCount = 8;

    [Header("박스 크기 (x=접선 방향 폭, y=높이, z=반지름 방향 두께)")]
    [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 2f, 0.3f);

    [SerializeField] private bool isTrigger = false;

    [ContextMenu("Generate Cylinder Collider")]
    private void Generate()
    {
        Clear();
        EnsureRigidbody();

        for (int i = 0; i < boxCount; i++)
        {
            float angle = 360f / boxCount * i;
            var rotation = Quaternion.Euler(0f, angle, 0f);
            var position = rotation * (Vector3.forward * radius);

            var segment = new GameObject($"BoxSegment_{i}");
            segment.transform.SetParent(transform, false);
            segment.transform.localPosition = position;
            segment.transform.localRotation = rotation;

            var box = segment.AddComponent<BoxCollider>();
            box.size = boxSize;
            box.isTrigger = isTrigger;
        }
    }

    [ContextMenu("Clear Cylinder Collider")]
    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (!child.name.StartsWith("BoxSegment_")) continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }

    // 자식 BoxCollider의 트리거 이벤트가 부모(이 오브젝트)의 스크립트(CaptureSystem 등)로 전달되려면
    // 부모에 Rigidbody가 있어야 한다 (Unity 복합 콜라이더 규칙). 움직일 필요는 없으니 Kinematic으로 고정.
    private void EnsureRigidbody()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
