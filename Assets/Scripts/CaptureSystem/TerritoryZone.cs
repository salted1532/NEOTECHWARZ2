using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// TerritoryZone이 자동 생성한 핀 오브젝트임을 표시하는 마커. 리스트 크기를 줄였을 때
// 참조가 사라진 핀을 골라내 정리하는 데 쓴다 (이름 문자열 비교 대신 컴포넌트로 식별).
public class TerritoryZonePin : MonoBehaviour { }

// 인스펙터에서 pinPoints 리스트의 Size만 원하는 꼭짓점 개수로 맞추면 빈 슬롯에 핀이 자동 생성된다.
// 각 핀을 씬 뷰에서 원하는 위치로 옮기면(Y는 무시, X/Z만 사용) 그 다각형이 영토 범위가 된다.
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class TerritoryZone : MonoBehaviour
{
    [Tooltip("리스트 크기(Size)만 원하는 꼭짓점 개수로 맞추면, 빈 슬롯에 핀 오브젝트가 자동으로 채워진다.")]
    [SerializeField] private List<Transform> pinPoints = new List<Transform>();

    [SerializeField] private CaptureOwner owner = CaptureOwner.Neutral;

    [Header("외곽선 표시")]
    [SerializeField] private Material outlineMaterial; // 원본 참고용 — 실제로 색이 바뀌는 건 런타임 복제본
    [SerializeField] private float outlineWidth = 0.3f;

    [Header("소유권별 색상 (자동 전환)")]
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color allyColor = Color.green;
    [SerializeField] private Color enemyColor = Color.red;

    private LineRenderer outlineRenderer;
    private Material runtimeMaterial; // outlineMaterial(또는 기본 셰이더)을 복제한 전용 인스턴스 — 이것만 색을 바꾼다

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void Awake()
    {
        outlineRenderer = GetComponent<LineRenderer>();
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;

        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        outlineRenderer.material = runtimeMaterial;
    }

    private void Update()
    {
        ApplyOutlineStyle();
        RefreshOutline();
    }

    private Color CurrentOwnerColor()
    {
        switch (owner)
        {
            case CaptureOwner.Ally: return allyColor;
            case CaptureOwner.Enemy: return enemyColor;
            default: return neutralColor;
        }
    }

    private void ApplyOutlineStyle()
    {
        Color c = CurrentOwnerColor();
        outlineRenderer.startColor = c;
        outlineRenderer.endColor = c;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
        if (runtimeMaterial != null) runtimeMaterial.color = c; // 복제본만 수정 — 원본 에셋(outlineMaterial)은 안 건드림
    }

    // 각 핀 위치에서 X, Z만 뽑아 다각형 정점 배열로 반환 (Y는 판정에 안 씀)
    public Vector2[] GetPolygonXZ()
    {
        var result = new Vector2[pinPoints.Count];
        for (int i = 0; i < pinPoints.Count; i++)
        {
            if (pinPoints[i] == null) return new Vector2[0];
            result[i] = new Vector2(pinPoints[i].position.x, pinPoints[i].position.z);
        }
        return result;
    }

    // point-in-polygon (crossing-number). 오목한 다각형도 정확히 판정한다.
    public bool Contains(Vector3 worldPos)
    {
        Vector2[] v = GetPolygonXZ();
        if (v.Length < 3) return false;

        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        bool inside = false;

        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if ((v[i].y > p.y) != (v[j].y > p.y) &&
                p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        }
        return inside;
    }

    private void RefreshOutline()
    {
        if (outlineRenderer == null) outlineRenderer = GetComponent<LineRenderer>();

        Vector2[] v = GetPolygonXZ();
        if (v.Length < 2)
        {
            outlineRenderer.positionCount = 0;
            return;
        }

        outlineRenderer.positionCount = pinPoints.Count;
        for (int i = 0; i < pinPoints.Count; i++)
            outlineRenderer.SetPosition(i, pinPoints[i].position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return; // Play 모드 중/전환 시점엔 씬 편집용 동기화를 돌리지 않는다

        // OnValidate 안에서 바로 씬을 건드리면 경고/에러가 날 수 있어 다음 에디터 틱으로 미룬다.
        EditorApplication.delayCall += SyncPinPoints;
    }

    private void SyncPinPoints()
    {
        if (this == null) return; // 그 사이 오브젝트가 삭제됐을 수 있음

        // 1) 빈 슬롯(리스트 크기를 늘려서 생긴 null)을 새 핀으로 채운다.
        for (int i = 0; i < pinPoints.Count; i++)
        {
            if (pinPoints[i] != null) continue;

            var pin = new GameObject($"PinPoint_{i}");
            pin.AddComponent<TerritoryZonePin>();
            pin.transform.SetParent(transform);
            pin.transform.localPosition = Vector3.zero;
            pinPoints[i] = pin.transform;
        }

        // 2) 리스트 크기를 줄여서 더 이상 참조되지 않는 핀 오브젝트를 정리한다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<TerritoryZonePin>() != null && !pinPoints.Contains(child))
                DestroyImmediate(child.gameObject);
        }

        RefreshOutline();
    }
#endif
}
