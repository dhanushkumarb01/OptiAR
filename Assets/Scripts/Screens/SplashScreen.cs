using UnityEngine;
using UnityEngine.UI;

public class SplashScreen : UIScreen
{
    public Button startBtn;

    [Header("Emblem diagram")]
    public RayDiagram emblem;
    public TopicData emblemTopic;
    public float emblemFocal = 15f;
    public float emblemDistance = 30f;

    protected override void OnSetup()
    {
        startBtn.onClick.AddListener(app.OpenHome);
    }

    protected override void OnShow()
    {
        emblem.Draw(emblemTopic, emblemFocal, emblemDistance, true, true, false);
    }
}
