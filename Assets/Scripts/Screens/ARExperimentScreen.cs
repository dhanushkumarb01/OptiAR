using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The placed experiment: ray diagram, image readouts and controls.
// Used in Learn mode (Done) and Test mode (Submit Answer).
// With AR the experiment is a 3D bench on the real surface; without it, the 2D diagram.
//
// Everything physics lets you change can be adjusted: focal length f, radius of curvature R,
// object distance u, image distance v, object height h, refractive index n (lenses) and the aperture.
// They are linked: mirror R = 2f, thin lens R = 2(n - 1)f (both surfaces the same), 1/u + 1/v = 1/f.
public class ARExperimentScreen : UIScreen
{
    [Header("Buttons")]
    public Button backBtn;
    public Button doneBtn; // "Done" in Learn, "Submit Answer" in Test
    public TMP_Text doneText;
    public GameObject submitIcon;
    public Button resetBtn;

    [Header("Header")]
    public TMP_Text codeText;
    public TMP_Text tagText;
    public GameObject challengeCard;
    public TMP_Text challengeKicker;
    public TMP_Text challengeTitle;
    public TMP_Text challengeText;

    [Header("Diagram")]
    public RayDiagram diagram;
    public RectTransform diagramFrame; // moved down in Test mode to make room for the challenge card
    public float learnTopInset = 150f;
    public float testTopInset = 380f;

    [Header("Readouts")]
    public TMP_Text natureText;
    public TMP_Text orientationText;
    public TMP_Text sizeText;
    public TMP_Text distanceText;

    [Header("Controls")]
    public Slider focalSlider;
    public TMP_Text focalText;
    [Tooltip("Optional. Its own slider for the object distance (it can also be dragged in the diagram)")]
    public Slider objectSlider;
    public TMP_Text objectText;
    public ToggleChip raysToggle;
    public ToggleChip labelsToggle;
    public ToggleChip measureToggle;
    [Tooltip("Optional. Many rays across the whole mirror/lens (AR only).")]
    public ToggleChip fanToggle;
    [Tooltip("Optional. Exact side view instead of the slightly turned bench (AR only).")]
    public ToggleChip sideViewToggle;

    [Header("Adjust panel")]
    public ToggleChip adjustToggle;
    public GameObject adjustPanel;
    public ParamRow radiusRow;
    public ParamRow objectRow;
    public ParamRow imageRow;
    public ParamRow heightRow;
    public ParamRow indexRow;     // lenses only
    public ParamRow apertureRow;

    [Header("AR")]
    public OpticsBenchView bench;
    [Tooltip("Opaque backgrounds that would hide the camera feed. Hidden while AR runs.")]
    public GameObject[] hideInAR;

    [Header("Readout colours")]
    public Color realColor = new Color(0.20f, 0.83f, 0.60f);
    public Color virtualColor = new Color(0.65f, 0.55f, 0.98f);
    public Color invertedColor = new Color(0.96f, 0.45f, 0.71f);
    public Color erectColor = new Color(0.13f, 0.83f, 0.93f);
    public Color neutralColor = new Color(0.58f, 0.64f, 0.72f);

    const float MinIndex = 1.3f, MaxIndex = 2f;

    // experiment values (kept when the student retries a challenge)
    float focal;
    float dist;
    float objectHeight = 2f;   // cm
    float refractiveIndex = 1.5f;
    float apertureFraction = 1f;
    bool rays = true;
    bool labels = true;
    bool measure = true;
    bool fan;
    bool sideView;
    bool adjust;

    OpticsResult result;
    bool ignoreSlider;
    bool useAR;
    Transform benchHome;

    bool FocalLocked => app.challenge != null && app.challenge.lockFocal;
    bool ObjectLocked => app.challenge != null && app.challenge.lockObject;

    protected override void OnSetup()
    {
        backBtn.onClick.AddListener(app.Back);
        doneBtn.onClick.AddListener(Done);
        resetBtn.onClick.AddListener(ResetExperiment);
        focalSlider.onValueChanged.AddListener(OnFocalChanged);
        if (objectSlider != null)
            objectSlider.onValueChanged.AddListener(OnObjectChanged);
        diagram.onDrag = SetDistance;

        raysToggle.onChanged = on => { rays = on; Refresh(); };
        labelsToggle.onChanged = on => { labels = on; Refresh(); };
        measureToggle.onChanged = on => { measure = on; Refresh(); };

        if (fanToggle != null)
            fanToggle.onChanged = on => { fan = on; Refresh(); };
        if (sideViewToggle != null)
            sideViewToggle.onChanged = on => { sideView = on; TurnBench(); };
        if (adjustToggle != null)
            adjustToggle.onChanged = on => { adjust = on; adjustPanel.SetActive(on); };

        if (radiusRow != null)
        {
            radiusRow.onChanged = OnRadiusChanged;
            objectRow.onChanged = v => SetDistance(Mathf.Round(v * 2f) / 2f);
            imageRow.onChanged = OnImageChanged;
            heightRow.onChanged = v => { objectHeight = Mathf.Round(v * 10f) / 10f; Refresh(); };
            indexRow.onChanged = OnIndexChanged;
            apertureRow.onChanged = v => { apertureFraction = v; Refresh(); };
        }

        if (bench != null)
        {
            bench.onDrag = SetDistance;
            benchHome = bench.transform.parent;
            bench.gameObject.SetActive(false);
        }
    }

