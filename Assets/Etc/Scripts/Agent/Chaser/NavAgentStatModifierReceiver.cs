using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavAgentStatModifierReceiver : MonoBehaviour
{
    private class Modifier
    {
        public float speedMultiplier = 1f;
        public float angularSpeedMultiplier = 1f;
        public float accelerationMultiplier = 1f;
        public float endTime = -1f;
    }

    private NavMeshAgent navAgent;

    private readonly Dictionary<string, Modifier> modifiers = new Dictionary<string, Modifier>();

    private float baseSpeed;
    private float baseAngularSpeed;
    private float baseAcceleration;

    private bool hasBaseStats = false;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        CaptureBaseStats();
    }

    private void OnEnable()
    {
        CaptureBaseStats();
    }

    private void LateUpdate()
    {
        if (navAgent == null)
            return;

        bool removed = false;

        List<string> expiredKeys = null;

        foreach (KeyValuePair<string, Modifier> pair in modifiers)
        {
            if (Time.time >= pair.Value.endTime)
            {
                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(pair.Key);
            }
        }

        if (expiredKeys != null)
        {
            for (int i = 0; i < expiredKeys.Count; i++)
                modifiers.Remove(expiredKeys[i]);

            removed = true;
        }

        if (removed)
            RecalculateStats();
    }

    public void ApplyTimedModifier(
        string key,
        float speedMultiplier,
        float angularSpeedMultiplier,
        float accelerationMultiplier,
        float duration)
    {
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
            return;

        if (string.IsNullOrWhiteSpace(key))
            return;

        if (duration <= 0f)
            return;

        if (!hasBaseStats || modifiers.Count == 0)
            CaptureBaseStats();

        if (!modifiers.TryGetValue(key, out Modifier modifier))
        {
            modifier = new Modifier();
            modifiers.Add(key, modifier);
        }

        modifier.speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        modifier.angularSpeedMultiplier = Mathf.Max(0.01f, angularSpeedMultiplier);
        modifier.accelerationMultiplier = Mathf.Max(0.01f, accelerationMultiplier);
        modifier.endTime = Time.time + duration;

        RecalculateStats();
    }

    public void RemoveModifier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (!modifiers.Remove(key))
            return;

        RecalculateStats();
    }

    private void CaptureBaseStats()
    {
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

        if (navAgent == null)
            return;

        baseSpeed = navAgent.speed;
        baseAngularSpeed = navAgent.angularSpeed;
        baseAcceleration = navAgent.acceleration;
        hasBaseStats = true;
    }

    private void RecalculateStats()
    {
        if (navAgent == null)
            return;

        if (!hasBaseStats)
            CaptureBaseStats();

        float speedMultiplier = 1f;
        float angularSpeedMultiplier = 1f;
        float accelerationMultiplier = 1f;

        foreach (KeyValuePair<string, Modifier> pair in modifiers)
        {
            Modifier modifier = pair.Value;

            speedMultiplier *= modifier.speedMultiplier;
            angularSpeedMultiplier *= modifier.angularSpeedMultiplier;
            accelerationMultiplier *= modifier.accelerationMultiplier;
        }

        navAgent.speed = baseSpeed * speedMultiplier;
        navAgent.angularSpeed = baseAngularSpeed * angularSpeedMultiplier;
        navAgent.acceleration = baseAcceleration * accelerationMultiplier;
    }
}