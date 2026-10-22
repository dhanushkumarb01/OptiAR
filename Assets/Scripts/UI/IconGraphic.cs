using System.Collections.Generic;
using UnityEngine;

public enum IconShape
{
    ChevronLeft,
    Camera,
    Check,
    Cross,
    CornerTopLeft,
    CornerTopRight,
    CornerBottomLeft,
    CornerBottomRight
}

// Resolution-independent stroke icons drawn inside the RectTransform.
public class IconGraphic : VectorShapeGraphic
{
    [SerializeField] private IconShape shape;
    [SerializeField, Min(0.5f)] private float strokeWidth = 4f;
    [Tooltip("Corner icons: colour of the short leg, so brackets can be two-toned.")]
    [SerializeField] private Color secondaryColor = Color.white;

    protected override bool IsProcedural => true;

    public IconShape Shape
    {
        get => shape;
        set { shape = value; SetVerticesDirty(); }
    }

    protected override void BuildShapes()
    {
        Rect r = rectTransform.rect;
        Vector2 P(float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);
        Color c = Color.white;

        switch (shape)
        {
            case IconShape.ChevronLeft:
                AddPolyline(new List<Vector2> { P(0.62f, 0.88f), P(0.30f, 0.5f), P(0.62f, 0.12f) }, strokeWidth, c);
                AddDisc(P(0.62f, 0.88f), strokeWidth * 0.5f, c);
                AddDisc(P(0.62f, 0.12f), strokeWidth * 0.5f, c);
                break;

            case IconShape.Check:
                AddPolyline(new List<Vector2> { P(0.12f, 0.52f), P(0.40f, 0.24f), P(0.88f, 0.76f) }, strokeWidth, c);
                AddDisc(P(0.12f, 0.52f), strokeWidth * 0.5f, c);
                AddDisc(P(0.88f, 0.76f), strokeWidth * 0.5f, c);
                break;

            case IconShape.Cross:
                AddLine(P(0.2f, 0.2f), P(0.8f, 0.8f), strokeWidth, c);
                AddLine(P(0.2f, 0.8f), P(0.8f, 0.2f), strokeWidth, c);
                break;

            case IconShape.Camera:
                BuildCamera(P, c);
                break;

            default:
                BuildCorner(r);
                break;
        }
    }

    private void BuildCamera(System.Func<float, float, Vector2> P, Color c)
    {
        // Body outline
        var body = new List<Vector2>
        {
            P(0.08f, 0.18f), P(0.92f, 0.18f), P(0.92f, 0.70f), P(0.70f, 0.70f),
            P(0.62f, 0.84f), P(0.38f, 0.84f), P(0.30f, 0.70f), P(0.08f, 0.70f), P(0.08f, 0.18f)
        };
        AddPolyline(body, strokeWidth, c);
        Rect r = rectTransform.rect;
        float lensRadius = Mathf.Min(r.width, r.height) * 0.17f;
        AddRing(P(0.5f, 0.44f), lensRadius, strokeWidth, c);
    }

    private void BuildCorner(Rect r)
    {
        float half = strokeWidth * 0.5f;
        bool left = shape == IconShape.CornerTopLeft || shape == IconShape.CornerBottomLeft;
        bool top = shape == IconShape.CornerTopLeft || shape == IconShape.CornerTopRight;

        float x = left ? r.xMin + half : r.xMax - half;
        float y = top ? r.yMax - half : r.yMin + half;
        float xEnd = left ? r.xMax : r.xMin;
        float yEnd = top ? r.yMin : r.yMax;

        Vector2 corner = new Vector2(x, y);
        AddLine(corner, new Vector2(xEnd, y), strokeWidth, Color.white);
        AddLine(corner, new Vector2(x, yEnd), strokeWidth, secondaryColor);
        AddDisc(corner, half, Color.white);
    }
}
