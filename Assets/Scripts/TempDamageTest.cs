using UnityEngine;

public class TempDamageTest : MonoBehaviour
{
    [Tooltip("Arraste o SmokeHealthReceiver do Player para aqui. Se ficar vazio, o script tenta encontrar o Player automaticamente.")]
    public SmokeHealthReceiver playerHealth;
    
    [Tooltip("Quantidade de vida a retirar por segundo.")]
    public float damagePerSecond = 20f;

    private float timer = 0f;

    private void Start()
    {
        // Tenta encontrar a vida do player caso não tenha sido arrastado no Inspector
        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<SmokeHealthReceiver>();
            }
        }
    }

    private void Update()
    {
        if (playerHealth != null)
        {
            timer += Time.deltaTime;
            
            // Quando passar 1 segundo, tira vida e reseta o temporizador
            if (timer >= 1f)
            {
                playerHealth.TakeSmokeDamage(damagePerSecond);
                timer = 0f;
            }
        }
    }
}
