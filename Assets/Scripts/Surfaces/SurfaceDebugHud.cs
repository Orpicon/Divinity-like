using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Divinity.Surfaces
{
    // Пример гибридного подхода: обычный MonoBehaviour в основной сцене (НЕ в SubScene)
    // читает состояние ECS-системы и рисует подсказку поверх экрана.
    public class SurfaceDebugHud : MonoBehaviour
    {
        Dictionary<SurfaceType, Color> surfaceTypeColor = new Dictionary<SurfaceType, Color>()
        {
            { SurfaceType.None, Color.white },
            { SurfaceType.Water, Color.blue },
            { SurfaceType.Oil, Color.brown },
            { SurfaceType.Poison, Color.green },
            { SurfaceType.Fire, Color.orange },
            { SurfaceType.ElectrifiedWater, Color.deepSkyBlue },
            { SurfaceType.Steam, Color.grey }
        };
        void OnGUI()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var paint = world?.GetExistingSystemManaged<SurfacePaintSystem>();
            if (paint == null)
                return;
            
            float margin = Mathf.Min(Screen.width, Screen.height) * 0.01f;
            var box = new Rect(margin, margin, Screen.width / 5f, Screen.height / 3f);
            float padding = box.height * 0.05f;
            var content = new Rect(box.x + padding, box.y + padding, box.width - padding * 2f, box.height - padding * 2f);

            _labelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };

            string brushColor = ColorUtility.ToHtmlStringRGB(surfaceTypeColor[paint.Brush]);
            var text = new GUIContent(
                $"Кисть: <color=#{brushColor}>{paint.Brush}</color>\n" +
                $"Радиус: {paint.BrushRadius:0.#} м\n\n" +
                "ЛКМ — рисовать\n" +
                "· 1 вода\n" +
                "· 2 масло\n" +
                "· 3 яд\n" +
                "· 4 огонь\n" +
                "· 5 электричество (только на воду)\n" +
                "· 0 стереть\n" +
                "+ / − — радиус кисти\n\n" +
                paint.Status);
            _labelStyle.fontSize = FitFontSize(_labelStyle, text, content);

            GUI.Box(box, GUIContent.none);
            GUI.Label(content, text, _labelStyle);
        }

        GUIStyle _labelStyle;

        // Подбираем максимальный размер шрифта, при котором текст (с переносом строк) влезает в прямоугольник.
        static int FitFontSize(GUIStyle style, GUIContent text, Rect rect)
        {
            const int minSize = 1;
            int low = minSize, high = Mathf.Max(minSize, (int)rect.height);
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                style.fontSize = mid;
                if (style.CalcHeight(text, rect.width) <= rect.height)
                    low = mid;
                else
                    high = mid - 1;
            }
            return low;
        }
    }
}
