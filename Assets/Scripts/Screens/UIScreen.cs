using System.Collections;
using UnityEngine;

// Base class for every screen. Fades the screen in and gives it access to the AppManager.
[RequireComponent(typeof(CanvasGroup))]
public class UIScreen : MonoBehaviour
{
    public float fadeDuration = 0.18f;
    public float slideDistance = 24f;
    public RectTransform slideTarget; // optional: slides up while fading in

    protected AppManager app;

    CanvasGroup group;
    Coroutine fade;
    Vector2 slideStart;

    public void Setup(AppManager manager)
    {
        app = manager;
        group = GetComponent<CanvasGroup>();
        if (slideTarget != null)
            slideStart = slideTarget.anchoredPosition;
        OnSetup();
    }

    public void Show(bool animate)
    {
        gameObject.SetActive(true);
        OnShow();

        if (fade != null)
            StopCoroutine(fade);

        if (animate && fadeDuration > 0f)
        {
            fade = StartCoroutine(FadeIn());
        }
        else
        {
            group.alpha = 1f;
            if (slideTarget != null)
                slideTarget.anchoredPosition = slideStart;
        }
    }

    public void Hide()
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        if (slideTarget != null)
            slideTarget.anchoredPosition = slideStart;

        if (gameObject.activeSelf)
            OnHide();

        gameObject.SetActive(false);
    }

    // called once at the start: hook up buttons here
    protected virtual void OnSetup() { }

    // called every time the screen opens: fill in the content here
    protected virtual void OnShow() { }

    protected virtual void OnHide() { }

    IEnumerator FadeIn()
    {
        group.alpha = 0f;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / fadeDuration));
            group.alpha = t;
            if (slideTarget != null)
                slideTarget.anchoredPosition = slideStart + Vector2.down * (slideDistance * (1f - t));
            yield return null;
        }

        group.alpha = 1f;
        if (slideTarget != null)
            slideTarget.anchoredPosition = slideStart;
        fade = null;
    }
}
