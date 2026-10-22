using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// The experiment in 3D, like a school optics lab: a table with an optical rail, a lit candle as the object,
// the mirror/lens in its holder and the candle's image (caught on a screen when it is real, a see-through
// "ghost" when it is virtual). Light pulses travel along the traced rays.
// It stands on a real surface in AR with the optical axis at the learner's eye level, seen from the front.
// Same Draw/onDrag API as RayDiagram.
//
// Rays come from the ray tracer (real reflection/refraction); the image and readouts use Optics.Solve.
// Optical units are cm. Heights are drawn taller than they really are (like a textbook diagram) and curved
// surfaces deeper, so concave/convex is easy to see; the stretch keeps straight lines straight and crossings in place.
public class OpticsBenchView : MonoBehaviour
{
    [Header("Size on the table")]
    [Tooltip("Metres from the mirror/lens to each end of the bench. Set when placed so the bench fits the screen.")]
    public float fitHalfWidth = 0.16f;
    public float minHalfWidth = 0.05f;
    public float maxHalfWidth = 1f;
    [Tooltip("When placed, the bench is sized to fill this much of the screen width")]
    [Range(0.4f, 1f)] public float fitScreenWidth = 0.85f;
    [Tooltip("Mirror/lens half-height as a fraction of the bench half-width")]
    [Range(0.05f, 0.6f)] public float elementHeightPerHalfWidth = 0.34f;
    [Tooltip("Curved surfaces are drawn at least this deep (fraction of the half-height) so concave/convex shows")]
    [Range(0.05f, 0.5f)] public float curvatureEmphasis = 0.22f;
    [Tooltip("How fast the bench zooms out/in to keep the image in view")]
    public float zoomSpeed = 4f;
    [Tooltip("Images further away than this many object ranges are shown at the edge of the bench")]
    public float maxExtentPerObjectRange = 3f;

    [Header("Lab table")]
    [Tooltip("Height of the optical axis above the surface the bench stands on (m). Set to eye level when placed.")]
    public float axisHeight = 0.25f;
    [Tooltip("The axis sits this far below the learner's eyes (m)")]
    public float belowEyes = 0.03f;
    [Tooltip("The bench is turned this much (degrees) so the mirror's face and the lens disc can be seen. 0 = exact side view.")]
    public float viewAngle = -30f;

    [Header("Optics")]
    public int fanRayCount = 13;

    [Header("Materials")]
    public Material rayMaterial;     // vertex-coloured unlit (e.g. URP Particles/Unlit)
    public Material dashedMaterial;  // same, with a dashed texture in tile mode
    public Material mirrorMaterial;  // polished metal: reflects the reflection probe
    public Material glassMaterial;   // OptiAR/Glass: refracts what is behind the lens
    public Material markerMaterial;
    public Material woodMaterial;    // table
    public Material metalMaterial;   // rods and carriages
    public Material holderMaterial;  // black plastic rings that hold the mirror/lens
    public Material paintMaterial;   // lit, tinted per part (rail, mirror back, wax, screen)
    public Material ghostMaterial;   // see-through, for the virtual image
    public Material flameMaterial;   // additive, vertex-coloured
    public Material glowMaterial;    // additive soft dot (flame halo, light pulses)
    public TMP_FontAsset labelFont;

    [Header("Light")]
    public Color candleLightColor = new Color(1f, 0.72f, 0.4f);
    public float candleLightIntensity = 0.35f;
    [Tooltip("Metres between light pulses on a ray")]
    public float pulseSpacing = 0.035f;
    [Tooltip("Metres per second")]
    public float pulseSpeed = 0.08f;

    [Header("Line widths (m)")]
    public float rayWidth = 0.0025f;

    [Header("Colours")]
    public Color axisColor = new Color(0.25f, 0.32f, 0.43f, 0.9f);
    public Color objectColor = new Color(0.96f, 0.62f, 0.04f);
    public Color imageColor = new Color(0.20f, 0.83f, 0.60f);
    public Color focusColor = new Color(0.98f, 0.80f, 0.08f);
    public Color centreColor = new Color(0.80f, 0.84f, 0.88f);
    public Color parallelRayColor = new Color(0.13f, 0.83f, 0.93f);
    public Color centralRayColor = new Color(0.96f, 0.45f, 0.71f);
    public Color focalRayColor = new Color(0.51f, 0.55f, 0.97f);
    public Color fanRayColor = new Color(0.98f, 0.92f, 0.55f);
    public Color measureColor = new Color(0.39f, 0.45f, 0.55f);
    public Color railColor = new Color(0.16f, 0.18f, 0.22f);
    public Color scaleColor = new Color(0.92f, 0.93f, 0.95f);
    public Color mirrorBackColor = new Color(0.22f, 0.24f, 0.28f);
    public Color waxColor = new Color(0.96f, 0.93f, 0.86f);
    public Color screenColor = new Color(0.97f, 0.97f, 0.95f);

    // called with the new object distance when the user drags the candle
    public System.Action<float> onDrag;

    // set by the experiment screen
    [System.NonSerialized] public bool fan;                       // many rays instead of the three textbook rays
    [System.NonSerialized] public float objectHeight = 2f;        // cm, candle base to flame tip
    [System.NonSerialized] public float refractiveIndex = SphericalLens.CrownGlass;
    [System.NonSerialized] public float apertureFraction = 1f;    // 0.2..1 of the largest mirror/lens

    TopicData topic;
    float focal;
    float dist;
    bool showRays, showLabels, showMeasure;

