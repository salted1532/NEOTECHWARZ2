#ifndef DISSOLVE_VERTICAL_INCLUDED
#define DISSOLVE_VERTICAL_INCLUDED

// 별도 노이즈 텍스처 없이 디졸브 경계를 울퉁불퉁하게 만들기 위한 2D 해시 기반 값 노이즈.
float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ValueNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);

    float a = Hash21(i);
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f); // smoothstep 보간

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 오브젝트 스페이스 Y좌표(heightOS)를 [_HeightRange.x, _HeightRange.y] 구간 기준으로 0~1 정규화하고,
// 노이즈를 섞어 픽셀별 "디졸브 임계값"을 만든다. invertDirection이 1이면 위->아래 방향으로 사라진다.
float ComputeDissolveValue(float heightOS, float2 uv, float4 heightRange, float noiseScale, float noiseStrength, float invertDirection)
{
    float heightT = saturate((heightOS - heightRange.x) / max(heightRange.y - heightRange.x, 1e-4));

    if (invertDirection > 0.5)
        heightT = 1.0 - heightT;

    float n = ValueNoise(uv * noiseScale) * noiseStrength;

    return saturate(heightT + n);
}

#endif
