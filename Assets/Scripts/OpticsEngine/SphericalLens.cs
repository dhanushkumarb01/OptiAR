using System.Collections.Generic;
using UnityEngine;

// Symmetric thick lens made of two spherical glass surfaces: biconvex (f > 0) or biconcave (f < 0).
// Rays refract at both surfaces with Snell's law; inside the glass they can be totally internally reflected.
public class SphericalLens : OpticalElement
{
    public const float CrownGlass = 1.52f;

    public readonly float refractiveIndex;
    public readonly float thickness; // at the centre
    public readonly SphericalSurface front;
    public readonly SphericalSurface back;

    const int MaxInternalBounces = 4;

    // Like a school lab lens: a thick lens's principal planes move away from the centre, which would pull
    // the traced image away from the thin-lens readouts, so the half-height is limited to this many focal lengths.
    public const float MaxAperturePerFocal = 0.15f;
    const float MinThicknessPerFocal = 0.01f;

    public override bool IsMirror => false;

    public float Radius => Mathf.Abs(front.radius);

    public SphericalLens(float signedFocal, float aperture, float refractiveIndex = CrownGlass)
    {
        FocalLength = signedFocal;
        this.refractiveIndex = refractiveIndex;
        bool converging = signedFocal > 0f;
        float n = refractiveIndex;
        float power = 1f / Mathf.Abs(signedFocal);
        float minThickness = MinThicknessPerFocal * Mathf.Abs(signedFocal);
        aperture = Mathf.Min(aperture, MaxAperturePerFocal * Mathf.Abs(signedFocal));

        // thin-lens guess from the lensmaker's equation, then correct for thickness so the
        // effective focal length matches f: 1/f = (n-1)(1/R1 - 1/R2 + (n-1)t/(n R1 R2))
        float r = 2f * (n - 1f) / power;
        float t = minThickness;
        for (int i = 0; i < 8; i++)
        {
            aperture = Mathf.Min(aperture, r * 0.85f);
            float sag = r - Mathf.Sqrt(r * r - aperture * aperture);
            t = converging ? 2f * sag + minThickness : minThickness;
            float k = power * t / n;
            r = (n - 1f) * (1f + Mathf.Sqrt(converging ? Mathf.Max(0f, 1f - k) : 1f + k)) / power;
        }

        Aperture = Mathf.Min(aperture, r * 0.85f);
        thickness = t;
        float sign = converging ? 1f : -1f;
        front = new SphericalSurface(-t * 0.5f, sign * r, Aperture);
        back = new SphericalSurface(t * 0.5f, -sign * r, Aperture);
    }

    public override bool Interact(OpticalRay ray, List<Vector2> hits, out OpticalRay outgoing)
    {
        outgoing = ray;
        float n = refractiveIndex;

        // into the glass through the front surface
        if (!front.Intersect(ray, out _, out Vector2 point, out Vector2 normal))
            return false;
        if (!OpticsMath.Refract(ray.direction, normal, 1f / n, out Vector2 dir))
            return false;
        hits.Add(point);

        var inside = new OpticalRay(point, dir, ray.intensity);
        for (int bounce = 0; bounce <= MaxInternalBounces; bounce++)
        {
            if (!NextSurfaceFromInside(inside, out point, out normal))
                return false; // left through the rim

            hits.Add(point);
            if (OpticsMath.Refract(inside.direction, normal, n, out dir))
            {
                outgoing = new OpticalRay(point, dir, inside.intensity);
                return true;
            }

            // total internal reflection: stays in the glass
            inside = new OpticalRay(point, OpticsMath.Reflect(inside.direction, normal), inside.intensity);
        }

        return false;
    }

    bool NextSurfaceFromInside(OpticalRay ray, out Vector2 point, out Vector2 normal)
    {
        bool hitBack = back.Intersect(ray, out float tBack, out Vector2 pBack, out Vector2 nBack);
        bool hitFront = front.Intersect(ray, out float tFront, out Vector2 pFront, out Vector2 nFront);

        if (hitBack && (!hitFront || tBack <= tFront))
        {
            point = pBack;
            normal = nBack;
            return true;
        }
        if (hitFront)
        {
            point = pFront;
            normal = nFront;
            return true;
        }

        point = normal = Vector2.zero;
        return false;
    }
}
