using System.Collections.Generic;
using UnityEngine;

// The image formed by a mirror or lens.
public struct OpticsResult
{
    public bool atInfinity;
    public float imageDistance; // positive = real, negative = virtual
    public float magnification; // negative = inverted

    public bool IsReal => !atInfinity && imageDistance > 0f;
    public bool IsInverted => !atInfinity && magnification < 0f;
    public float Size => Mathf.Abs(magnification);
}

// Sign convention used inside the app ("real is positive"):
//   u > 0 for the real object, v > 0 for a real image, f > 0 for converging (concave mirror, convex lens).
//   1/u + 1/v = 1/f   and   m = -v/u   (the same formula for mirrors and lenses)
// The Concept screen shows the Cartesian (NCERT) forms. They give the same answers with
//   lens:   u_c = -u, v_c = v,  f_c = f    ->  1/v_c - 1/u_c = 1/f_c,  m = v_c/u_c
//   mirror: u_c = -u, v_c = -v, f_c = -f   ->  1/v_c + 1/u_c = 1/f_c,  m = -v_c/u_c
public static class Optics
{
    // f is signed (see TopicData.SignedFocal), u is the object distance
    public static OpticsResult Solve(float f, float u)
    {
        u = Mathf.Max(0.01f, u);

        // object at the focus -> image at infinity
        if (Mathf.Abs(u - f) < 0.01f)
            return new OpticsResult { atInfinity = true };

        float v = u * f / (u - f);
        if (float.IsNaN(v) || float.IsInfinity(v))
            return new OpticsResult { atInfinity = true };

        return new OpticsResult { imageDistance = v, magnification = -v / u };
    }

    // returns true if the image matches the challenge
    public static bool CheckAnswer(Challenge challenge, OpticsResult result, out string feedback)
    {
        return CheckAnswer(challenge, 0f, 0f, result, out feedback);
    }

    // Also checks the set-up the student chose: focal length f (cm, unsigned) and object distance u (cm).
    public static bool CheckAnswer(Challenge challenge, float focalLength, float u, OpticsResult result, out string feedback)
    {
        var problems = new List<string>();

        if (challenge.targetObjectDistance > 0f && Mathf.Abs(u - challenge.targetObjectDistance) > challenge.tolerance)
            problems.Add($"the candle is not in the right place yet (u = {u:0.#} cm)");
        if (challenge.targetFocalLength > 0f && Mathf.Abs(focalLength - challenge.targetFocalLength) > Mathf.Max(0.5f, challenge.tolerance * 0.5f))
            problems.Add($"the focal length is {focalLength:0.#} cm, which doesn't give the asked result");
        if (challenge.checkImageDistance && !result.atInfinity && Mathf.Abs(result.imageDistance - challenge.targetImageDistance) > challenge.tolerance)
            problems.Add($"the image is at v = {result.imageDistance:0.#} cm, not {challenge.targetImageDistance:0.#} cm");
        if (challenge.targetSize > 0f && !result.atInfinity && Mathf.Abs(result.Size - challenge.targetSize) > challenge.targetSize * 0.06f + 0.02f)
            problems.Add($"the image is {result.Size:0.00}× the candle, not {challenge.targetSize:0.##}×");

        if (result.atInfinity)
        {
            problems.Add("the object is at the focus, so the image forms at infinity");
        }
        else
        {
            if (challenge.nature == Nature.Real && !result.IsReal)
                problems.Add("the image must be real");
            if (challenge.nature == Nature.Virtual && result.IsReal)
                problems.Add("the image must be virtual");
            if (challenge.orientation == Orientation.Inverted && !result.IsInverted)
                problems.Add("the image must be inverted");
            if (challenge.orientation == Orientation.Erect && result.IsInverted)
                problems.Add("the image must be erect");
            // small tolerance so a value right on the limit (e.g. exactly 0.5×) still counts
            if (challenge.minMagnification > 0f && result.Size < challenge.minMagnification - 0.001f)
                problems.Add($"the image must be at least {challenge.minMagnification:0.##}× the object size");
            if (challenge.maxMagnification > 0f && result.Size > challenge.maxMagnification + 0.001f)
                problems.Add($"the image must be at most {challenge.maxMagnification:0.##}× the object size");
        }

        bool passed = problems.Count == 0;
        if (passed)
        {
            string nature = result.IsReal ? "real" : "virtual";
            string orientation = result.IsInverted ? "inverted" : "erect";
            feedback = $"Your image is {nature}, {orientation} and {result.Size:0.00}× the object size.";
        }
        else
        {
            string text = string.Join("; ", problems);
            feedback = char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";
        }
        return passed;
    }
}
