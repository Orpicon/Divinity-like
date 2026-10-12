using System;
using UnityEngine;
using UnityEngine.UI;

namespace Divinity.Surfaces.UI
{
    [RequireComponent(typeof(Button))]
    public class SurfaceBrushButton : MonoBehaviour
    {
        [SerializeField] SurfaceType _type;
        [SerializeField] Image _swatch;
        [SerializeField] GameObject _selection;

        Button _button;

        public event Action<SurfaceType> Clicked;

        public SurfaceType Type => _type;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
            _swatch.color = SwatchColor(_type);
        }

        void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
        }

        public void SetSelected(bool selected)
        {
            _selection.SetActive(selected);
        }

        void OnClick()
        {
            Clicked?.Invoke(_type);
        }

        static Color SwatchColor(SurfaceType type)
        {
            if (type == SurfaceType.None)
                return Color.white;

            var look = SurfaceRules.Look(type);
            var color = new Color(look.Color.x, look.Color.y, look.Color.z).gamma;
            color.a = 1f;
            return color;
        }
    }
}
