using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One adjustable quantity in the experiment: a name, its value and a slider.
public class ParamRow : MonoBehaviour
{
    public TMP_Text label;
    public TMP_Text value;
    public Slider slider;

    public System.Action<float> onChanged;

    bool ignore;

    void Awake()
    {
        slider.onValueChanged.AddListener(v =>
        {
            if (!ignore)
                onChanged?.Invoke(v);
        });
    }

    // updates the row without calling onChanged
    public void Show(float min, float max, float current, string text, bool editable = true)
    {
        ignore = true;
        slider.minValue = min;
        slider.maxValue = Mathf.Max(min + 0.001f, max);
        slider.value = current;
        ignore = false;
        value.text = text;
        slider.interactable = editable;
    }
}
