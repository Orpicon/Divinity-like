#ifndef DIVINITY_SURFACE_FLOOR_INCLUDED
#define DIVINITY_SURFACE_FLOOR_INCLUDED

TEXTURE2D(_SurfaceTypeMap);
TEXTURE2D(_SurfacePalette);
float4 _SurfaceGridParams;
float4 _SurfaceGridSize;
float _SurfaceTime;
float4 _SurfaceBrush;
float4 _SurfaceBrushColor;

float SurfaceHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SurfaceValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = SurfaceHash(i);
    float b = SurfaceHash(i + float2(1, 0));
    float c = SurfaceHash(i + float2(0, 1));
    float d = SurfaceHash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

struct SurfaceLayer
{
    float3 albedo;
    float smoothness;
    float3 emission;
};

SurfaceLayer SampleSurfaceLayer(uint type, float phase)
{
    float4 baseColor = LOAD_TEXTURE2D(_SurfacePalette, int2(type, 0));
    float4 altColor = LOAD_TEXTURE2D(_SurfacePalette, int2(type, 1));
    float4 glow = LOAD_TEXTURE2D(_SurfacePalette, int2(type, 2));

    float flicker = altColor.a > 0.0 ? 0.5 + 0.5 * sin(_SurfaceTime * altColor.a + phase) : 0.0;

    SurfaceLayer layer;
    layer.albedo = lerp(baseColor.rgb, altColor.rgb, flicker);
    layer.smoothness = baseColor.a;
    layer.emission = glow.rgb * (0.6 + 0.4 * flicker);
    return layer;
}

SurfaceLayer LerpLayer(SurfaceLayer a, SurfaceLayer b, float t)
{
    SurfaceLayer r;
    r.albedo = lerp(a.albedo, b.albedo, t);
    r.smoothness = lerp(a.smoothness, b.smoothness, t);
    r.emission = lerp(a.emission, b.emission, t);
    return r;
}

float4 LoadCellData(int2 cell)
{
    bool inside = all(cell >= 0) && all(cell < (int2)_SurfaceGridSize.xy);
    return inside ? LOAD_TEXTURE2D(_SurfaceTypeMap, cell) : float4(0, 0, -1e6, 0);
}

float CellTransitionProgress(float4 data)
{
    return saturate((_SurfaceTime - data.b) / max(_SurfaceGridSize.w, 1e-3));
}

SurfaceLayer SampleCell(float4 data, float dissolveNoise, float phase)
{
    uint type = (uint)round(data.r);
    uint previousType = (uint)round(data.g);

    float t = smoothstep(dissolveNoise - 0.15, dissolveNoise + 0.15, lerp(-0.3, 1.3, CellTransitionProgress(data)));

    SurfaceLayer current = SampleSurfaceLayer(type, phase);
    [branch] if (type == previousType || t >= 1.0)
        return current;
    return LerpLayer(SampleSurfaceLayer(previousType, phase), current, t);
}

void EvaluateSurfaceFloor(float3 positionWS, out float3 baseColor, out float smoothness, out float3 emission)
{
    float cellSize = max(_SurfaceGridParams.z, 1e-4);
    float2 posCells = (positionWS.xz - _SurfaceGridParams.xy) / cellSize;

    float2 g = posCells - 0.5;

    float2 warp = float2(SurfaceValueNoise(posCells * 1.3), SurfaceValueNoise(posCells * 1.3 + 17.0)) - 0.5;
    g += warp * _SurfaceGridSize.z;

    int2 c0 = (int2)floor(g);
    float2 f = frac(g);

    float4 d00 = LoadCellData(c0);
    float4 d10 = LoadCellData(c0 + int2(1, 0));
    float4 d01 = LoadCellData(c0 + int2(0, 1));
    float4 d11 = LoadCellData(c0 + int2(1, 1));

    float phase = SurfaceValueNoise(positionWS.xz * 0.8) * 6.2831;

    bool sameType = d00.r == d10.r && d00.r == d01.r && d00.r == d11.r;
    bool settled = min(min(CellTransitionProgress(d00), CellTransitionProgress(d10)),
                       min(CellTransitionProgress(d01), CellTransitionProgress(d11))) >= 1.0;

    SurfaceLayer result;
    [branch] if (sameType && settled)
    {
        result = SampleSurfaceLayer((uint)round(d00.r), phase);
    }
    else
    {
        float blend = max(_SurfaceGridParams.w, 0.01);
        f = smoothstep(0.5 - blend * 0.5, 0.5 + blend * 0.5, f);

        float dissolveNoise = SurfaceValueNoise(positionWS.xz * 2.5);

        SurfaceLayer l00 = SampleCell(d00, dissolveNoise, phase);
        SurfaceLayer l10 = SampleCell(d10, dissolveNoise, phase);
        SurfaceLayer l01 = SampleCell(d01, dissolveNoise, phase);
        SurfaceLayer l11 = SampleCell(d11, dissolveNoise, phase);
        result = LerpLayer(LerpLayer(l00, l10, f.x), LerpLayer(l01, l11, f.x), f.y);
    }

    baseColor = result.albedo;
    smoothness = result.smoothness;
    emission = result.emission;

    if (_SurfaceBrush.w > 0.0)
    {
        float dist = distance(positionWS.xz, _SurfaceBrush.xy);
        float ringWidth = 0.06;
        float ring = 1.0 - smoothstep(0.0, ringWidth, abs(dist - _SurfaceBrush.z));
        float inside = dist < _SurfaceBrush.z ? 0.15 : 0.0;
        baseColor = lerp(baseColor, _SurfaceBrushColor.rgb, saturate(ring + inside));
        emission += _SurfaceBrushColor.rgb * ring * 0.5;
    }
}

#endif
