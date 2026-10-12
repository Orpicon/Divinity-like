using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SurfaceSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceGridConfig>();
            state.RequireForUpdate<SurfaceCellRef>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SurfaceGridConfig>();
            ref var timer = ref SystemAPI.GetSingletonRW<SurfaceSimulationTimer>().ValueRW;

            timer.TimeUntilTick -= SystemAPI.Time.DeltaTime;

            const int MaxStepsPerFrame = 4;
            int steps = 0;
            while (timer.TimeUntilTick <= 0f && steps < MaxStepsPerFrame)
            {
                timer.TimeUntilTick += config.TickInterval;
                steps++;
            }
            if (timer.TimeUntilTick <= 0f)
                timer.TimeUntilTick = config.TickInterval;
            if (steps == 0)
                return;

            var snapshot = new NativeArray<CellSnapshot>(config.Size.x * config.Size.y, Allocator.TempJob);
            float now = (float)SystemAPI.Time.ElapsedTime;
            var handle = state.Dependency;

            for (int i = 0; i < steps; i++)
            {
                handle = new SnapshotJob
                {
                    Snapshot = snapshot,
                    FreshElectricThreshold = config.ElectricDuration - config.TickInterval * 0.5f,
                }.ScheduleParallel(handle);

                handle = new StepJob
                {
                    Snapshot = snapshot,
                    Config = config,
                    Now = now,
                }.ScheduleParallel(handle);
            }

            state.Dependency = snapshot.Dispose(handle);
        }

        struct CellSnapshot
        {
            public SurfaceType Type;
            public bool SpreadsElectricity;
        }

        [BurstCompile]
        partial struct SnapshotJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public NativeArray<CellSnapshot> Snapshot;
            public float FreshElectricThreshold;

            void Execute(in SurfaceCell cell, in Surface surface)
            {
                Snapshot[cell.Index] = new CellSnapshot
                {
                    Type = surface.Type,
                    SpreadsElectricity = surface.Type == SurfaceType.ElectrifiedWater && surface.Lifetime >= FreshElectricThreshold,
                };
            }
        }

        [BurstCompile]
        partial struct StepJob : IJobEntity
        {
            [ReadOnly] public NativeArray<CellSnapshot> Snapshot;
            public SurfaceGridConfig Config;
            public float Now;

            void Execute(in SurfaceCell cell, ref Surface surface)
            {
                GetNeighbours(cell.Coord, out bool fireNear, out bool electricityNear);

                switch (surface.Type)
                {
                    case SurfaceType.Oil:
                    case SurfaceType.Poison:
                        if (fireNear)
                            surface = SurfaceRules.ChangeTo(surface, SurfaceRules.Make(SurfaceType.Fire, Config), Now);
                        return;

                    case SurfaceType.Water:
                        if (fireNear)
                            surface = SurfaceRules.ChangeTo(surface, SurfaceRules.Make(SurfaceType.Steam, Config), Now);
                        else if (electricityNear)
                            surface = SurfaceRules.ChangeTo(surface, SurfaceRules.Make(SurfaceType.ElectrifiedWater, Config), Now);
                        return;

                    case SurfaceType.Fire:
                    case SurfaceType.ElectrifiedWater:
                    case SurfaceType.Steam:
                        surface.Lifetime -= Config.TickInterval;
                        if (surface.Lifetime <= 0f)
                            surface = SurfaceRules.ChangeTo(surface, SurfaceRules.Make(SurfaceRules.AfterExpire(surface.Type), Config), Now);
                        return;
                }
            }

            void GetNeighbours(int2 coord, out bool fireNear, out bool electricityNear)
            {
                fireNear = false;
                electricityNear = false;
                Check(coord + new int2(1, 0), ref fireNear, ref electricityNear);
                Check(coord + new int2(-1, 0), ref fireNear, ref electricityNear);
                Check(coord + new int2(0, 1), ref fireNear, ref electricityNear);
                Check(coord + new int2(0, -1), ref fireNear, ref electricityNear);
            }

            void Check(int2 c, ref bool fireNear, ref bool electricityNear)
            {
                if (c.x < 0 || c.y < 0 || c.x >= Config.Size.x || c.y >= Config.Size.y)
                    return;
                var n = Snapshot[c.y * Config.Size.x + c.x];
                fireNear |= n.Type == SurfaceType.Fire;
                electricityNear |= n.SpreadsElectricity;
            }
        }
    }
}
