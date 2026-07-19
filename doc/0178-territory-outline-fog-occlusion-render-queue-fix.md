# 0178 - 영토 외곽선을 FogPlane과 동일한 방식(렌더 큐)으로 부분 차폐

## 요청

[[territory-outline-hidden-in-fog-design]](0177)에서 만든 "핀 포인트 기준으로 선 전체를 껐다 켰다"
방식을 사용자가 반려("그런식으로 하면 안될거 같네 다시 되돌려줘") — 이유를 물으니: "정확히는 가려진
부분 fog부분만 안보이도록 해야하는데... 라인렌더러는 껏다가 키는게 아니라 부분만 보이고 어두운
부분에 가려진 부분은 안보이는 형식으로 가야할거같아" — 즉 오브젝트 전체 on/off가 아니라, 안개가
지형을 덮는 것과 똑같이 **선의 일부분씩만** 안개에 가려지길 원함.

## 원인 조사

`Assets/AssetFolder/AOSFogWar/Shaders/FogPlane.shader`를 직접 확인:

```
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite OFF
ZTest Always
```

`ZTest Always`가 핵심 — 이 셰이더는 깊이(depth)로 "가려지는" 게 아니라, **Opaque 큐가 전부 그려진
다음, depth를 무시하고 안개 텍스처의 알파값만큼 무조건 덮어 칠하는** 방식이다. 알파=1(안개)이면
완전히 덮고, 알파=0(트임)이면 전혀 안 덮어서 아래 지형이 그대로 보인다 — 이게 지형에서 "부분만
가려지는" 것처럼 보이는 이유.

`TerritoryZone.Awake()`의 `outlineMaterial`은 (현재 씬 기준) 인스펙터에 비어 있어(`outlineMaterial:
{fileID: 0}`) 코드에서 `new Material(Shader.Find("Universal Render Pipeline/Unlit"))`로 즉석
생성한다. 이렇게 코드로만 인스턴스화하면 렌더 큐가 확실히 Opaque로 고정된다는 보장이 없어, 만약
Transparent 큐 쪽으로 걸리면 `FogPlane`(Transparent)보다 나중에(위에) 그려져 안개를 뚫고 보여버린다.

## 수정

`Assets/Scripts/CaptureSystem/TerritoryZone.cs` `Awake()` — `runtimeMaterial` 생성 직후 렌더 큐를
명시적으로 Opaque(`Geometry`, 2000)로 고정.

Before:
```csharp
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        outlineRenderer.material = runtimeMaterial;
```

After:
```csharp
        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        // FogPlane(Transparent 큐, ZTest Always)보다 먼저 그려지도록 Opaque 큐에 고정 - 그래야
        // FogPlane이 지형을 덮는 것과 동일한 방식(안개 알파값만큼 덮어 칠함)으로 라인도 부분 차폐된다.
        runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        outlineRenderer.material = runtimeMaterial;
```

이 한 줄만으로 별도의 온/오프 스크립트 로직 없이 라인이 안개와 완전히 같은 매커니즘(같은 프레임,
같은 알파 블렌딩)으로 덮인다 — [[territory-outline-hidden-in-fog-design]](0177)에서 되돌린 핀 포인트
전체 on/off 방식과 달리, 씬 파일이나 다른 스크립트(`TerritoryFogReveal`, `csFogWar`)는 전혀 안
건드림.

## 확인 필요 / 검증

에디터에서 Play 모드로 실제 확인해보고, 여전히 안개를 뚫고 보이면 `outlineMaterial`이 나중에 커스텀
머티리얼로 채워질 때 그 머티리얼 자체의 Surface Type(Opaque/Transparent) 설정도 같이 확인이
필요함 — `renderQueue`를 강제해도 셰이더 패스 자체가 Transparent 전용 Blend/ZWrite로 컴파일된
경우엔 큐 값만으로 완전히 해결 안 될 수 있음(이번 케이스는 `outlineMaterial`이 비어있어 fallback
URP/Unlit 사용 중이라 문제 없을 것으로 예상).
