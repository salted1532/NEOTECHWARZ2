# 0180 - 라인 렌더러 관련 코드/설정 되돌리기 (점령지 시야 기능은 유지)

## 요청

"현재 세션에서 거점 점령시 시야가 밝혀지는 부분 빼고 지금 라인렌더러 때문에 바꾼 모든 코드적인거
설정같은거 다 되돌려줘 레이어12관련해서 내가 바꾼건 건들지말고 좀 되돌려줄래" —
[[territory-permanent-vision-design]](0175)/[[territory-fog-reveal-timing-fix]](0176)의 점령지
강제 시야 기능은 그대로 두고, 그 이후 라인 렌더러가 안개를 뚫고 보이는 문제를 고치려고 건드렸던
코드/설정만 전부 되돌려달라는 요청. Layer 12(Indicators) 관련해서 사용자가 직접 에디터에서 바꾼
것(`Capture_territory` 등의 레이어)은 손대지 말라는 조건.

## 되돌린 대상 확인

이번 세션에서 라인 렌더러 문제 때문에 만든 변경 이력:

1. [[territory-outline-hidden-in-fog-design]](0177) — `TerritoryZone.cs`에 핀 포인트 기준 안개 확인 후
   `LineRenderer.enabled`를 껐다 켰다 하는 로직 → **사용자가 이미 반려("그런식으로 하면 안될거 같네
   다시 되돌려줘")해서 그 시점에 이미 원복 완료.** 지금 다시 확인해도 `TerritoryZone.cs`에 관련 코드
   없음(`fogWar` 필드, `UpdateFogVisibility()`, `IsAnyPinCurrentlyVisible()` 전부 없음).
2. [[territory-outline-fog-occlusion-render-queue-fix]](0178) — `TerritoryZone.cs` `Awake()`에서
   `runtimeMaterial.renderQueue`를 Opaque(`Geometry`)로 강제 → **0179에서 이미 제거 완료.** 지금
   다시 확인해도 없음.
3. [[fogplane-overlay-render-queue-fix]](0179) — `Assets/AssetFolder/AOSFogWar/csFogWar.cs`의
   `InitializeFog()`에서 `FogPlane` 머티리얼의 `renderQueue`를 `Overlay`(4000)로 강제한 부분 →
   **이번에 제거함** (유일하게 남아있던 라인/파티클 전용 수정).

## 변경 사항

`Assets/AssetFolder/AOSFogWar/csFogWar.cs` `InitializeFog()`

Before:
```csharp
            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            // 파티클/라인 렌더러 등 다른 Transparent 큐 오브젝트와 카메라 거리 기준으로 정렬 경쟁을 하면
            // (거리에 따라) 안개보다 나중에 그려져 뚫고 보이는 경우가 생긴다. Overlay로 큐를 못박아
            // 씬의 그 무엇보다도 항상 마지막에 그려지도록 해서, ZTest Always와 함께 예외 없이 덮는다.
            fogPlane.GetComponent<MeshRenderer>().material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);
```

After:
```csharp
            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);
```

## 유지되는 것 (건드리지 않음)

- `Assets/Scripts/CaptureSystem/TerritoryManager.cs`의 `Zones` 읽기 전용 노출 (0175)
- `Assets/Scripts/FogOfWar/TerritoryFogReveal.cs` 전체 (0175/0176) — 점령지 강제 시야 기능
- `csFogWar.cs`의 `OnBeforeFogTextureUpdate` 이벤트와 그 호출부 (0176) — 점령지 시야가 반투명하게
  보이던 타이밍 문제를 고친 부분이라, 라인 렌더러 문제와는 무관하게 그대로 유지
- `Capture_Point.prefab`, `Capture_point WHITE/GREEN/RED.prefab`의 레이어 값 — 사용자가 에디터에서
  직접 바꾼 것이라 이번에도 확인만 하고 손대지 않음(현재 전부 Layer 0 확인됨)

## 결과

라인 렌더러가 안개를 뚫고 보이는 문제 자체는 아직 미해결 상태로 남음(이번 요청은 "일단 되돌리기"가
목적) — 필요하면 다음에 다시 처음부터 진단하면 됨.