    // metres per cm: x along the axis, y for heights
    float xScale, yScale, targetXScale;
    float aperture, candleHeight, halfExtent, elementHalfHeight;
    // curved surfaces are drawn `curve` times deeper; points within `zone` cm of the element are moved to match
    float curve = 1f, zone;
    bool animating;
    float flameBaseHeight;

    Transform content;
    MeshFilter elementFilter, frameFilter, backFilter;
    MeshRenderer elementRenderer;
    LineRenderer axisLine, measureLine, imageMeasureLine;
    Transform tableTop, rail, objectCarriage, objectRod, elementCarriage, elementRod;
    Transform screenCard, screenRod;
    readonly Transform[] legs = new Transform[4];
    readonly List<Transform> ticks = new List<Transform>();
    Candle objectCandle, imageCandle;
    ReflectionProbe probe;
    readonly List<LineRenderer> rayLines = new List<LineRenderer>();
    readonly List<LineRenderer> dashLines = new List<LineRenderer>();
    readonly List<Transform> markers = new List<Transform>();
    readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    readonly List<Track> tracks = new List<Track>();
    readonly List<Transform> pulses = new List<Transform>();
    int rayCount, dashCount, markerCount, labelCount;
    static Mesh flameMesh;

    // the mirror is left out of its own reflection
    const int SelfReflectionLayer = 2;

    class Candle
    {
        public Transform root, wax, wick, flame, halo;
        public MeshRenderer waxRenderer, flameRenderer, haloRenderer;
        public Light light;
    }

