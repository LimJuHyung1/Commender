using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SphereCollider))]
public class Smoke : MonoBehaviour
{
    [Header("연막 설정")]
    [SerializeField] private float lifetime = 5.0f;
    [SerializeField] private float fadeOutTime = 2.0f;
    [SerializeField] private LayerMask targetLayer;

    [Header("디버프 수치 설정")]
    [SerializeField] private float reducedRadius = 1.5f;  // [추가] 이 연막이 줄일 반지름 수치
    [SerializeField] private float debuffDuration = 4.0f; // 디버프 지속 시간

    private ParticleSystem smokeParticle;
    private SphereCollider smokeCollider;

    private void Awake()
    {
        smokeParticle = GetComponentInChildren<ParticleSystem>();
        smokeCollider = GetComponent<SphereCollider>();
    }

    private void Start()
    {
        StartCoroutine(DisappearRoutine());
    }

    private IEnumerator DisappearRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (smokeCollider != null) smokeCollider.enabled = false;
        if (smokeParticle != null) smokeParticle.Stop();
        yield return new WaitForSeconds(fadeOutTime);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            TargetController target = other.GetComponentInParent<TargetController>();
            if (target != null)
            {
                // [수정] 이제 연막탄이 직접 정의한 reducedRadius를 타겟에게 전달합니다.
                Debug.Log($"<color=gray>[Smoke]</color> {other.name}에게 {reducedRadius}의 시야 제한 적용!");
                target.ApplySmokeDebuff(reducedRadius, debuffDuration);
            }
        }
    }
}