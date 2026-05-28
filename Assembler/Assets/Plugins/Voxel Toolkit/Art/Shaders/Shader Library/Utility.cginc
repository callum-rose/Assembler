void SelectColor_float(float4 TextureColor, float4 VertexColor, float2 uv, out float4 OutputColor)
{
    OutputColor = any(uv.x < -1) ? VertexColor : TextureColor;
}

float CubeFaceIndex(float3 normal)
{
    float3 a = abs(normal);

    bool xMajor = a.x > a.y && a.x > a.z;
    bool yMajor = a.y > a.z;

    int axis =
        xMajor ? 0 :
        yMajor ? 1 : 2;

    int sign =
        axis == 0 ? (normal.x > 0) :
        axis == 1 ? (normal.y > 0) :
                    (normal.z > 0);

    return axis * 2 + (sign ? 0 : 1);
}

void ConvertUVWToUV_float(float3 normal, float3 uvw, out float2 uv, out float side)
{
    uv = uvw.xy * abs(normal.z) + uvw.xz * abs(normal.y) + uvw.zy * abs(normal.x);
    side = CubeFaceIndex(normal);
}
