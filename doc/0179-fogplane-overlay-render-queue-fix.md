# 0179 - FogPlane을 Overlay 큐로 고정해 파티클/라인 등 Transparent 오브젝트까지 확실히 덮기

## 요청

[[territory-outline-fog-occlusion-render-queue-fix]](0178)에서 라인 렌더러 전용으로 Opaque 큐를
강제했는데도 여전히 안개를 뚫고 보였고, 추가로 "라인 렌더러 + 파티클도 어둠안에서 보이는걸
확인했어 이건 왜 csFogWar 스크립트에서 가려주지 못하는 걸까"라는 질문 — 파티클까지 뚫린다는 건
라인 하나만의 문제가 아니라 구조적인 문제라는 신호.

## 원인

`FogPlane.shader`는 `"Queue" = "Transparent"`(~3000)로 그려진다. Unity/URP 렌더링 순서:

1. **Opaque 큐**(지형, 유닛 메쉬 등)를 통째로 먼저 그림
2. **Transparent 큐**(파티클, 반투명 이펙트, 그리고 이번에 확인한 라인도 포함)를 **카메라 거리
   기준으로 정렬**해서 그림

`FogPlane`의 `ZTest Always`는 "자기보다 먼저 그려진 것"만 depth 상관없이 덮어버린다. 그런데 파티클
등 **같은 Transparent 큐 안에 있는 다른 오브젝트와는 그냥 카메라 거리로 정렬 경쟁**을 하는 처지라,
그것들이 카메라에 더 가깝다고 판정되면 `FogPlane`보다 나중에 그려져 위에 얹혀버린다 — 이게 파티클/
라인이 뚫고 보이는 진짜 원인. 지형(Opaque)만 항상 안전하게 덮이는 건 Opaque 큐가 애초에 Transparent
큐보다 항상 먼저 끝나기 때문.

0178에서 라인 하나에만 렌더 큐를 강제했던 건 이 문제의 일부(라인)만 겨냥한 임시방편이었고, 앞으로도
계속 나올 수 있는 다른 반투명 이펙트(파티클 등)는 못 잡는 구조. 매번 개별 오브젝트를 손보는 대신,
**`FogPlane` 자신을 씬에서 가장 마지막에 그려지도록 못박는** 쪽이 근본 해결책 — 그러면 Transparent
큐 안에서의 거리 경쟁에서 항상 이겨서 무엇이든(파티클, 라인, 앞으로 추가될 이펙트까지) 예외 없이
덮는다.

## 변경 사항

### 1. `Assets/AssetFolder/AOSFogWar/csFogWar.cs` — `InitializeFog()`에 Overlay 큐 고정 추가

Before:
```csharp
            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);
```

After:
```csharp
            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            // 파티클/라인 렌더러 등 다른 Transparent 큐 오브젝트와 카메라 거리 기준으로 정렬 경쟁을 하면
            // (거리에 따라) 안개보다 나중에 그려져 뚫고 보이는 경우가 생긴다. Overlay로 큐를 못박아
            // 씬의 그 무엇보다도 항상 마지막에 그려지도록 해서, ZTest Always와 함께 예외 없이 덮는다.
            fogPlane.GetComponent<MeshRenderer>().material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);
```

### 2. `Assets/Scripts/CaptureSystem/TerritoryZone.cs` — 0178의 라인 전용 큐 강제 제거

이제 관리 지점을 `FogPlane` 하나로 모으므로, 개별 오브젝트(라인)에 걸어뒀던 임시방편은 제거.

Before:
```csharp
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        // FogPlane(Transparent 큐, ZTest Always)보다 먼저 그려지도록 Opaque 큐에 고정 - 그래야
        // FogPlane이 지형을 덮는 것과 동일한 방식(안개 알파값만큼 덮어 칠함)으로 라인도 부분 차폐된다.
        runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        outlineRenderer.material = runtimeMaterial;
```

After:
```csharp
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        outlineRenderer.material = runtimeMaterial;
```

## 확인 필요

Play 모드에서 실제로 라인/파티클이 안개 속에서 안 보이는지 확인 부탁드립니다. 만약 이번에도 여전히
뚫려 보인다면, `RenderQueue.Overlay`(4000)보다도 더 높은 값을 쓰는 다른 이펙트가 있거나, URP
렌더러 에셋 설정(예: 별도의 Render Feature가 큐와 무관하게 특정 레이어를 나중에 그리는 경우)이
개입하고 있을 가능성이 있어 Frame Debugger로 직접 순서를 봐야 합니다.
