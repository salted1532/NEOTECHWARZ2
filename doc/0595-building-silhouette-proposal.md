# 0595. 건물(플레이어/아군 OC/적)에도 실루엣 적용 - 제안 (보류)

**날짜:** 2026-08-16

**상태:** 사용자가 "아니"로 보류 - 그보다 먼저 기존 유닛 실루엣이 직접 지은 건물/아군 OC 건물 뒤에서
안 뜨는 버그(doc/0596)부터 고쳐달라고 함. 이 제안 자체는 승인/거절 결정 안 됨, 나중에 다시 꺼낼 수
있음.

## 요청 내용

> 플레이어의 건물, 적건물, 아군OC 건물등에서도 작동하는거야?

확인 결과 현재는 유닛(`UnitController`, `AllyController`)에만 실루엣(doc/0592~0594)이 적용돼 있고
건물(`BuildingController`, `EnemyBuildingController`, `AllyBuildingController`)에는 적용돼 있지 않음.
적용 범위를 물어본 결과 세 건물 전부(플레이어 건물/아군 OC 건물/적 건물)에 추가하기로 확정.

## 조사 - 유닛과 똑같이 만들면 안 되는 이유

유닛용 실루엣(`UnitSilhouette.shader`)은 지형(Ground, 레이어 7) + 건물(Building, 레이어 9)만 그린
가림막 깊이 텍스처와 비교한다. 유닛 자신은 애초에 그 두 레이어에 없으므로 자기 부품에 가려서
오판하는 문제(doc/0594)가 구조적으로 불가능했다.

그런데 건물 자체는 정확히 그 "Building" 레이어(9)에 속해 있다 - 즉 건물용 실루엣 판정에 유닛과
똑같이 Ground+Building 가림막 텍스처를 그대로 쓰면, 건물 자신도 그 텍스처에 포함되어 doc/0594와
똑같은 자기 부품 오판 버그가 건물에서 재발한다. 실제로 테스트에 썼던
`struct_Radar_Outpost_A_yup.prefab`를 확인해보니 `MeshRenderer`가 2개(베이스 + 튀어나온 부분, 아마
레이더 안테나)로 나뉘어 있어 - 이 위험이 이론상이 아니라 실제로 존재하는 구조임을 확인함.

## 해결 방향

건물 전용으로 **Ground(지형) 레이어만** 담은 별도의 가림막 텍스처를 하나 더 만들어서, 건물의 실루엣
판정은 이 텍스처와만 비교한다. Building 레이어를 아예 빼버리므로 건물 자신(과 다른 모든 건물)이 그
텍스처에 없어 자기 부품 오판이 구조적으로 불가능해진다.

**트레이드오프 (확인 필요):** 이 방식은 "언덕/지형에 가려진 건물"만 실루엣이 뜨고, "다른 건물에 가려진
건물"은 실루엣이 안 뜬다. 건물끼리 서로 가리는 경우까지 구분하려면 오브젝트별 식별 정보(스텐실 등)가
추가로 필요해서 훨씬 복잡해지는데, 건물은 안 움직이는 고정 목표라 "내 부대가 어디 있는지 놓치지 않는"
유닛 실루엣의 원래 목적(doc/0592)과 달리 위치를 잊어버릴 일이 없어서 굳이 필요할까 싶다. 이대로
진행하고, 실사용해보고 부족하면 나중에 확장하는 걸 추천.

**안개(Fog of War)와의 상호작용:** `EnemyBuildingController`를 확인해보니 안개로 아직 발견 안 된 적/아군
OC 건물이라도 미니맵 아이콘만 껐다 켰다 하고(`minimapIcon.enabled`) 3D 모델 자체의 렌더러는 안개 상태와
무관하게 항상 켜져 있음 - 즉 이건 이번 변경이 새로 만드는 문제가 아니라 이미 존재하는 동작이라 이번
작업 범위 밖으로 둠 (건드리면 별도 요청으로 진행하는 게 나을 듯).

## 변경 계획

### 1. 새 셰이더 `Assets/Shader/BuildingSilhouette.shader`
`UnitSilhouette.shader`와 거의 동일하되, `_OccluderDepthTexGroundOnly`(Ground 레이어만 담은 텍스처)와
비교한다.
```hlsl
Shader "Custom/BuildingSilhouette"
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
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            TEXTURE2D(_OccluderDepthTexGroundOnly);
            SAMPLER(sampler_OccluderDepthTexGroundOnly);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.positionCS.xy / _ScreenParams.xy;
                float occluderDepth = SAMPLE_DEPTH_TEXTURE(_OccluderDepthTexGroundOnly, sampler_OccluderDepthTexGroundOnly, screenUV);

                float myLinear01 = Linear01Depth(IN.positionCS.z, _ZBufferParams);
                float occluderLinear01 = Linear01Depth(occluderDepth, _ZBufferParams);

                clip(myLinear01 - occluderLinear01 - 0.0005);

                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
```

### 2. 새 머티리얼 `Assets/Resources/BuildingSilhouette.mat`
`UnitSilhouette.mat`와 동일한 구조, 위 셰이더를 참조, `_Color = #19FF00`.

### 3. `Assets/Scripts/Effects/UnitSilhouette.cs` 수정 (개요)
Ground 전용 가림막 카메라/텍스처를 하나 더 만들고, 건물에 붙일 `ApplyToBuilding()`을 추가.
`AppendMaterial()`은 머티리얼을 파라미터로 받도록 바꿔서 유닛/건물이 공유.

### 4. 호출부 추가
- `Assets/Scripts/Building/BuildingController.cs`의 `Start()` 끝
- `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`의 `Start()` 끝 (AllyBuildingController가
  상속하므로 아군 OC 건물도 자동 적용)

## 변경 예정 파일
- `Assets/Shader/BuildingSilhouette.shader` (신규)
- `Assets/Resources/BuildingSilhouette.mat` (신규)
- `Assets/Scripts/Effects/UnitSilhouette.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`

---

## 적용 (사용자 승인 후)

(보류 - doc/0596 버그 수정 먼저 진행)
