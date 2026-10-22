using UnityEngine;

// Keeps a full-screen panel inside the phone's safe area (notch, gesture bar).
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rect;
    Rect lastArea;
    Vector2Int lastSize;

    void OnEnable()
    {
        rect = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (lastArea != Screen.safeArea || lastSize.x != Screen.width || lastSize.y != Screen.height)
            Apply();
    }

    void Apply()
    {
        Rect area = Screen.safeArea;
        lastArea = area;
        lastSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
