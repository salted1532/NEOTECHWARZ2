using UnityEngine;

// 유닛이 언덕/건물 등에 가려졌을 때도 위치를 알 수 있도록, 렌더러마다 실루엣 머티리얼을 추가
// 슬롯으로 덧붙인다(doc/0592). 지형/건물 레이어만 그린 "가림막 전용" 깊이 텍스처를 매 프레임
// 만들어서 셰이더가 그것과만 비교하게 한다(doc/0594) - 화면 전체 깊이버퍼를 썼던 이전 방식은
// 유닛이 자기 자신의 다른 부품(터렛 등)에 가려진 것도 "가려짐"으로 오판했다.
public static class UnitSilhouette
{
    private static Material silhouetteMaterial;

    private static Camera occluderCamera;
    private static RenderTexture occluderDepthTexture;
    private static int occluderTexWidth;
    private static int occluderTexHeight;

    private const string OccluderDepthTexName = "_OccluderDepthTex";
    private const int OccluderLayerMask = (1 << 7) | (1 << 9); // Ground(7) | Building(9) - 라이브로 확인한 레이어(doc/0594)

    public static void Apply(GameObject root)
    {
        if (silhouetteMaterial == null)
            silhouetteMaterial = Resources.Load<Material>("UnitSilhouette");

        if (silhouetteMaterial == null)
            return;

        EnsureOccluderCamera();

        foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            AppendMaterial(mr);

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            AppendMaterial(smr);
    }

    // ParticleSystemRenderer/TrailRenderer/LineRenderer 등은 GetComponentsInChildren<Renderer>()로
    // 뭉뚱그리면 같이 걸려서 이펙트에도 초록 실루엣이 덧그려지는 사고가 나므로, 실제 몸체 메쉬
    // 렌더러 두 타입(Mesh/SkinnedMesh)만 명시적으로 골라서 처리한다.
    private static void AppendMaterial(Renderer renderer)
    {
        Material[] materials = renderer.materials; // 인스턴스 복사본 - sharedMaterials를 직접 건드리지 않음
        Material[] extended = new Material[materials.Length + 1];
        materials.CopyTo(extended, 0);
        extended[materials.Length] = silhouetteMaterial;
        renderer.materials = extended;
    }

    // 메인 카메라의 자식으로 보조 카메라를 만들어 지형/건물 레이어만 깊이로 렌더링한다 - 부모-자식
    // 관계라 위치/회전은 매 프레임 자동으로 따라간다. 줌은 위치 이동으로 구현돼 있어(CameraControl.cs)
    // FOV는 안 바뀌므로 최초 설정 시 한 번만 복사하면 충분하다. 화면 해상도가 바뀌면(창 크기 조절 등)
    // 텍스처를 다시 만든다.
    private static void EnsureOccluderCamera()
    {
        if (occluderCamera != null && Screen.width == occluderTexWidth && Screen.height == occluderTexHeight)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
            return;

        if (occluderDepthTexture != null)
            occluderDepthTexture.Release();

        occluderTexWidth = Screen.width;
        occluderTexHeight = Screen.height;
        occluderDepthTexture = new RenderTexture(occluderTexWidth, occluderTexHeight, 24, RenderTextureFormat.Depth)
        {
            name = "SilhouetteOccluderDepth"
        };

        if (occluderCamera == null)
        {
            GameObject camObj = new GameObject("SilhouetteOccluderDepthCamera");
            camObj.transform.SetParent(mainCam.transform, false);
            camObj.AddComponent<OccluderCameraResizeWatcher>();

            occluderCamera = camObj.AddComponent<Camera>();
            occluderCamera.fieldOfView = mainCam.fieldOfView;
            occluderCamera.nearClipPlane = mainCam.nearClipPlane;
            occluderCamera.farClipPlane = mainCam.farClipPlane;
            occluderCamera.orthographic = mainCam.orthographic;
            occluderCamera.orthographicSize = mainCam.orthographicSize;
            occluderCamera.cullingMask = OccluderLayerMask;
            occluderCamera.clearFlags = CameraClearFlags.Depth;
            occluderCamera.allowMSAA = false;
            occluderCamera.allowHDR = false;
        }

        occluderCamera.targetTexture = occluderDepthTexture;
        Shader.SetGlobalTexture(OccluderDepthTexName, occluderDepthTexture);
    }

    // 창 크기 변경처럼 유닛이 새로 스폰되지 않는 동안에도 해상도가 바뀔 수 있으므로, 보조 카메라
    // 자신에게 매 프레임 가벼운 크기 체크를 붙여둔다 (정수 비교라 비용은 무시할 수준).
    private class OccluderCameraResizeWatcher : MonoBehaviour
    {
        private void Update() => EnsureOccluderCamera();
    }
}
