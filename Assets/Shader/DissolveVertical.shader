// 유닛 파괴 연출용: 세로(높이) 방향으로 점진적으로 사라지는 디졸브 쉐이더 (URP).
// 실제 디졸브 계산(노이즈/임계값)은 DissolveVertical.hlsl에 있고, 이 파일은 그걸 감싸는 URP 패스다.
Shader "Custom/DissolveVertical"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Dissolve)]
        // 0 = 완전히 보임, 1 = 완전히 사라짐. 사망 연출 스크립트에서 시간에 따라 0->1로 올려주면 된다.
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        // 오브젝트 스페이스 기준 (최소 높이, 최대 높이). 유닛 메시의 발밑~정수리에 맞게 조정.
        _HeightRange ("Height Range (min, max, _, _)", Vector) = (0, 2, 0, 0)
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        [Toggle(_INVERT_DIRECTION)] _InvertDirection ("Invert Direction (Top -> Down)", Float) = 0

        [Header(Edge)]
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.08
        [HDR] _EdgeColor ("Edge Color", Color) = (0.3, 1.4, 2.5, 1)
        _EdgeIntensity ("Edge Intensity", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _INVERT_DIRECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DissolveVertical.hlsl"

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
                float  heightOS   : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _DissolveAmount;
                float4 _HeightRange;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _EdgeIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.heightOS = IN.positionOS.y;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
            #if defined(_INVERT_DIRECTION)
                float invert = 1.0;
            #else
                float invert = 0.0;
            #endif

                float dissolveValue = ComputeDissolveValue(
                    IN.heightOS, IN.uv, _HeightRange, _NoiseScale, _NoiseStrength, invert);

                // 임계값보다 낮은 픽셀은 이미 "사라진" 부분이라 그려내지 않는다.
                clip(dissolveValue - _DissolveAmount);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight();
                half nDotL = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half3 ambient = SampleSH(IN.normalWS) * albedo;
                half3 litColor = albedo * mainLight.color * nDotL + ambient;

                // 디졸브 경계 바로 위쪽만 얇게 발광 색으로 덮어써서 "타들어가는/녹는" 가장자리를 표현.
                float edgeMask = 1.0 - smoothstep(0.0, _EdgeWidth, dissolveValue - _DissolveAmount);
                half3 finalColor = litColor + _EdgeColor.rgb * _EdgeIntensity * edgeMask;

                return half4(finalColor, texColor.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
