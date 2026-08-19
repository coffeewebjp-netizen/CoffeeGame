using UnityEngine;

namespace CoffeeGame.UI
{
    [DisallowMultipleComponent]
    public sealed class SafeAreaRectTransform : MonoBehaviour
    {

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        private void LateUpdate()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            rectTransform.anchorMin = new Vector2(safe.xMin / width, safe.yMin / height);
            rectTransform.anchorMax = new Vector2(safe.xMax / width, safe.yMax / height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    
    }
}
