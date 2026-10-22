using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

// Scans for a surface with ARCore, then lets the user place the experiment.
// Phones without ARCore (and the Editor) fall back to "finding" a surface after scanTime seconds
// and use the 2D diagram.
public class ARScanScreen : UIScreen
{
    public Button backBtn;
    public Button placeBtn;
    public float scanTime = 2.5f;

    [Header("States")]
    public GameObject scanningState;
    public GameObject detectedState;
    public TMP_Text instructionText;
    public Graphic[] corners;
    public Color scanningColor = new Color(0.85f, 0.9f, 1f, 0.75f);
    public Color detectedColor = new Color(0.13f, 0.83f, 0.93f);

    [Header("Scan line")]
    public RectTransform scanLine;
    public RectTransform scanArea;
    public float scanLinePeriod = 2.4f;

    [Header("AR")]
    [Tooltip("Opaque backgrounds that would hide the camera feed. Hidden while AR runs.")]
    public GameObject[] hideInAR;

    bool scanning;
    bool found;
    float scanStart;
    bool useAR;
    string placeText;
    Pose hitPose;
    TrackableId hitPlane;

    protected override void OnSetup()
    {
        backBtn.onClick.AddListener(app.Back);
        placeBtn.onClick.AddListener(Place);
    }

    protected override void OnShow()
    {
        placeText = $"Tap to place your {app.topic.displayName} experiment";
        instructionText.text = placeText;
        SetFound(false);

        found = false;
        scanning = true;
        scanStart = Time.unscaledTime;

        useAR = app.ar != null && app.ar.Supported;
        if (useAR)
        {
            app.ar.ClearAnchor();
            app.ar.Begin();
        }
        foreach (GameObject go in hideInAR)
            go.SetActive(!useAR);
    }

    protected override void OnHide()
    {
        scanning = false;
        if (useAR)
            app.ar.ShowScanVisuals(false);
        // the experiment screen hides them again if it uses AR
        foreach (GameObject go in hideInAR)
            go.SetActive(true);
    }

    void Update()
    {
        if (useAR)
            UpdateAR();
        else if (scanning && Time.unscaledTime - scanStart >= scanTime)
        {
            // no ARCore: pretend we found a surface
            scanning = false;
            found = true;
            SetFound(true);
        }

        // move the scan line up and down
        if (scanningState.activeSelf)
        {
            float t = Mathf.PingPong(Time.unscaledTime / scanLinePeriod * 2f, 1f);
            float height = scanArea.rect.height;
            scanLine.anchoredPosition = new Vector2(0f, Mathf.Lerp(-height * 0.5f, height * 0.5f, Mathf.SmoothStep(0f, 1f, t)));
        }
    }

    void UpdateAR()
    {
        bool hit = app.ar.TryGetCenterHit(out hitPose, out hitPlane);
        if (hit != found)
        {
            found = hit;
            SetFound(hit);
        }

        string hint = app.ar.TrackingHint();
        instructionText.text = hint ?? placeText;

        // tapping anywhere on the camera view places the experiment too
        if (found && TappedCameraView())
            Place();
    }

    static bool TappedCameraView()
    {
        if (Input.touchCount != 1)
            return false;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
            return false;
        return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject(touch.fingerId);
    }

    void SetFound(bool value)
    {
        scanningState.SetActive(!value);
        detectedState.SetActive(value);
        foreach (Graphic corner in corners)
            corner.color = value ? detectedColor : scanningColor;
    }

    void Place()
    {
        if (!found)
            return;

        if (useAR)
            app.ar.PlaceAnchor(hitPose, hitPlane);
        app.OpenExperiment();
    }
}
