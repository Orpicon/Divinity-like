using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Divinity.Surfaces.UI
{
    public class SurfaceHud : MonoBehaviour
    {
        [SerializeField] SurfaceBrushButton[] _brushButtons;
        [SerializeField] Slider _radiusSlider;
        [SerializeField] Text _radiusLabel;
        [SerializeField] Text _statusLabel;

        SurfacePaintSystem _paint;
        SurfaceType? _shownBrush;
        float _shownRadius = -1f;
        string _shownStatus;

        void Awake()
        {
            _radiusSlider.minValue = SurfacePaintSystem.MinBrushRadius;
            _radiusSlider.maxValue = SurfacePaintSystem.MaxBrushRadius;
            _radiusSlider.onValueChanged.AddListener(OnRadiusChanged);

            foreach (var button in _brushButtons)
                button.Clicked += OnBrushClicked;
        }

        void OnDestroy()
        {
            _radiusSlider.onValueChanged.RemoveListener(OnRadiusChanged);

            foreach (var button in _brushButtons)
                button.Clicked -= OnBrushClicked;
        }

        void Update()
        {
            if (!TryGetPaintSystem())
                return;

            ShowBrush(_paint.Brush);
            ShowRadius(_paint.BrushRadius);
            ShowStatus(_paint.Status);
        }

        bool TryGetPaintSystem()
        {
            if (_paint != null)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            _paint = world.GetExistingSystemManaged<SurfacePaintSystem>();
            return _paint != null;
        }

        void ShowBrush(SurfaceType brush)
        {
            if (_shownBrush == brush)
                return;

            _shownBrush = brush;
            foreach (var button in _brushButtons)
                button.SetSelected(button.Type == brush);
        }

        void ShowRadius(float radius)
        {
            if (Mathf.Approximately(_shownRadius, radius))
                return;

            _shownRadius = radius;
            _radiusSlider.SetValueWithoutNotify(radius);
            _radiusLabel.text = $"Радиус: {radius:0.#} м";
        }

        void ShowStatus(string status)
        {
            if (_shownStatus == status)
                return;

            _shownStatus = status;
            _statusLabel.text = status;
        }

        void OnBrushClicked(SurfaceType brush)
        {
            if (TryGetPaintSystem())
                _paint.SetBrush(brush);
        }

        void OnRadiusChanged(float radius)
        {
            if (!TryGetPaintSystem())
                return;

            _paint.SetBrushRadius(radius);
            _shownRadius = -1f;
        }
    }
}
