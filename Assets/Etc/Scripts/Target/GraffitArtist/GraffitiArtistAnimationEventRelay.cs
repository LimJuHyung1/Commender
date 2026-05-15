using UnityEngine;

public class GraffitiArtistAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private GraffitiArtist owner;
    [SerializeField] private bool enableDebugLog = true;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<GraffitiArtist>();

        if (enableDebugLog)
        {
            Debug.Log(
                $"[GraffitiArtistAnimationEventRelay] Awake. Owner Found: {owner != null}"
            );
        }
    }

    public void SetOwner(GraffitiArtist graffitiArtist)
    {
        owner = graffitiArtist;
    }

    public void OnGraffitiSkillAnimationFinished()
    {
        if (enableDebugLog)
            Debug.Log("[GraffitiArtistAnimationEventRelay] Animation Event received.");

        if (owner == null)
            owner = GetComponentInParent<GraffitiArtist>();

        if (owner == null)
        {
            if (enableDebugLog)
                Debug.LogWarning("[GraffitiArtistAnimationEventRelay] Owner is null.");

            return;
        }

        owner.OnGraffitiSkillAnimationFinished();
    }
}