using System.IO;
using Divinity.Surfaces.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Divinity.Surfaces.Editor
{
    public static class SurfaceHudBuilder
    {
        const string PrefabFolder = "Assets/Surfaces/UI";
        const string PrefabPath = PrefabFolder + "/SurfaceHud.prefab";
        const int UiLayer = 5;

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.1f);
        static readonly Color SelectionColor = new Color(1f, 1f, 1f, 0.3f);
        static readonly Color MutedTextColor = new Color(1f, 1f, 1f, 0.6f);

        static readonly (SurfaceType Type, string Label, string Hotkey)[] Brushes =
        {
            (SurfaceType.Water, "Вода", "1"),
            (SurfaceType.Oil, "Масло", "2"),
            (SurfaceType.Poison, "Яд", "3"),
            (SurfaceType.Fire, "Огонь", "4"),
            (SurfaceType.ElectrifiedWater, "Электричество", "5"),
            (SurfaceType.None, "Стереть", "0"),
        };

        [MenuItem("Tools/Divinity/Rebuild Surface HUD Prefab")]
        public static GameObject BuildPrefab()
        {
            var resources = new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            };
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("SurfaceHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SurfaceHud));
            root.layer = UiLayer;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = CreateRect("SafeArea", root.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeArea>();

            var panel = CreateBrushPanel(safeArea, resources);
            CreateText("Title", panel, font, "Поверхность", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);

            var buttons = new SurfaceBrushButton[Brushes.Length];
            for (int i = 0; i < Brushes.Length; i++)
                buttons[i] = CreateBrushButton(panel, resources, font, Brushes[i].Type, Brushes[i].Label, Brushes[i].Hotkey);

            var radiusLabel = CreateText("RadiusLabel", panel, font, "Радиус", 24, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            var slider = CreateSlider(panel, resources);
            CreateText("Hint", panel, font, "ЛКМ — рисовать, + / − — радиус", 20, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleLeft);

            var statusLabel = CreateStatusBar(safeArea, resources, font);

            var hud = new SerializedObject(root.GetComponent<SurfaceHud>());
            var buttonsProperty = hud.FindProperty("_brushButtons");
            buttonsProperty.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                buttonsProperty.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            hud.FindProperty("_radiusSlider").objectReferenceValue = slider;
            hud.FindProperty("_radiusLabel").objectReferenceValue = radiusLabel;
            hud.FindProperty("_statusLabel").objectReferenceValue = statusLabel;
            hud.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        [MenuItem("Tools/Divinity/Add Surface HUD To Scene")]
        public static void AddToScene()
        {
            if (Object.FindAnyObjectByType<SurfaceHud>() == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                    prefab = BuildPrefab();

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add Surface HUD");
            }

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Add Surface HUD");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static RectTransform CreateBrushPanel(RectTransform parent, DefaultControls.Resources resources)
        {
            var panel = CreateRect("BrushPanel", parent);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(24f, -24f);
            panel.sizeDelta = new Vector2(380f, 0f);

            AddImage(panel, resources.background, PanelColor, true);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        static SurfaceBrushButton CreateBrushButton(RectTransform parent, DefaultControls.Resources resources, Font font, SurfaceType type, string label, string hotkey)
        {
            var rect = CreateRect("Brush" + type, parent);
            var background = AddImage(rect, resources.standard, ButtonColor, true);
            rect.gameObject.AddComponent<Button>().targetGraphic = background;
            SetHeight(rect, 56f);

            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var selection = CreateRect("Selection", rect);
            Stretch(selection);
            selection.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            AddImage(selection, resources.standard, SelectionColor, false);
            selection.gameObject.SetActive(false);

            var swatch = CreateRect("Swatch", rect);
            var swatchImage = AddImage(swatch, resources.knob, Color.white, false);
            var swatchSize = swatch.gameObject.AddComponent<LayoutElement>();
            swatchSize.minWidth = swatchSize.preferredWidth = 32f;
            swatchSize.minHeight = swatchSize.preferredHeight = 32f;

            var labelText = CreateText("Label", rect, font, label, 24, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            labelText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateText("Hotkey", rect, font, hotkey, 20, FontStyle.Normal, MutedTextColor, TextAnchor.MiddleRight);

            var button = rect.gameObject.AddComponent<SurfaceBrushButton>();
            var serialized = new SerializedObject(button);
            serialized.FindProperty("_type").intValue = (int)type;
            serialized.FindProperty("_swatch").objectReferenceValue = swatchImage;
            serialized.FindProperty("_selection").objectReferenceValue = selection.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        static Slider CreateSlider(RectTransform parent, DefaultControls.Resources resources)
        {
            var sliderObject = DefaultControls.CreateSlider(resources);
            sliderObject.name = "RadiusSlider";
            sliderObject.transform.SetParent(parent, false);
            foreach (var child in sliderObject.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = UiLayer;

            SetHeight((RectTransform)sliderObject.transform, 32f);
            return sliderObject.GetComponent<Slider>();
        }

        static Text CreateStatusBar(RectTransform parent, DefaultControls.Resources resources, Font font)
        {
            var bar = CreateRect("StatusBar", parent);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 24f);
            bar.sizeDelta = new Vector2(-48f, 48f);
            AddImage(bar, resources.background, PanelColor, false);

            var label = CreateText("StatusLabel", bar, font, "", 22, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            var labelRect = (RectTransform)label.transform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(16f, 0f);
            labelRect.offsetMax = new Vector2(-16f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = UiLayer;
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool raycastTarget)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        static Text CreateText(string name, RectTransform parent, Font font, string content, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        static void SetHeight(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