    // back to the starting values for the current topic/challenge
    public void ResetValues()
    {
        TopicData topic = app.topic;
        if (topic == null)
            return;

        Challenge challenge = app.challenge;
        focal = challenge != null && challenge.startFocalLength > 0f ? challenge.startFocalLength : topic.defaultFocalLength;
        dist = challenge != null && challenge.startObjectDistance > 0f ? challenge.startObjectDistance : topic.defaultObjectDistance;
        refractiveIndex = 1.5f;
        apertureFraction = 1f;
        // a candle about half as tall as the mirror/lens, so all three textbook rays hit it
        objectHeight = Mathf.Round(OpticsBenchView.ApertureFor(topic, focal, apertureFraction) * 0.5f * 10f) / 10f;
        rays = true;
        labels = true;
        measure = true;
        fan = false;
    }

    protected override void OnShow()
    {
        TopicData topic = app.topic;
        if (topic == null)
            return;

        bool testing = app.challenge != null;
        codeText.text = topic.shortCode;
        tagText.text = $"AR — {topic.displayName}";
        doneText.text = testing ? "Submit Answer" : "Done";
        submitIcon.SetActive(testing);

        challengeCard.SetActive(testing);
        diagramFrame.offsetMax = new Vector2(diagramFrame.offsetMax.x, -(testing ? testTopInset : learnTopInset));
        if (testing)
        {
            if (challengeKicker != null)
                challengeKicker.text = topic.QuestionCount > 1 ? $"QUESTION {app.questionIndex + 1} OF {topic.QuestionCount}" : "CHALLENGE";
            challengeTitle.text = app.challenge.title;
            challengeText.text = app.challenge.instruction;
        }

        ignoreSlider = true;
        focalSlider.minValue = topic.minFocalLength;
        focalSlider.maxValue = topic.maxFocalLength;
        focalSlider.value = focal;
        focalSlider.interactable = !FocalLocked;
        ignoreSlider = false;

        raysToggle.SetOn(rays);
        labelsToggle.SetOn(labels);
        measureToggle.SetOn(measure);
        if (adjustToggle != null)
        {
            adjustToggle.SetOn(adjust);
            adjustPanel.SetActive(adjust);
        }

        ShowBench(app.UseAR);
        Refresh();
    }

    protected override void OnHide()
    {
        ShowBench(false);
    }

    // the 3D bench sits on the AR anchor; the 2D diagram is used without AR
    void ShowBench(bool on)
    {
        useAR = on && bench != null;
        diagramFrame.gameObject.SetActive(!useAR);
        foreach (GameObject go in hideInAR)
            go.SetActive(!useAR);
        if (fanToggle != null)
        {
            fanToggle.gameObject.SetActive(useAR);
            fanToggle.SetOn(fan);
        }
        if (sideViewToggle != null)
        {
            sideViewToggle.gameObject.SetActive(useAR);
            sideViewToggle.SetOn(sideView);
        }

        if (bench == null)
            return;

        // keep the bench out of the anchor when it isn't used, so it isn't destroyed with it
        bench.transform.SetParent(useAR ? app.ar.Anchor : benchHome, false);
        bench.transform.localPosition = Vector3.zero;
        TurnBench();
        bench.gameObject.SetActive(useAR);
        if (useAR)
            bench.FitToView(app.topic, Camera.main);
    }

    // turned a little so the mirror's face and the lens disc show, or exactly side-on
    void TurnBench()
    {
        if (bench != null)
            bench.transform.localRotation = Quaternion.Euler(0f, sideView ? 0f : bench.viewAngle, 0f);
    }

    void Done()
    {
        if (app.challenge != null)
            app.SubmitAnswer(focal, dist, result);
        else
            app.FinishExperiment();
    }

    void ResetExperiment()
    {
        ResetValues();
        OnShow();
    }

    // ----- Linked quantities -----

    void OnFocalChanged(float value)
    {
        if (ignoreSlider)
            return;

        SetFocal(Mathf.Round(value * 2f) / 2f);
    }

    void OnObjectChanged(float value)
    {
        if (ignoreSlider)
            return;

        SetDistance(Mathf.Round(value * 2f) / 2f);
    }

    void SetFocal(float value)
    {
        if (FocalLocked)
            return;
        focal = Mathf.Clamp(value, app.topic.minFocalLength, app.topic.maxFocalLength);
        Refresh();
    }

    // mirror: R = 2f.  thin lens with two equal surfaces: R = 2(n - 1)f
    float RadiusPerFocal => app.topic.isMirror ? 2f : 2f * (refractiveIndex - 1f);

    void OnRadiusChanged(float radius)
    {
        SetFocal(Mathf.Round(radius / RadiusPerFocal * 2f) / 2f);
    }

