using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomeScreen : UIScreen
{
    public Button learnBtn;
    public Button testBtn;
    public TopicCard[] cards;
    public TMP_Text completeText;
    public TMP_Text percentText;
    public RectTransform progressFill;
    public ScrollRect scroll;

    protected override void OnSetup()
    {
        learnBtn.onClick.AddListener(app.OpenLearn);
        testBtn.onClick.AddListener(app.OpenTest);

        foreach (TopicCard card in cards)
            card.GetComponent<Button>().onClick.AddListener(() => app.OpenConcept(card.topic));
    }

    protected override void OnShow()
    {
        scroll.verticalNormalizedPosition = 1f;

        foreach (TopicCard card in cards)
            card.Show(card.topic.displayName, null, null, app.IsDone(card.topic));

        int done = app.DoneCount();
        int total = app.topics.Count;
        float progress = total > 0 ? (float)done / total : 0f;

        completeText.text = $"{done}/{total} complete";
        percentText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        progressFill.anchorMax = new Vector2(progress, progressFill.anchorMax.y);
        progressFill.gameObject.SetActive(progress > 0f);
    }
}
