using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UI graphic that draws simple shapes (lines, discs, rings, polygons) with soft anti-aliased edges.
// Used by the ray diagrams and icons. Coordinates are local to the RectTransform.
[RequireComponent(typeof(CanvasRenderer))]
public class VectorShapeGraphic : MaskableGraphic
{
    private const float Feather = 1.2f;
    private const int MaxCircleSegments = 48;

    private enum ShapeKind { Line, Disc, Ring, Polygon, Triangle }

    private struct Shape
    {
        public ShapeKind kind;
        public Vector2 a, b, c;
        public float width, radius;
        public Color32 color;
        public int pointStart, pointCount;
    }

    private readonly List<Shape> shapes = new List<Shape>();
    private readonly List<Vector2> points = new List<Vector2>();
    private bool populating;

    // True when shapes are generated in BuildShapes rather than added from outside.
    protected virtual bool IsProcedural => false;

    public void ClearShapes()
    {
        shapes.Clear();
        points.Clear();
        MarkDirty();
    }

    private void MarkDirty()
    {
        if (!populating)
            SetVerticesDirty();
    }

    public void AddLine(Vector2 from, Vector2 to, float width, Color color)
    {
        shapes.Add(new Shape { kind = ShapeKind.Line, a = from, b = to, width = width, color = color });
        MarkDirty();
    }

    public void AddDashedLine(Vector2 from, Vector2 to, float width, Color color, float dash, float gap)
    {
        float length = Vector2.Distance(from, to);
        if (length < 0.01f || dash <= 0f)
            return;

        Vector2 dir = (to - from) / length;
        for (float d = 0f; d < length; d += dash + gap)
            AddLine(from + dir * d, from + dir * Mathf.Min(d + dash, length), width, color);
    }

