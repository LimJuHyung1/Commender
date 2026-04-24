using System.Collections.Generic;
using UnityEngine;

public class TargetVisibilityController : MonoBehaviour
{
    [Header("References")]
    public TargetController targetController;
    public List<VisionSensor> agentVisionSensors = new List<VisionSensor>();

    [Header("Hide Targets")]
    public List<Renderer> targetRenderers = new List<Renderer>();
    public List<Canvas> targetCanvases = new List<Canvas>();
    public List<GameObject> extraVisibleObjects = new List<GameObject>();

    [Header("Options")]
    public bool hideWhenNotVisible = true;
    public bool autoFindSceneSensors = true;
    public bool debugVisibility = false;

    private bool isCurrentlyVisible = true;
    private Transform targetRoot;
    private string lastVisibleReason = "None";

    private void Awake()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        targetRoot = targetController != null ? targetController.transform : transform.root;

        if (targetRenderers.Count == 0)
            CollectTargetRenderers();

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
        lastVisibleReason = visibleReason;

        if (isCurrentlyVisible == canPlayerSee)
            return;

        isCurrentlyVisible = canPlayerSee;
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

    private void CollectTargetRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            targetRenderers.Add(renderer);
        }
    }

    private void CollectSceneSensorsIfNeeded()
    {
        if (!autoFindSceneSensors)
            return;

        for (int i = agentVisionSensors.Count - 1; i >= 0; i--)
        {
            VisionSensor sensor = agentVisionSensors[i];

            if (sensor == null ||
                !sensor.isActiveAndEnabled ||
                !sensor.gameObject.activeInHierarchy)
            {
                agentVisionSensors.RemoveAt(i);
            }
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

        if (GameManager.Instance != null &&
            targetController != null &&
            GameManager.Instance.IsTargetDebugRevealEnabled)
        {
            visibleReason = "ReconReveal";
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
            Renderer renderer = targetRenderers[i];

            if (renderer == null)
                continue;

            renderer.enabled = visible;
        }

        for (int i = 0; i < targetCanvases.Count; i++)
        {
            Canvas canvas = targetCanvases[i];

            if (canvas == null)
                continue;

            canvas.enabled = visible;
        }

        for (int i = 0; i < extraVisibleObjects.Count; i++)
        {
            GameObject targetObject = extraVisibleObjects[i];

            if (targetObject == null)
                continue;

            targetObject.SetActive(visible);
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