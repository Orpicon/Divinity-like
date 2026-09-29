using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces
{
    // Тип поверхности на клетке. byte — чтобы компонент был компактным (данные в чанках плотнее).
    public enum SurfaceType : byte
    {
        None,
        Water,
        Oil,
        Poison,
        Fire,
        ElectrifiedWater,
        Steam,
    }

    // Настройки сетки. Singleton: существует ровно на одной сущности, создаётся бейкером из SurfaceGridAuthoring.
    public struct SurfaceGridConfig : IComponentData
    {
        public int2 Size;       // число клеток
        public float CellSize;  // размер клетки в метрах
        public float3 Origin;
        public float TickInterval; // = CellSize / скорость распространения: чем мельче сетка, тем чаще тик
        public float FireDuration;
        public float ElectricDuration;
        public float SteamDuration;
        public uint Seed;

        // Внешний вид пола (читает SurfaceFloorRenderSystem и передаёт в шейдер).
        public float BlendWidth; // 0..1 — ширина плавного перехода между клетками
        public float EdgeNoise;  // насколько "рваные" края луж
        public float TransitionDuration; // за сколько секунд клетка визуально "перетекает" в новый тип
    }

    // Изменяемое состояние симуляции держим отдельно от конфига: конфиг только читается, таймер пишется.
    public struct SurfaceSimulationTimer : IComponentData
    {
        public float TimeUntilTick;
    }

    // Статичные данные клетки: где она на сетке.
    public struct SurfaceCell : IComponentData
    {
        public int Index; // y * Size.x + x
        public int2 Coord;
    }

    // Текущая поверхность на клетке.
    public struct Surface : IComponentData
    {
        public SurfaceType Type;
        public float Lifetime; // для временных поверхностей: огонь, электричество, пар

        // Для плавной смены вида: из какого типа клетка перешла и когда (время мира, в секундах).
        // Шейдер смешивает PreviousType -> Type в течение TransitionDuration после ChangedAt.
        public SurfaceType PreviousType;
        public float ChangedAt;
    }

    // Таблица "индекс клетки -> сущность". DynamicBuffer на сущности сетки, заполняется при спавне.
    // Нужна, чтобы по координате (клик мышью) быстро найти сущность клетки.
    public struct SurfaceCellRef : IBufferElementData
    {
        public Entity Value;
    }
}
