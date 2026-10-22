using System.Collections.Generic;
using UnityEngine;

// A mirror or lens centred on the origin.
public abstract class OpticalElement
{
    // signed: positive = converging (concave mirror, convex lens)
    public float FocalLength { get; protected set; }

    // half-height of the element in cm
    public float Aperture { get; protected set; }

    public abstract bool IsMirror { get; }

    // Sends a ray at the element. Adds every point where the ray meets a surface to hits
    // and returns the ray leaving the element. False if the ray misses (or is lost inside).
    public abstract bool Interact(OpticalRay ray, List<Vector2> hits, out OpticalRay outgoing);
}

// Part of a sphere, cut to the aperture. Radius is signed: the centre is at vertexX + radius.
public struct SphericalSurface
{
    public float vertexX;
    public float radius;
    public float aperture;

    public SphericalSurface(float vertexX, float radius, float aperture)
    {
        this.vertexX = vertexX;
        this.radius = radius;
        this.aperture = aperture;
    }

    public Vector2 Centre => new Vector2(vertexX + radius, 0f);

    // x of the surface at height y
    public float SurfaceX(float y)
    {
        float r = Mathf.Abs(radius);
        float sag = r - Mathf.Sqrt(Mathf.Max(0f, r * r - y * y));
        return vertexX + Mathf.Sign(radius) * sag;
    }

    // nearest hit in front of the ray that lies on the cap (not the far side of the sphere)
    public bool Intersect(OpticalRay ray, out float t, out Vector2 point, out Vector2 normal)
    {
        t = 0f;
        point = normal = Vector2.zero;

        Vector2 centre = Centre;
        if (!OpticsMath.IntersectCircle(ray.origin, ray.direction, centre, Mathf.Abs(radius), out float t1, out float t2))
            return false;

        if (!OnCap(ray, t1, centre, out point))
        {
            if (!OnCap(ray, t2, centre, out point))
                return false;
            t = t2;
        }
        else
        {
            t = t1;
        }

        normal = (point - centre) / Mathf.Abs(radius);
        return true;
    }

    bool OnCap(OpticalRay ray, float t, Vector2 centre, out Vector2 point)
    {
        point = ray.At(t);
        if (t <= OpticsMath.Epsilon * 10f)
            return false;
        if (Mathf.Abs(point.y) > aperture)
            return false;
        // the cap is the half of the sphere that contains the vertex
        return (point.x - centre.x) * -radius > 0f;
    }
}
