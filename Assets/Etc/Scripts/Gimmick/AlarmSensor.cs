using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class AlarmSensor : MonoBehaviour
{
    public static readonly List<AlarmSensor> ActiveSensors = new List<AlarmSensor>();

    [Header("Alarm Cycle")]
    public float rotationSpeed = 720f;
    public float activeDuration = 5f;

    [Header("Alert Text")]
    public Text alertText;
    public string alertMessage = "타겟이 당신의 위치를 알아챘습니다";
    public bool textFacesCamera = true;

    [Header("Target Avoidance")]
    public float outerAvoidanceDistance = 4f;
    public float dangerPenalty = 25f;

    private readonly List<Light> spotLights = new List<Light>();
    private DOTweenAnimation[] alertTextTweens = new DOTweenAnimation[0];

    private Collider sensorCollider;
    private Coroutine alarmRoutine;
    private Coroutine textTweenRoutine;
    private Camera mainCamera;
    private bool isActive;

    public bool IsActive => isActive;
    public Vector3 Position => transform.position;
    public float OuterAvoidanceDistance => outerAvoidanceDistance;
    public float DangerPenalty => dangerPenalty;

    private void Awake()
    {
        sensorCollider = GetComponent<Collider>();
        sensorCollider.isTrigger = true;

        mainCamera = Camera.main;

        FindChildSpotLights();
        FindAlertText();
        FindAlertTextTweens();

        SetSpotLightsActive(false);
        SetAlertTextActive(false);
    }

    private void LateUpdate()
    {
        if (!textFacesCamera)
            return;

        if (alertText == null || !alertText.gameObject.activeInHierarchy)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Transform targetTransform = GetAlertTextRoot();

        targetTransform.LookAt(
            targetTransform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up
        );
    }

    private Transform GetAlertTextRoot()
    {
        Canvas parentCanvas = alertText.GetComponentInParent<Canvas>();

        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
            return parentCanvas.transform;

        return alertText.transform;
    }

    private void OnDisable()
    {
        if (alarmRoutine != null)
        {
            StopCoroutine(alarmRoutine);
            alarmRoutine = null;
        }

        if (textTweenRoutine != null)
        {
            StopCoroutine(textTweenRoutine);
            textTweenRoutine = null;
        }

        UnregisterActiveSensor();
        SetSpotLightsActive(false);
        SetAlertTextActive(false);
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
        SetAlertTextActive(true);

        if (spotLights.Count < 2)
        {
            yield return new WaitForSeconds(activeDuration);

            SetSpotLightsActive(false);
            SetAlertTextActive(false);
            UnregisterActiveSensor();

            alarmRoutine = null;
            yield break;
        }

        Vector3 firstLightStartRotation = spotLights[0].transform.localEulerAngles;
        Vector3 secondLightStartRotation = spotLights[1].transform.localEulerAngles;

        float elapsedTime = 0f;

        ApplySpotLightRotation(
            firstLightStartRotation,
            secondLightStartRotation,
            0f
        );

        while (elapsedTime < activeDuration)
        {
            elapsedTime += Time.deltaTime;

            float rotationAmount = rotationSpeed * elapsedTime;

            ApplySpotLightRotation(
                firstLightStartRotation,
                secondLightStartRotation,
                rotationAmount
            );

            yield return null;
        }

        SetSpotLightsActive(false);
        SetAlertTextActive(false);
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

    private void FindAlertText()
    {
        if (alertText != null)
            return;

        alertText = GetComponentInChildren<Text>(true);
    }

    private void FindAlertTextTweens()
    {
        if (alertText == null)
        {
            alertTextTweens = new DOTweenAnimation[0];
            return;
        }

        alertTextTweens = alertText.GetComponents<DOTweenAnimation>();
    }

    private bool IsAgent(Collider other)
    {
        if (other == null)
            return false;

        AgentController agent = other.GetComponentInParent<AgentController>();
        return agent != null;
    }

    private void ApplySpotLightRotation(
        Vector3 firstStartRotation,
        Vector3 secondStartRotation,
        float rotationAmount)
    {
        if (spotLights.Count < 2)
            return;

        Light firstLight = spotLights[0];
        Light secondLight = spotLights[1];

        if (firstLight == null || secondLight == null)
            return;

        Vector3 firstRotation = firstStartRotation;
        Vector3 secondRotation = secondStartRotation;

        firstRotation.x = Mathf.Repeat(firstStartRotation.x + rotationAmount, 360f);
        firstRotation.y = Mathf.Repeat(firstStartRotation.y + rotationAmount, 360f);

        secondRotation.x = Mathf.Repeat(firstRotation.x + 180f, 360f);
        secondRotation.y = Mathf.Repeat(secondStartRotation.y + rotationAmount, 360f);

        firstLight.transform.localEulerAngles = firstRotation;
        secondLight.transform.localEulerAngles = secondRotation;
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

    private void SetAlertTextActive(bool active)
    {
        if (alertText == null)
            return;

        alertText.text = alertMessage;
        alertText.gameObject.SetActive(active);

        if (active)
        {
            if (textTweenRoutine != null)
                StopCoroutine(textTweenRoutine);

            textTweenRoutine = StartCoroutine(PlayAlertTextTweensAfterActivation());
        }
        else
        {
            if (textTweenRoutine != null)
            {
                StopCoroutine(textTweenRoutine);
                textTweenRoutine = null;
            }
        }
    }

    private IEnumerator PlayAlertTextTweensAfterActivation()
    {
        yield return null;

        Canvas.ForceUpdateCanvases();
        PlayAllAlertTextTweens();

        textTweenRoutine = null;
    }

    private void PlayAllAlertTextTweens()
    {
        FindAlertTextTweens();

        if (alertTextTweens == null || alertTextTweens.Length == 0)
        {
            Debug.LogWarning("[AlarmSensor] Text 오브젝트에서 DOTweenAnimation 컴포넌트를 찾지 못했습니다.");
            return;
        }

        for (int i = 0; i < alertTextTweens.Length; i++)
        {
            DOTweenAnimation tween = alertTextTweens[i];

            if (tween == null)
                continue;

            tween.CreateTween(true, true);
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