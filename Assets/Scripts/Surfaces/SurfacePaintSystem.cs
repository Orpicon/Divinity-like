using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Divinity.Surfaces
{
    // Ввод игрока: ЛКМ "разливает" выбранную поверхность в точке пола под курсором.
    // SystemBase (managed-система, class) — потому что здесь нужны managed-API: Camera.main и Input System.
    // Burst здесь не используется, да и не нужен: работы на кадр совсем мало.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SurfaceSimulationSystem))]
    public partial class SurfacePaintSystem : SystemBase
    {
        static readonly int BrushId = Shader.PropertyToID("_SurfaceBrush");
        static readonly int BrushColorId = Shader.PropertyToID("_SurfaceBrushColor");

        public SurfaceType Brush { get; private set; } = SurfaceType.Fire;
        // Радиус кисти в метрах — не зависит от дискретности сетки.
        public float BrushRadius { get; private set; } = 1.5f;
        const float BrushRadiusStep = 0.5f;
        const float MinBrushRadius = 0.5f;
        const float MaxBrushRadius = 10f;

        // Диагностика для HUD: что система видит прямо сейчас.
        public string Status { get; private set; } = "";

        // RequireForUpdate здесь намеренно нет: клавиатура и диагностика должны работать,
        // даже если сетка ещё не загрузилась. Наличие данных проверяем вручную в OnUpdate.

        protected override void OnDestroy()
        {
            HideBrush();
        }

        protected override void OnUpdate()
        {
            HandleKeyboard();

            if (!SystemAPI.TryGetSingleton(out SurfaceGridConfig config))
            {
                SetStatus("Сетка не найдена: SubScene с SurfaceGrid не загружена", false);
                return;
            }
            if (!SystemAPI.HasSingleton<SurfaceCellRef>())
            {
                SetStatus("Сетка найдена, но клетки ещё не созданы", false);
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                SetStatus("Нет камеры с тегом MainCamera", false);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                SetStatus("Мышь не найдена (Input System)", false);
                return;
            }

            // Пересекаем луч из камеры с плоскостью сетки — физика для этого не нужна.
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, (Vector3)config.Origin);
            if (!plane.Raycast(ray, out float distance))
            {
                SetStatus("Курсор не над полом", false);
                return;
            }

            float3 hit = ray.GetPoint(distance);
            int2 center = (int2)math.floor((hit.xz - config.Origin.xz) / config.CellSize);
            bool insideGrid = math.all(center >= 0) && math.all(center < config.Size);

            ShowBrush(hit);
            SetStatus(insideGrid ? $"Клетка под курсором: {center.x}, {center.y} (сетка {config.Size.x}×{config.Size.y})" : "Курсор вне сетки", true);

            if (mouse.leftButton.isPressed)
                Paint(hit.xz, config);
        }

        // Закрашиваем все клетки, центр которых попадает в круг кисти.
        void Paint(float2 point, in SurfaceGridConfig config)
        {
            var cells = SystemAPI.GetSingletonBuffer<SurfaceCellRef>(isReadOnly: true);
            float now = (float)SystemAPI.Time.ElapsedTime;

            // Прямоугольник клеток, который покрывает круг кисти, с обрезкой по границам сетки.
            float2 local = (point - config.Origin.xz) / config.CellSize;
            float radiusInCells = BrushRadius / config.CellSize;
            int2 min = math.max((int2)math.floor(local - radiusInCells), 0);
            int2 max = math.min((int2)math.ceil(local + radiusInCells), config.Size - 1);

            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                float2 cellCenter = new float2(x + 0.5f, y + 0.5f);
                if (math.distancesq(cellCenter, local) > radiusInCells * radiusInCells)
                    continue;

                var entity = cells[y * config.Size.x + x].Value;

                // EntityManager.Get/SetComponentData на главном потоке сами дожидаются джобов,
                // которые пишут в Surface (синхронизация) — поэтому это безопасно, но не бесплатно.
                // Для массовых изменений лучше собирать команды и применять их джобом.
                var current = EntityManager.GetComponentData<Surface>(entity);
                if (SurfaceRules.TryApplyBrush(Brush, current, config, now, out var result))
                    EntityManager.SetComponentData(entity, result);
            }
        }

        // Кольцо кисти рисует шейдер пола: передаём ему центр, радиус в метрах и цвет.
        void ShowBrush(float3 center)
        {
            var look = SurfaceRules.Look(Brush);
            // У "стирания" цвет земли почти не виден на полу — рисуем кольцо белым.
            var color = Brush == SurfaceType.None ? Color.white : new Color(look.Color.x, look.Color.y, look.Color.z);
            Shader.SetGlobalVector(BrushId, new Vector4(center.x, center.z, BrushRadius, 1f));
            Shader.SetGlobalColor(BrushColorId, color);
        }

        static void HideBrush()
        {
            Shader.SetGlobalVector(BrushId, Vector4.zero);
        }

        void SetStatus(string status, bool brushVisible)
        {
            Status = status;
            if (!brushVisible)
                HideBrush();
        }

        void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame) Brush = SurfaceType.Water;
            if (keyboard.digit2Key.wasPressedThisFrame) Brush = SurfaceType.Oil;
            if (keyboard.digit3Key.wasPressedThisFrame) Brush = SurfaceType.Poison;
            if (keyboard.digit4Key.wasPressedThisFrame) Brush = SurfaceType.Fire;
            if (keyboard.digit5Key.wasPressedThisFrame) Brush = SurfaceType.ElectrifiedWater;
            if (keyboard.digit0Key.wasPressedThisFrame) Brush = SurfaceType.None;

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                BrushRadius = math.min(BrushRadius + BrushRadiusStep, MaxBrushRadius);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                BrushRadius = math.max(BrushRadius - BrushRadiusStep, MinBrushRadius);
        }
    }
}
