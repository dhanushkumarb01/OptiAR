using System.Collections.Generic;
using UnityEngine;

public enum RayMode
{
    Principal, // the three textbook rays (parallel, through the pole/centre, through the focus)
    Fan        // many rays across the whole aperture, shows spherical aberration
}

public class TraceResult
{
    public readonly List<RayPath> paths = new List<RayPath>();

    // where the traced rays (or their extensions) meet; false when they leave parallel (image at infinity)
    public bool hasImage;
    public Vector2 image;
    public bool imageIsReal;
}

// Traces rays from the tip of the object through a mirror or lens.
public static class RayTracer
{
    // past this distance (in focal lengths) the rays count as parallel
    const float InfinityFocals = 60f;

    // tip: object tip, e.g. (-u, h). reach: how far rays are drawn after the element.
    public static TraceResult Trace(OpticalElement element, Vector2 tip, RayMode mode, float reach, int fanCount = 15)
    {
        var result = new TraceResult();

        if (mode == RayMode.Principal)
        {
            float f = element.FocalLength;
            Add(result, element, tip, Vector2.right, RayRole.Parallel, reach);
            Add(result, element, tip, -tip, RayRole.Central, reach);

            // converging: through F in front. diverging: aimed at F behind.
            Vector2 focus = new Vector2(-f, 0f);
            Vector2 toFocus = focus - tip;
            // object inside F: the ray comes from F through the object
            if (toFocus.x < 0f)
                toFocus = -toFocus;
            if (toFocus.x > OpticsMath.Epsilon)
                Add(result, element, tip, toFocus, RayRole.Focal, reach);
        }
        else
        {
            float a = element.Aperture * 0.96f;
            for (int i = 0; i < fanCount; i++)
            {
                float y = fanCount > 1 ? Mathf.Lerp(-a, a, i / (fanCount - 1f)) : 0f;
                Add(result, element, tip, new Vector2(0f, y) - tip, RayRole.Fan, reach);
            }
        }

        FindImage(result, Mathf.Abs(element.FocalLength));
        AddVirtualExtensions(result);
        return result;
    }

    // Traces one ray. Rays that miss the element carry straight on.
    public static RayPath TraceRay(OpticalElement element, Vector2 start, Vector2 direction, RayRole role, float reach)
    {
        var path = new RayPath { role = role };
        var ray = new OpticalRay(start, direction);
        var hits = new List<Vector2>();

        if (!element.Interact(ray, hits, out OpticalRay outgoing))
        {
            // missed, or lost through the lens rim
            Vector2 from = start;
            foreach (Vector2 hit in hits)
            {
                path.segments.Add(new RaySegment(from, hit));
                from = hit;
            }
            if (hits.Count == 0)
                path.segments.Add(new RaySegment(start, ray.At(reach)));
            return path;
        }

        Vector2 previous = start;
        foreach (Vector2 hit in hits)
        {
            path.segments.Add(new RaySegment(previous, hit));
            previous = hit;
        }

        path.hitElement = true;
        path.outgoing = outgoing;
        path.intensity = outgoing.intensity;
        path.segments.Add(new RaySegment(outgoing.origin, outgoing.At(reach)));
        return path;
    }

    static void Add(TraceResult result, OpticalElement element, Vector2 tip, Vector2 direction, RayRole role, float reach)
    {
        result.paths.Add(TraceRay(element, tip, direction.normalized, role, reach));
    }

    // Least-squares meeting point of the outgoing rays (as lines, so it finds virtual images too).
    static void FindImage(TraceResult result, float focal)
    {
        float a11 = 0f, a12 = 0f, a22 = 0f, b1 = 0f, b2 = 0f;
        int count = 0;

        foreach (RayPath path in result.paths)
        {
            if (!path.hitElement)
                continue;

            Vector2 p = path.outgoing.origin;
            Vector2 d = path.outgoing.direction;
            // projector onto the line's normal: I - d d^T
            float m11 = 1f - d.x * d.x, m12 = -d.x * d.y, m22 = 1f - d.y * d.y;
            a11 += m11; a12 += m12; a22 += m22;
            b1 += m11 * p.x + m12 * p.y;
            b2 += m12 * p.x + m22 * p.y;
            count++;
        }

        result.hasImage = false;
        if (count < 2)
            return;

        float det = a11 * a22 - a12 * a12;
        if (Mathf.Abs(det) < 1e-9f)
            return;

        var image = new Vector2((a22 * b1 - a12 * b2) / det, (a11 * b2 - a12 * b1) / det);
        if (Mathf.Abs(image.x) > InfinityFocals * focal)
            return;

        // real if the light actually travels forward to the meeting point
        float along = 0f;
        foreach (RayPath path in result.paths)
        {
            if (path.hitElement)
                along += Vector2.Dot(image - path.outgoing.origin, path.outgoing.direction);
        }

        result.hasImage = true;
        result.image = image;
        result.imageIsReal = along > 0f;
    }

    // dashed lines from where each ray leaves the element back to the virtual image
    static void AddVirtualExtensions(TraceResult result)
    {
        if (!result.hasImage || result.imageIsReal)
            return;

        foreach (RayPath path in result.paths)
        {
            if (!path.hitElement)
                continue;

            float t = Vector2.Dot(result.image - path.outgoing.origin, path.outgoing.direction);
            if (t < 0f)
                path.segments.Add(new RaySegment(path.outgoing.origin, path.outgoing.At(t), true));
        }
    }

    // where a ray leaving the element crosses the axis (y = 0); used to show the spread of focus points
    public static bool AxisCrossing(OpticalRay ray, out float x)
    {
        x = 0f;
        if (Mathf.Abs(ray.direction.y) < OpticsMath.Epsilon)
            return false;
        float t = -ray.origin.y / ray.direction.y;
        x = ray.origin.x + ray.direction.x * t;
        return true;
    }
}
