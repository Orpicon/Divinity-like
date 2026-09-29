#ifndef DIVINITY_SURFACE_FLOOR_INCLUDED
#define DIVINITY_SURFACE_FLOOR_INCLUDED

// Логика вида пола с поверхностями. Не зависит от пайплайна: её подключает SurfaceFloorLitData.hlsl
// (HDRP Lit) и может подключить любой другой шейдер.
//
// Данные приходят из SurfaceFloorRenderSystem / SurfacePaintSystem через Shader.SetGlobal* — материал их не хранит.
//
// _SurfaceTypeMap : RGBAFloat, один тексель = одна клетка: r = тип, g = прошлый тип, b = время смены типа.
// _SurfacePalette : RGBAHalf, ширина = число типов, 3 строки:
//                   y=0: rgb основной цвет,        a = smoothness
//                   y=1: rgb второй цвет мерцания, a = скорость мерцания (0 — не мерцает)
//                   y=2: rgb цвет свечения (HDR),  a = не используется
// _SurfaceGridParams: xy = мировые XZ угла сетки, z = размер клетки, w = ширина перехода между клетками (0..1)
// _SurfaceGridSize  : xy = размер сетки в клетках, z = сила "рваности" краёв, w = длительность смены типа (сек)
// _SurfaceTime      : время мира в секундах (то же, что пишется в b-канал при смене типа)
// _SurfaceBrush     : xy = мировые XZ центра кисти, z = радиус в метрах, w = 1 если кисть видна
// _SurfaceBrushColor: цвет кольца кисти
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

// Простой value noise — для "рваных" краёв луж и неровного фронта смены типа.
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

// Внешний вид одного типа поверхности в точке.
// Сюда потом придут текстуры: вместо цветов из палитры — выборка из Texture2DArray по индексу type.
// phase — фаза мерцания; её считают по мировой позиции, чтобы мерцание было плавным и не зависело от клеток.
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

// Данные клетки: r = тип, g = прошлый тип, b = время смены. За пределами сетки — "нет поверхности", давно без изменений.
float4 LoadCellData(int2 cell)
{
    bool inside = all(cell >= 0) && all(cell < (int2)_SurfaceGridSize.xy);
    return inside ? LOAD_TEXTURE2D(_SurfaceTypeMap, cell) : float4(0, 0, -1e6, 0);
}

// Прогресс смены типа 0..1 (1 — переход закончен).
float CellTransitionProgress(float4 data)
{
    return saturate((_SurfaceTime - data.b) / max(_SurfaceGridSize.w, 1e-3));
}

// Вид одной клетки с учётом смены типа: прошлый тип "растворяется" в новом за _SurfaceGridSize.w секунд.
// dissolveNoise делает фронт перехода неровным — новая поверхность "растекается" пятнами, а не просто проявляется.
SurfaceLayer SampleCell(float4 data, float dissolveNoise, float phase)
{
    uint type = (uint)round(data.r);
    uint previousType = (uint)round(data.g);

    // Растягиваем прогресс на [-0.3; 1.3], чтобы шум успел пройти весь диапазон: в начале нет ни одного
    // пятна нового типа, в конце не осталось ни одного пятна старого.
    float t = smoothstep(dissolveNoise - 0.15, dissolveNoise + 0.15, lerp(-0.3, 1.3, CellTransitionProgress(data)));

    SurfaceLayer current = SampleSurfaceLayer(type, phase);
    [branch] if (type == previousType || t >= 1.0)
        return current;
    return LerpLayer(SampleSurfaceLayer(previousType, phase), current, t);
}

// Главная функция: итоговый вид пола в мировой точке positionWS (абсолютные координаты).
void EvaluateSurfaceFloor(float3 positionWS, out float3 baseColor, out float smoothness, out float3 emission)
{
    float cellSize = max(_SurfaceGridParams.z, 1e-4);
    float2 posCells = (positionWS.xz - _SurfaceGridParams.xy) / cellSize;

    // Координата в "пространстве клеток", где центры клеток — целые числа.
    float2 g = posCells - 0.5;

    // Искажаем координату шумом, чтобы границы были неровными, а не по линейке сетки.
    float2 warp = float2(SurfaceValueNoise(posCells * 1.3), SurfaceValueNoise(posCells * 1.3 + 17.0)) - 0.5;
    g += warp * _SurfaceGridSize.z;

    int2 c0 = (int2)floor(g);
    float2 f = frac(g);

    float4 d00 = LoadCellData(c0);
    float4 d10 = LoadCellData(c0 + int2(1, 0));
    float4 d01 = LoadCellData(c0 + int2(0, 1));
    float4 d11 = LoadCellData(c0 + int2(1, 1));

    float phase = SurfaceValueNoise(positionWS.xz * 0.8) * 6.2831;

    // Быстрый путь — большая часть пола: все 4 клетки одного типа и ни одна не в процессе смены.
    // Тогда смешивать нечего, и считаем один слой вместо восьми.
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
        // Ширина перехода: 1 — плавно через всю клетку, меньше — резче граница между клетками.
        float blend = max(_SurfaceGridParams.w, 0.01);
        f = smoothstep(0.5 - blend * 0.5, 0.5 + blend * 0.5, f);

        // Шум для фронта смены типа — в метрах, чтобы пятна были одного размера при любой дискретности сетки.
        float dissolveNoise = SurfaceValueNoise(positionWS.xz * 2.5);

        // Билинейное смешивание 4 ближайших клеток. Каждая даёт свой "слой" поверхности.
        SurfaceLayer l00 = SampleCell(d00, dissolveNoise, phase);
        SurfaceLayer l10 = SampleCell(d10, dissolveNoise, phase);
        SurfaceLayer l01 = SampleCell(d01, dissolveNoise, phase);
        SurfaceLayer l11 = SampleCell(d11, dissolveNoise, phase);
        result = LerpLayer(LerpLayer(l00, l10, f.x), LerpLayer(l01, l11, f.x), f.y);
    }

    baseColor = result.albedo;
    smoothness = result.smoothness;
    emission = result.emission;

    // Кольцо кисти под курсором (_SurfaceBrush.w = 0 — кисть скрыта).
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
