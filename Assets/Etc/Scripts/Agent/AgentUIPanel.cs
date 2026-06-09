using UnityEngine;
using UnityEngine.UI;

public class AgentUIPanel : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Text agentNameText;

    [Header("Input")]
    [SerializeField] private InputField inputField;

    [Header("Skill UI")]
    [SerializeField] private AgentSkillGaugeUI skillGaugeUI;

    [Header("Options")]
    [SerializeField] private bool autoFindReferences = true;

    private int boundIndex = -1;
    private GameObject boundAgentObject;
    private AgentController boundAgentController;

    public int BoundIndex => boundIndex;
    public GameObject BoundAgentObject => boundAgentObject;
    public AgentController BoundAgentController => boundAgentController;
    public InputField InputField => inputField;

    private void Awake()
    {
        CacheReferences();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (!autoFindReferences)
            return;

        if (inputField == null)
            inputField = GetComponentInChildren<InputField>(true);

        if (skillGaugeUI == null)
            skillGaugeUI = GetComponentInChildren<AgentSkillGaugeUI>(true);

        if (agentNameText == null)
            agentNameText = GetComponentInChildren<Text>(true);
    }

    public void Bind(int index, GameObject agentObject)
    {
        CacheReferences();

        boundIndex = index;
        boundAgentObject = agentObject;
        boundAgentController = FindAgentController(agentObject);

        UpdateAgentName();
        BindSkillGauge();
        ClearInputFieldState();
    }

    public void Bind(GameObject agentObject)
    {
        Bind(boundIndex, agentObject);
    }

    public void Clear()
    {
        boundIndex = -1;
        boundAgentObject = null;
        boundAgentController = null;

        if (agentNameText != null)
            agentNameText.text = string.Empty;

        if (skillGaugeUI != null)
            skillGaugeUI.ClearAgentUI();

        ClearInputFieldState();
    }

    private AgentController FindAgentController(GameObject agentObject)
    {
        if (agentObject == null)
            return null;

        AgentController agentController = agentObject.GetComponent<AgentController>();

        if (agentController == null)
            agentController = agentObject.GetComponentInChildren<AgentController>(true);

        return agentController;
    }

    private void UpdateAgentName()
    {
        if (agentNameText == null)
            return;

        if (boundAgentObject == null)
        {
            agentNameText.text = string.Empty;
            return;
        }

        agentNameText.text = GetCleanAgentName(boundAgentObject.name);
    }

    private void BindSkillGauge()
    {
        if (skillGaugeUI == null)
            return;

        skillGaugeUI.BindAgentController(boundAgentController);
    }

    private void ClearInputFieldState()
    {
        if (inputField == null)
            return;

        inputField.text = "";
        inputField.DeactivateInputField();
    }

    private string GetCleanAgentName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return string.Empty;

        return objectName.Replace("(Clone)", "").Trim();
    }
}