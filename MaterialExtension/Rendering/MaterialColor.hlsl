sampler2D source : register(s0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 pixel = tex2D(source, uv);
    float3 color = pixel.rgb / max(pixel.a, 0.0001);
    float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
    // Retain artwork color while restraining bright red/yellow areas.
    color = lerp(luminance.xxx, color, 0.84);
    color = color * 1.04 / (1.0 + color * 0.14);
    return float4(saturate(color) * pixel.a, pixel.a);
}
