using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the challenge list, or the pass/fail result after Submit Answer.
public class TestScreen : UIScreen
{
    public Button backBtn;

    [Header("Challenge list")]
    public GameObject listState;
    public TopicCard[] cards;
    public TMP_Text passedText;

    [Header("Result")]
    public GameObject resultState;
    public Image resultBadge;
    public IconGraphic resultIcon;
    public TMP_Text titleText;
    public TMP_Text challengeText;
    public TMP_Text feedbackText;
    public Button primaryBtn;
    public TMP_Text primaryText;
    public Button secondaryBtn;
    public TMP_Text secondaryText;
    public Color passColor = new Color(0.13f, 0.77f, 0.37f);
    public Color failColor = new Color(0.96f, 0.45f, 0.71f);

    protected override void OnSetup()
    {
        backBtn.onClick.AddListener(app.Back);
        primaryBtn.onClick.AddListener(OnPrimary);
        secondaryBtn.onClick.AddListener(OnSecondary);

        foreach (TopicCard card in cards)
            card.GetComponent<Button>().onClick.AddListener(() => app.StartChallenge(card.topic));
    }

    protected override void OnShow()
    {
        listState.SetActive(!app.showResult);
        resultState.SetActive(app.showResult);

        if (app.showResult)
            ShowResult();
        else
            ShowChallenges();
    }

    void ShowChallenges()
    {
        foreach (TopicCard card in cards)
        {
            TopicData topic = card.topic;
            int count = topic.QuestionCount;
            string summary = count > 1
                ? $"{count} questions · {app.AnsweredCount(topic)} answered. Set up the experiment to answer each one."
                : topic.Question(0).instruction;
            card.Show(count > 1 ? $"{topic.displayName} Quiz" : topic.Question(0).title, summary, topic.displayName.ToUpperInvariant(), app.IsDone(topic));
        }

        passedText.text = $"{app.DoneCount()}/{app.topics.Count} passed";
    }

    void ShowResult()
    {
        bool passed = app.passed;
        Color color = passed ? passColor : failColor;

        resultBadge.color = new Color(color.r, color.g, color.b, 0.07f);
        resultIcon.Shape = passed ? IconShape.Check : IconShape.Cross;
        resultIcon.color = color;

        titleText.text = passed ? "Correct!" : "Not Quite";
        int count = app.topic.QuestionCount;
        challengeText.text = count > 1 ? $"Question {app.questionIndex + 1} of {count}: {app.challenge.title}" : app.challenge.title;

        // after a correct answer, show how to work it out; after a wrong one, only what is wrong (so they can try again)
        string explanation = app.challenge.explanation;
        feedbackText.text = passed && !string.IsNullOrEmpty(explanation) ? $"{app.feedback}\n\n{explanation}" : app.feedback;

        primaryText.text = passed ? (app.HasNextQuestion ? "Next Question" : "Back to Home") : "Try Again";
        secondaryText.text = passed ? "More Challenges" : "Back to Home";
    }

    // Fail: Try Again.  Pass: Next Question (or Back to Home after the last one).
    void OnPrimary()
    {
        if (app.showResult && !app.passed)
            app.Retry();
        else if (app.showResult && app.HasNextQuestion)
            app.NextQuestion();
        else
            app.OpenHome();
    }

    // Pass: More Challenges.  Fail: Back to Home.
    void OnSecondary()
    {
        if (app.showResult && app.passed)
            app.OpenChallengeList();
        else
            app.OpenHome();
    }
}
