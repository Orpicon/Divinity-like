using UnityEngine;

namespace Divinity.Surfaces.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _screenSize;

        void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _screenSize.x || Screen.height != _screenSize.y)
                Apply();
        }

        void Apply()
        {
            _applied = Screen.safeArea;
            _screenSize = new Vector2Int(Screen.width, Screen.height);
            if (_screenSize.x <= 0 || _screenSize.y <= 0)
                return;

            Vector2 min = _applied.position;
            Vector2 max = _applied.position + _applied.size;
            min.x /= _screenSize.x;
            min.y /= _screenSize.y;
            max.x /= _screenSize.x;
            max.y /= _screenSize.y;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