    // a ray as drawn, for the light pulses
    class Track
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> lengths = new List<float>(); // running length at each point
        public Color color;
    }

    // touch state
    bool dragging;
    float lastPinch, lastTwist;

    // Sizes the bench so all of it fits across the screen from where the learner stands,
    // and lifts the optical axis to their eye level.
    public void FitToView(TopicData t, Camera cam)
    {
        if (t == null || cam == null)
            return;

        // the bench is seen from the front, so what matters is the distance along the floor/table
        float distance = Vector3.ProjectOnPlane(transform.position - cam.transform.position, transform.up).magnitude;
        float halfVertical = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float visibleWidth = 2f * distance * Mathf.Tan(halfVertical) * cam.aspect;
        fitHalfWidth = Mathf.Clamp(fitScreenWidth * visibleWidth * 0.5f, minHalfWidth, maxHalfWidth);
        xScale = 0f; // snap the zoom on the next draw

        // the optical axis at eye level: the learner looks straight at the experiment, not down on it
        float eyes = Vector3.Dot(cam.transform.position - transform.position, transform.up);
        axisHeight = Mathf.Max(eyes - belowEyes, StandHeight(elementHeightPerHalfWidth * fitHalfWidth) + 0.02f);
    }

    // half-height (cm) of the mirror/lens for a focal length and aperture setting
    public static float ApertureFor(TopicData t, float focalLength, float fraction)
    {
        float f = Mathf.Abs(focalLength);
        fraction = Mathf.Clamp(fraction, 0.2f, 1f);
        return t.isMirror
            ? fraction * 0.8f * f
            : fraction * SphericalLens.MaxAperturePerFocal * f;
    }

    public void Draw(TopicData t, float focalLength, float objectDistance, bool rays, bool labelsOn, bool measure)
    {
        topic = t;
        focal = Mathf.Abs(focalLength);
        dist = objectDistance;
        showRays = rays;
        showLabels = labelsOn;
        showMeasure = measure;
        Redraw();
    }

    public void Redraw()
    {
        if (topic == null)
            return;
        EnsureParts();

        bool mirror = topic.isMirror;
        float signedF = topic.SignedFocal(focal);
        float requested = ApertureFor(topic, focal, apertureFraction);
        OpticalElement element = mirror
            ? (OpticalElement)new SphericalMirror(signedF, requested)
            : new SphericalLens(signedF, requested, refractiveIndex);

        aperture = element.Aperture;
        candleHeight = Mathf.Min(objectHeight, aperture * 0.9f);
        elementHalfHeight = elementHeightPerHalfWidth * fitHalfWidth;
        yScale = elementHalfHeight / aperture;

        // where the image is (mirrors fold the light back, so a real image is in front, -x)
        OpticsResult result = Optics.Solve(signedF, dist);
        float imageX = result.atInfinity ? 0f : (mirror ? -result.imageDistance : result.imageDistance);

        // zoom out (or back in) so the image stays on the bench, up to a limit
        float baseExtent = topic.maxObjectDistance + 4f;
        float limit = baseExtent * maxExtentPerObjectRange;
        halfExtent = result.atInfinity ? baseExtent : Mathf.Clamp(Mathf.Abs(imageX) + 6f, baseExtent, limit);
        targetXScale = fitHalfWidth / halfExtent;
        if (xScale <= 0f)
            xScale = targetXScale;
        animating = !Mathf.Approximately(xScale, targetXScale);
        // while zooming, the bench is drawn at the current zoom
        halfExtent = fitHalfWidth / xScale;

        content.localPosition = new Vector3(0f, axisHeight, 0f);
        rayCount = dashCount = markerCount = labelCount = 0;
        tracks.Clear();

        SetLine(axisLine, new[] { new Vector2(-halfExtent, 0f), new Vector2(halfExtent, 0f) }, axisColor, rayWidth * 0.6f);
        float rimX = DrawElement(element);
        DrawStage();
        DrawHolders(rimX);
        DrawFocalPoints(signedF, mirror);
        DrawObject();
        DrawImage(result, imageX, mirror);
        if (showRays)
            DrawRays(element);
        DrawMeasure(result, imageX);

        HideUnused(rayLines, rayCount);
        HideUnused(dashLines, dashCount);
        for (int i = markerCount; i < markers.Count; i++) markers[i].gameObject.SetActive(false);
        for (int i = labelCount; i < labels.Count; i++) labels[i].gameObject.SetActive(false);

        UpdateReflections();
    }

    // optical (cm) -> bench local (m)
    Vector3 Local(Vector2 p) => new Vector3(p.x * xScale, p.y * yScale, 0f);

    // a point on a mirror/lens surface, moved onto the deepened surface that is drawn
    Vector2 OnSurface(Vector2 p) => Mathf.Abs(p.x) <= zone ? new Vector2(p.x * curve, p.y) : p;

    // ----- Parts -----

    void EnsureParts()
    {
        if (content != null)
            return;

        content = new GameObject("Content").transform;
        content.SetParent(transform, false);

        // soft room light from above and behind the learner, so the wax, screen and glass show their shape
        var lamp = new GameObject("LabLight").AddComponent<Light>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localRotation = Quaternion.Euler(45f, -25f, 0f);
        lamp.type = LightType.Directional;
        lamp.color = new Color(1f, 0.97f, 0.92f);
        lamp.intensity = 0.9f;
        lamp.shadows = LightShadows.None;

        elementFilter = MeshPart("Element", glassMaterial);
        elementRenderer = elementFilter.GetComponent<MeshRenderer>();
        elementFilter.gameObject.layer = SelfReflectionLayer;
        frameFilter = MeshPart("Holder", holderMaterial != null ? holderMaterial : metalMaterial);
        backFilter = MeshPart("MirrorBack", paintMaterial);

        tableTop = Part(PrimitiveType.Cube, "TableTop", woodMaterial);
        for (int i = 0; i < legs.Length; i++)
            legs[i] = Part(PrimitiveType.Cube, "TableLeg", woodMaterial);
        rail = Part(PrimitiveType.Cube, "Rail", paintMaterial);
        Tint(rail, railColor);
        objectCarriage = Part(PrimitiveType.Cube, "ObjectCarriage", metalMaterial);
        objectRod = Part(PrimitiveType.Cylinder, "ObjectRod", metalMaterial);
        elementCarriage = Part(PrimitiveType.Cube, "ElementCarriage", metalMaterial);
        elementRod = Part(PrimitiveType.Cylinder, "ElementRod", metalMaterial);
        screenCard = Part(PrimitiveType.Cube, "Screen", paintMaterial);
        Tint(screenCard, screenColor);
        screenRod = Part(PrimitiveType.Cylinder, "ScreenRod", metalMaterial);

        objectCandle = NewCandle("Candle", true);
        imageCandle = NewCandle("CandleImage", false);

        // a live reflection of the bench for the mirror (and the edges of the lens)
        var probeGo = new GameObject("Reflection");
        probeGo.transform.SetParent(content, false);
        probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.resolution = 128;
        probe.boxProjection = true;
        // behind the bench the reflections show the studio sky (RenderSettings.skybox)
        probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
        probe.resolution = 256;
        probe.cullingMask = ~(1 << SelfReflectionLayer);
        probe.nearClipPlane = 0.002f;
        probe.importance = 10;

        axisLine = NewLine("Axis", rayMaterial);
        measureLine = NewLine("Measure", rayMaterial);
        imageMeasureLine = NewLine("ImageMeasure", rayMaterial);
    }

    Transform Part(PrimitiveType type, string name, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(content, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }

    MeshFilter MeshPart(string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(content, false);
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = new Mesh { name = name };
        return filter;
    }

    static void Tint(Component part, Color color)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        part.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    static void Box(Transform part, Vector3 centre, Vector3 size)
    {
        part.gameObject.SetActive(true);
        part.localPosition = centre;
        part.localScale = size;
    }

    // a cylinder primitive is 2 units tall and 1 unit wide
    static void Rod(Transform part, Vector3 bottom, float length, float radius)
    {
        part.gameObject.SetActive(length > 0.0005f);
        part.localPosition = bottom + Vector3.up * length * 0.5f;
        part.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    LineRenderer NewLine(string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(content, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = material;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = LineTextureMode.Tile;
        return line;
    }

    void SetLine(LineRenderer line, Vector2[] points, Color color, float width)
    {
        line.gameObject.SetActive(true);
        line.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, Local(points[i]));
        line.startColor = line.endColor = color;
        line.widthMultiplier = width;
    }

    LineRenderer NextLine(List<LineRenderer> pool, ref int count, bool dashed)
    {
        if (count == pool.Count)
            pool.Add(NewLine(dashed ? "Dash" : "Ray", dashed ? dashedMaterial : rayMaterial));
        return pool[count++];
    }

    static void HideUnused(List<LineRenderer> pool, int used)
    {
        for (int i = used; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }

    // ----- Element -----

    // Builds the mirror/lens from the same surfaces the rays are traced against, drawn deeper so the
    // curve is easy to see, plus its metal ring. Returns the x (m) of the rim, where the holder grips it.
    float DrawElement(OpticalElement element)
    {
        const int steps = 20;
        float halfWidth; // half the thickness of the ring that holds it
        float rimX;
        // profiles are in metres: x along the axis, y = distance from the axis
        var profile = new List<Vector2>();

        if (element is SphericalMirror m)
        {
            float sag = Mathf.Abs(m.surface.SurfaceX(aperture));
            curve = Mathf.Max(1f, curvatureEmphasis * elementHalfHeight / Mathf.Max(1e-5f, sag * xScale));
            zone = sag + 0.01f;

            for (int i = 0; i <= steps; i++)
            {
                float r = aperture * i / steps;
                profile.Add(new Vector2(m.surface.SurfaceX(r) * xScale * curve, r * yScale));
            }
            elementRenderer.sharedMaterial = mirrorMaterial;

            // silvered glass has a thickness and a painted back
            float thick = elementHalfHeight * 0.06f;
            var shift = new Vector2(thick, 0f);
            var back = new List<Vector2> { profile[steps], profile[steps] + shift };
            for (int i = steps; i >= 0; i--)
                back.Add(profile[i] + shift);
            backFilter.gameObject.SetActive(true);
            BuildRevolution(backFilter.sharedMesh, back, 48);
            Tint(backFilter, mirrorBackColor);

            rimX = profile[steps].x + thick * 0.5f;
            halfWidth = thick;
        }
        else
        {
            var lens = (SphericalLens)element;
            float centre = lens.thickness;
            float edge = lens.back.SurfaceX(aperture) - lens.front.SurfaceX(aperture);
            // convex: thick in the middle; concave: thick at the edge
            float widest = Mathf.Max(centre, edge);
            curve = Mathf.Max(1f, curvatureEmphasis * 1.4f * elementHalfHeight / Mathf.Max(1e-5f, widest * xScale));
            zone = widest * 0.5f + 0.01f;

            float sx = xScale * curve;
            for (int i = 0; i <= steps; i++)
                profile.Add(new Vector2(lens.front.SurfaceX(aperture * i / steps) * sx, aperture * i / steps * yScale));
            // duplicate the rim points so the edge stays sharp
            profile.Add(profile[profile.Count - 1]);
            for (int i = steps; i >= 0; i--)
                profile.Add(new Vector2(lens.back.SurfaceX(aperture * i / steps) * sx, aperture * i / steps * yScale));
            profile.Insert(steps + 2, profile[steps + 2]);
            elementRenderer.sharedMaterial = glassMaterial;
            backFilter.gameObject.SetActive(false);

            rimX = 0f;
            halfWidth = edge * sx * 0.5f;
        }

        BuildRevolution(elementFilter.sharedMesh, profile, 48);

        // flat holder ring gripping the rim, like a lab lens/mirror holder
        // (rounded rectangle cross-section, traced clockwise so it faces outwards)
        float band = Mathf.Max(halfWidth * 1.15f, elementHalfHeight * 0.07f);
        float depth = elementHalfHeight * 0.09f;
        float corner = Mathf.Min(band, depth) * 0.35f;
        var ring = new List<Vector2>();
        AddCorner(ring, new Vector2(rimX - band + corner, elementHalfHeight - corner * 0.2f), corner, 180f, 90f);
        AddCorner(ring, new Vector2(rimX - band + corner, elementHalfHeight + depth - corner), corner, 180f, 90f, true);
        AddCorner(ring, new Vector2(rimX + band - corner, elementHalfHeight + depth - corner), corner, 90f, 0f, true);
        AddCorner(ring, new Vector2(rimX + band - corner, elementHalfHeight - corner * 0.2f), corner, 0f, -90f, true);
        ring.Add(ring[0]);
        BuildRevolution(frameFilter.sharedMesh, ring, 64);

        return rimX;
    }

    // a quarter circle (or just its end point) for a rounded corner, going clockwise
    static void AddCorner(List<Vector2> points, Vector2 centre, float radius, float from, float to, bool arc = false)
    {
        if (!arc)
        {
            points.Add(centre + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad)) * radius);
            return;
        }
        for (int i = 0; i <= 4; i++)
        {
            float a = Mathf.Lerp(from, to, i / 4f) * Mathf.Deg2Rad;
            points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }

    // spins a profile (x along the axis, y = distance from it, in metres) around the axis
    static void BuildRevolution(Mesh mesh, List<Vector2> profile, int segments)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        int rings = profile.Count;

        for (int j = 0; j <= segments; j++)
        {
            float angle = j * Mathf.PI * 2f / segments;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            foreach (Vector2 p in profile)
                vertices.Add(new Vector3(p.x, p.y * cos, p.y * sin));
        }

        for (int j = 0; j < segments; j++)
        {
            for (int i = 0; i < rings - 1; i++)
            {
                // wound so a profile going away from the axis faces -x (the mirror faces the candle)
                // and a closed profile (lens, ring) faces outwards
                int a = j * rings + i, b = a + 1, c = a + rings, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void UpdateReflections()
    {
        float width = 2f * halfExtent * xScale;
        probe.size = new Vector3(width, elementHalfHeight * 8f, elementHalfHeight * 6f);
        probe.center = Vector3.zero;
        probe.RenderProbe();
    }

    // ----- Lab table, rail and holders (metres; the axis is y = 0) -----

    float RailHeight => elementHalfHeight * 0.25f;
    // from the table top up to the optical axis
    float StandHeight() => StandHeight(elementHalfHeight);
    static float StandHeight(float halfHeight) => halfHeight * 0.25f + halfHeight * 1.35f;

    void DrawStage()
    {
        float x0 = -halfExtent * xScale, x1 = halfExtent * xScale;
        float top = -StandHeight();
        float board = elementHalfHeight * 0.3f;
        float depth = elementHalfHeight * 5f;
        float width = x1 - x0 + elementHalfHeight * 1.5f;

        Box(tableTop, new Vector3(0f, top - board * 0.5f, 0f), new Vector3(width, board, depth));

        // legs down to the real floor/table
        float legLength = axisHeight - StandHeight() - board;
        float leg = elementHalfHeight * 0.35f;
        for (int i = 0; i < legs.Length; i++)
        {
            float lx = (i % 2 == 0 ? -1f : 1f) * (width * 0.5f - leg);
            float lz = (i < 2 ? -1f : 1f) * (depth * 0.5f - leg);
            if (legLength > 0.01f)
                Box(legs[i], new Vector3(lx, -axisHeight + legLength * 0.5f, lz), new Vector3(leg, legLength, leg));
            else
                legs[i].gameObject.SetActive(false);
        }

        // optical rail with a cm scale; the ticks get sparser when the bench is zoomed out
        float railDepth = elementHalfHeight * 0.6f;
        float railTop = top + RailHeight;
        Box(rail, new Vector3(0f, top + RailHeight * 0.5f, 0f), new Vector3(x1 - x0, RailHeight, railDepth));

        float step = 5f;
        while (step * xScale < elementHalfHeight * 0.12f)
            step *= 2f;
        int tickCount = 0;
        for (float cm = Mathf.Ceil(-halfExtent / step) * step; cm <= halfExtent; cm += step)
        {
            if (tickCount == ticks.Count)
            {
                Transform tick = Part(PrimitiveType.Cube, "Tick", paintMaterial);
                Tint(tick, scaleColor);
                ticks.Add(tick);
            }
            bool major = Mathf.Repeat(cm, step * 2f) < 0.01f;
            Box(ticks[tickCount++], new Vector3(cm * xScale, railTop, railDepth * (major ? -0.2f : -0.3f)),
                new Vector3(elementHalfHeight * 0.03f, elementHalfHeight * 0.02f, railDepth * (major ? 0.55f : 0.35f)));
        }
        for (int i = tickCount; i < ticks.Count; i++)
            ticks[i].gameObject.SetActive(false);
    }

    void DrawHolders(float rimX)
    {
        float railTop = -StandHeight() + RailHeight;
        var carriage = new Vector3(elementHalfHeight * 0.5f, elementHalfHeight * 0.22f, elementHalfHeight * 0.8f);
        float rod = elementHalfHeight * 0.05f;
        float rodBottom = railTop + carriage.y;

        // the candle stand slides on the rail with the candle
        float objectX = -dist * xScale;
        Box(objectCarriage, new Vector3(objectX, railTop + carriage.y * 0.5f, 0f), carriage);
        Rod(objectRod, new Vector3(objectX, rodBottom, 0f), -rodBottom, rod * 2.2f);

        // the mirror/lens holder grips the bottom of its ring
        Box(elementCarriage, new Vector3(rimX, railTop + carriage.y * 0.5f, 0f), carriage);
        Rod(elementRod, new Vector3(rimX, rodBottom, 0f), -elementHalfHeight - rodBottom, rod * 1.3f);
    }

    // ----- Candle (the object) and its image -----

    Candle NewCandle(string name, bool withLight)
    {
        var candle = new Candle { root = new GameObject(name).transform };
        candle.root.SetParent(content, false);

        candle.wax = Part(PrimitiveType.Cylinder, "Wax", paintMaterial);
        candle.wax.SetParent(candle.root, false);
        candle.waxRenderer = candle.wax.GetComponent<MeshRenderer>();

        candle.wick = Part(PrimitiveType.Cylinder, "Wick", paintMaterial);
        candle.wick.SetParent(candle.root, false);
        Tint(candle.wick, new Color(0.12f, 0.1f, 0.08f));

        var flame = new GameObject("Flame");
        flame.transform.SetParent(candle.root, false);
        flame.AddComponent<MeshFilter>().sharedMesh = FlameMesh();
        candle.flameRenderer = flame.AddComponent<MeshRenderer>();
        candle.flameRenderer.sharedMaterial = flameMaterial;
        candle.flame = flame.transform;

        candle.halo = Part(PrimitiveType.Quad, "Glow", glowMaterial);
        candle.halo.SetParent(candle.root, false);
        candle.haloRenderer = candle.halo.GetComponent<MeshRenderer>();

        if (withLight)
        {
            var lightGo = new GameObject("CandleLight");
            lightGo.transform.SetParent(candle.root, false);
            candle.light = lightGo.AddComponent<Light>();
            candle.light.type = LightType.Point;
            candle.light.color = candleLightColor;
            candle.light.shadows = LightShadows.None;
        }
        return candle;
    }

    // A candle standing on the axis at foot (cm), height (cm) from the base to the flame tip; negative = upside down.
    // ghost: a virtual image, seen but not really there.
    void SetCandle(Candle candle, Vector2 foot, float height, bool ghost, float brightness)
    {
        float length = Mathf.Abs(height) * yScale;
        candle.root.gameObject.SetActive(length > 0.0005f);
        candle.root.localPosition = Local(foot);
        candle.root.localRotation = height >= 0f ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);

        float flameLength = length * 0.32f;
        float waxLength = length - flameLength;
        float radius = Mathf.Min(elementHalfHeight * 0.085f, length * 0.2f);

        Rod(candle.wax, Vector3.zero, waxLength, radius);
        candle.waxRenderer.sharedMaterial = ghost ? ghostMaterial : paintMaterial;
        Tint(candle.waxRenderer, ghost ? new Color(waxColor.r, waxColor.g, waxColor.b, 0.3f) : waxColor);
        Rod(candle.wick, new Vector3(0f, waxLength, 0f), flameLength * 0.18f, radius * 0.1f);

        candle.flame.localPosition = new Vector3(0f, waxLength + flameLength * 0.04f, 0f);
        candle.flame.localScale = new Vector3(radius * 0.9f, flameLength * 0.96f, radius * 0.9f);
        Tint(candle.flameRenderer, new Color(1f, 1f, 1f, brightness));

        candle.halo.localPosition = new Vector3(0f, waxLength + flameLength * 0.45f, 0f);
        candle.halo.localScale = Vector3.one * flameLength * 2.4f;
        Tint(candle.haloRenderer, new Color(1f, 0.75f, 0.4f, 0.55f * brightness));

        if (candle.light != null)
        {
            candle.light.transform.localPosition = candle.halo.localPosition;
            candle.light.range = fitHalfWidth * 0.7f;
        }
    }

    void DrawObject()
    {
        SetCandle(objectCandle, new Vector2(-dist, 0f), candleHeight, false, 1f);
        flameBaseHeight = objectCandle.flame.localScale.y;
        AddLabel(new Vector2(-dist, candleHeight + aperture * 0.25f), "Object", objectColor);
    }

    void DrawImage(OpticsResult result, float imageX, bool mirror)
    {
        screenCard.gameObject.SetActive(false);
        screenRod.gameObject.SetActive(false);

        if (result.atInfinity)
        {
            imageCandle.root.gameObject.SetActive(false);
            AddLabel(new Vector2(mirror ? -halfExtent * 0.6f : halfExtent * 0.6f, aperture * 1.6f),
                "Image at infinity:\nthe light leaves parallel", imageColor, true);
            return;
        }

        // too far to show: point to it from the edge of the bench
        if (Mathf.Abs(imageX) > halfExtent - 4f)
        {
            imageCandle.root.gameObject.SetActive(false);
            float edge = Mathf.Sign(imageX) * (halfExtent - 10f);
            string arrow = imageX > 0f ? "→" : "←";
            AddLabel(new Vector2(edge, aperture * 1.6f), $"Image {arrow}\n{Mathf.Abs(result.imageDistance):0} cm away", imageColor, true);
            return;
        }

        float height = Mathf.Clamp(result.magnification * candleHeight, -aperture * 2.5f, aperture * 2.5f);
        bool real = result.IsReal;
        // a real image is light gathered on the screen; a virtual one is only seen by looking into the mirror/lens
        SetCandle(imageCandle, new Vector2(imageX, 0f), height, !real, real ? 0.9f : 0.45f);

        if (real)
            DrawScreen(imageX, height);

        AddLabel(new Vector2(imageX, height + Mathf.Sign(height) * aperture * 0.3f), real ? "Real image" : "Virtual image", imageColor);
    }

    // a white card on a stand, just behind the image, where a real image can be caught
    void DrawScreen(float imageX, float height)
    {
        float top = Mathf.Max(0f, height) * yScale + elementHalfHeight * 0.25f;
        float bottom = Mathf.Min(0f, height) * yScale - elementHalfHeight * 0.25f;
        float z = elementHalfHeight * 0.45f;
        float x = imageX * xScale;
        Box(screenCard, new Vector3(x, (top + bottom) * 0.5f, z), new Vector3(elementHalfHeight * 0.9f, top - bottom, 0.002f));

        float railTop = -StandHeight() + RailHeight;
        Rod(screenRod, new Vector3(x, railTop, z), bottom - railTop, elementHalfHeight * 0.04f);
    }

    // teardrop flame: base at y = 0, tip at y = 1, white-yellow at the bottom fading to orange
    static Mesh FlameMesh()
    {
        if (flameMesh != null)
            return flameMesh;

        const int rings = 14, segments = 20;
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            float r = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f)) * (1f - 0.25f * t);
            Color c = Color.Lerp(new Color(1f, 0.97f, 0.8f, 1f), new Color(1f, 0.45f, 0.08f, 0.5f), t);
            for (int j = 0; j <= segments; j++)
            {
                float a = j * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, t, Mathf.Sin(a) * r));
                colors.Add(c);
            }
        }
        for (int i = 0; i < rings; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = i * (segments + 1) + j, b = a + 1, c = a + segments + 1, d = c + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        flameMesh = new Mesh { name = "Flame" };
        flameMesh.SetVertices(vertices);
        flameMesh.SetColors(colors);
        flameMesh.SetTriangles(triangles, 0);
        flameMesh.RecalculateNormals();
        flameMesh.RecalculateBounds();
        return flameMesh;
    }

    // ----- Points and labels -----

    void DrawFocalPoints(float signedF, bool mirror)
    {
        if (mirror)
        {
            AddMarker(new Vector2(-signedF, 0f), focusColor, "F");
            AddMarker(new Vector2(-2f * signedF, 0f), centreColor, "C");
            AddLabel(new Vector2(0f, -aperture * 1.2f), "P", centreColor);
        }
        else
        {
            AddMarker(new Vector2(-focal, 0f), focusColor, "F₁");
            AddMarker(new Vector2(focal, 0f), focusColor, "F₂");
            AddMarker(new Vector2(-2f * focal, 0f), centreColor, "2F₁");
            AddMarker(new Vector2(2f * focal, 0f), centreColor, "2F₂");
            AddLabel(new Vector2(0f, -aperture * 1.2f), "O", centreColor);
        }
    }

    void AddMarker(Vector2 p, Color color, string label)
    {
        if (Mathf.Abs(p.x) > halfExtent)
            return;

        if (markerCount == markers.Count)
            markers.Add(Part(PrimitiveType.Sphere, "Marker", markerMaterial));

        Transform marker = markers[markerCount++];
        marker.gameObject.SetActive(true);
        marker.localPosition = Local(p);
        marker.localScale = Vector3.one * elementHalfHeight * 0.1f;
        Tint(marker, color);

        AddLabel(p + new Vector2(0f, -aperture * 0.3f), label, color);
    }

    void AddLabel(Vector2 p, string text, Color color, bool always = false)
    {
        if (!showLabels && !always)
            return;

        if (labelCount == labels.Count)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(content, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (labelFont != null)
                tmp.font = labelFont;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.rectTransform.sizeDelta = new Vector2(0.2f, 0.05f);
            labels.Add(tmp);
        }

        TextMeshPro label = labels[labelCount++];
        label.gameObject.SetActive(true);
        label.text = text;
        label.color = color;
        label.fontSize = elementHalfHeight * 2.6f;
        // a little in front of the bench so the mirror/lens doesn't cover it
        label.transform.localPosition = Local(p) + new Vector3(0f, 0f, -elementHalfHeight * 0.2f);
    }

    void DrawMeasure(OpticsResult result, float imageX)
    {
        if (!showMeasure)
        {
            measureLine.gameObject.SetActive(false);
            imageMeasureLine.gameObject.SetActive(false);
            return;
        }

        float y = -aperture * 1.45f;
        SetLine(measureLine, new[] { new Vector2(-dist, y), new Vector2(0f, y) }, objectColor, rayWidth * 0.8f);
        AddLabel(new Vector2(-dist * 0.5f, y - aperture * 0.18f), $"u = {dist:0.#} cm", objectColor, true);

        if (result.atInfinity || Mathf.Abs(imageX) > halfExtent - 4f)
        {
            imageMeasureLine.gameObject.SetActive(false);
            return;
        }
        float y2 = y - aperture * 0.45f;
        SetLine(imageMeasureLine, new[] { new Vector2(0f, y2), new Vector2(imageX, y2) }, imageColor, rayWidth * 0.8f);
        AddLabel(new Vector2(imageX * 0.5f, y2 - aperture * 0.18f), $"v = {result.imageDistance:0.#} cm", imageColor, true);
    }

    // ----- Rays and light -----

    void DrawRays(OpticalElement element)
    {
        float reach = halfExtent * 3f;
        Vector2 flameTip = new Vector2(-dist, candleHeight);
        TraceResult traced = RayTracer.Trace(element, flameTip, fan ? RayMode.Fan : RayMode.Principal, reach, fanRayCount);
        Rect bounds = Rect.MinMaxRect(-halfExtent, -aperture * 2.6f, halfExtent, aperture * 2.6f);

        foreach (RayPath path in traced.paths)
        {
            Color color = RayColor(path.role);
            color.a *= Mathf.Clamp01(path.intensity);
            // a ray that passes outside the real mirror/lens is faded, not bent
            if (!path.hitElement)
                color.a *= 0.3f;

            // the real path is one connected polyline; virtual extensions are separate dashed lines.
            // Points where the ray meets a surface are moved onto the (deepened) surface that is drawn.
            var points = new List<Vector2>();
            int lastReal = path.segments.FindLastIndex(s => !s.isVirtual);
            for (int i = 0; i < path.segments.Count; i++)
            {
                RaySegment s = path.segments[i];
                if (s.isVirtual)
                {
                    if (Clip(OnSurface(s.from), s.to, bounds, out Vector2 a, out Vector2 b))
                    {
                        LineRenderer dash = NextLine(dashLines, ref dashCount, true);
                        SetLine(dash, new[] { a, b }, new Color(color.r, color.g, color.b, color.a * 0.55f), rayWidth * 0.7f);
                    }
                    continue;
                }

                Vector2 start = i == 0 ? s.from : OnSurface(s.from);
                Vector2 end = i == lastReal ? s.to : OnSurface(s.to);
                if (!Clip(start, end, bounds, out Vector2 from, out Vector2 to))
                    continue;
                if (points.Count == 0 || (points[points.Count - 1] - from).sqrMagnitude > 1e-6f)
                    points.Add(from);
                points.Add(to);
            }

            if (points.Count >= 2)
            {
                LineRenderer line = NextLine(rayLines, ref rayCount, false);
                SetLine(line, points.ToArray(), color, rayWidth);
                if (path.hitElement)
                    AddTrack(points, color);
            }
        }
    }

    void AddTrack(List<Vector2> points, Color color)
    {
        var track = new Track { color = color };
        float total = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = Local(points[i]);
            if (i > 0)
                total += Vector3.Distance(track.points[i - 1], p);
            track.points.Add(p);
            track.lengths.Add(total);
        }
        tracks.Add(track);
    }

    Color RayColor(RayRole role)
    {
        switch (role)
        {
            case RayRole.Parallel: return parallelRayColor;
            case RayRole.Central: return centralRayColor;
            case RayRole.Focal: return focalRayColor;
            default: return fanRayColor;
        }
    }

    // Liang-Barsky: keeps the part of a segment inside the rect
    static bool Clip(Vector2 a, Vector2 b, Rect r, out Vector2 ca, out Vector2 cb)
    {
        float t0 = 0f, t1 = 1f;
        Vector2 d = b - a;
        float[] p = { -d.x, d.x, -d.y, d.y };
        float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };

        for (int i = 0; i < 4; i++)
        {
            if (Mathf.Abs(p[i]) < 1e-9f)
            {
                if (q[i] < 0f) { ca = cb = a; return false; }
                continue;
            }
            float t = q[i] / p[i];
            if (p[i] < 0f) t0 = Mathf.Max(t0, t);
            else t1 = Mathf.Min(t1, t);
        }

        ca = a + d * t0;
        cb = a + d * t1;
        return t0 < t1;
    }

    // light pulses run from the candle along every ray, so you can see which way the light goes
    void UpdatePulses(Camera cam)
    {
        int used = 0;
        if (showRays)
        {
            float offset = Mathf.Repeat(Time.time * pulseSpeed, pulseSpacing);
            float size = rayWidth * 5f;
            foreach (Track track in tracks)
            {
                float length = track.lengths[track.lengths.Count - 1];
                int segment = 1;
                for (float s = offset; s < length; s += pulseSpacing)
                {
                    while (segment < track.lengths.Count - 1 && track.lengths[segment] < s)
                        segment++;
                    float l0 = track.lengths[segment - 1], l1 = track.lengths[segment];
                    Vector3 p = Vector3.Lerp(track.points[segment - 1], track.points[segment], l1 > l0 ? (s - l0) / (l1 - l0) : 0f);

                    if (used == pulses.Count)
                        pulses.Add(Part(PrimitiveType.Quad, "Pulse", glowMaterial));
                    Transform pulse = pulses[used++];
                    pulse.gameObject.SetActive(true);
                    pulse.localPosition = p;
                    pulse.localScale = Vector3.one * size;
                    Billboard(pulse, cam);
                    Tint(pulse, new Color(track.color.r, track.color.g, track.color.b, 0.9f));
                }
            }
        }
        for (int i = used; i < pulses.Count; i++)
            pulses[i].gameObject.SetActive(false);
    }

    // ----- Every frame: zoom, flame, touch -----

    void Update()
    {
        if (topic == null || content == null)
            return;

        // smooth zoom when the image moves further away or comes back
        if (animating)
        {
            xScale = Mathf.Lerp(xScale, targetXScale, 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime));
            if (Mathf.Abs(xScale - targetXScale) < targetXScale * 0.002f)
                xScale = targetXScale;
            Redraw();
        }

        // the candle flickers
        float flicker = Mathf.PerlinNoise(Time.time * 6f, 0.3f);
        if (objectCandle.light != null)
            objectCandle.light.intensity = candleLightIntensity * (0.85f + 0.3f * flicker);
        Vector3 flame = objectCandle.flame.localScale;
        objectCandle.flame.localScale = new Vector3(flame.x, flameBaseHeight * (0.94f + 0.12f * flicker), flame.z);

        HandleTouch();
    }

    void HandleTouch()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (Input.touchCount >= 2)
        {
            dragging = false;
            PinchAndTwist();
            return;
        }
        lastPinch = 0f;

        bool down, held;
        Vector2 screen;
        int pointer;
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            down = touch.phase == TouchPhase.Began;
            held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            screen = touch.position;
            pointer = touch.fingerId;
        }
        else
        {
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            screen = Input.mousePosition;
            pointer = -1;
        }

        if (!held)
        {
            dragging = false;
            return;
        }

        // touches on the UI (sliders, chips, buttons) belong to the UI
        if (down && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointer))
            return;

        if (!AxisPoint(cam, screen, out float x, out float side))
            return;

        // start a drag only near the candle, then follow the finger along the rail
        if (down)
            dragging = Mathf.Abs(x + dist) < halfExtent * 0.12f && Mathf.Abs(side) < elementHalfHeight * 2f;

        if (dragging)
        {
            float value = Mathf.Clamp(-x, topic.minObjectDistance, topic.maxObjectDistance);
            value = Mathf.Round(value * 2f) / 2f;
            if (!Mathf.Approximately(value, dist))
                onDrag?.Invoke(value);
        }
    }

    // where the finger points on the upright plane the rays are drawn in (seen face-on):
    // x = position along the axis in cm, side = height above/below the axis in m
    bool AxisPoint(Camera cam, Vector2 screen, out float x, out float side)
    {
        x = side = 0f;
        var plane = new Plane(content.forward, content.position);
        Ray ray = cam.ScreenPointToRay(screen);
        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 local = content.InverseTransformPoint(ray.GetPoint(enter));
        x = local.x / xScale;
        side = local.y;
        return true;
    }

    void PinchAndTwist()
    {
        Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
        Vector2 delta = b.position - a.position;
        float pinch = delta.magnitude;
        float twist = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        if (lastPinch > 0f && pinch > 0f)
        {
            float ratio = Mathf.Clamp(fitHalfWidth * pinch / lastPinch, minHalfWidth, maxHalfWidth) / fitHalfWidth;
            fitHalfWidth *= ratio;
            xScale *= ratio;
            transform.Rotate(Vector3.up, -Mathf.DeltaAngle(lastTwist, twist), Space.Self);
            Redraw();
        }

        lastPinch = pinch;
        lastTwist = twist;
    }

    // labels, the flame glow and the light pulses always face the camera
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || content == null)
            return;

        for (int i = 0; i < labelCount && i < labels.Count; i++)
            Billboard(labels[i].transform, cam);
        Billboard(objectCandle.halo, cam);
        Billboard(imageCandle.halo, cam);
        UpdatePulses(cam);
    }

    static void Billboard(Transform t, Camera cam)
    {
        t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, cam.transform.up);
    }
}
