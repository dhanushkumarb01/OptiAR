using System.Collections.Generic;
using UnityEngine;

// Opens the screens, handles Back, and remembers what the user picked.
public class AppManager : MonoBehaviour
{
    public List<TopicData> topics = new List<TopicData>();

    [Header("Screens")]
    public SplashScreen splashScreen;
    public HomeScreen homeScreen;
    public LearnTopicsScreen learnScreen;
    public LearnConceptScreen conceptScreen;
    public ARScanScreen scanScreen;
    public ARExperimentScreen experimentScreen;
    public TestScreen testScreen;

    [Header("AR")]
    public ARController ar;

    // what the user is doing right now (not saved in the scene)
    [System.NonSerialized] public TopicData topic;
    [System.NonSerialized] public Challenge challenge; // null in Learn mode
    [System.NonSerialized] public int questionIndex;   // which of the topic's questions
    [System.NonSerialized] public bool showResult;
    [System.NonSerialized] public bool passed;
    [System.NonSerialized] public string feedback;

    // true when the experiment is placed on a real surface (ARCore); false uses the 2D diagram
    public bool UseAR => ar != null && ar.Supported && ar.Anchor != null;

    UIScreen screen;
    Stack<UIScreen> history = new Stack<UIScreen>();

    void Awake()
    {
        // portrait only
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;

        UIScreen[] screens = { splashScreen, homeScreen, learnScreen, conceptScreen, scanScreen, experimentScreen, testScreen };
        foreach (UIScreen s in screens)
        {
            s.Setup(this);
            s.Hide();
        }
    }

    void Start()
    {
        // ask ARCore early so the answer is ready by the time the user starts an experiment
        if (ar != null)
            StartCoroutine(ar.CheckSupport());

        Show(splashScreen, false, false);
    }

    void Update()
    {
        // Android back button
        if (Input.GetKeyDown(KeyCode.Escape))
            Back();
    }

    // hide the current screen and show the new one
    void Show(UIScreen next, bool remember = true, bool animate = true)
    {
        if (screen != null && screen != next)
        {
            if (remember)
                history.Push(screen);
            screen.Hide();
        }

        // AR only runs on the scan and experiment screens
        if (ar != null && ar.Running && next != scanScreen && next != experimentScreen)
            ar.End();

        screen = next;
        next.Show(animate);
    }

    public void Back()
    {
        if (history.Count > 0)
            Show(history.Pop(), false);
    }

    void SetHistory(params UIScreen[] screens)
    {
        history.Clear();
        foreach (UIScreen s in screens)
            history.Push(s);
    }

    // ----- Learn -----

    public void OpenHome()
    {
        history.Clear();
        challenge = null;
        showResult = false;
        Show(homeScreen, false);
    }

    public void OpenLearn()
    {
        challenge = null;
        showResult = false;
        Show(learnScreen);
    }

    public void OpenConcept(TopicData t)
    {
        challenge = null;
        topic = t;
        Show(conceptScreen);
    }

    public void OpenScan()
    {
        if (topic == null)
            return;

        experimentScreen.ResetValues();
        Show(scanScreen);
    }

    public void OpenExperiment()
    {
        if (topic == null)
            return;

        // the scan screen is skipped when going Back
        Show(experimentScreen, screen != scanScreen);
    }

    public void FinishExperiment()
    {
        SetHistory(homeScreen);
        Show(learnScreen, false);
    }

    // ----- Test -----

    public void OpenTest()
    {
        showResult = false;
        Show(testScreen);
    }

    public void StartChallenge(TopicData t)
    {
        topic = t;
        // carry on from the first question not answered yet
        questionIndex = 0;
        while (questionIndex < t.QuestionCount - 1 && IsAnswered(t, questionIndex))
            questionIndex++;
        challenge = t.Question(questionIndex);
        showResult = false;
        OpenScan();
    }

    // focal length (cm) and object distance (cm) the student set, and the image they made
    public void SubmitAnswer(float focalLength, float objectDistance, OpticsResult result)
    {
        if (challenge == null || topic == null)
            return;

        passed = Optics.CheckAnswer(challenge, focalLength, objectDistance, result, out feedback);
        showResult = true;
        if (passed)
        {
            PlayerPrefs.SetInt(QuestionKey(topic, questionIndex), 1);
            if (AnsweredCount(topic) == topic.QuestionCount)
                CompleteTopic(topic);
            PlayerPrefs.Save();
        }

        SetHistory(homeScreen);
        Show(testScreen, false);
    }

    public bool HasNextQuestion => topic != null && questionIndex < topic.QuestionCount - 1;

    public void NextQuestion()
    {
        if (!HasNextQuestion)
            return;

        questionIndex++;
        challenge = topic.Question(questionIndex);
        showResult = false;
        experimentScreen.ResetValues();
        SetHistory(homeScreen, testScreen);
        if (ar != null && ar.Supported)
            Show(scanScreen, false);
        else
            Show(experimentScreen, false);
    }

    static string QuestionKey(TopicData t, int index) => $"OptiAR.Question.{t.name}.{index}";

    public bool IsAnswered(TopicData t, int index) => PlayerPrefs.GetInt(QuestionKey(t, index), 0) == 1;

    public int AnsweredCount(TopicData t)
    {
        int count = 0;
        for (int i = 0; i < t.QuestionCount; i++)
        {
            if (IsAnswered(t, i))
                count++;
        }
        return count;
    }

    // go back to the same experiment after a fail
    public void Retry()
    {
        if (challenge == null)
            return;

        showResult = false;
        SetHistory(homeScreen, testScreen);
        // AR stopped on the result screen, so find the surface again (the values are kept)
        if (ar != null && ar.Supported)
            Show(scanScreen, false);
        else
            Show(experimentScreen, false);
    }

    public void OpenChallengeList()
    {
        showResult = false;
        challenge = null;
        SetHistory(homeScreen);
        Show(testScreen, false);
    }

    // ----- Progress (saved with PlayerPrefs) -----

    public bool IsDone(TopicData t)
    {
        return PlayerPrefs.GetInt("OptiAR.TopicComplete." + t.name, 0) == 1;
    }

    public int DoneCount()
    {
        int count = 0;
        foreach (TopicData t in topics)
        {
            if (t != null && IsDone(t))
                count++;
        }
        return count;
    }

    void CompleteTopic(TopicData t)
    {
        PlayerPrefs.SetInt("OptiAR.TopicComplete." + t.name, 1);
        PlayerPrefs.Save();
    }
}
