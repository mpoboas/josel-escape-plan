using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SmokeDamageTrigger : MonoBehaviour
{
    [SerializeField] private SmokeHealthReceiver smokeHealthReceiver;
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
    }

    private void OnParticleTrigger()
    {
        if (particleSystemRef == null)
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

        int insideCount = particleSystemRef.GetTriggerParticles(
            ParticleSystemTriggerEventType.Inside,
            insideParticles
        );

        if (insideCount <= 0)
        {
            return;
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
}
