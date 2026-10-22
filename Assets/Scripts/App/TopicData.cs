using UnityEngine;

// All the content for one topic (mirror or lens). One asset per topic in Assets/Data.
[CreateAssetMenu(fileName = "TopicData", menuName = "OptiAR/Topic Data")]
public class TopicData : ScriptableObject
{
    [Header("Topic")]
    public string displayName;
    public string shortCode; // shown on the AR badge, e.g. "CM"
    public bool isMirror;
    public bool converging;  // concave mirror and convex lens
    public Color accentColor = Color.white;
    public Sprite illustration;

    [Header("Concept")]
    public string subtitle;
    [TextArea(2, 5)] public string summary;
    [TextArea(1, 3)] public string[] keyBehaviours;
    public string formulaHeading;
    public string formula;
    public string magnificationFormula;

    [Header("Experiment (cm)")]
    public float defaultFocalLength = 15f;
    public float minFocalLength = 5f;
    public float maxFocalLength = 30f;
    public float defaultObjectDistance = 25f;
    public float minObjectDistance = 3f;
    public float maxObjectDistance = 60f;
    public float conceptObjectDistance = 40f; // used for the diagram on the concept screen

    [Header("Test")]
    public Challenge challenge = new Challenge();
    [Tooltip("The Test mode questions, asked in order. Empty = just the challenge above.")]
    public Challenge[] questions = new Challenge[0];

    public int QuestionCount => questions != null && questions.Length > 0 ? questions.Length : 1;

    public Challenge Question(int index)
    {
        return questions != null && questions.Length > 0 ? questions[Mathf.Clamp(index, 0, questions.Length - 1)] : challenge;
    }

    // focal length is positive for converging and negative for diverging
    public float SignedFocal(float f)
    {
        return converging ? Mathf.Abs(f) : -Mathf.Abs(f);
    }
}

public enum Nature { Any, Real, Virtual }
public enum Orientation { Any, Erect, Inverted }

// A Test mode question: the student sets up the experiment to answer it, then submits.
[System.Serializable]
public class Challenge
{
    public string title;
    [TextArea(2, 4)] public string instruction;
    public Nature nature = Nature.Any;
    public Orientation orientation = Orientation.Any;

    [Tooltip("0 = no limit")] public float minMagnification;
    [Tooltip("0 = no limit")] public float maxMagnification;
    [Tooltip("Object distance at the start. 0 = topic default")] public float startObjectDistance;

    [Header("Set-up")]
    [Tooltip("Focal length at the start (cm). 0 = topic default")] public float startFocalLength;
    [Tooltip("The focal length (and R, n) can't be changed")] public bool lockFocal;
    [Tooltip("The object can't be moved")] public bool lockObject;

    [Header("Targets (0 = not checked)")]
    [Tooltip("Object distance u (cm)")] public float targetObjectDistance;
    [Tooltip("Check the image distance below")] public bool checkImageDistance;
    [Tooltip("Image distance v (cm): + real, - virtual")] public float targetImageDistance;
    [Tooltip("Focal length (cm)")] public float targetFocalLength;
    [Tooltip("Image size |m| (times the object)")] public float targetSize;
    [Tooltip("Allowed error for distances (cm)")] public float tolerance = 1f;

    [Tooltip("Shown after the answer: how to work it out")]
    [TextArea(2, 6)] public string explanation;
}
