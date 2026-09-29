using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Divinity.Surfaces
{
    // Мост между ECS-данными и шейдером пола.
    //
    // Пол — один обычный Plane с материалом SurfaceFloor. Сетку он не "знает": шейдер для каждого пикселя
    // сам вычисляет, в какую клетку тот попадает, и смешивает внешний вид 4 ближайших клеток.
    // Эта система только передаёт шейдеру данные через глобальные свойства:
    //   _SurfaceTypeMap — текстура, где один тексель = одна клетка: (тип, прошлый тип, время смены типа);
    //   _SurfacePalette — как выглядит каждый тип (цвет, мерцание, свечение);
    //   _SurfaceGridParams / _SurfaceGridSize — где сетка лежит в мире, её размер и настройки вида;
//   _SurfaceTime — время мира, от которого шейдер отсчитывает плавные переходы.
    //
    // SystemBase, потому что работаем с managed-объектами (Texture2D, Shader).
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SurfaceFloorRenderSystem : SystemBase
    {
        static readonly int TypeMapId = Shader.PropertyToID("_SurfaceTypeMap");
        static readonly int PaletteId = Shader.PropertyToID("_SurfacePalette");
        static readonly int GridParamsId = Shader.PropertyToID("_SurfaceGridParams");
        static readonly int GridSizeId = Shader.PropertyToID("_SurfaceGridSize");
        static readonly int TimeId = Shader.PropertyToID("_SurfaceTime");

        Texture2D _typeMap;
        Texture2D _palette;
        EntityQuery _changedCells;

        protected override void OnCreate()
        {
            RequireForUpdate<SurfaceGridConfig>();
            RequireForUpdate<SurfaceCellRef>();

            // Запрос с фильтром изменений: он "не пустой", только если в каком-то чанке кто-то
            // получил Surface на запись (ref) с прошлого запуска этой системы.
            _changedCells = SystemAPI.QueryBuilder().WithAll<SurfaceCell, Surface>().Build();
            _changedCells.SetChangedVersionFilter(ComponentType.ReadOnly<Surface>());

            _palette = CreatePalette();
            Shader.SetGlobalTexture(PaletteId, _palette);
        }

        protected override void OnDestroy()
        {
            if (_typeMap != null) Object.Destroy(_typeMap);
            if (_palette != null) Object.Destroy(_palette);
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<SurfaceGridConfig>();

            Shader.SetGlobalVector(GridParamsId, new Vector4(config.Origin.x, config.Origin.z, config.CellSize, config.BlendWidth));
            Shader.SetGlobalVector(GridSizeId, new Vector4(config.Size.x, config.Size.y, config.EdgeNoise, config.TransitionDuration));
            Shader.SetGlobalFloat(TimeId, (float)SystemAPI.Time.ElapsedTime);

            bool recreated = EnsureTypeMap(config);

            // Ничего не поменялось — текстуру не трогаем (фильтр изменений работает на уровне чанков).
            if (!recreated && _changedCells.IsEmpty)
                return;

            // GetPixelData даёт NativeArray, который смотрит прямо в память текстуры на CPU —
            // Burst-джоб пишет туда без копирования, а Apply() отправляет результат на GPU.
            new WriteTypeMapJob { Pixels = _typeMap.GetPixelData<float4>(0) }.ScheduleParallel();

            // Apply() можно вызвать только на главном потоке и только когда джоб закончил писать.
            CompleteDependency();
            _typeMap.Apply(updateMipmaps: false);
        }

        bool EnsureTypeMap(in SurfaceGridConfig config)
        {
            if (_typeMap != null && _typeMap.width == config.Size.x && _typeMap.height == config.Size.y)
                return false;

            if (_typeMap != null) Object.Destroy(_typeMap);
            // RGBAFloat: (тип, прошлый тип, время смены, 0) на клетку. Float, потому что время растёт и половинной точности
            // ему не хватит. linear — это данные, а не цвет. Без мипмапов: шейдер читает тексели через Load.
            _typeMap = new Texture2D(config.Size.x, config.Size.y, TextureFormat.RGBAFloat, mipChain: false, linear: true)
            {
                name = "SurfaceTypeMap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            Shader.SetGlobalTexture(TypeMapId, _typeMap);
            return true;
        }

        static Texture2D CreatePalette()
        {
            int count = System.Enum.GetValues(typeof(SurfaceType)).Length;
            var palette = new Texture2D(count, 3, TextureFormat.RGBAHalf, mipChain: false, linear: true)
            {
                name = "SurfacePalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (int i = 0; i < count; i++)
            {
                var look = SurfaceRules.Look((SurfaceType)i);
                palette.SetPixel(i, 0, new Color(look.Color.x, look.Color.y, look.Color.z, look.Smoothness));
                palette.SetPixel(i, 1, new Color(look.FlickerColor.x, look.FlickerColor.y, look.FlickerColor.z, look.FlickerSpeed));
                palette.SetPixel(i, 2, new Color(look.Emission.x, look.Emission.y, look.Emission.z, 0f));
            }
            palette.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return palette;
        }

        [BurstCompile]
        partial struct WriteTypeMapJob : IJobEntity
        {
            // Пишем по cell.Index (строка за строкой, как и тексели текстуры), индексы уникальны.
            [NativeDisableParallelForRestriction] public NativeArray<float4> Pixels;

            void Execute(in SurfaceCell cell, in Surface surface)
            {
                Pixels[cell.Index] = new float4((float)surface.Type, (float)surface.PreviousType, surface.ChangedAt, 0f);
            }
        }
    }
}
