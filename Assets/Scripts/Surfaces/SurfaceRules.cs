using Unity.Mathematics;

namespace Divinity.Surfaces
{
    // Чистые функции без состояния: их можно вызывать и из Burst-джобов, и из обычного кода.
    public static class SurfaceRules
    {
        public static bool IsTemporary(SurfaceType type) =>
            type == SurfaceType.Fire || type == SurfaceType.ElectrifiedWater || type == SurfaceType.Steam;

        // Во что превращается временная поверхность, когда её время истекло.
        public static SurfaceType AfterExpire(SurfaceType type) =>
            type == SurfaceType.ElectrifiedWater ? SurfaceType.Water : SurfaceType.None;

        public static Surface Make(SurfaceType type, in SurfaceGridConfig config)
        {
            float lifetime = type switch
            {
                SurfaceType.Fire => config.FireDuration,
                SurfaceType.ElectrifiedWater => config.ElectricDuration,
                SurfaceType.Steam => config.SteamDuration,
                _ => 0f,
            };
            return new Surface { Type = type, Lifetime = lifetime, PreviousType = type, ChangedAt = -1e6f }; // "давно" — без анимации перехода
        }

        // Переход клетки в новое состояние с запоминанием, из какого типа и когда она перешла (для плавной смены вида).
        // Если тип не поменялся (например, огонь просто "подпитали"), текущий визуальный переход не сбрасывается.
        public static Surface ChangeTo(in Surface current, Surface next, float now)
        {
            if (next.Type != current.Type)
            {
                next.PreviousType = current.Type;
                next.ChangedAt = now;
            }
            else
            {
                next.PreviousType = current.PreviousType;
                next.ChangedAt = current.ChangedAt;
            }
            return next;
        }

        // Что происходит, когда игрок "кастует" поверхность на клетку.
        // Возвращает false, если кисть на этой клетке ничего не меняет.
        public static bool TryApplyBrush(SurfaceType brush, in Surface current, in SurfaceGridConfig config, float now, out Surface result)
        {
            result = current;
            switch (brush)
            {
                case SurfaceType.ElectrifiedWater:
                    // Электричество действует только на воду.
                    if (current.Type != SurfaceType.Water)
                        return false;
                    break;
                case SurfaceType.Fire:
                    // Огонь на воде — пар.
                    if (current.Type == SurfaceType.Water || current.Type == SurfaceType.ElectrifiedWater)
                        brush = SurfaceType.Steam;
                    break;
            }

            if (current.Type == brush && !IsTemporary(brush))
                return false;

            result = ChangeTo(current, Make(brush, config), now);
            return true;
        }

        // Внешний вид типа поверхности. Из этих данных SurfaceFloorRenderSystem собирает палитру для шейдера пола.
        // Цвета в линейном пространстве. Позже здесь появятся ссылки на текстуры вместо цветов.
        public static SurfaceLook Look(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.Water:
                    return new SurfaceLook(new float3(0.08f, 0.3f, 0.75f), smoothness: 0.9f);
                case SurfaceType.Oil:
                    return new SurfaceLook(new float3(0.1f, 0.08f, 0.04f), smoothness: 0.8f);
                case SurfaceType.Poison:
                    return new SurfaceLook(new float3(0.3f, 0.7f, 0.1f), smoothness: 0.6f);
                case SurfaceType.Steam:
                    return new SurfaceLook(new float3(0.8f, 0.8f, 0.82f), smoothness: 0.1f);
                case SurfaceType.Fire:
                    return new SurfaceLook(new float3(1f, 0.25f, 0.02f), smoothness: 0.2f,
                        flickerColor: new float3(1f, 0.8f, 0.1f), flickerSpeed: 12f, emission: new float3(1f, 0.35f, 0.05f) * 2f);
                case SurfaceType.ElectrifiedWater:
                    return new SurfaceLook(new float3(0.3f, 0.6f, 1f), smoothness: 0.9f,
                        flickerColor: new float3(0.95f, 0.97f, 1f), flickerSpeed: 40f, emission: new float3(0.4f, 0.7f, 1f) * 1.5f);
                default:
                    return new SurfaceLook(new float3(0.22f, 0.19f, 0.15f), smoothness: 0.15f); // земля
            }
        }
    }

    public struct SurfaceLook
    {
        public float3 Color;
        public float Smoothness;
        public float3 FlickerColor; // второй цвет, между которым "мерцает" поверхность
        public float FlickerSpeed;  // 0 — не мерцает
        public float3 Emission;     // свечение (огонь, искры)

        public SurfaceLook(float3 color, float smoothness, float3 flickerColor = default, float flickerSpeed = 0f, float3 emission = default)
        {
            Color = color;
            Smoothness = smoothness;
            FlickerColor = flickerSpeed > 0f ? flickerColor : color;
            FlickerSpeed = flickerSpeed;
            Emission = emission;
        }
    }
}
