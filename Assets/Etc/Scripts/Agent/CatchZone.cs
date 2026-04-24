using UnityEngine;
using System;

public class CatchZone : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private LayerMask targetLayer;

    public static event Action<GameObject> OnTargetCaught;

    public static AgentController LastCatchingAgent { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;

        TargetHologram targetHologram = other.GetComponentInParent<TargetHologram>();
        if (targetHologram != null)
        {
            Debug.Log($"<color=cyan>[CatchZone]</color> {targetHologram.name} 은(는) 타겟 홀로그램이므로 체포 처리하지 않습니다.");
            return;
        }

        if (((1 << other.gameObject.layer) & targetLayer) == 0)
            return;

        TargetController targetController = other.GetComponentInParent<TargetController>();

        if (targetController != null)
        {
            if (targetController.TryActivateEmergencyEscape())
            {
                Debug.Log($"<color=orange>[CatchZone]</color> {targetController.name} 이(가) 긴급 회피를 사용해 포획을 회피했습니다.");
                return;
            }

            if (!targetController.CanBeCaught)
            {
                Debug.Log($"<color=orange>[CatchZone]</color> {targetController.name} 은(는) 현재 포획 불가 상태입니다.");
                return;
            }
        }

        GameObject caughtObject = targetController != null ? targetController.gameObject : other.gameObject;

        LastCatchingAgent = GetComponentInParent<AgentController>();

        Debug.Log($"<color=yellow>[CatchZone]</color> {caughtObject.name} 포획 성공!");
        OnTargetCaught?.Invoke(caughtObject);

        var targetAgent = caughtObject.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (targetAgent != null)
            targetAgent.isStopped = true;
    }
}