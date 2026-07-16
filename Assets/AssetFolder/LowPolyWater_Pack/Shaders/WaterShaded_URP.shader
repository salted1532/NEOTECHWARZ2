// LowPolyWater_Pack의 WaterShaded.shader(Built-in RP 전용, GrabPass + _CameraDepthTexture 직접 참조)를
// URP에서 쓸 수 있도록 새로 작성한 버전. 이 프로젝트는 URP만 쓰므로 Built-in 전용 원본은 그대로 두고
// 별도 파일로 분리했다 (doc/0159 참고).
//
// 원본 대비 생략/단순화한 부분:
// - GrabPass("_RefractionTex")는 원본 frag()에서 실제로 한 번도 샘플링되지 않는 죽은 코드였어서 통째로 제거
//   (URP는 SRP Batcher와 충돌하는 GrabPass를 지원하지 않아서 어차피 그대로 옮길 수 없었음).
// - 원본은 Point/Spot 라이트 분기(_WorldSpaceLightPos0.w)가 있었지만, 이 프로젝트 씬은 Directional Light
//   하나만 메인 라이트로 쓰므로 URP의 GetMainLight()(항상 방향광 기준) 하나로 단순화.
// - WATER_EDGEBLEND_ON/OFF 멀티컴파일 분기는 제거하고 항상 켜진 상태(원래 머티리얼의 기본 활성 변형)로 고정.
// - offsets.y(버텍스 오프셋)는 원본에서도 항상 half3(0,0,0)으로 고정된 죽은 값이라 그 결과인
//   saturate(0 - _Foam.y)를 상수 그대로 인라인.
Shader "LowPolyWater/WaterShaded_URP"
{
    Properties
    {
        _BaseColor ("Base color", Color) = (.54, .95, .99, 0.5)
        _SpecColor ("Specular Material Color", Color) = (1,1,1,1)
        _Shininess ("Shininess", Float) = 10
        _ShoreTex ("Shore & Foam texture", 2D) = "black" {}

        _InvFadeParemeter ("Auto blend parameter (Edge, Shore, Distance scale)", Vector) = (0.2, 0.39, 0.5, 1.0)

        _BumpTiling ("Foam Tiling", Vector) = (1.0, 1.0, -2.0, 3.0)
        _BumpDirection ("Foam movement", Vector) = (1.0, 1.0, -1.0, 1.0)

        _Foam ("Foam (intensity, cutoff)", Vector) = (0.1, 0.375, 0.0, 0.0)
        [MaterialToggle] _isInnerAlphaBlendOrColor("Fade inner to color or alpha?", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 500
        ColorMask RGB

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest LEqual
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_ShoreTex);
            SAMPLER(sampler_ShoreTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float _Shininess;
                float4 _InvFadeParemeter;
                float4 _BumpTiling;
                float4 _BumpDirection;
                float4 _Foam;
                float _isInnerAlphaBlendOrColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 bumpCoords  : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;

                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float2 tileableUv = positionWS.xz;

                // 원본과 동일하게 _Time.x(=t/20, 느린 시간축)로 UV를 흘려서 파도 무늬가 움직이는 것처럼 보이게 함
                o.bumpCoords = (tileableUv.xyxy + _Time.xxxx * _BumpDirection.xyzw) * _BumpTiling.xyzw;

                o.positionWS = positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionHCS);
                o.fogCoord = ComputeFogFactor(o.positionHCS.z);

                return o;
            }

            half4 CalculateBaseColor(Varyings i)
            {
                float3 normalWS = normalize(i.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float nDotL = dot(normalWS, lightDir);

                float3 ambient = SampleSH(normalWS) * _BaseColor.rgb;
                float3 diffuse = mainLight.color * _BaseColor.rgb * max(0.0, nDotL);

                float3 specular = float3(0, 0, 0);
                if (nDotL >= 0.0)
                {
                    specular = mainLight.color * _SpecColor.rgb *
                        pow(max(0.0, dot(reflect(-lightDir, normalWS), viewDir)), _Shininess);
                }

                return half4(ambient + diffuse + specular, 1.0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);

                half4 edgeBlendFactors = saturate(_InvFadeParemeter * (depth - i.screenPos.w));
                edgeBlendFactors.y = 1.0 - edgeBlendFactors.y;

                half4 baseColor = CalculateBaseColor(i);

                half4 shoreA = SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, i.bumpCoords.xy * 2.0);
                half4 shoreB = SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, i.bumpCoords.zw * 2.0);
                half4 foam = (shoreA * shoreB) - 0.125;

                // saturate(0.0 - _Foam.y): 원본의 offsets.y(항상 0)에서 온 상수항, 동일하게 유지
                baseColor.rgb += foam.rgb * _Foam.x * (edgeBlendFactors.y + saturate(0.0 - _Foam.y));

                if (_isInnerAlphaBlendOrColor == 0)
                    baseColor.rgb += 1.0 - edgeBlendFactors.x;
                if (_isInnerAlphaBlendOrColor == 1.0)
                    baseColor.a = edgeBlendFactors.x;

                baseColor.rgb = MixFog(baseColor.rgb, i.fogCoord);
                return baseColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
