using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Divinity.Surfaces
{
    public class SurfaceGridAuthoring : MonoBehaviour
    {
        [Tooltip("Размер области с поверхностями в метрах (X, Z). Угол области — позиция этого объекта")]
        public Vector2 WorldSize = new Vector2(64f, 64f);

        [Range(0.25f, 8f), Tooltip("Дискретность сетки: сколько клеток на один метр. Больше — мельче клетки и точнее края луж")]
        public float CellsPerMeter = 2f;

        [Tooltip("Скорость распространения огня и электричества, метров в секунду. Не зависит от дискретности сетки")]
        public float SpreadSpeed = 8f;

        public float FireDuration = 3f;
        public float ElectricDuration = 1f;
        public float SteamDuration = 4f;
        public uint Seed = 12345;

        [Header("Пол")]
        [Range(0f, 1f), Tooltip("Ширина плавного перехода между клетками: 1 — через всю клетку, 0 — почти резкая граница")]
        public float BlendWidth = 0.8f;

        [Range(0f, 1.5f), Tooltip("Насколько неровные края у луж")]
        public float EdgeNoise = 0.6f;

        [Range(0f, 2f), Tooltip("За сколько секунд клетка плавно перетекает из одного типа поверхности в другой")]
        public float TransitionDuration = 0.35f;

        public float CellSize => 1f / math.max(0.01f, CellsPerMeter);

        public int2 CellCount => math.max(1, (int2)math.round(new float2(WorldSize.x, WorldSize.y) * CellsPerMeter));

        void OnDrawGizmosSelected()
        {
            var size = new Vector3(CellCount.x * CellSize, 0f, CellCount.y * CellSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + size * 0.5f, size);
        }

        class Baker : Baker<SurfaceGridAuthoring>
        {
            public override void Bake(SurfaceGridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                DependsOn(authoring.transform);

                float cellSize = authoring.CellSize;
                float tickInterval = cellSize / math.max(0.1f, authoring.SpreadSpeed);

                AddComponent(entity, new SurfaceGridConfig
                {
                    Size = authoring.CellCount,
                    CellSize = cellSize,
                    Origin = authoring.transform.position,
                    TickInterval = tickInterval,
                    FireDuration = authoring.FireDuration,
                    ElectricDuration = authoring.ElectricDuration,
                    SteamDuration = authoring.SteamDuration,
                    Seed = math.max(1u, authoring.Seed),
                    BlendWidth = authoring.BlendWidth,
                    EdgeNoise = authoring.EdgeNoise,
                    TransitionDuration = authoring.TransitionDuration,
                });
                AddComponent(entity, new SurfaceSimulationTimer { TimeUntilTick = tickInterval });
            }
        }
    }
}
