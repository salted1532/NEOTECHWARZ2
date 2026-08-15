// 유닛이 언덕/건물 등에 가려졌을 때 보이는 실루엣 (doc/0592). 화면 전체의 공유 깊이버퍼와 비교하면
// (ZTest Greater) 터렛처럼 유닛 자기 자신의 다른 부품에 가려진 것도 "가려짐"으로 오판한다(doc/0594) -
// 그래서 지형(Ground)/건물(Building) 레이어만 그린 별도의 "가림막 전용" 깊이 텍스처
// (_OccluderDepthTex, UnitSilhouette.cs가 매 프레임 전역으로 채워줌)와 직접 비교한다. 유닛은 그
// 텍스처에 아예 없으므로 자기 부품/다른 유닛에 가려서 오판하는 경우가 구조적으로 없다.
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
            ZTest Always // 하드웨어 깊이테스트 대신 프래그먼트 안에서 _OccluderDepthTex와 직접 비교해서 판단한다
            Blend One Zero // 알파블렌딩 없이 단색으로 덮어써서 가려진 부분에서도 또렷하게 보이도록

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            // UnitSilhouette.cs가 Shader.SetGlobalTexture로 매 프레임 채워주는 전역 텍스처라 Properties에
            // 등록할 필요 없음 - 머티리얼마다 따로 배선하지 않아도 모든 인스턴스가 바로 접근 가능.
            TEXTURE2D(_OccluderDepthTex);
            SAMPLER(sampler_OccluderDepthTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.positionCS.xy / _ScreenParams.xy;
                float occluderDepth = SAMPLE_DEPTH_TEXTURE(_OccluderDepthTex, sampler_OccluderDepthTex, screenUV);

                // 가림막 카메라는 메인 카메라와 근/원평면·FOV가 동일하므로(UnitSilhouette.cs) 같은
                // _ZBufferParams로 둘 다 선형화해서 바로 비교할 수 있다. 가림막이 없는 픽셀은 깊이버퍼가
                // 원거리 클리어값을 그대로 갖고 있어 occluderLinear01이 1에 가까워, 자연스럽게 "가려짐
                // 없음"으로 떨어진다(별도 예외처리 불필요).
                float myLinear01 = Linear01Depth(IN.positionCS.z, _ZBufferParams);
                float occluderLinear01 = Linear01Depth(occluderDepth, _ZBufferParams);

                // 내가 가림막보다 뚜렷하게 더 멀 때만(=가려짐) 그린다. 작은 여유값(epsilon)은 순수
                // 부동소수점 잡음 방지용 - 자기 자신과 비교하는 경우 자체가 없어져서 doc/0593의
                // Offset 트릭은 더 이상 필요 없다.
                clip(myLinear01 - occluderLinear01 - 0.0005);

                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
