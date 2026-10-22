using UnityEngine.UI;

public class LearnTopicsScreen : UIScreen
{
    public Button backBtn;
    public TopicCard[] cards;

    protected override void OnSetup()
    {
        backBtn.onClick.AddListener(app.Back);

        foreach (TopicCard card in cards)
            card.GetComponent<Button>().onClick.AddListener(() => app.OpenConcept(card.topic));
    }

    protected override void OnShow()
    {
        foreach (TopicCard card in cards)
            card.Show(card.topic.displayName, card.topic.subtitle, null, app.IsDone(card.topic));
    }
}
