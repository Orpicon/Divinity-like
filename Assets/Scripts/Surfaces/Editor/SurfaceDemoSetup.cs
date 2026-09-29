using System.IO;
using UnityEditor;
using UnityEngine;

namespace Divinity.Surfaces.Editor
{
    // Создаёт объекты демо в открытой сцене:
    //   SurfaceGrid     — authoring сетки (данные), его нужно переместить в SubScene;
    //   SurfaceFloor    — Plane с материалом на HLSL-шейдере Divinity/SurfaceFloor, остаётся в основной сцене;
    //   SurfaceDebugHud — подсказка по управлению, остаётся в основной сцене.
    public static class SurfaceDemoSetup
    {
        const string Folder = "Assets/Surfaces";
        const string ShaderPath = Folder + "/Shaders/SurfaceFloor.shader";
        const string MaterialPath = Folder + "/SurfaceFloor.mat";

        [MenuItem("Tools/Divinity/Create Surfaces Demo")]
        public static void CreateDemo()
        {
            var grid = new GameObject("SurfaceGrid");
            var authoring = grid.AddComponent<SurfaceGridAuthoring>();
            grid.transform.position = new Vector3(-authoring.WorldSize.x * 0.5f, 0f, -authoring.WorldSize.y * 0.5f);
            Undo.RegisterCreatedObjectUndo(grid, "Create Surfaces Demo");

            CreateFloor(authoring);

            if (Object.FindAnyObjectByType<SurfaceDebugHud>() == null)
            {
                var hud = new GameObject("SurfaceDebugHud", typeof(SurfaceDebugHud));
                Undo.RegisterCreatedObjectUndo(hud, "Create Surfaces Demo");
            }

            Selection.activeGameObject = grid;
            Debug.Log("Surfaces demo: выделен объект SurfaceGrid. ПКМ по нему в Hierarchy → New Sub Scene → From Selection. " +
                      "SurfaceFloor и SurfaceDebugHud оставьте в основной сцене.");
        }

        // Для сцены, где SurfaceGrid уже есть: создаёт только пол.
        // Если SubScene открыта для редактирования — размер пола возьмётся из SurfaceGridAuthoring,
        // иначе — из значений по умолчанию (сетка 64×64 с центром в начале координат).
        [MenuItem("Tools/Divinity/Create Surface Floor")]
        public static void CreateFloorOnly()
        {
            var authoring = Object.FindAnyObjectByType<SurfaceGridAuthoring>();
            Selection.activeGameObject = CreateFloor(authoring);
        }

        static GameObject CreateFloor(SurfaceGridAuthoring authoring)
        {
            Vector2 size;
            Vector3 origin;
            if (authoring != null)
            {
                size = new Vector2(authoring.CellCount.x, authoring.CellCount.y) * authoring.CellSize;
                origin = authoring.transform.position;
            }
            else
            {
                size = new Vector2(64f, 64f);
                origin = new Vector3(-32f, 0f, -32f);
            }

            // Стандартный Plane — 10×10 единиц с центром в середине, поэтому масштаб = размер / 10.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "SurfaceFloor";
            floor.transform.position = origin + new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
            floor.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateFloorMaterial();
            Undo.RegisterCreatedObjectUndo(floor, "Create Surface Floor");
            return floor;
        }

        static Material GetOrCreateFloorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"Не найден шейдер {ShaderPath}");
                return null;
            }

            Directory.CreateDirectory(Folder);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
