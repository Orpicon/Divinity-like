using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces
{
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

    public struct SurfaceGridConfig : IComponentData
    {
        public int2 Size;
        public float CellSize;
        public float3 Origin;
        public float TickInterval;
        public float FireDuration;
        public float ElectricDuration;
        public float SteamDuration;
        public uint Seed;

        public float BlendWidth;
        public float EdgeNoise;
        public float TransitionDuration;
    }

    public struct SurfaceSimulationTimer : IComponentData
    {
        public float TimeUntilTick;
    }

    public struct SurfaceCell : IComponentData
    {
        public int Index;
        public int2 Coord;
    }

    public struct Surface : IComponentData
    {
        public SurfaceType Type;
        public float Lifetime;

        public SurfaceType PreviousType;
        public float ChangedAt;
    }

    public struct SurfaceCellRef : IBufferElementData
    {
        public Entity Value;
    }
}
