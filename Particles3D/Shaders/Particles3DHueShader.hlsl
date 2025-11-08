struct Point // C#からのデータ構造をシンプルにするため残す
{
    float inputValue; // (今回は未使用だが、構造体を揃えるために残す)
    float hueShift; // 適用する色相シフト量 (H)
    float saturation; // 適用する彩度乗数 (S)
    float luminance; // 適用する明度乗数 (L)
};

cbuffer Constants : register(b0)
{
    // Point0 の値だけを直接使うための定数に簡略化
    float hueShift;
    float saturationFactor;
    float luminanceFactor;
    float factor; // lerp 係数
};

Texture2D<float4> InputTexture : register(t0);
SamplerState InputSampler : register(s0);

// --- HSL 変換関数 --- (処理が重い可能性がある)
float3 RGBToHSL(float3 color)
{
    float r = color.r;
    float g = color.g;
    float b = color.b;

    float max_val = max(max(r, g), b);
    float min_val = min(min(r, g), b);
    
    float h = 0.0;
    float s = 0.0;
    float l = (max_val + min_val) / 2.0;

    if (max_val == min_val)
    {
        h = s = 0.0;
    }
    else
    {
        float d = max_val - min_val;
        s = l > 0.5 ? d / (2.0 - max_val - min_val) : d / (max_val + min_val);
        
        if (max_val == r)
        {
            h = (g - b) / d + (g < b ? 6.0 : 0.0);
        }
        else if (max_val == g)
        {
            h = (b - r) / d + 2.0;
        }
        else
        {
            h = (r - g) / d + 4.0;
        }
        h /= 6.0;
    }
    return float3(h, s, l);
}

float HueToRGB(float p, float q, float t)
{
    if (t < 0.0)
        t += 1.0;
    if (t > 1.0)
        t -= 1.0;
    if (t < 1.0 / 6.0)
        return p + (q - p) * 6.0 * t;
    if (t < 1.0 / 2.0)
        return q;
    if (t < 2.0 / 3.0)
        return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
    return p;
}

float3 HSLToRGB(float3 hsl)
{
    if (hsl.y == 0.0)
    {
        return float3(hsl.z, hsl.z, hsl.z);
    }
    else
    {
        float q = hsl.z < 0.5 ? hsl.z * (1.0 + hsl.y) : hsl.z + hsl.y - hsl.z * hsl.y;
        float p = 2.0 * hsl.z - q;
        return float3(
            HueToRGB(p, q, hsl.x + 1.0 / 3.0),
            HueToRGB(p, q, hsl.x),
            HueToRGB(p, q, hsl.x - 1.0 / 3.0)
        );
    }
}
// ------------------------------

float4 main(float4 pos : SV_POSITION, float4 posScene : SCENE_POSITION, float4 uv0 : TEXCOORD0) : SV_Target
{
    float4 color = InputTexture.Sample(InputSampler, uv0.xy);
    
    if (color.a < 0.001)
    {
        return color;
    }

    float3 originalRgb = color.rgb / color.a;
    

    float3 hsl = RGBToHSL(originalRgb);
    float3 modifiedHsl = hsl;

    // C#側で設定した単一の補正値 (huePoints[0] 相当) を直接使用します

    modifiedHsl.x = frac((modifiedHsl.x * 360.0 + hueShift) / 360.0);
    modifiedHsl.y = saturate(modifiedHsl.y * saturationFactor);
    modifiedHsl.z = saturate(modifiedHsl.z * luminanceFactor);

    float3 finalRgb = HSLToRGB(modifiedHsl);
    finalRgb = lerp(originalRgb, finalRgb, factor);
    
    return float4(saturate(finalRgb) * color.a, color.a);
}