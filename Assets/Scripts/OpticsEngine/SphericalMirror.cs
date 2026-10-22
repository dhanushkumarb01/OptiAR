using System.Collections.Generic;
using UnityEngine;

// Concave (f > 0) or convex (f < 0) spherical mirror facing -x, pole at the origin. R = 2f.
// Because the surface is a real sphere, not a parabola, wide rays focus short of F (spherical aberration).
public class SphericalMirror : OpticalElement
{
    public readonly SphericalSurface surface;

    public override bool IsMirror => true;

    public float Radius => Mathf.Abs(surface.radius);

    public SphericalMirror(float signedFocal, float aperture)
    {
        FocalLength = signedFocal;
        float r = 2f * Mathf.Abs(signedFocal);
        // a cap can't be taller than its sphere
        Aperture = Mathf.Min(aperture, r * 0.9f);
        // concave: centre of curvature in front (-x). convex: behind (+x).
        surface = new SphericalSurface(0f, -2f * signedFocal, Aperture);
    }

    public override bool Interact(OpticalRay ray, List<Vector2> hits, out OpticalRay outgoing)
    {
        outgoing = ray;
        if (!surface.Intersect(ray, out _, out Vector2 point, out Vector2 normal))
            return false;

        hits.Add(point);
        outgoing = new OpticalRay(point, OpticsMath.Reflect(ray.direction, normal), ray.intensity);
        return true;
    }
}
