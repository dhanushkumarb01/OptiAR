using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pill-shaped on/off button (Rays / Labels / Measure).
[RequireComponent(typeof(Button))]
public class ToggleChip : MonoBehaviour
{
    public Image background;
    public Image border;
    public TMP_Text label;
    public Color onBackground = new Color(0.10f, 0.21f, 0.42f);
    public Color offBackground = new Color(0.09f, 0.13f, 0.21f);
    public Color onBorder = new Color(0.23f, 0.51f, 0.96f);
    public Color offBorder = new Color(0.15f, 0.21f, 0.31f);
    public Color onText = new Color(0.88f, 0.93f, 1f);
    public Color offText = new Color(0.45f, 0.53f, 0.65f);

    public System.Action<bool> onChanged;

    bool isOn = true;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
        UpdateColors();
    }

    void Toggle()
    {
        SetOn(!isOn);
        onChanged?.Invoke(isOn);
    }

    public void SetOn(bool on)
    {
        isOn = on;
        UpdateColors();
    }

    void UpdateColors()
    {
        background.color = isOn ? onBackground : offBackground;
        border.color = isOn ? onBorder : offBorder;
        label.color = isOn ? onText : offText;
    }
}
