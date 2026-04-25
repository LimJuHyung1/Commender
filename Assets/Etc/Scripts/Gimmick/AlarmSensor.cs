using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AlarmSensor : MonoBehaviour
{
    public static readonly List<AlarmSensor> ActiveSensors = new List<AlarmSensor>();

    [Header("Alarm Cycle")]
    public float rotationSpeed = 720f;
    public float activeDuration = 5f;

    [Header("Target Avoidance")]
    public float outerAvoidanceDistance = 4f;
    public float dangerPenalty = 25f;

    private readonly List<Light> spotLights = new List<Light>();
    private Collider sensorCollider;
    private Coroutine alarmRoutine;
    private bool isActive;

    private Vector3 firstLightBaseRotation;
    private Vector3 secondLightBaseRotation;

    public bool IsActive => isActive;
    public Vector3 Position => transform.position;
    public float OuterAvoidanceDistance => outerAvoidanceDistance;
    public float DangerPenalty => dangerPenalty;

    private void Awake()
    {
        sensorCollider = GetComponent<Collider>();
        sensorCollider.isTrigger = true;

        FindChildSpotLights();
        CacheBaseRotations();
        SetSpotLightsActive(false);
    }

    private void OnDisable()
    {
        if (alarmRoutine != null)
        {
            StopCoroutine(alarmRoutine);
            alarmRoutine = null;
        }

        UnregisterActiveSensor();
        SetSpotLightsActive(false);
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        activeDuration = Mathf.Max(0.01f, activeDuration);
        outerAvoidanceDistance = Mathf.Max(0f, outerAvoidanceDistance);
        dangerPenalty = Mathf.Max(0f, dangerPenalty);

        Collider targetCollider = GetComponent<Collider>();

        if (targetCollider != null)
            targetCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alarmRoutine != null)
            return;

        if (!IsAgent(other))
            return;

        alarmRoutine = StartCoroutine(AlarmCycle());
    }

    public bool IsPositionInside(Vector3 position)
    {
        if (sensorCollider == null)
            return false;

        Vector3 closestPoint = sensorCollider.ClosestPoint(position);
        return Vector3.Distance(position, closestPoint) <= 0.05f;
    }

    public float GetDistanceFromZone(Vector3 position)
    {
        if (sensorCollider == null)
            return Vector3.Distance(position, transform.position);

        Vector3 closestPoint = sensorCollider.ClosestPoint(position);
        return Vector3.Distance(position, closestPoint);
    }

    public bool IsPositionInAvoidanceRange(Vector3 position)
    {
        if (!isActive)
            return false;

        if (IsPositionInside(position))
            return true;

        return GetDistanceFromZone(position) <= outerAvoidanceDistance;
    }

    public float GetAvoidancePenalty(Vector3 position)
    {
        if (!isActive)
            return 0f;

        if (IsPositionInside(position))
            return dangerPenalty;

        if (outerAvoidanceDistance <= 0f)
            return 0f;

        float distance = GetDistanceFromZone(position);

        if (distance > outerAvoidanceDistance)
            return 0f;

        float ratio = 1f - Mathf.Clamp01(distance / outerAvoidanceDistance);
        return ratio * dangerPenalty;
    }

    private IEnumerator AlarmCycle()
    {
        RegisterActiveSensor();
        SetSpotLightsActive(true);

        float elapsedTime = 0f;
        float startX = firstLightBaseRotation.x;

        ApplySpotLightRotation(startX);

        while (elapsedTime < activeDuration)
        {
            elapsedTime += Time.deltaTime;

            float currentX = startX + rotationSpeed * elapsedTime;
            ApplySpotLightRotation(currentX);

            yield return null;
        }

        SetSpotLightsActive(false);
        UnregisterActiveSensor();

        alarmRoutine = null;
    }

    private void FindChildSpotLights()
    {
        spotLights.Clear();

        Light[] childLights = GetComponentsInChildren<Light>(true);

        for (int i = 0; i < childLights.Length; i++)
        {
            Light childLight = childLights[i];

            if (childLight == null)
                continue;

            if (childLight.type != LightType.Spot)
                continue;

            spotLights.Add(childLight);

            if (spotLights.Count >= 2)
                break;
        }
    }

    private void CacheBaseRotations()
    {
        if (spotLights.Count >= 1 && spotLights[0] != null)
            firstLightBaseRotation = spotLights[0].transform.localEulerAngles;

        if (spotLights.Count >= 2 && spotLights[1] != null)
            secondLightBaseRotation = spotLights[1].transform.localEulerAngles;
    }

    private bool IsAgent(Collider other)
    {
        if (other == null)
            return false;

        AgentController agent = other.GetComponentInParent<AgentController>();
        return agent != null;
    }

    private void ApplySpotLightRotation(float firstLightX)
    {
        if (spotLights.Count < 2)
            return;

        Light firstLight = spotLights[0];
        Light secondLight = spotLights[1];

        if (firstLight == null || secondLight == null)
            return;

        float normalizedFirstX = Mathf.Repeat(firstLightX, 360f);
        float normalizedSecondX = Mathf.Repeat(firstLightX + 180f, 360f);

        firstLightBaseRotation.x = normalizedFirstX;
        secondLightBaseRotation.x = normalizedSecondX;

        firstLight.transform.localEulerAngles = firstLightBaseRotation;
        secondLight.transform.localEulerAngles = secondLightBaseRotation;
    }

    private void SetSpotLightsActive(bool active)
    {
        for (int i = 0; i < spotLights.Count; i++)
        {
            Light spotLight = spotLights[i];

            if (spotLight == null)
                continue;

            spotLight.gameObject.SetActive(active);
        }
    }

    private void RegisterActiveSensor()
    {
        isActive = true;

        if (!ActiveSensors.Contains(this))
            ActiveSensors.Add(this);
    }

    private void UnregisterActiveSensor()
    {
        isActive = false;

        if (ActiveSensors.Contains(this))
            ActiveSensors.Remove(this);
    }
}