    // same glass shape (R stays), different glass: the focal length changes
    void OnIndexChanged(float n)
    {
        if (FocalLocked)
            return;
        float radius = RadiusPerFocal * focal;
        refractiveIndex = Mathf.Round(n * 100f) / 100f;
        focal = Mathf.Clamp(radius / RadiusPerFocal, app.topic.minFocalLength, app.topic.maxFocalLength);
        Refresh();
    }

    // choose where the image should be: the object moves to make it (1/u = 1/f - 1/v)
    void OnImageChanged(float v)
    {
        if (Mathf.Abs(v) < 0.5f)
            return;
        float f = app.topic.SignedFocal(focal);
        float u = v * f / (v - f);
        // some image positions are impossible for this mirror/lens (or need the object off the bench)
        if (float.IsNaN(u) || u < app.topic.minObjectDistance || u > app.topic.maxObjectDistance)
        {
            Refresh();
            return;
        }
        SetDistance(Mathf.Round(u * 2f) / 2f);
    }

    void SetDistance(float value)
    {
        if (ObjectLocked)
            return;
        dist = value;
        Refresh();
    }

    // recalculate the image and update everything on screen
    void Refresh()
    {
        TopicData topic = app.topic;
        if (topic == null)
            return;

        float aperture = OpticsBenchView.ApertureFor(topic, focal, apertureFraction);
        objectHeight = Mathf.Clamp(objectHeight, 0.2f, aperture * 0.9f);
        result = Optics.Solve(topic.SignedFocal(focal), dist);

        focalText.text = $"{focal:0.#} cm";
        objectText.text = ObjectLocked ? $"{dist:0.#} cm (fixed)" : $"{dist:0.#} cm";
        ignoreSlider = true;
        focalSlider.value = focal;
        if (objectSlider != null)
        {
            objectSlider.minValue = topic.minObjectDistance;
            objectSlider.maxValue = topic.maxObjectDistance;
            objectSlider.value = dist;
            objectSlider.interactable = !ObjectLocked;
        }
        ignoreSlider = false;
        ShowReadouts();
        ShowAdjustPanel(topic, aperture);

        if (useAR)
        {
            bench.fan = fan;
            bench.objectHeight = objectHeight;
            bench.refractiveIndex = refractiveIndex;
            bench.apertureFraction = apertureFraction;
            bench.Draw(topic, focal, dist, rays, labels, measure);
        }
        else
        {
            diagram.Draw(topic, focal, dist, rays, labels, measure);
        }
    }

    void ShowAdjustPanel(TopicData topic, float aperture)
    {
        if (radiusRow == null)
            return;

        bool lockF = FocalLocked, lockU = ObjectLocked;
        float k = RadiusPerFocal;
        radiusRow.Show(k * topic.minFocalLength, k * topic.maxFocalLength, k * focal, $"{k * focal:0.#} cm", !lockF);
        objectRow.Show(topic.minObjectDistance, topic.maxObjectDistance, dist, $"{dist:0.#} cm", !lockU);

        float range = topic.maxObjectDistance * 3f;
        string v = result.atInfinity ? "∞ (at the focus)" : $"{result.imageDistance:0.#} cm  ({(result.IsReal ? "real" : "virtual")})";
        imageRow.Show(-range, range, result.atInfinity ? range : Mathf.Clamp(result.imageDistance, -range, range), v, !lockU);

        string imageHeight = result.atInfinity ? "" : $"  →  h′ = {result.magnification * objectHeight:0.#} cm";
        heightRow.Show(0.2f, aperture * 0.9f, objectHeight, $"{objectHeight:0.#} cm{imageHeight}");

        indexRow.gameObject.SetActive(!topic.isMirror);
        indexRow.Show(MinIndex, MaxIndex, refractiveIndex, $"{refractiveIndex:0.00}  ({GlassName(refractiveIndex)})", !lockF);

        apertureRow.Show(0.3f, 1f, apertureFraction, $"{2f * aperture:0.#} cm across");
    }

    static string GlassName(float n)
    {
        if (n < 1.4f) return "like water";
        if (n < 1.56f) return "crown glass";
        if (n < 1.75f) return "flint glass";
        return "dense flint";
    }

    void ShowReadouts()
    {
        if (result.atInfinity)
        {
            SetText(natureText, "—", neutralColor);
            SetText(orientationText, "—", neutralColor);
            SetText(sizeText, "∞", sizeText.color);
            SetText(distanceText, "∞", neutralColor);
            return;
        }

        SetText(natureText, result.IsReal ? "Real" : "Virtual", result.IsReal ? realColor : virtualColor);
        SetText(orientationText, result.IsInverted ? "Inverted" : "Erect", result.IsInverted ? invertedColor : erectColor);
        sizeText.text = $"{result.Size:0.00}×";
        SetText(distanceText, $"{Mathf.Abs(result.imageDistance):0}cm", neutralColor);
    }

    void SetText(TMP_Text label, string text, Color color)
    {
        label.text = text;
        label.color = color;
    }
}
