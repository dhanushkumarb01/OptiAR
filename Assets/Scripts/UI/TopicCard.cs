using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A clickable topic card (used on Home, Learn and Test). The screen handles the click.
[RequireComponent(typeof(Button))]
public class TopicCard : MonoBehaviour
{
    public TopicData topic;
    public Image image;
    public TMP_Text titleText;
    public TMP_Text descriptionText; // optional
    public TMP_Text detailText;      // optional
    public GameObject doneBadge;
    public bool accentTitle;         // colour the title with the topic colour

    // pass null to keep the text that is already on the card
    public void Show(string title, string description, string detail, bool done)
    {
        if (topic.illustration != null)
            image.sprite = topic.illustration;
        if (accentTitle)
            titleText.color = topic.accentColor;

        if (title != null)
            titleText.text = title;
        if (descriptionText != null && description != null)
            descriptionText.text = description;
        if (detailText != null && detail != null)
            detailText.text = detail;

        doneBadge.SetActive(done);
    }
}
