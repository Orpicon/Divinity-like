using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Divinity.Surfaces
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SurfaceSimulationSystem))]
    public partial class SurfacePaintSystem : SystemBase
    {
        public const float BrushRadiusStep = 0.5f;
        public const float MinBrushRadius = 0.5f;
        public const float MaxBrushRadius = 10f;

        static readonly int BrushId = Shader.PropertyToID("_SurfaceBrush");
        static readonly int BrushColorId = Shader.PropertyToID("_SurfaceBrushColor");

        public SurfaceType Brush { get; private set; } = SurfaceType.Fire;
        public float BrushRadius { get; private set; } = 1.5f;
        public string Status { get; private set; } = "";

        public void SetBrush(SurfaceType brush)
        {
            Brush = brush;
        }

        public void SetBrushRadius(float radius)
        {
            float snapped = math.round(radius / BrushRadiusStep) * BrushRadiusStep;
            BrushRadius = math.clamp(snapped, MinBrushRadius, MaxBrushRadius);
        }

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

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                SetStatus("Курсор над интерфейсом", false);
                return;
            }

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

        void Paint(float2 point, in SurfaceGridConfig config)
        {
            var cells = SystemAPI.GetSingletonBuffer<SurfaceCellRef>(isReadOnly: true);
            float now = (float)SystemAPI.Time.ElapsedTime;

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
                var current = EntityManager.GetComponentData<Surface>(entity);
                if (SurfaceRules.TryApplyBrush(Brush, current, config, now, out var result))
                    EntityManager.SetComponentData(entity, result);
            }
        }

        void ShowBrush(float3 center)
        {
            var look = SurfaceRules.Look(Brush);
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

            if (keyboard.digit1Key.wasPressedThisFrame) SetBrush(SurfaceType.Water);
            if (keyboard.digit2Key.wasPressedThisFrame) SetBrush(SurfaceType.Oil);
            if (keyboard.digit3Key.wasPressedThisFrame) SetBrush(SurfaceType.Poison);
            if (keyboard.digit4Key.wasPressedThisFrame) SetBrush(SurfaceType.Fire);
            if (keyboard.digit5Key.wasPressedThisFrame) SetBrush(SurfaceType.ElectrifiedWater);
            if (keyboard.digit0Key.wasPressedThisFrame) SetBrush(SurfaceType.None);

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                SetBrushRadius(BrushRadius + BrushRadiusStep);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                SetBrushRadius(BrushRadius - BrushRadiusStep);
        }
    }
}