    public void AddPolyline(IList<Vector2> path, float width, Color color, bool roundJoins = true)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            AddLine(path[i], path[i + 1], width, color);
            if (roundJoins && i > 0)
                AddDisc(path[i], width * 0.5f, color);
        }
    }

    public void AddDisc(Vector2 center, float radius, Color color)
    {
        shapes.Add(new Shape { kind = ShapeKind.Disc, a = center, radius = radius, color = color });
        MarkDirty();
    }

    public void AddRing(Vector2 center, float radius, float width, Color color)
    {
        shapes.Add(new Shape { kind = ShapeKind.Ring, a = center, radius = radius, width = width, color = color });
        MarkDirty();
    }

    public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        shapes.Add(new Shape { kind = ShapeKind.Triangle, a = a, b = b, c = c, color = color });
        MarkDirty();
    }

    // Filled polygon. Must be star-shaped around its centroid.
    public void AddPolygon(IList<Vector2> outline, Color color)
    {
        shapes.Add(new Shape { kind = ShapeKind.Polygon, color = color, pointStart = points.Count, pointCount = outline.Count });
        points.AddRange(outline);
        MarkDirty();
    }

    // Filled arrowhead whose tip is at tip, pointing along direction.
    public void AddArrowHead(Vector2 tip, Vector2 direction, float length, float halfWidth, Color color)
    {
        if (direction.sqrMagnitude < 1e-6f)
            return;

        Vector2 d = direction.normalized;
        Vector2 n = new Vector2(-d.y, d.x);
        Vector2 baseCenter = tip - d * length;
        AddTriangle(tip, baseCenter + n * halfWidth, baseCenter - n * halfWidth, color);
    }

    // Procedural subclasses add their shapes here; called before every tessellation.
    protected virtual void BuildShapes() { }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        populating = true;
        if (IsProcedural)
        {
            shapes.Clear();
            points.Clear();
            BuildShapes();
        }
        populating = false;

        foreach (Shape shape in shapes)
        {
            Color32 tinted = Tint(shape.color);
            switch (shape.kind)
            {
                case ShapeKind.Line: TessellateLine(vh, shape.a, shape.b, shape.width, tinted); break;
                case ShapeKind.Disc: TessellateDisc(vh, shape.a, shape.radius, tinted); break;
                case ShapeKind.Ring: TessellateRing(vh, shape.a, shape.radius, shape.width, tinted); break;
                case ShapeKind.Triangle: TessellatePolygon(vh, new List<Vector2> { shape.a, shape.b, shape.c }, tinted); break;
                case ShapeKind.Polygon: TessellatePolygon(vh, points.GetRange(shape.pointStart, shape.pointCount), tinted); break;
            }
        }
    }

    private Color32 Tint(Color32 shapeColor)
    {
        return (Color)shapeColor * color;
    }

    private static Color32 Transparent(Color32 c)
    {
        return new Color32(c.r, c.g, c.b, 0);
    }

    private static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 c0, Color32 c1, Color32 c2, Color32 c3)
    {
        int start = vh.currentVertCount;
        vh.AddVert(p0, c0, Vector4.zero);
        vh.AddVert(p1, c1, Vector4.zero);
        vh.AddVert(p2, c2, Vector4.zero);
        vh.AddVert(p3, c3, Vector4.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void TessellateLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 1e-6f)
            return;

        Vector2 dir = delta.normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float core = Mathf.Max(0.25f, width * 0.5f - Feather * 0.5f);
        float outer = core + Feather;
        Color32 clear = Transparent(color);

        AddQuad(vh, a + normal * core, b + normal * core, b - normal * core, a - normal * core, color, color, color, color);
        AddQuad(vh, a + normal * outer, b + normal * outer, b + normal * core, a + normal * core, clear, clear, color, color);
        AddQuad(vh, a - normal * core, b - normal * core, b - normal * outer, a - normal * outer, color, color, clear, clear);
    }

    private static int SegmentsFor(float radius)
    {
        return Mathf.Clamp(Mathf.CeilToInt(radius * 0.8f), 12, MaxCircleSegments);
    }

    private static void TessellateDisc(VertexHelper vh, Vector2 center, float radius, Color32 color)
    {
        int segments = SegmentsFor(radius);
        float core = Mathf.Max(0.1f, radius - Feather * 0.5f);
        float outer = core + Feather;
        Color32 clear = Transparent(color);

        int centerIndex = vh.currentVertCount;
        vh.AddVert(center, color, Vector4.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + dir * core, color, Vector4.zero);
            vh.AddVert(center + dir * outer, clear, Vector4.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int inner = centerIndex + 1 + i * 2;
            int nextInner = inner + 2;
            vh.AddTriangle(centerIndex, inner, nextInner);
            vh.AddTriangle(inner, inner + 1, nextInner + 1);
            vh.AddTriangle(inner, nextInner + 1, nextInner);
        }
    }

    private static void TessellateRing(VertexHelper vh, Vector2 center, float radius, float width, Color32 color)
    {
        int segments = SegmentsFor(radius);
        float half = Mathf.Max(0.25f, width * 0.5f - Feather * 0.5f);
        float[] radii = { radius - half - Feather, radius - half, radius + half, radius + half + Feather };
        Color32 clear = Transparent(color);
        Color32[] colors = { clear, color, color, clear };

        int start = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (int r = 0; r < 4; r++)
                vh.AddVert(center + dir * Mathf.Max(0f, radii[r]), colors[r], Vector4.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int row = start + i * 4;
            int next = row + 4;
            for (int r = 0; r < 3; r++)
            {
                vh.AddTriangle(row + r, row + r + 1, next + r + 1);
                vh.AddTriangle(row + r, next + r + 1, next + r);
            }
        }
    }

    private static void TessellatePolygon(VertexHelper vh, List<Vector2> outline, Color32 color)
    {
        int count = outline.Count;
        if (count < 3)
            return;

        Vector2 centroid = Vector2.zero;
        foreach (Vector2 p in outline)
            centroid += p;
        centroid /= count;

        Color32 clear = Transparent(color);
        int centerIndex = vh.currentVertCount;
        vh.AddVert(centroid, color, Vector4.zero);

        for (int i = 0; i < count; i++)
        {
            Vector2 p = outline[i];
            Vector2 outward = p - centroid;
            outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector2.zero;
            vh.AddVert(p, color, Vector4.zero);
            vh.AddVert(p + outward * Feather, clear, Vector4.zero);
        }

        for (int i = 0; i < count; i++)
        {
            int inner = centerIndex + 1 + i * 2;
            int nextInner = centerIndex + 1 + ((i + 1) % count) * 2;
            vh.AddTriangle(centerIndex, inner, nextInner);
            vh.AddTriangle(inner, inner + 1, nextInner + 1);
            vh.AddTriangle(inner, nextInner + 1, nextInner);
        }
    }
}
