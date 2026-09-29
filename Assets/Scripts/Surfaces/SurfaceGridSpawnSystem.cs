using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces
{
    // Создаёт сущности клеток один раз, когда SubScene загрузилась и появился SurfaceGridConfig.
    // ISystem + [BurstCompile] — неуправляемая система, весь её код компилируется Burst'ом.
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SurfaceGridSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Система не будет вызывать OnUpdate, пока в мире нет сущности с SurfaceGridConfig
            // (SubScene грузится асинхронно, в первые кадры её ещё нет).
            state.RequireForUpdate<SurfaceGridConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<SurfaceGridConfig>();
            var em = state.EntityManager;

            // Буфер ссылок на клетки добавляется ниже — если он уже есть, сетка создана.
            if (em.HasBuffer<SurfaceCellRef>(gridEntity))
                return;

            var config = SystemAPI.GetSingleton<SurfaceGridConfig>();

            // Клетки — чистые данные: у них нет ни меша, ни трансформа. Рисует их один объект пола через шейдер.
            // Архетип задаём заранее и создаём все сущности пачкой — они лягут в чанки подряд,
            // это быстро и для создания, и для последующей итерации.
            // NativeArray, а не params-массив: managed-массивы в Burst-коде создавать нельзя.
            var cellTypes = new NativeArray<ComponentType>(2, Allocator.Temp);
            cellTypes[0] = ComponentType.ReadWrite<SurfaceCell>();
            cellTypes[1] = ComponentType.ReadWrite<Surface>();
            var cellArchetype = em.CreateArchetype(cellTypes);

            int count = config.Size.x * config.Size.y;
            var cells = em.CreateEntity(cellArchetype, count, Allocator.Temp);

            var random = Random.CreateFromIndex(config.Seed);
            float2 noiseOffset = random.NextFloat2(0f, 1000f);

            // Одноразовая инициализация на главном потоке — для десятков тысяч сущностей это ещё нормально.
            // Для постоянной работы с данными используются джобы (см. SurfaceSimulationSystem).
            for (int i = 0; i < count; i++)
            {
                int2 coord = new int2(i % config.Size.x, i / config.Size.x);
                em.SetComponentData(cells[i], new SurfaceCell { Index = i, Coord = coord });
                em.SetComponentData(cells[i], SurfaceRules.Make(InitialSurface(((float2)coord + 0.5f) * config.CellSize + noiseOffset), config));
            }

            // Структурное изменение (добавление буфера) делаем после всех SetComponentData.
            var buffer = em.AddBuffer<SurfaceCellRef>(gridEntity);
            buffer.ResizeUninitialized(count);
            for (int i = 0; i < count; i++)
                buffer[i] = new SurfaceCellRef { Value = cells[i] };
        }

        // Лужи воды, масла и яда по шуму, чтобы сразу было с чем поиграть.
        // Шум считаем в метрах, чтобы рисунок луж не зависел от дискретности сетки.
        static SurfaceType InitialSurface(float2 p)
        {
            if (noise.snoise(p * 0.08f) > 0.45f) return SurfaceType.Water;
            if (noise.snoise(p * 0.1f + 100f) > 0.5f) return SurfaceType.Oil;
            if (noise.snoise(p * 0.12f + 200f) > 0.65f) return SurfaceType.Poison;
            return SurfaceType.None;
        }
    }
}
