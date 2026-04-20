using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class FloorViewController : MonoBehaviour
{
    private enum UpperFloorViewMode
    {
        Hide,
        Transparent
    }

    [Header("Floor Search")]
    [SerializeField] private Transform searchRoot;
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string firstFloorTag = "1_Floor";
    [SerializeField] private string secondFloorTag = "2_Floor";

    [Header("Upper Floor Display")]
    [SerializeField] private UpperFloorViewMode upperFloorViewMode = UpperFloorViewMode.Transparent;
    [SerializeField][Range(0.05f, 1f)] private float transparentAlpha = 0.2f;
    [SerializeField] private bool disableSecondFloorColliders = true;

    [Header("UI")]
    [SerializeField] private Button floorToggleButton;

    [Header("State")]
    [SerializeField] private bool startInLowerFloorView = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    private readonly List<FloorPiece> firstFloorPieces = new List<FloorPiece>();
    private readonly List<FloorPiece> secondFloorPieces = new List<FloorPiece>();

    private bool isInitialized;
    private bool isSystemEnabled;
    private bool isLowerFloorView;

    public bool IsSystemEnabled => isSystemEnabled;
    public bool IsLowerFloorView => isLowerFloorView;

    private void Awake()
    {
        isLowerFloorView = startInLowerFloorView;
        UpdateButtonState(false);
    }

    private void OnDestroy()
    {
        RestoreAllFloors();
        ClearCache();
    }

    public void SetSearchRoot(Transform root)
    {
        if (searchRoot == root)
            return;

        RestoreAllFloors();
        ClearCache();
        searchRoot = root;
    }

    public void SetSystemEnabled(bool enabled)
    {
        isSystemEnabled = enabled;
        UpdateButtonState(isSystemEnabled);

        if (!isSystemEnabled)
        {
            isLowerFloorView = false;
            RestoreAllFloors();
            return;
        }

        if (!isInitialized)
            CacheFloors();

        ApplyCurrentViewState();
    }

    public void RefreshFloorCache()
    {
        RestoreAllFloors();
        ClearCache();

        if (isSystemEnabled)
        {
            CacheFloors();
            ApplyCurrentViewState();
        }
    }

    public void ToggleFloorView()
    {
        if (!isSystemEnabled)
            return;

        isLowerFloorView = !isLowerFloorView;
        ApplyCurrentViewState();
    }

    public void EnterLowerFloorView()
    {
        if (!isSystemEnabled)
            return;

        isLowerFloorView = true;
        ApplyCurrentViewState();
    }

    public void ExitLowerFloorView()
    {
        if (!isSystemEnabled)
            return;

        isLowerFloorView = false;
        ApplyCurrentViewState();
    }

    private void UpdateButtonState(bool enabled)
    {
        if (floorToggleButton == null)
            return;

        floorToggleButton.interactable = enabled;
    }

    private void CacheFloors()
    {
        ClearCache();

        if (searchRoot == null)
        {
            Debug.LogWarning("[FloorViewController] searchRoot가 비어 있습니다.");
            return;
        }

        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        bool useGroundLayerFilter = groundLayer >= 0;

        if (!useGroundLayerFilter)
            Debug.LogWarning($"[FloorViewController] Ground 레이어 '{groundLayerName}' 를 찾지 못했습니다. 태그 기준으로만 찾습니다.");

        Transform[] allTransforms = searchRoot.GetComponentsInChildren<Transform>(true);

        HashSet<GameObject> firstFloorRoots = new HashSet<GameObject>();
        HashSet<GameObject> secondFloorRoots = new HashSet<GameObject>();

        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr == null)
                continue;

            GameObject go = tr.gameObject;

            if (useGroundLayerFilter && go.layer != groundLayer)
                continue;

            if (go.CompareTag(firstFloorTag))
            {
                firstFloorRoots.Add(go);
            }
            else if (go.CompareTag(secondFloorTag))
            {
                secondFloorRoots.Add(go);
            }
        }

        foreach (GameObject root in firstFloorRoots)
        {
            if (root != null)
                firstFloorPieces.Add(new FloorPiece(root));
        }

        foreach (GameObject root in secondFloorRoots)
        {
            if (root != null)
                secondFloorPieces.Add(new FloorPiece(root));
        }

        isInitialized = true;

        if (showDebugLog)
        {
            Debug.Log($"[FloorViewController] 1층 수집: {firstFloorPieces.Count}, 2층 수집: {secondFloorPieces.Count}");
        }
    }

    private void ApplyCurrentViewState()
    {
        if (!isInitialized)
            return;

        if (!isSystemEnabled)
        {
            RestoreAllFloors();
            return;
        }

        if (isLowerFloorView)
            ApplySecondFloorHiddenState();
        else
            RestoreSecondFloorState();
    }

    private void ApplySecondFloorHiddenState()
    {
        for (int i = 0; i < secondFloorPieces.Count; i++)
        {
            FloorPiece piece = secondFloorPieces[i];
            if (piece == null)
                continue;

            if (upperFloorViewMode == UpperFloorViewMode.Hide)
                piece.SetVisible(false);
            else
                piece.SetTransparent(true, transparentAlpha);

            if (disableSecondFloorColliders)
                piece.SetCollidersEnabled(false);
            else
                piece.RestoreColliderState();
        }
    }

    private void RestoreSecondFloorState()
    {
        for (int i = 0; i < secondFloorPieces.Count; i++)
        {
            FloorPiece piece = secondFloorPieces[i];
            if (piece == null)
                continue;

            piece.RestoreOriginalState();
        }
    }

    private void RestoreAllFloors()
    {
        for (int i = 0; i < firstFloorPieces.Count; i++)
        {
            FloorPiece piece = firstFloorPieces[i];
            if (piece == null)
                continue;

            piece.RestoreOriginalState();
        }

        for (int i = 0; i < secondFloorPieces.Count; i++)
        {
            FloorPiece piece = secondFloorPieces[i];
            if (piece == null)
                continue;

            piece.RestoreOriginalState();
        }
    }

    private void ClearCache()
    {
        firstFloorPieces.Clear();
        secondFloorPieces.Clear();
        isInitialized = false;
    }

    private sealed class FloorPiece
    {
        private readonly Renderer[] renderers;
        private readonly Collider[] colliders;

        private readonly bool[] originalRendererEnabledStates;
        private readonly bool[] originalColliderEnabledStates;
        private readonly Material[][] originalSharedMaterials;

        private Material[][] runtimeTransparentMaterials;
        private bool isUsingTransparentRuntimeMaterials;

        public FloorPiece(GameObject rootObject)
        {
            renderers = rootObject.GetComponentsInChildren<Renderer>(true);
            colliders = rootObject.GetComponentsInChildren<Collider>(true);

            originalRendererEnabledStates = new bool[renderers.Length];
            originalColliderEnabledStates = new bool[colliders.Length];
            originalSharedMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                originalRendererEnabledStates[i] = renderer.enabled;
                originalSharedMaterials[i] = renderer.sharedMaterials;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                originalColliderEnabledStates[i] = collider.enabled;
            }
        }

        public void SetVisible(bool visible)
        {
            if (visible)
                RestoreOriginalMaterialsIfNeeded();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = visible ? originalRendererEnabledStates[i] : false;
            }
        }

        public void SetTransparent(bool transparent, float alpha)
        {
            if (!transparent)
            {
                RestoreOriginalMaterialsIfNeeded();

                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    renderer.enabled = originalRendererEnabledStates[i];
                }

                return;
            }

            CreateTransparentRuntimeMaterialsIfNeeded(alpha);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = originalRendererEnabledStates[i];
            }

            UpdateTransparentAlpha(alpha);
        }

        public void SetCollidersEnabled(bool enabled)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = enabled ? originalColliderEnabledStates[i] : false;
            }
        }

        public void RestoreColliderState()
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                collider.enabled = originalColliderEnabledStates[i];
            }
        }

        public void RestoreOriginalState()
        {
            RestoreOriginalMaterialsIfNeeded();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = originalRendererEnabledStates[i];
            }

            RestoreColliderState();
        }

        private void CreateTransparentRuntimeMaterialsIfNeeded(float alpha)
        {
            if (isUsingTransparentRuntimeMaterials)
                return;

            runtimeTransparentMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] sourceMaterials = originalSharedMaterials[i];
                if (sourceMaterials == null || sourceMaterials.Length == 0)
                    continue;

                Material[] copiedMaterials = new Material[sourceMaterials.Length];

                for (int j = 0; j < sourceMaterials.Length; j++)
                {
                    Material source = sourceMaterials[j];
                    if (source == null)
                        continue;

                    Material copied = new Material(source);
                    SetupTransparentMaterial(copied, alpha);
                    copiedMaterials[j] = copied;
                }

                runtimeTransparentMaterials[i] = copiedMaterials;
                renderer.materials = copiedMaterials;
            }

            isUsingTransparentRuntimeMaterials = true;
        }

        private void UpdateTransparentAlpha(float alpha)
        {
            if (!isUsingTransparentRuntimeMaterials || runtimeTransparentMaterials == null)
                return;

            for (int i = 0; i < runtimeTransparentMaterials.Length; i++)
            {
                Material[] materials = runtimeTransparentMaterials[i];
                if (materials == null)
                    continue;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    ApplyAlpha(material, alpha);
                }
            }
        }

        private void RestoreOriginalMaterialsIfNeeded()
        {
            if (!isUsingTransparentRuntimeMaterials)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.sharedMaterials = originalSharedMaterials[i];
            }

            if (runtimeTransparentMaterials != null)
            {
                for (int i = 0; i < runtimeTransparentMaterials.Length; i++)
                {
                    Material[] materials = runtimeTransparentMaterials[i];
                    if (materials == null)
                        continue;

                    for (int j = 0; j < materials.Length; j++)
                    {
                        Material material = materials[j];
                        if (material == null)
                            continue;

                        if (Application.isPlaying)
                            Object.Destroy(material);
                        else
                            Object.DestroyImmediate(material);
                    }
                }
            }

            runtimeTransparentMaterials = null;
            isUsingTransparentRuntimeMaterials = false;
        }

        private static void SetupTransparentMaterial(Material material, float alpha)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            ApplyAlpha(material, alpha);
        }

        private static void ApplyAlpha(Material material, float alpha)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color.a = alpha;
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                Color color = material.GetColor("_Color");
                color.a = alpha;
                material.SetColor("_Color", color);
            }
        }
    }
}