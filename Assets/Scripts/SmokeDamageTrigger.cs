using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SmokeDamageTrigger : MonoBehaviour
{
    [SerializeField] private bool applySmokeDamageToPlayer = false;
    [SerializeField] private SmokeHealthReceiver smokeHealthReceiver;
    [SerializeField] private SmokeVisionEffect smokeVisionEffect;
    [SerializeField] private float baseDamagePerParticle = 1f;
    [SerializeField] private int initialBufferSize = 256;

    private ParticleSystem particleSystemRef;
    private List<ParticleSystem.Particle> insideParticles;

    public void Configure(SmokeHealthReceiver receiver, float damagePerParticle = -1f)
    {
        smokeHealthReceiver = receiver;
        if (damagePerParticle > 0f)
        {
            baseDamagePerParticle = damagePerParticle;
        }
    }

    private void Awake()
    {
        particleSystemRef = GetComponent<ParticleSystem>();
        insideParticles = new List<ParticleSystem.Particle>(Mathf.Max(16, initialBufferSize));
        TryResolveReceiver();
        TryResolveVisionEffect();
    }

    private void OnParticleTrigger()
    {
        if (particleSystemRef == null)
        {
            return;
        }

        int insideCount = particleSystemRef.GetTriggerParticles(
            ParticleSystemTriggerEventType.Inside,
            insideParticles
        );

        if (insideCount <= 0)
        {
            return;
        }

        if (smokeVisionEffect == null)
        {
            TryResolveVisionEffect();
        }

        if (smokeVisionEffect != null)
        {
            smokeVisionEffect.SetParticleExposure(insideCount);
        }

        if (!applySmokeDamageToPlayer)
        {
            return;
        }

        if (smokeHealthReceiver == null)
        {
            TryResolveReceiver();
            if (smokeHealthReceiver == null)
            {
                return;
            }
        }

        float damage = insideCount * baseDamagePerParticle * Time.deltaTime;
        smokeHealthReceiver.TakeSmokeDamage(damage);
    }

    private void TryResolveReceiver()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            smokeHealthReceiver = player.GetComponent<SmokeHealthReceiver>();
        }
    }

    private void TryResolveVisionEffect()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            smokeVisionEffect = player.GetComponent<SmokeVisionEffect>();
        }
    }
}
