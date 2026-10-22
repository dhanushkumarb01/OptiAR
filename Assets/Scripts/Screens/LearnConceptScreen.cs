using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LearnConceptScreen : UIScreen
{
    public Button backBtn;
    public Button startBtn;
    public ScrollRect scroll;

    [Header("Content")]
    public TMP_Text topicText;
    public TMP_Text titleText;
    public RayDiagram diagram;
    public TMP_Text summaryText;
    public GameObject behaviourTemplate; // first row of the KEY BEHAVIOURS list, copied for the others
    public TMP_Text formulaTitleText;
    public TMP_Text formulaText;
    public TMP_Text magnificationText;

    List<GameObject> behaviourRows = new List<GameObject>();

    protected override void OnSetup()
    {
        backBtn.onClick.AddListener(app.Back);
        startBtn.onClick.AddListener(app.OpenScan);
        behaviourRows.Add(behaviourTemplate);
    }

    protected override void OnShow()
    {
        TopicData topic = app.topic;
        if (topic == null)
            return;

        topicText.text = topic.displayName.ToUpperInvariant();
        topicText.color = topic.accentColor;
        titleText.text = topic.subtitle;
        summaryText.text = topic.summary;
        formulaTitleText.text = topic.formulaHeading;
        formulaText.text = topic.formula;
        magnificationText.text = topic.magnificationFormula;

        ShowBehaviours(topic.keyBehaviours);
        diagram.Draw(topic, topic.defaultFocalLength, topic.conceptObjectDistance, true, true, true);

        scroll.verticalNormalizedPosition = 1f;
    }

    void ShowBehaviours(string[] behaviours)
    {
        // make enough rows
        while (behaviourRows.Count < behaviours.Length)
        {
            GameObject row = Instantiate(behaviourTemplate, behaviourTemplate.transform.parent);
            row.name = "Behaviour" + (behaviourRows.Count + 1);
            behaviourRows.Add(row);
        }

        for (int i = 0; i < behaviourRows.Count; i++)
        {
            GameObject row = behaviourRows[i];
            bool used = i < behaviours.Length;
            row.SetActive(used);
            if (used)
            {
                row.transform.Find("Index/Number").GetComponent<TMP_Text>().text = (i + 1).ToString();
                row.transform.Find("Text").GetComponent<TMP_Text>().text = behaviours[i];
            }
        }
    }
}
