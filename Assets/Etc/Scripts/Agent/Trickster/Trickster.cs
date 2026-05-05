using System.Collections;
using System.Reflection;
using UnityEngine;

public class Trickster : AgentController
{
    private const string SkillFakeBox = "fakebox";
    private const string SkillJokerCard = "jokercard";

    [Header("Fake Box")]
    [SerializeField] private FakeBox fakeBoxPrefab;
    [SerializeField] private Transform deployParent;
    [SerializeField] private float deployYOffset = 0f;
    [SerializeField] private bool replaceExistingFakeBox = true;

    [Header("Joker Card Effect")]
    [SerializeField] private GameObject jokerCardEffectPrefab;
    [SerializeField] private Transform jokerCardEffectParent;

    private FakeBox currentFakeBox;
    private Coroutine jokerCardRoutine;

    private bool isJokerCardActive;

    private float originalMoveSpeed;
    private float originalSpotLightRange;
    private float originalSpotLightOuterAngle;

    private FloatMemberSnapshot viewRadiusSnapshot;
    private FloatMemberSnapshot viewAngleSnapshot;

    protected override void Awake()
    {
        agentID = 3;
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        TryAutoUseJokerCard();
    }

    protected override void OnDisable()
    {
        StopJokerCard(true);
        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        deployYOffset = Mathf.Max(0f, deployYOffset);
    }

    public override void ExecuteSkill(string skillName, Vector3 targetPos)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        string skill = skillName.Trim().ToLower();

        Debug.Log($"[Trickster {AgentID}] Skill request: {skillName}, Position: {targetPos}");

        if (IsFakeBoxSkill(skill))
        {
            ExecuteFakeBox(targetPos);
            return;
        }

        if (IsJokerCardSkill(skill))
        {
            Debug.LogWarning($"[Trickster {AgentID}] Joker Card is an automatic skill. It activates when its gauge is full.");
            return;
        }

