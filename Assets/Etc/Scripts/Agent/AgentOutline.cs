using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AgentOutline : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Renderer[] sourceRenderers;
    [SerializeField] private bool includeInactiveChildren = true;

    private readonly List<Renderer> shellRenderers = new List<Renderer>();

    private void Awake()
    {
        BuildShells();
        SetOutlineVisible(false);
    }

    public void SetOutlineVisible(bool state)
    {
        for (int i = 0; i < shellRenderers.Count; i++)
        {
            if (shellRenderers[i] != null)
                shellRenderers[i].enabled = state;
        }
    }

    private void BuildShells()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning($"[{name}] outlineMaterial 이 없습니다.");
            return;
        }

        if (shellRenderers.Count > 0)
            return;

        Renderer[] targets = sourceRenderers != null && sourceRenderers.Length > 0
            ? sourceRenderers
            : GetComponentsInChildren<Renderer>(includeInactiveChildren);

        for (int i = 0; i < targets.Length; i++)
        {
            Renderer source = targets[i];
            if (source == null)
                continue;

            if (source.GetComponent<AgentOutlineShellMarker>() != null)
                continue;

            if (source is MeshRenderer meshRenderer)
            {
                CreateMeshShell(meshRenderer);
            }
            else if (source is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedShell(skinnedMeshRenderer);
            }
        }
    }

    private void CreateMeshShell(MeshRenderer source)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        GameObject shellObject = new GameObject(source.gameObject.name + "_OutlineShell");
        shellObject.layer = source.gameObject.layer;
        shellObject.transform.SetParent(source.transform, false);
        shellObject.AddComponent<AgentOutlineShellMarker>();

        MeshFilter shellFilter = shellObject.AddComponent<MeshFilter>();
        shellFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer shellRenderer = shellObject.AddComponent<MeshRenderer>();
        shellRenderer.sharedMaterials = CreateOutlineMaterials(source.sharedMaterials.Length);
        ApplyRendererSettings(shellRenderer);
        shellRenderer.enabled = false;

        shellRenderers.Add(shellRenderer);
    }

    private void CreateSkinnedShell(SkinnedMeshRenderer source)
    {
        if (source.sharedMesh == null)
            return;

        GameObject shellObject = new GameObject(source.gameObject.name + "_OutlineShell");
        shellObject.layer = source.gameObject.layer;
        shellObject.transform.SetParent(source.transform, false);
        shellObject.AddComponent<AgentOutlineShellMarker>();

        SkinnedMeshRenderer shellRenderer = shellObject.AddComponent<SkinnedMeshRenderer>();
        shellRenderer.sharedMesh = source.sharedMesh;
        shellRenderer.rootBone = source.rootBone;
        shellRenderer.bones = source.bones;
        shellRenderer.sharedMaterials = CreateOutlineMaterials(source.sharedMaterials.Length);
        shellRenderer.localBounds = source.localBounds;
        shellRenderer.updateWhenOffscreen = source.updateWhenOffscreen;
        ApplyRendererSettings(shellRenderer);
        shellRenderer.enabled = false;

        shellRenderers.Add(shellRenderer);
    }

    private Material[] CreateOutlineMaterials(int count)
    {
        int materialCount = Mathf.Max(1, count);
        Material[] materials = new Material[materialCount];

        for (int i = 0; i < materialCount; i++)
            materials[i] = outlineMaterial;

        return materials;
    }

    private void ApplyRendererSettings(Renderer renderer)
    {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }
}

public class AgentOutlineShellMarker : MonoBehaviour
{
}