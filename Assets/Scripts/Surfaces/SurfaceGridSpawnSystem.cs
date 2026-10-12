using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SurfaceGridSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceGridConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gridEntity = SystemAPI.GetSingletonEntity<SurfaceGridConfig>();
            var em = state.EntityManager;

            if (em.HasBuffer<SurfaceCellRef>(gridEntity))
                return;

            var config = SystemAPI.GetSingleton<SurfaceGridConfig>();

            var cellTypes = new NativeArray<ComponentType>(2, Allocator.Temp);
            cellTypes[0] = ComponentType.ReadWrite<SurfaceCell>();
            cellTypes[1] = ComponentType.ReadWrite<Surface>();
            var cellArchetype = em.CreateArchetype(cellTypes);

            int count = config.Size.x * config.Size.y;
            var cells = em.CreateEntity(cellArchetype, count, Allocator.Temp);

            var random = Random.CreateFromIndex(config.Seed);
            float2 noiseOffset = random.NextFloat2(0f, 1000f);

            for (int i = 0; i < count; i++)
            {
                int2 coord = new int2(i % config.Size.x, i / config.Size.x);
                em.SetComponentData(cells[i], new SurfaceCell { Index = i, Coord = coord });
                em.SetComponentData(cells[i], SurfaceRules.Make(InitialSurface(((float2)coord + 0.5f) * config.CellSize + noiseOffset), config));
            }

            var buffer = em.AddBuffer<SurfaceCellRef>(gridEntity);
            buffer.ResizeUninitialized(count);
            for (int i = 0; i < count; i++)
                buffer[i] = new SurfaceCellRef { Value = cells[i] };
        }

        static SurfaceType InitialSurface(float2 p)
        {
            if (noise.snoise(p * 0.08f) > 0.45f) return SurfaceType.Water;
            if (noise.snoise(p * 0.1f + 100f) > 0.5f) return SurfaceType.Oil;
            if (noise.snoise(p * 0.12f + 200f) > 0.65f) return SurfaceType.Poison;
            return SurfaceType.None;
        }
    }
}
