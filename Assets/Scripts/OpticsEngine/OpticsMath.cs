using UnityEngine;

// Vector forms of the two laws everything else is built on.
public static class OpticsMath
{
    public const float Epsilon = 1e-5f;

    // law of reflection: d' = d - 2(d·n)n
    public static Vector2 Reflect(Vector2 d, Vector2 n)
    {
        return d - 2f * Vector2.Dot(d, n) * n;
    }

    // Snell's law. eta = n1 / n2. Returns false on total internal reflection.
    public static bool Refract(Vector2 d, Vector2 n, float eta, out Vector2 refracted)
    {
        // the normal has to face the incoming ray
        if (Vector2.Dot(d, n) > 0f)
            n = -n;

        float cosI = -Vector2.Dot(d, n);
        float k = 1f - eta * eta * (1f - cosI * cosI);
        if (k < 0f)
        {
            refracted = Vector2.zero;
            return false;
        }

        refracted = (eta * d + (eta * cosI - Mathf.Sqrt(k)) * n).normalized;
        return true;
    }

    // ray origin + t * dir against a circle. t1 <= t2. dir must be normalised.
    public static bool IntersectCircle(Vector2 origin, Vector2 dir, Vector2 centre, float radius, out float t1, out float t2)
    {
        Vector2 oc = origin - centre;
        float b = Vector2.Dot(oc, dir);
        float c = Vector2.Dot(oc, oc) - radius * radius;
        float disc = b * b - c;
        if (disc < 0f)
        {
            t1 = t2 = 0f;
            return false;
        }

        float s = Mathf.Sqrt(disc);
        t1 = -b - s;
        t2 = -b + s;
        return true;
    }
}