        Debug.LogWarning($"[Trickster {AgentID}] Unknown skill: {skillName}");
    }

    private void ExecuteFakeBox(Vector3 targetPos)
    {
        if (fakeBoxPrefab == null)
        {
            Debug.LogWarning($"[Trickster {AgentID}] fakeBoxPrefab is not assigned.");
            return;
        }

        if (!TryConsumeSkillGaugeForSkill(SkillFakeBox))
            return;

        ForceStopForSkill();
        DeployFakeBox(targetPos);
    }

    private void DeployFakeBox(Vector3 targetPos)
    {
        Vector3 spawnPos = BuildSpawnPosition(targetPos);

        if (replaceExistingFakeBox && currentFakeBox != null)
        {
            Destroy(currentFakeBox.gameObject);
            currentFakeBox = null;
        }

        currentFakeBox = Instantiate(
            fakeBoxPrefab,
            spawnPos,
            Quaternion.identity,
            deployParent != null ? deployParent : null
        );

        currentFakeBox.SetOwner(this);

        Debug.Log($"[Trickster {AgentID}] Fake Box deployed: {spawnPos}");
    }

    private void TryAutoUseJokerCard()
    {
        if (isJokerCardActive)
            return;

        if (!CanUseSkillGaugeForSkill(SkillJokerCard, false))
            return;

        if (!TryConsumeSkillGaugeForSkill(SkillJokerCard))
            return;

        jokerCardRoutine = StartCoroutine(JokerCardRoutine());
    }

    private IEnumerator JokerCardRoutine()
    {
        isJokerCardActive = true;

        CacheJokerCardOriginalValues();
        ApplyJokerCardBuff();
        SpawnJokerCardEffect();

        float duration = stats != null ? stats.jokerCardDuration : 6f;

        Debug.Log($"[Trickster {AgentID}] Joker Card activated. Duration: {duration:0.##}");

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        StopJokerCard(false);
    }

    private void CacheJokerCardOriginalValues()
    {
        if (navAgent != null)
            originalMoveSpeed = navAgent.speed;

        if (spotLight != null)
        {
            originalSpotLightRange = spotLight.range;
            originalSpotLightOuterAngle = spotLight.spotAngle;
        }

        viewRadiusSnapshot = FloatMemberSnapshot.Create(visionSensor, "viewRadius");
        viewAngleSnapshot = FloatMemberSnapshot.Create(visionSensor, "viewAngle");
    }

    private void ApplyJokerCardBuff()
    {
        float moveSpeedMultiplier = stats != null ? stats.jokerCardMoveSpeedMultiplier : 1.25f;
        float viewRadiusMultiplier = stats != null ? stats.jokerCardViewRadiusMultiplier : 1.2f;
        float viewAngleBonus = stats != null ? stats.jokerCardViewAngleBonus : 15f;

        moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
        viewRadiusMultiplier = Mathf.Max(0f, viewRadiusMultiplier);

        if (navAgent != null)
            navAgent.speed = originalMoveSpeed * moveSpeedMultiplier;

        if (viewRadiusSnapshot.IsValid)
            viewRadiusSnapshot.Set(viewRadiusSnapshot.OriginalValue * viewRadiusMultiplier);

        if (viewAngleSnapshot.IsValid)
            viewAngleSnapshot.Set(Mathf.Clamp(viewAngleSnapshot.OriginalValue + viewAngleBonus, 1f, 360f));

        if (spotLight != null)
        {
            spotLight.range = originalSpotLightRange * viewRadiusMultiplier;
            spotLight.spotAngle = Mathf.Clamp(originalSpotLightOuterAngle + viewAngleBonus, 1f, 179f);
        }
    }

    private void StopJokerCard(bool immediate)
    {
        if (jokerCardRoutine != null)
        {
            StopCoroutine(jokerCardRoutine);
            jokerCardRoutine = null;
        }

        if (!isJokerCardActive && !immediate)
            return;

        RestoreJokerCardOriginalValues();

        isJokerCardActive = false;

        if (!immediate)
            Debug.Log($"[Trickster {AgentID}] Joker Card finished.");
    }

    private void RestoreJokerCardOriginalValues()
    {
        if (navAgent != null)
            navAgent.speed = originalMoveSpeed;

        if (viewRadiusSnapshot.IsValid)
            viewRadiusSnapshot.Restore();

        if (viewAngleSnapshot.IsValid)
            viewAngleSnapshot.Restore();

        if (spotLight != null)
        {
            spotLight.range = originalSpotLightRange;
            spotLight.spotAngle = originalSpotLightOuterAngle;
        }
    }

    private void SpawnJokerCardEffect()
    {
        if (jokerCardEffectPrefab == null)
            return;

        Transform parent = jokerCardEffectParent != null ? jokerCardEffectParent : transform;

        Instantiate(
            jokerCardEffectPrefab,
            transform.position,
            Quaternion.identity,
            parent
        );
    }

    private void ForceStopForSkill()
    {
        currentTarget = null;
        isManualMoving = false;

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.ResetPath();
        }
    }

    private Vector3 BuildSpawnPosition(Vector3 targetPos)
    {
        Vector3 rayOrigin = new Vector3(targetPos.x, targetPos.y + 2f, targetPos.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return new Vector3(hit.point.x, hit.point.y + deployYOffset, hit.point.z);

        return new Vector3(targetPos.x, deployYOffset, targetPos.z);
    }

    private bool IsFakeBoxSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("fakebox") ||
               skill.Contains("fake box") ||
               skill.Contains("magicbox") ||
               skill.Contains("magic box") ||
               skill.Contains("페이크박스") ||
               skill.Contains("페이크 박스") ||
               skill.Contains("마술상자") ||
               skill.Contains("마술 상자");
    }

    private bool IsJokerCardSkill(string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return false;

        return skill.Contains("jokercard") ||
               skill.Contains("joker card") ||
               skill.Contains("조커카드") ||
               skill.Contains("조커 카드");
    }

    private sealed class FloatMemberSnapshot
    {
        private readonly object target;
        private readonly FieldInfo field;
        private readonly PropertyInfo property;

        public bool IsValid { get; private set; }
        public float OriginalValue { get; private set; }

        private FloatMemberSnapshot(object target, FieldInfo field, PropertyInfo property, float originalValue)
        {
            this.target = target;
            this.field = field;
            this.property = property;
            OriginalValue = originalValue;
            IsValid = target != null && (field != null || property != null);
        }

        public static FloatMemberSnapshot Create(object target, string memberName)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
                return new FloatMemberSnapshot(null, null, null, 0f);

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, flags);

            if (field != null && field.FieldType == typeof(float))
            {
                float value = (float)field.GetValue(target);
                return new FloatMemberSnapshot(target, field, null, value);
            }

            PropertyInfo property = type.GetProperty(memberName, flags);

            if (property != null &&
                property.PropertyType == typeof(float) &&
                property.CanRead &&
                property.CanWrite)
            {
                float value = (float)property.GetValue(target);
                return new FloatMemberSnapshot(target, null, property, value);
            }

            return new FloatMemberSnapshot(null, null, null, 0f);
        }

        public void Set(float value)
        {
            if (!IsValid)
                return;

            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            if (property != null)
                property.SetValue(target, value);
        }

        public void Restore()
        {
            Set(OriginalValue);
        }
    }
}