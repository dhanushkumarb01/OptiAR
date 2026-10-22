using System.Collections.Generic;
using UnityEngine;

// The engine works in the meridional plane: x along the principal axis (light starts travelling +x),
// y is height, units are cm, and the mirror pole / lens centre is at the origin.

public struct OpticalRay
{
    public Vector2 origin;
    public Vector2 direction; // normalised
    public float intensity;

    public OpticalRay(Vector2 origin, Vector2 direction, float intensity = 1f)
    {
        this.origin = origin;
        this.direction = direction.normalized;
        this.intensity = intensity;
    }

    public Vector2 At(float t) => origin + direction * t;
}

public struct RaySegment
{
    public Vector2 from;
    public Vector2 to;
    public bool isVirtual; // dashed extension back to a virtual image

    public RaySegment(Vector2 from, Vector2 to, bool isVirtual = false)
    {
        this.from = from;
        this.to = to;
        this.isVirtual = isVirtual;
    }
}

public enum RayRole { Parallel, Central, Focal, Fan }

// One traced ray, as straight pieces from the object to the end of the bench.
public class RayPath
{
    public RayRole role;
    public float intensity = 1f;
    public bool hitElement;
    public readonly List<RaySegment> segments = new List<RaySegment>();

    // the ray after the last surface (only valid when hitElement)
    public OpticalRay outgoing;
}
