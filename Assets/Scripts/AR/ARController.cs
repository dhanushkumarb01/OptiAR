using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Wraps AR Foundation (ARCore on Android) so the screens don't talk to it directly.
// AR only runs while the Scan and Experiment screens are open.
public class ARController : MonoBehaviour
{
    public ARSession session;
    public GameObject xrOrigin; // XR Origin (Mobile AR): AR camera + the managers below
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;
    public Transform reticle;
    [Tooltip("Camera used when AR is off. Turned off while the AR camera runs.")]
    public Camera appCamera;

    [Header("Placement")]
    [Tooltip("The experiment stands this far straight ahead of the learner (metres), wherever they aim")]
    public float placeDistance = 0.6f;

    public bool Supported { get; private set; }
    public bool Checked { get; private set; }
    public bool Running { get; private set; }
    public Transform Anchor => anchor != null ? anchor.transform : null;

    ARAnchor anchor;
    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        SetRunning(false);
    }

    // Finds out if this phone can run ARCore, and offers to install Google Play Services for AR.
    public IEnumerator CheckSupport()
    {
        if (Checked)
            yield break;

        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            yield return ARSession.CheckAvailability();

        if (ARSession.state == ARSessionState.NeedsInstall)
            yield return ARSession.Install();

        Supported = ARSession.state >= ARSessionState.Ready;
        Checked = true;
    }

    public void Begin()
    {
        if (!Supported)
            return;
        SetRunning(true);
        ShowScanVisuals(true);
    }

    public void End()
    {
        ClearAnchor();
        SetRunning(false);
    }

    void SetRunning(bool on)
    {
        Running = on;
        // only one enabled camera may be tagged MainCamera, so switch the app camera first
        if (appCamera != null)
            appCamera.gameObject.SetActive(!on);
        if (xrOrigin != null)
            xrOrigin.SetActive(on);
        if (session != null)
            session.enabled = on;
        if (!on && reticle != null)
            reticle.gameObject.SetActive(false);
    }

    // planes and the reticle are only shown while looking for a surface
    public void ShowScanVisuals(bool on)
    {
        if (reticle != null && !on)
            reticle.gameObject.SetActive(false);
        if (planeManager == null)
            return;

        planeManager.enabled = on;
        foreach (ARPlane plane in planeManager.trackables)
            plane.gameObject.SetActive(on);
    }

    // Where the experiment would go: standing on the surface found under the middle of the screen,
    // straight ahead of the learner and turned to face them.
    public bool TryGetCenterHit(out Pose pose, out TrackableId planeId)
    {
        pose = default;
        planeId = TrackableId.invalidId;

        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool hit = Running && raycastManager.Raycast(centre, hits, TrackableType.PlaneWithinPolygon);
        if (hit)
        {
            pose = InFrontOfViewer(hits[0].pose);
            planeId = hits[0].trackableId;
        }

        if (reticle != null)
        {
            reticle.gameObject.SetActive(hit);
            if (hit)
                reticle.SetPositionAndRotation(pose.position, pose.rotation);
        }
        return hit;
    }

    // Keeps the surface height but moves the point to placeDistance straight ahead of the learner,
    // and turns it so the optical axis runs left to right across their view (a front view).
    Pose InFrontOfViewer(Pose surface)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return surface;

        Vector3 up = surface.up;
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.ProjectOnPlane(cam.transform.up, up); // looking straight down
        forward.Normalize();

        // the point on the surface right below the camera
        Vector3 camera = cam.transform.position;
        Vector3 foot = camera - up * Vector3.Dot(camera - surface.position, up);
        return new Pose(foot + forward * placeDistance, Quaternion.LookRotation(forward, up));
    }

    // Anchors the experiment to the surface. The pose comes from TryGetCenterHit.
    public Transform PlaceAnchor(Pose pose, TrackableId planeId)
    {
        ClearAnchor();

        ARPlane plane = planeManager.GetPlane(planeId);
        if (plane != null)
            anchor = anchorManager.AttachAnchor(plane, pose);

        if (anchor == null)
        {
            var go = new GameObject("ExperimentAnchor");
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            anchor = go.AddComponent<ARAnchor>();
        }

        ShowScanVisuals(false);
        return anchor.transform;
    }

    public void ClearAnchor()
    {
        if (anchor != null)
            Destroy(anchor.gameObject);
        anchor = null;
    }

    // short help when ARCore loses track of the world
    public string TrackingHint()
    {
        if (ARSession.state == ARSessionState.SessionTracking)
            return null;

        switch (ARSession.notTrackingReason)
        {
            case NotTrackingReason.Initializing:
            case NotTrackingReason.Relocalizing:
                return "Move your phone slowly to look around";
            case NotTrackingReason.InsufficientLight:
                return "It's too dark. Turn on a light";
            case NotTrackingReason.InsufficientFeatures:
                return "Point at a table with some texture";
            case NotTrackingReason.ExcessiveMotion:
                return "Move your phone more slowly";
            case NotTrackingReason.CameraUnavailable:
                return "The camera isn't available";
            default:
                return null;
        }
    }
}
