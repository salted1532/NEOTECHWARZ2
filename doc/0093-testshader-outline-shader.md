# 0093 - testShader에 아웃라인 쉐이더 추가 (제안)

**날짜:** 2026-07-13

## 요청 내용

> shader 폴더 안에 있는 testShader를 쉐이더를 추가해서 아웃라인 outline을 추가시켜줘

## 조사 내용

- `Assets/Shader/testShader.mat`은 커스텀 쉐이더가 아니라 Unity 기본 `Universal Render Pipeline/Lit` 쉐이더(guid `933532a4fcc9baf4fa0491de14d08ed7`)를 그대로 쓰는 머티리얼이었다. 즉 "testShader"는 이름만 쉐이더고 실제로는 아웃라인 기능이 없는 상태.
- `Assets/Shader` 폴더에는 이미 손으로 짠 커스텀 URP 쉐이더 사례(`DissolveVertical.shader` + `.hlsl`, `Dissolve.mat`)가 있어서 같은 방식(Shader Graph가 아니라 `.shader` 직접 작성)을 그대로 따르기로 함.
- 목표: `testShader.mat`이 참조할 새 커스텀 쉐이더(`Outline.shader`)를 만들고, 인버티드 헐(inverted hull) 기법으로 아웃라인을 그린다 — 메시를 노멀 방향으로 살짝 부풀린 뒤 앞면을 컬링(`Cull Front`)해서 뒷면만 그리면, 원래 메시가 그 위에 덮여 그려지면서 가장자리에 테두리만 남는 흔한 아웃라인 기법.
- `testShader.mat`에 이미 지정돼 있던 `_BaseMap`/`_EmissionMap` 텍스처와 `_BaseColor`/`_EmissionColor` 값은 새 쉐이더에서도 같은 프로퍼티 이름을 그대로 써서 유지되도록 설계함(쉐이더를 바꿔도 기존 값이 안 날아가게).

## 계획한 코드 변경

### 1. 새 파일 `Assets/Shader/Outline.shader` (신규)

```hlsl
// 아웃라인 테두리를 추가한 범용 URP 쉐이더 (testShader용).
// 인버티드 헐(inverted hull) 기법: 메시를 노멀 방향으로 부풀려 앞면을 컬링해서 뒷면만 그리고,
// 그 위에 원래 메시(Cull Back)를 덮어 그려서 가장자리에만 테두리가 남게 만든다.
Shader "Custom/Outline"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap ("Emission Map", 2D) = "white" {}

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // 1패스: 부풀린 메시의 뒷면만 그려서 테두리를 만든다. 2패스(본체)보다 먼저 그려야
        // 본체가 그 위를 덮어써서 가장자리만 남는다.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(positionOS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // 2패스: 기존 Lit 머티리얼과 동등한 기본 라이팅(메인 라이트 + 앰비언트) + 이미션.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight();
                half nDotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half3 ambient = SampleSH(IN.normalWS) * albedo;
                half3 litColor = albedo * mainLight.color * nDotL + ambient;

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;

                return half4(litColor + emission, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
```

같은 폴더의 다른 커스텀 쉐이더와 동일하게 `.meta` 파일도 함께 만든다 (guid를 직접 지정 - Unity를 실행하지 않은 채로 만들기 때문에, `testShader.mat`이 참조할 guid를 미리 고정해야 함):

```
fileFormatVersion: 2
guid: 5e2a731fca4e4d0b8f19c6d3a7b2e001
ShaderImporter:
  externalObjects: {}
  defaultTextures: []
  nonModifiableTextures: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

### 2. `Assets/Shader/testShader.mat` - 쉐이더 참조 교체 + 아웃라인 프로퍼티 추가

기존 코드 (`testShader.mat:11`):
```yaml
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
```

변경 코드:
```yaml
  m_Shader: {fileID: 4800000, guid: 5e2a731fca4e4d0b8f19c6d3a7b2e001, type: 3}
```

`m_Floats` 목록에 `_OutlineWidth` 추가 (기존 `_OcclusionStrength`와 `_Parallax` 사이 등 알파벳 순서 아무데나):
```yaml
    - _OutlineWidth: 0.02
```

`m_Colors` 목록에 `_OutlineColor` 추가:
```yaml
    - _OutlineColor: {r: 0, g: 0, b: 0, a: 1}
```

기존에 있던 `_BaseMap`(텍스처 `e090eeefd4ad15d458f94bb93b319505`), `_EmissionMap`(텍스처 `b32e92d958ce49b45b8500150fd09d1f`), `_BaseColor`, `_EmissionColor`, `_Metallic` 등 다른 프로퍼티 값들은 그대로 둔다 (새 쉐이더가 같은 이름의 프로퍼티는 그대로 읽고, Standard Lit 전용 프로퍼티(`_Metallic`, `_Smoothness` 등)는 새 쉐이더가 안 쓰므로 그냥 무시됨 - 지우지 않아도 무해함).

## 영향받는 파일 (신규 승인 시)

- `Assets/Shader/Outline.shader` (신규)
- `Assets/Shader/Outline.shader.meta` (신규)
- `Assets/Shader/testShader.mat` (쉐이더 참조 + 아웃라인 프로퍼티 2개 추가)

## 참고 / 확인 필요 사항

- 아웃라인 두께(`_OutlineWidth`)와 색(`_OutlineColor`)은 인스펙터에서 머티리얼마다 조절 가능. 기본값은 검은색 두께 0.02.
- `_OutlineWidth`를 오브젝트 스페이스 노멀 방향으로 밀어내는 방식이라, 메시 스케일이 크게 다른 오브젝트에 그대로 재사용하면 두께가 불균일하게 보일 수 있음 (필요하면 나중에 화면공간 두께 보정 추가 가능 - 지금은 요청 범위 밖이라 생략).
- 아직 프로젝트 파일에는 반영하지 않음 - 승인 시 위 내용 그대로 적용 예정.
