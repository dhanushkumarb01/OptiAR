using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Checks the ray tracer against the textbook (paraxial) formula and real-world effects.
public class OpticsEngineTests
{
    const float Focal = 15f;
    const float Aperture = 6f;

    // signed focal (+ converging) and whether it's a mirror, for the four topics
    static readonly object[] Elements =
    {
        new object[] { "Concave mirror", Focal, true },
        new object[] { "Convex mirror", -Focal, true },
        new object[] { "Convex lens", Focal, false },
        new object[] { "Concave lens", -Focal, false },
    };

    static readonly float[] DistancesInFocals = { 0.5f, 1.5f, 2f, 3f };

    static OpticalElement Create(float signedFocal, bool mirror)
    {
        return mirror ? new SphericalMirror(signedFocal, Aperture) : new SphericalLens(signedFocal, Aperture);
    }

    // same "real is positive" formula as Optics.Solve: 1/u + 1/v = 1/f
    static float ThinImageDistance(float f, float u) => u * f / (u - f);

    // traces two rays that stay very close to the axis and returns where they (or their extensions) meet
    static Vector2 ParaxialImage(OpticalElement element, float u, out bool real)
    {
        float h = Aperture * 0.004f;
        Vector2 tip = new Vector2(-u, h);
        RayPath a = RayTracer.TraceRay(element, tip, (new Vector2(0f, h * 0.5f) - tip).normalized, RayRole.Fan, 100f);
        RayPath b = RayTracer.TraceRay(element, tip, (new Vector2(0f, -h * 0.5f) - tip).normalized, RayRole.Fan, 100f);
        Assert.IsTrue(a.hitElement && b.hitElement, "paraxial rays must hit the element");

        Vector2 p = a.outgoing.origin, d = a.outgoing.direction;
        Vector2 q = b.outgoing.origin, e = b.outgoing.direction;
        float cross = d.x * e.y - d.y * e.x;
        Vector2 qp = q - p;
        float t = (qp.x * e.y - qp.y * e.x) / cross;
        real = t > 0f;
        return p + d * t;
    }

    [TestCaseSource(nameof(Elements))]
    public void ParaxialRaysMatchTheMirrorAndLensFormula(string name, float f, bool mirror)
    {
        OpticalElement element = Create(f, mirror);
        // a thick lens is not exactly a thin lens, so it gets a little more room
        float tolerance = mirror ? 0.02f : 0.04f;

        foreach (float k in DistancesInFocals)
        {
            float u = Mathf.Abs(f) * k;
            float v = ThinImageDistance(f, u);
            float m = -v / u;

            Vector2 image = ParaxialImage(element, u, out bool real);
            // mirrors fold the light back, so a real image (v > 0) is in front (-x)
            float expectedX = mirror ? -v : v;
            float expectedY = m * Aperture * 0.004f;

            Assert.AreEqual(expectedX, image.x, Mathf.Abs(v) * tolerance, $"{name}, u = {u}: image position");
            Assert.AreEqual(v > 0f, real, $"{name}, u = {u}: real/virtual");
            Assert.AreEqual(expectedY < 0f, image.y < 0f, $"{name}, u = {u}: inverted/erect");
            Assert.AreEqual(Mathf.Abs(m), Mathf.Abs(image.y / (Aperture * 0.004f)), Mathf.Abs(m) * tolerance * 2f, $"{name}, u = {u}: magnification");
        }
    }

