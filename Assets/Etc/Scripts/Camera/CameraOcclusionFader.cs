using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFader : MonoBehaviour
{
    private sealed class RendererFadeState
    {
        public Renderer Renderer;
        public Material[] OriginalSharedMaterials;
        public MaterialPropertyBlock OriginalPropertyBlock;
        public float CurrentAlpha = 1f;
        public bool MaterialOverridden;

        public RendererFadeState(Renderer renderer)
        {
            Renderer = renderer;
            OriginalSharedMaterials = renderer.sharedMaterials;
            OriginalPropertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(OriginalPropertyBlock);
        }
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private AgentCameraFollow cameraFollow;

    [Header("Occlusion")]
    [SerializeField] private LayerMask occluderLayers;
    [SerializeField] private Material transparentMaterial;

    [Header("Settings")]
    [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.25f;
    [SerializeField, Min(0.01f)] private float castRadius = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 8f;
    [SerializeField] private bool useTransparentMaterialOverride = true;

    private readonly Dictionary<Renderer, RendererFadeState> fadeStates = new Dictionary<Renderer, RendererFadeState>();
    private readonly Dictionary<Collider, Renderer[]> rendererCache = new Dictionary<Collider, Renderer[]>();
    private readonly HashSet<Renderer> currentBlockers = new HashSet<Renderer>();
    private readonly List<Renderer> removeBuffer = new List<Renderer>();

    private readonly RaycastHit[] hitBuffer = new RaycastHit[64];
    private readonly MaterialPropertyBlock runtimePropertyBlock = new MaterialPropertyBlock();

    private Transform manualTarget;
    private bool hasManualFocusPoint;
    private Vector3 manualFocusPoint;

    private void Reset()
    {
        occluderLayers = LayerMask.GetMask("Obstacle", "Wall");
    }

    private void Awake()
    {
        ResolveReferences();

        if (occluderLayers.value == 0)
            occluderLayers = LayerMask.GetMask("Obstacle", "Wall");
    }

    private void OnDisable()
    {
        RestoreAllImmediately();
    }

    private void OnDestroy()
    {
        RestoreAllImmediately();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        currentBlockers.Clear();

        if (occluderLayers.value == 0)
        {
            RestoreAllImmediately();
            return;
        }

        if (!TryGetCurrentFocusPoint(out Vector3 focusPoint, out Transform targetRoot))
        {
            RestoreAllImmediately();
            return;
        }

        DetectBlockers(focusPoint, targetRoot);
        UpdateFadeStates();
    }

    public void SetManualTarget(Transform target)
    {
        if (target == null)
        {
            ClearManualTarget();
            return;
        }

        manualTarget = target;
        hasManualFocusPoint = false;
        manualFocusPoint = Vector3.zero;
    }

    public void SetManualFocusPoint(Vector3 focusPoint)
    {
        manualTarget = null;
        hasManualFocusPoint = true;
        manualFocusPoint = focusPoint;
    }

    public void ClearManualTarget()
    {
        manualTarget = null;
        hasManualFocusPoint = false;
        manualFocusPoint = Vector3.zero;

        RestoreAllImmediately();
    }

    public void RestoreAllImmediately()
    {
        foreach (KeyValuePair<Renderer, RendererFadeState> pair in fadeStates)
        {
            RestoreRenderer(pair.Value);
        }

        fadeStates.Clear();
        currentBlockers.Clear();
        removeBuffer.Clear();
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cameraFollow == null && targetCamera != null)
            cameraFollow = targetCamera.GetComponent<AgentCameraFollow>();

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<AgentCameraFollow>();
    }

    private bool TryGetCurrentFocusPoint(out Vector3 focusPoint, out Transform targetRoot)
    {
        focusPoint = Vector3.zero;
        targetRoot = null;

        if (manualTarget != null)
        {
            targetRoot = manualTarget;
            focusPoint = GetFocusPoint(manualTarget);
            return true;
        }

        if (hasManualFocusPoint)
        {
            focusPoint = manualFocusPoint;
            return true;
        }

        if (cameraFollow != null &&
            cameraFollow.enabled &&
            cameraFollow.TryGetFocusedAgentFocusPoint(out focusPoint))
        {
            return true;
        }

        return false;
    }

    private void DetectBlockers(Vector3 focusPoint, Transform targetRoot)
    {
        Vector3 origin = targetCamera.transform.position;
        Vector3 toFocus = focusPoint - origin;
        float distance = toFocus.magnitude;

        if (distance <= 0.01f)
            return;

        Vector3 direction = toFocus / distance;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            castRadius,
            direction,
            hitBuffer,
            distance,
            occluderLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hitBuffer[i].collider;

            if (hitCollider == null)
                continue;

            if (targetRoot != null && hitCollider.transform.IsChildOf(targetRoot))
                continue;

            Renderer[] renderers = GetRenderers(hitCollider);

            for (int j = 0; j < renderers.Length; j++)
            {
                Renderer renderer = renderers[j];

                if (!IsValidRenderer(renderer))
                    continue;

                currentBlockers.Add(renderer);
            }
        }
    }

    private Renderer[] GetRenderers(Collider sourceCollider)
    {
        if (sourceCollider == null)
            return new Renderer[0];

        if (rendererCache.TryGetValue(sourceCollider, out Renderer[] cachedRenderers))
            return cachedRenderers;

        Renderer[] renderers = sourceCollider.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            Renderer parentRenderer = sourceCollider.GetComponentInParent<Renderer>();

            renderers = parentRenderer != null
                ? new Renderer[] { parentRenderer }
                : new Renderer[0];
        }

        rendererCache[sourceCollider] = renderers;
        return renderers;
    }

    private bool IsValidRenderer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (!renderer.enabled)
            return false;

        if (!renderer.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private void UpdateFadeStates()
    {
        foreach (Renderer blocker in currentBlockers)
        {
            if (blocker == null)
                continue;

            RendererFadeState state = GetOrCreateState(blocker);
            UpdateRendererAlpha(state, fadedAlpha);
        }

        removeBuffer.Clear();

        foreach (KeyValuePair<Renderer, RendererFadeState> pair in fadeStates)
        {
            Renderer renderer = pair.Key;
            RendererFadeState state = pair.Value;

            if (renderer == null)
            {
                removeBuffer.Add(renderer);
                continue;
            }

            if (currentBlockers.Contains(renderer))
                continue;

            UpdateRendererAlpha(state, 1f);

            if (state.CurrentAlpha >= 0.999f)
            {
                RestoreRenderer(state);
                removeBuffer.Add(renderer);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            fadeStates.Remove(removeBuffer[i]);
        }
    }

    private RendererFadeState GetOrCreateState(Renderer renderer)
    {
        if (fadeStates.TryGetValue(renderer, out RendererFadeState state))
            return state;

        state = new RendererFadeState(renderer);
        fadeStates.Add(renderer, state);

        return state;
    }

    private void UpdateRendererAlpha(RendererFadeState state, float targetAlpha)
    {
        if (state == null || state.Renderer == null)
            return;

        float deltaTime = Time.unscaledDeltaTime > 0f
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        state.CurrentAlpha = Mathf.MoveTowards(
            state.CurrentAlpha,
            targetAlpha,
            fadeSpeed * deltaTime
        );

        ApplyMaterialOverrideIfNeeded(state);
        ApplyAlpha(state);
    }

    private void ApplyMaterialOverrideIfNeeded(RendererFadeState state)
    {
        if (!useTransparentMaterialOverride)
            return;

        if (transparentMaterial == null)
            return;

        if (state.MaterialOverridden)
            return;

        if (state.OriginalSharedMaterials == null || state.OriginalSharedMaterials.Length == 0)
            return;

        Material[] replacementMaterials = new Material[state.OriginalSharedMaterials.Length];

        for (int i = 0; i < replacementMaterials.Length; i++)
        {
            replacementMaterials[i] = transparentMaterial;
        }

        state.Renderer.sharedMaterials = replacementMaterials;
        state.MaterialOverridden = true;
    }

    private void ApplyAlpha(RendererFadeState state)
    {
        runtimePropertyBlock.Clear();
        state.Renderer.GetPropertyBlock(runtimePropertyBlock);

        Color color = GetRepresentativeColor(state.Renderer);
        color.a = state.CurrentAlpha;

        runtimePropertyBlock.SetColor(BaseColorId, color);
        runtimePropertyBlock.SetColor(ColorId, color);
        runtimePropertyBlock.SetFloat(AlphaId, state.CurrentAlpha);

        state.Renderer.SetPropertyBlock(runtimePropertyBlock);
    }

    private Color GetRepresentativeColor(Renderer renderer)
    {
        if (renderer == null)
            return Color.white;

        Material[] materials = renderer.sharedMaterials;

        if (materials == null)
            return Color.white;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material == null)
                continue;

            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);

            if (material.HasProperty(ColorId))
                return material.GetColor(ColorId);
        }

        return Color.white;
    }

    private void RestoreRenderer(RendererFadeState state)
    {
        if (state == null || state.Renderer == null)
            return;

        if (state.MaterialOverridden && state.OriginalSharedMaterials != null)
        {
            state.Renderer.sharedMaterials = state.OriginalSharedMaterials;
        }

        state.Renderer.SetPropertyBlock(state.OriginalPropertyBlock);
        state.CurrentAlpha = 1f;
        state.MaterialOverridden = false;
    }

    private Vector3 GetFocusPoint(Transform root)
    {
        if (root == null)
            return Vector3.zero;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (TryGetCombinedBounds(renderers, out Bounds rendererBounds))
            return rendererBounds.center;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        if (TryGetCombinedBounds(colliders, out Bounds colliderBounds))
            return colliderBounds.center;

        return root.position + Vector3.up;
    }

    private bool TryGetCombinedBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetCombinedBounds(Collider[] colliders, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (colliders == null)
            return false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }
}