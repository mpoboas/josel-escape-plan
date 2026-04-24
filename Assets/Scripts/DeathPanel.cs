using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathPanel : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Arraste o SmokeHealthReceiver do Player para aqui. Se ficar vazio, o script tentará encontrar automaticamente.")]
    [SerializeField] private SmokeHealthReceiver playerHealth;
    [Tooltip("Arraste o GameObject que contém o painel visual da morte.")]
    [SerializeField] private GameObject deathPanelObject;
    
    [Header("Definições")]
    [Tooltip("Nome da cena do Menu Principal")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isDead = false;

    private void Start()
    {
        // Garante que o painel começa desativado
        if (deathPanelObject != null)
        {
            deathPanelObject.SetActive(false);
        }

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
        // Verifica se o player ficou com vida <= 0 e ainda não ativamos o painel
        if (!isDead && playerHealth != null && playerHealth.health <= 0f)
        {
            Die();
        }

        // Se estiver morto, aguarda pelo Enter para carregar o EndPanel
        if (isDead)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TransitionToEndPanel();
            }
        }
    }

    private void Die()
    {
        isDead = true;
        GameAudioManager.Instance?.PlayFailOnce();

        // Ativa o painel de morte
        if (deathPanelObject != null)
        {
            deathPanelObject.SetActive(true);
        }

        // Limpa efeitos visuais de fumo/fogo para o painel ficar legível
        var visionEffect = Object.FindAnyObjectByType<SmokeVisionEffect>();
        if (visionEffect != null)
        {
            visionEffect.ClearEffects();
        }

        // Pára o tempo no jogo para interromper movimentação/fumo
        Time.timeScale = 0f;

        // Desbloquear e mostrar o cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void TransitionToEndPanel()
    {
        // Desativa este painel
        if (deathPanelObject != null)
            deathPanelObject.SetActive(false);

        // Procura e ativa o EndPanel (o uGUI correto agora é o único EndPanelController na cena)
        var endPanel = Object.FindAnyObjectByType<EndPanelController>(FindObjectsInactive.Include);
        if (endPanel != null)
        {
            endPanel.gameObject.SetActive(true);
            Debug.Log("[DeathPanel] Transitioned to EndPanel.");
        }
        else
        {
            Debug.LogError("[DeathPanel] EndPanelController not found. Loading menu as fallback.");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