    [TestCase(-15f, true)]
    [TestCase(-15f, false)]
    public void DivergingElementsAlwaysMakeVirtualErectDiminishedImages(float f, bool mirror)
    {
        OpticalElement element = Create(f, mirror);
        for (float u = 3f; u <= 60f; u += 3f)
        {
            Vector2 image = ParaxialImage(element, u, out bool real);
            Assert.IsFalse(real, $"u = {u}");
            Assert.Greater(image.y, 0f, $"u = {u}: erect");
            Assert.Less(image.y, Aperture * 0.004f, $"u = {u}: diminished");
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PrincipalRaysFindTheSameImageAsTheFormula(bool mirror)
    {
        OpticalElement element = Create(Focal, mirror);
        TraceResult result = RayTracer.Trace(element, new Vector2(-30f, 2f), RayMode.Principal, 100f);

        Assert.AreEqual(3, result.paths.Count);
        Assert.IsTrue(result.hasImage);
        Assert.IsTrue(result.imageIsReal);
        // object at 2F: image at 2F, same size, inverted (textbook rays are not paraxial, so allow aberration)
        Assert.AreEqual(mirror ? -30f : 30f, result.image.x, 30f * 0.08f);
        Assert.AreEqual(-2f, result.image.y, 2f * 0.15f);
    }

    [Test]
    public void VirtualImagesGetDashedExtensions()
    {
        OpticalElement element = Create(-Focal, false);
        TraceResult result = RayTracer.Trace(element, new Vector2(-20f, 2f), RayMode.Principal, 100f);

        Assert.IsTrue(result.hasImage);
        Assert.IsFalse(result.imageIsReal);
        foreach (RayPath path in result.paths)
            Assert.IsTrue(path.segments.Exists(s => s.isVirtual), path.role.ToString());
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WideRaysFocusCloserThanNarrowRays(bool mirror)
    {
        // spherical aberration: parallel light far from the axis crosses it nearer the element
        OpticalElement element = Create(Focal, mirror);
        float narrow = AxisCrossingOfParallelRay(element, element.Aperture * 0.05f);
        float wide = AxisCrossingOfParallelRay(element, element.Aperture * 0.95f);

        Assert.AreEqual(Focal, narrow, Focal * 0.03f, "narrow rays focus at F");
        Assert.Less(wide, narrow - 0.05f, "wide rays focus short of F");
    }

    static float AxisCrossingOfParallelRay(OpticalElement element, float height)
    {
        RayPath path = RayTracer.TraceRay(element, new Vector2(-50f, height), Vector2.right, RayRole.Parallel, 100f);
        Assert.IsTrue(path.hitElement);
        Assert.IsTrue(RayTracer.AxisCrossing(path.outgoing, out float x));
        return Mathf.Abs(x);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ObjectAtTheFocusSendsOutParallelLight(bool mirror)
    {
        OpticalElement element = Create(Focal, mirror);
        TraceResult result = RayTracer.Trace(element, new Vector2(-Focal, 0.05f), RayMode.Fan, 100f, 5);

        // the five rays leave (nearly) parallel: no image, or one very far away
        var directions = new List<Vector2>();
        foreach (RayPath path in result.paths)
        {
            if (path.hitElement)
                directions.Add(path.outgoing.direction);
        }
        Assert.GreaterOrEqual(directions.Count, 2);
        float angle = Vector2.Angle(directions[1], directions[directions.Count - 2]);
        Assert.Less(angle, 1.5f);
    }

    [Test]
    public void SteepRaysInsideGlassAreTotallyInternallyReflected()
    {
        // critical angle for crown glass is about 41 degrees
        Vector2 normal = Vector2.up;
        Vector2 shallow = new Vector2(Mathf.Sin(30f * Mathf.Deg2Rad), -Mathf.Cos(30f * Mathf.Deg2Rad));
        Vector2 steep = new Vector2(Mathf.Sin(60f * Mathf.Deg2Rad), -Mathf.Cos(60f * Mathf.Deg2Rad));

        Assert.IsTrue(OpticsMath.Refract(shallow, normal, SphericalLens.CrownGlass, out _));
        Assert.IsFalse(OpticsMath.Refract(steep, normal, SphericalLens.CrownGlass, out _));
    }

    [Test]
    public void RefractionFollowsSnellsLaw()
    {
        Vector2 normal = Vector2.up;
        float incidence = 35f * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Sin(incidence), -Mathf.Cos(incidence));

        Assert.IsTrue(OpticsMath.Refract(d, normal, 1f / SphericalLens.CrownGlass, out Vector2 t));
        float sinOut = Mathf.Abs(t.x);
        Assert.AreEqual(Mathf.Sin(incidence), SphericalLens.CrownGlass * sinOut, 1e-4f);
    }

    [Test]
    public void ReflectionAngleEqualsIncidenceAngle()
    {
        Vector2 d = new Vector2(1f, -1f).normalized;
        Vector2 r = OpticsMath.Reflect(d, Vector2.up);
        Assert.AreEqual(d.x, r.x, 1e-6f);
        Assert.AreEqual(-d.y, r.y, 1e-6f);
    }

    [TestCase(5f)]
    [TestCase(15f)]
    [TestCase(30f)]
    public void LensSurfacesStayInsideTheGlass(float f)
    {
        var lens = new SphericalLens(f, Aperture);
        // the two surfaces must not cross at the rim
        Assert.Greater(lens.back.SurfaceX(lens.Aperture) - lens.front.SurfaceX(lens.Aperture), 0f);
        Assert.Less(lens.Aperture, lens.Radius);
    }
}
