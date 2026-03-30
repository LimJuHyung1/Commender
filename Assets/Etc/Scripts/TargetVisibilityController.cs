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

    private bool isCurrentlyVisible = true;

    private void Awake()
    {
        if (targetController == null)
            targetController = GetComponent<TargetController>();

        if (targetRenderers.Count == 0)
            targetRenderers.AddRange(GetComponentsInChildren<Renderer>(true));

        if (targetCanvases.Count == 0)
            targetCanvases.AddRange(GetComponentsInChildren<Canvas>(true));
    }

    private void Update()
    {
        bool canPlayerSee = CanPlayerSeeTarget();

        if (isCurrentlyVisible == canPlayerSee)
            return;

        isCurrentlyVisible = canPlayerSee;
        ApplyVisibility(isCurrentlyVisible);
    }

    private bool CanPlayerSeeTarget()
    {
        if (targetController != null && targetController.IsRevealedToPlayer)
            return true;

        for (int i = 0; i < agentVisionSensors.Count; i++)
        {
            VisionSensor sensor = agentVisionSensors[i];
            if (sensor == null)
                continue;

            if (sensor.IsSeeingTarget && sensor.CurrentSeenTarget == transform)
                return true;
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

        if (!agentVisionSensors.Contains(sensor))
            agentVisionSensors.Add(sensor);
    }
}