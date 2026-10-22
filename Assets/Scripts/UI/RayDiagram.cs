using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Draws the ray diagram for a mirror or lens.
// If interactive, dragging left/right moves the object.
[RequireComponent(typeof(VectorShapeGraphic))]
public class RayDiagram : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("Labels (optional)")]
    public TMP_Text focusLabel;
    public TMP_Text secondFocusLabel;
    public TMP_Text centreLabel;
    public TMP_Text poleLabel;
    public TMP_Text objectLabel;
    public TMP_Text imageLabel;
    public TMP_Text measureLabel;

    [Header("Layout")]
    public bool interactive;
    [Range(0.3f, 0.95f)] public float mirrorPoleX = 0.80f;
    [Range(0.3f, 0.95f)] public float lensCentreX = 0.56f;
    [Range(0.1f, 0.9f)] public float axisHeight = 0.52f;
    [Range(0.05f, 0.4f)] public float objectHeight = 0.17f;
    [Range(0.2f, 1f)] public float elementHeight = 0.62f;
    [Range(0f, 0.3f)] public float sideMargin = 0.06f;
    public float lineWidth = 3.5f;
    public float measureOffset = 0.17f;

    [Header("Colours")]
    [Tooltip("Used for the mirror/lens instead of the topic colour when alpha is above zero.")]
    public Color elementColorOverride = new Color(0f, 0f, 0f, 0f);
    public Color axisColor = new Color(0.25f, 0.32f, 0.43f, 0.9f);
    public Color objectColor = new Color(0.96f, 0.62f, 0.04f);
    public Color imageColor = new Color(0.20f, 0.83f, 0.60f);
    public Color focusColor = new Color(0.98f, 0.80f, 0.08f);
    public Color centreColor = new Color(0.80f, 0.84f, 0.88f);
    public Color parallelRayColor = new Color(0.13f, 0.83f, 0.93f);
    public Color centralRayColor = new Color(0.96f, 0.45f, 0.71f);
    public Color focalRayColor = new Color(0.51f, 0.55f, 0.97f);
    public Color measureColor = new Color(0.39f, 0.45f, 0.55f);

    // called with the new object distance when the user drags
    public System.Action<float> onDrag;

    VectorShapeGraphic shapes;
    TopicData topic;
    float focal;
    float dist;
    OpticsResult result;
    bool showRays, showLabels, showMeasure;
    bool redraw;

    // saved from the last draw, used to turn a drag into a distance
    float poleX;
    float axisY;
    float scale;

    VectorShapeGraphic Shapes => shapes != null ? shapes : shapes = GetComponent<VectorShapeGraphic>();

    public void Draw(TopicData t, float focalLength, float objectDistance, bool rays, bool labels, bool measure)
    {
        topic = t;
        focal = Mathf.Abs(focalLength);
        dist = objectDistance;
        result = Optics.Solve(topic.SignedFocal(focal), dist);
        showRays = rays;
        showLabels = labels;
        showMeasure = measure;
        redraw = false;
        Redraw();
    }

    void OnRectTransformDimensionsChange()
    {
        // can't redraw during a layout rebuild, so wait for LateUpdate
        redraw = topic != null;
    }

    void LateUpdate()
    {
        if (!redraw)
            return;
        redraw = false;
        Redraw();
    }

    // ----- Drawing -----

    void Redraw()
    {
        VectorShapeGraphic g = Shapes;
        g.ClearShapes();
        if (topic == null)
            return;

        Rect r = g.rectTransform.rect;
        if (r.width <= 1f || r.height <= 1f)
            return;

        bool mirror = topic.isMirror;
        poleX = r.xMin + r.width * (mirror ? mirrorPoleX : lensCentreX);
        axisY = r.yMin + r.height * axisHeight;
        scale = (poleX - r.xMin - r.width * sideMargin) / Mathf.Max(1f, topic.maxObjectDistance);

        float h = r.height * objectHeight;
        float half = Mathf.Min(r.height * elementHeight * 0.5f, r.height * 0.48f);
        float signedF = topic.SignedFocal(focal);

        // principal axis
        g.AddDashedLine(new Vector2(r.xMin, axisY), new Vector2(r.xMax, axisY), 2f, axisColor, 10f, 8f);

        if (mirror) DrawMirror(g, half);
        else DrawLens(g, half);

        DrawFocalPoints(g, signedF, mirror);

        if (showRays)
            DrawRays(g, h, half, mirror);

        DrawObject(g, h);
        DrawImage(g, h, r, mirror);

        if (showMeasure)
            DrawMeasure(g, r);

        PlaceLabels(h, half, signedF, mirror, r);
    }

    Vector2 ToLocal(float x, float y, bool afterElement, bool mirror)
    {
        if (afterElement && mirror)
            x = -x;
        return new Vector2(poleX + x, axisY + y);
    }

    // how far the curved mirror surface bends at height y
    float MirrorSurfaceOffset(float y, float half)
    {
        float t = Mathf.Clamp(y / half, -1.2f, 1.2f);
        float bend = topic.converging ? -1f : 1f;
        return bend * half * 0.16f * t * t;
    }

    Color ElementColor => elementColorOverride.a > 0f ? elementColorOverride : topic.accentColor;

    void DrawMirror(VectorShapeGraphic g, float half)
    {
        Color accent = ElementColor;
        var curve = new List<Vector2>();
        const int steps = 28;
        for (int i = 0; i <= steps; i++)
        {
            float y = Mathf.Lerp(-half, half, i / (float)steps);
            curve.Add(new Vector2(poleX + MirrorSurfaceOffset(y, half), axisY + y));
        }

        g.AddPolyline(curve, 14f, WithAlpha(accent, 0.09f));
        g.AddPolyline(curve, 5f, accent);

        // hatching on the back of the mirror
        Color hatch = WithAlpha(accent, 0.45f);
        const int marks = 9;
        for (int i = 0; i <= marks; i++)
        {
            float y = Mathf.Lerp(-half * 0.92f, half * 0.92f, i / (float)marks);
            Vector2 start = new Vector2(poleX + MirrorSurfaceOffset(y, half) + 6f, axisY + y);
            g.AddLine(start, start + new Vector2(12f, -10f), 2f, hatch);
        }
    }

    void DrawLens(VectorShapeGraphic g, float half)
    {
        Color accent = ElementColor;
        var outline = new List<Vector2>();
        const int steps = 24;

        if (topic.converging)
        {
            float halfWidth = half * 0.14f;
            for (int i = 0; i <= steps * 2; i++)
            {
                float angle = i * Mathf.PI / steps;
                outline.Add(new Vector2(poleX + Mathf.Cos(angle) * halfWidth, axisY + Mathf.Sin(angle) * half));
            }
        }
        else
        {
            float edge = half * 0.17f;
            float waist = edge * 0.3f;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(1f, -1f, i / (float)steps);
                outline.Add(new Vector2(poleX - (waist + (edge - waist) * t * t), axisY + half * t));
            }
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(-1f, 1f, i / (float)steps);
                outline.Add(new Vector2(poleX + (waist + (edge - waist) * t * t), axisY + half * t));
            }
            outline.Add(outline[0]);
        }

        g.AddPolygon(outline.GetRange(0, outline.Count - 1), WithAlpha(accent, 0.05f));
        g.AddPolyline(outline, 12f, WithAlpha(accent, 0.08f));
        g.AddPolyline(outline, 4f, accent);
    }

    void DrawFocalPoints(VectorShapeGraphic g, float signedF, bool mirror)
    {
        if (mirror)
        {
            g.AddDisc(new Vector2(poleX - signedF * scale, axisY), 8f, focusColor);
            g.AddRing(new Vector2(poleX - 2f * signedF * scale, axisY), 6f, 2.5f, centreColor);
        }
        else
        {
            float f = Mathf.Abs(signedF) * scale;
            g.AddDisc(new Vector2(poleX - f, axisY), 8f, focusColor);
            g.AddDisc(new Vector2(poleX + f, axisY), 8f, focusColor);
        }
    }

    void DrawObject(VectorShapeGraphic g, float h)
    {
        float x = poleX - dist * scale;
        Vector2 foot = new Vector2(x, axisY);
        Vector2 tip = new Vector2(x, axisY + h);

        // circle around the object shows it can be dragged
        if (interactive)
        {
            Vector2 centre = new Vector2(x, axisY + h * 0.55f);
            float radius = h * 0.72f;
            g.AddDisc(centre, radius, WithAlpha(objectColor, 0.035f));
            g.AddRing(centre, radius, 3f, WithAlpha(objectColor, 0.9f));
        }

        g.AddLine(foot, tip - new Vector2(0f, 10f), 4f, objectColor);
        g.AddArrowHead(tip, Vector2.up, 18f, 10f, objectColor);
    }

    void DrawImage(VectorShapeGraphic g, float h, Rect r, bool mirror)
    {
        if (result.atInfinity)
            return;

        float imageHeight = Mathf.Clamp(result.magnification * h, -r.height, r.height);
        Vector2 foot = ToLocal(result.imageDistance * scale, 0f, true, mirror);
        Vector2 tip = ToLocal(result.imageDistance * scale, imageHeight, true, mirror);
        Vector2 direction = imageHeight >= 0f ? Vector2.up : Vector2.down;
        float headLength = Mathf.Min(18f, Mathf.Abs(imageHeight) * 0.6f);

        Color c = result.IsReal ? imageColor : WithAlpha(imageColor, 0.65f);
        Vector2 shaftEnd = tip - direction * headLength * 0.6f;
        if (result.IsReal) g.AddLine(foot, shaftEnd, 4f, c);
        else g.AddDashedLine(foot, shaftEnd, 4f, c, 9f, 6f);
        g.AddArrowHead(tip, direction, headLength, headLength * 0.55f, c);
    }

    void DrawRays(VectorShapeGraphic g, float h, float half, bool mirror)
    {
        float u = dist * scale;
        float far = Shapes.rectTransform.rect.width * 2.5f;

        DrawRay(g, h, h, u, far, half, mirror, parallelRayColor);
        DrawRay(g, h, 0f, u, far, half, mirror, centralRayColor);
        if (!result.atInfinity)
            DrawRay(g, h, result.magnification * h, u, far, half, mirror, focalRayColor);
    }

    // One ray from the object tip to the mirror/lens at height hitY, then on through the image point.
    // Worked out as if light always goes left to right; for mirrors the second half is flipped back.
    void DrawRay(VectorShapeGraphic g, float h, float hitY, float u, float far, float half, bool mirror, Color color)
    {
        Vector2 objectTip = new Vector2(-u, h);
        Vector2 hit = new Vector2(0f, hitY);

        Vector2 outgoing;
        Vector2 imagePoint = Vector2.zero;
        bool virtualImage = false;

        if (result.atInfinity)
        {
            outgoing = new Vector2(u, -h).normalized;
        }
        else
        {
            imagePoint = new Vector2(result.imageDistance * scale, result.magnification * h);
            virtualImage = result.imageDistance < 0f;
            outgoing = (virtualImage ? hit - imagePoint : imagePoint - hit).normalized;
            if (outgoing.x <= 0.01f)
                outgoing = new Vector2(1f, 0f);
        }

        float surface = mirror ? MirrorSurfaceOffset(hitY, half) : 0f;
        Vector2 start = ToLocal(objectTip.x, objectTip.y, false, mirror);
        Vector2 hitLocal = ToLocal(0f, hitY, false, mirror) + new Vector2(surface, 0f);
        Vector2 endUnfolded = hit + outgoing * far;
        Vector2 end = ToLocal(endUnfolded.x, endUnfolded.y, true, mirror) + new Vector2(surface, 0f);

        g.AddLine(start, hitLocal, lineWidth, color);
        g.AddLine(hitLocal, end, lineWidth, color);
        g.AddArrowHead(Vector2.Lerp(start, hitLocal, 0.55f), hitLocal - start, 14f, 7f, color);

        Vector2 outDirection = end - hitLocal;
        float arrowDistance = Mathf.Min(outDirection.magnitude * 0.5f, 90f);
        g.AddArrowHead(hitLocal + outDirection.normalized * arrowDistance, outDirection, 14f, 7f, color);

        // dashed line back to a virtual image
        if (virtualImage)
        {
            Vector2 imageLocal = ToLocal(imagePoint.x, imagePoint.y, true, mirror);
            g.AddDashedLine(hitLocal, imageLocal, 2f, WithAlpha(color, 0.55f), 8f, 7f);
        }
    }

    void DrawMeasure(VectorShapeGraphic g, Rect r)
    {
        float y = axisY - r.height * measureOffset;
        float objectX = poleX - dist * scale;
        g.AddLine(new Vector2(objectX, y), new Vector2(poleX, y), 2f, measureColor);
        g.AddLine(new Vector2(objectX, y - 8f), new Vector2(objectX, y + 8f), 2f, measureColor);
        g.AddLine(new Vector2(poleX, y - 8f), new Vector2(poleX, y + 8f), 2f, measureColor);
    }

    // ----- Labels -----

    void PlaceLabels(float h, float half, float signedF, bool mirror, Rect r)
    {
        bool labels = showLabels;
        float objectX = poleX - dist * scale;
        float above = 30f;

        if (mirror)
        {
            Place(focusLabel, labels, "F", new Vector2(poleX - signedF * scale, axisY + above));
            Place(secondFocusLabel, false, null, Vector2.zero);
            Place(centreLabel, labels, "C", new Vector2(poleX - 2f * signedF * scale, axisY + above));
            Place(poleLabel, labels, "P", new Vector2(poleX + MirrorSurfaceOffset(-half, half) + 26f, axisY - half - 8f));
        }
        else
        {
            float f = Mathf.Abs(signedF) * scale;
            Place(focusLabel, labels, "F₁", new Vector2(poleX - f, axisY + above));
            Place(secondFocusLabel, labels, "F₂", new Vector2(poleX + f, axisY + above));
            Place(centreLabel, false, null, Vector2.zero);
            Place(poleLabel, labels, "O", new Vector2(poleX + 22f, axisY - 26f));
        }

        Place(objectLabel, labels, mirror ? "O" : "A", new Vector2(objectX - 26f, axisY + h + 4f));

        if (result.atInfinity)
        {
            Place(imageLabel, false, null, Vector2.zero);
        }
        else
        {
            float imageHeight = Mathf.Clamp(result.magnification * h, -r.height, r.height);
            Vector2 tip = ToLocal(result.imageDistance * scale, imageHeight, true, mirror);
            Place(imageLabel, labels, mirror ? "I" : "A′", tip + new Vector2(24f, imageHeight >= 0f ? 4f : -4f));
        }

        if (measureLabel != null)
        {
            float y = axisY - r.height * measureOffset;
            float labelX = (objectX + poleX) * 0.5f;
            if (mirror)
                labelX = Mathf.Min(labelX, poleX - measureLabel.rectTransform.rect.width * 0.5f - 24f);
            Place(measureLabel, showMeasure, $"u = {dist:0.#} cm", new Vector2(labelX, y - 24f));
        }
    }

    void Place(TMP_Text label, bool visible, string text, Vector2 pos)
    {
        if (label == null)
            return;

        label.gameObject.SetActive(visible);
        if (!visible)
            return;

        if (text != null)
            label.text = text;

        // labels are centre-anchored children of the diagram
        label.rectTransform.anchoredPosition = pos - Shapes.rectTransform.rect.center;
    }

    // ----- Dragging the object -----

    public void OnPointerDown(PointerEventData eventData) => DragTo(eventData);

    public void OnDrag(PointerEventData eventData) => DragTo(eventData);

    void DragTo(PointerEventData eventData)
    {
        if (!interactive || topic == null || scale <= 0f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Shapes.rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 pos))
            return;

        float value = (poleX - pos.x) / scale;
        value = Mathf.Clamp(value, topic.minObjectDistance, topic.maxObjectDistance);
        value = Mathf.Round(value * 2f) / 2f;
        onDrag?.Invoke(value);
    }

    static Color WithAlpha(Color c, float alpha)
    {
        c.a *= alpha;
        return c;
    }
}
