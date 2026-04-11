using System.Collections.Generic;
using UnityEngine;

public class TargetVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TargetController targetController;
    [SerializeField] private List<VisionSensor> agentVisionSensors = new List<VisionSensor>();

    [Header("Hide Targets")]
    [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();
    [SerializeField] private List<Canvas> targetCanvases = new List<Canvas>();
    [SerializeField] private List<GameObject> extraVisibleObjects = new List<GameObject>();

    [Header("Options")]
    [SerializeField] private bool hideWhenNotVisible = true;
    [SerializeField] private bool autoFindSceneSensors = true;
    [SerializeField] private bool useRevealState = false;
    [SerializeField] private bool debugVisibility = true;

    private bool isCurrentlyVisible = true;
    private Transform targetRoot;
    private string lastVisibleReason = "None";

    private void Awake()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        targetRoot = targetController != null ? targetController.transform : transform.root;

        if (targetRenderers.Count == 0)
            targetRenderers.AddRange(GetComponentsInChildren<Renderer>(true));

        if (targetCanvases.Count == 0)
            targetCanvases.AddRange(GetComponentsInChildren<Canvas>(true));

        CollectSceneSensorsIfNeeded();
    }

    private void OnEnable()
    {
        CollectSceneSensorsIfNeeded();
        RefreshVisibilityImmediate();
    }

    private void Update()
    {
        bool canPlayerSee = CanPlayerSeeTarget(out string visibleReason);

        if (isCurrentlyVisible == canPlayerSee)
            return;

        isCurrentlyVisible = canPlayerSee;
        lastVisibleReason = visibleReason;
        ApplyVisibility(isCurrentlyVisible);

        if (debugVisibility)
            Debug.Log($"[TargetVisibility] visible={isCurrentlyVisible}, reason={lastVisibleReason}");
    }

    private void RefreshVisibilityImmediate()
    {
        isCurrentlyVisible = CanPlayerSeeTarget(out string visibleReason);
        lastVisibleReason = visibleReason;
        ApplyVisibility(isCurrentlyVisible);

        if (debugVisibility)
            Debug.Log($"[TargetVisibility] visible={isCurrentlyVisible}, reason={lastVisibleReason}");
    }

    private void CollectSceneSensorsIfNeeded()
    {
        if (!autoFindSceneSensors)
            return;

        for (int i = agentVisionSensors.Count - 1; i >= 0; i--)
        {
            VisionSensor sensor = agentVisionSensors[i];
            if (sensor == null || !sensor.isActiveAndEnabled || !sensor.gameObject.activeInHierarchy)
                agentVisionSensors.RemoveAt(i);
        }

        VisionSensor[] sensors = FindObjectsByType<VisionSensor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < sensors.Length; i++)
        {
            VisionSensor sensor = sensors[i];
            if (sensor == null)
                continue;

            if (!sensor.isActiveAndEnabled || !sensor.gameObject.activeInHierarchy)
                continue;

            if (!agentVisionSensors.Contains(sensor))
                agentVisionSensors.Add(sensor);
        }
    }

    private bool CanPlayerSeeTarget(out string visibleReason)
    {
        visibleReason = "None";

        if (targetRoot == null)
            return false;

        if (useRevealState && targetController != null && targetController.IsRevealedToPlayer)
        {
            visibleReason = "RevealState";
            return true;
        }

        for (int i = agentVisionSensors.Count - 1; i >= 0; i--)
        {
            VisionSensor sensor = agentVisionSensors[i];
            if (sensor == null)
            {
                agentVisionSensors.RemoveAt(i);
                continue;
            }

            if (!sensor.isActiveAndEnabled || !sensor.gameObject.activeInHierarchy)
                continue;

            if (sensor.CanDirectlySeeTransform(targetRoot))
            {
                visibleReason = $"DirectSight:{sensor.name}";
                return true;
            }
        }

        return false;
    }

    private void ApplyVisibility(bool visible)
    {
        if (!hideWhenNotVisible)
            return;

        for (int i = 0; i < targetRenderers.Count; i++)
        {
            if (targetRenderers[i] != null)
                targetRenderers[i].enabled = visible;
        }

        for (int i = 0; i < targetCanvases.Count; i++)
        {
            if (targetCanvases[i] != null)
                targetCanvases[i].enabled = visible;
        }

        for (int i = 0; i < extraVisibleObjects.Count; i++)
        {
            if (extraVisibleObjects[i] != null)
                extraVisibleObjects[i].SetActive(visible);
        }
    }

    public void RegisterSensor(VisionSensor sensor)
    {
        if (sensor == null)
            return;

        if (!sensor.isActiveAndEnabled || !sensor.gameObject.activeInHierarchy)
            return;

        if (!agentVisionSensors.Contains(sensor))
            agentVisionSensors.Add(sensor);
    }
}