using UnityEngine;
using TMPro;

/// <summary>
/// Gere o toast de inspeção na HUD.
/// Arrastar no Inspector: o painel (InspectPanel), o texto (InspectText) e o background (Image).
/// </summary>
public class InspectUI : MonoBehaviour
{
    [Header("Referências UI")]
    public GameObject       panel;
    public TMP_Text         messageText;
    public UnityEngine.UI.Image background;

    [Header("Cores")]
    public Color safeColor   = new Color(0.18f, 0.72f, 0.25f, 0.92f); // verde
    public Color dangerColor = new Color(0.88f, 0.22f, 0.18f, 0.92f); // vermelho

    [Header("Duração")]
    [Tooltip("Segundos até o toast desaparecer.")]
    public float displayDuration = 3f;

    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Mostra o toast com a mensagem e cor consoante o resultado.</summary>
    public void ShowToast(InspectResult result)
    {
        if (panel == null || messageText == null) return;

        messageText.text = result.message;

        if (background != null)
            background.color = result.isSafe ? safeColor : dangerColor;

        panel.SetActive(true);

        // Reinicia o timer se já estiver a mostrar
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private System.Collections.IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        if (panel != null) panel.SetActive(false);
    }
}
