# 0592. 언덕/건물에 가려진 유닛 실루엣 표시 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 언덕이나 건물 뒤편으로 가게 되면 가려진 유닛이 보이도록하는 기능 외곽선을 만들거나 건물에
> 투영된 다른 색으로 된 메쉬가 보이도록 하거나 그래야할듯 어떤게 좋을까
>
> 실루엣을 그럼 #19FF00 이 색으로 해주고 카메라 기준 언덕이나 건물에 가려진 유닛의 경우 실루엣이
> 보이도록 해줘

이전 턴에서 아웃라인(엣지 디텍션) 대신 실루엣(ZTest Greater로 가려진 부분만 그리는 단색 메쉬)을
추천했고, 사용자가 그 방향으로 색상(#19FF00)까지 확정.

## 동작 원리

셰이더 트릭 하나로 끝나서 별도의 "가려짐 판정" 코드가 필요 없다 - 유닛의 기존 메쉬를 단색 머티리얼로
한 번 더(추가 머티리얼 슬롯) 그리되, 깊이 테스트를 `Greater`로 뒤집는다. 그러면 GPU가 매 픽셀마다
자동으로 "지금 이 자리에 이미 더 가까운(카메라 기준) 뭔가가 그려져 있는가"를 판정해주므로, 언덕이나
건물처럼 불투명한 오브젝트에 가려진 픽셀에서만 실루엣 패스가 그려지고, 가려지지 않은 부분은 기존
표면 패스가 이미 그 픽셀을 차지하고 있어 실루엣 패스가 안 보인다. Transparent 큐에 넣어서(불투명
오브젝트들이 전부 깊이버퍼를 다 쓴 뒤에 그려지도록) 그리기 순서에 따라 결과가 흔들리는 걸 방지한다.

## 적용 범위 (확인 필요)

플레이어(NTA) 유닛 + 아군 OC 유닛에만 적용하고, 적 유닛(OC 비아군/Spore_Brood)에는 적용하지 않을
계획이다 - 적에게도 적용하면 지형 뒤에 숨어있는 적 위치가 그대로 노출돼서 안개(Fog of War)/시야
시스템과 충돌하는 밸런스 변화가 된다. "내 부대가 지금 어디 있는지 놓치지 않는" 용도로 한정.
(적에게도 원하면 말해줘 - `EnemyUnitController.cs`에도 같은 한 줄만 추가하면 됨.)

건물 자체(요청한 "가려진 유닛"이 아니라 가리는 쪽으로 언급됨)에는 적용하지 않는다.

## 변경 계획

### 1. 새 셰이더 `Assets/Shader/UnitSilhouette.shader`
`DissolveVertical.shader`와 동일한 프로젝트 관례(URP HLSL 패스, CBUFFER)를 따른다.
```hlsl
Shader "Custom/UnitSilhouette"
{
    Properties
    {
        _Color ("Silhouette Color", Color) = (0.098, 1, 0, 1) // #19FF00
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest Greater
            Blend One Zero // 알파블렌딩 없이 단색으로 덮어써서 가려진 부분에서도 또렷하게 보이도록

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
```

### 2. 새 머티리얼 `Assets/Resources/UnitSilhouette.mat`
위 셰이더 사용, `_Color = #19FF00`. `Resources` 폴더에 둬서 프리팹을 하나도 안 건드리고
`Resources.Load`로 코드에서 바로 가져다 쓴다 (`TerritoryZone.cs`/`RadiusIndicator.cs`가 이미
`Shader.Find`로 런타임에 셰이더/머티리얼을 얻어오는 것과 같은 관례).

### 3. 새 헬퍼 `Assets/Scripts/Effects/UnitSilhouette.cs`
```csharp
using UnityEngine;

// 유닛이 언덕/건물 등에 가려졌을 때도 위치를 알 수 있도록, 렌더러마다 실루엣 머티리얼을 추가
// 슬롯으로 덧붙인다(doc/0592). 셰이더의 ZTest Greater가 "가려진 픽셀에서만 그리기"를 전담하므로
// 여기선 가림 판정 로직이 필요 없다 - 머티리얼만 붙여주면 끝.
public static class UnitSilhouette
{
    private static Material silhouetteMaterial;

    public static void Apply(GameObject root)
    {
        if (silhouetteMaterial == null)
            silhouetteMaterial = Resources.Load<Material>("UnitSilhouette");

        if (silhouetteMaterial == null)
            return;

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
}
```

### 4. 호출부 추가
`Assets/Scripts/Unit/UnitController.cs`의 `Awake()`, `Assets/Scripts/FogOfWar/Ally/AllyController.cs`의
`Awake()` 끝에 `UnitSilhouette.Apply(gameObject);` 한 줄씩 추가.

## 변경 예정 파일
- `Assets/Shader/UnitSilhouette.shader` (신규)
- `Assets/Resources/UnitSilhouette.mat` (신규)
- `Assets/Scripts/Effects/UnitSilhouette.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs`

---

## 적용 (사용자 승인 후)

제안대로 전부 적용함 (사용자가 "이대로 진행시켜줘"로 승인):

1. `Assets/Shader/UnitSilhouette.shader` 생성 - `Shader.Find`로 정상 컴파일 확인.
2. `Assets/Resources/UnitSilhouette.mat` 생성(+`.meta`) - `Dissolve.mat`과 동일한 YAML 구조로 직접
   작성, 셰이더를 guid로 참조. `_Color = (0.09803922, 1, 0, 1)`(#19FF00) 확인.
3. `Assets/Scripts/Effects/UnitSilhouette.cs` 생성 - `Resources.Load`로 머티리얼을 캐싱해두고,
   `MeshRenderer`/`SkinnedMeshRenderer`에만(파티클/트레일 렌더러 제외) 머티리얼을 추가 슬롯으로 붙임.
4. `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/FogOfWar/Ally/AllyController.cs`의
   `Awake()` 끝에 `UnitSilhouette.Apply(gameObject);` 추가.

`npx uloop-cli compile` 성공 확인(Error 0개). Unity 에디터에서 Play Mode로 실제 확인:
- 머티리얼 로드/색상/`Resources.Load` 일치 검증 통과
- Play Mode에서 자연스럽게 스폰된(Awake가 실제로 실행된) 유닛의 렌더러 4개 전부에
  `UnitSilhouette` 머티리얼이 마지막 슬롯으로 정상 추가됨 확인 (matCount 1→2)

## 변경된 파일
- `Assets/Shader/UnitSilhouette.shader` (신규)
- `Assets/Resources/UnitSilhouette.mat` (신규)
- `Assets/Scripts/Effects/UnitSilhouette.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs`
