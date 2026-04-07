using UnityEngine;
using UnityEngine.UI;

public class SmokeVisionEffect : MonoBehaviour
{
    [Header("Trigger To Vision Mapping")]
    [SerializeField] private float particlesForFullEffect = 140f;
    [SerializeField] private float riseSpeed = 6f;
    [SerializeField] private float decaySpeed = 2f;

    [Header("Visual Strength")]
    [SerializeField] private float maxFogAlpha = 0.35f;
    [SerializeField] private float maxVignetteAlpha = 0.7f;

    private Image fogImage;
    private Image vignetteImage;
    private float targetExposure;
    private float currentExposure;

    private void Awake()
    {
        CreateOverlay();
    }

    private void Update()
    {
        targetExposure = Mathf.MoveTowards(targetExposure, 0f, decaySpeed * Time.deltaTime);
        float speed = currentExposure < targetExposure ? riseSpeed : decaySpeed;
        currentExposure = Mathf.MoveTowards(currentExposure, targetExposure, speed * Time.deltaTime);
        ApplyExposure(currentExposure);
    }

    public void SetParticleExposure(int insideParticleCount)
    {
        if (insideParticleCount <= 0)
        {
            return;
        }

        float normalized = Mathf.Clamp01(insideParticleCount / Mathf.Max(1f, particlesForFullEffect));
        targetExposure = Mathf.Max(targetExposure, normalized);
    }

    private void CreateOverlay()
    {
        GameObject canvasGO = new GameObject("SmokeVisionCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>().enabled = false;

        fogImage = CreateFullscreenImage(canvasGO.transform, "SmokeFog");
        fogImage.color = new Color(0.55f, 0.55f, 0.55f, 0f);

        vignetteImage = CreateFullscreenImage(canvasGO.transform, "SmokeVignette");
        vignetteImage.sprite = CreateVignetteSprite();
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.color = new Color(0.45f, 0.45f, 0.45f, 0f);
    }

    private static Image CreateFullscreenImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go.AddComponent<Image>();
    }

    private void ApplyExposure(float exposure)
    {
        if (fogImage != null)
        {
            Color c = fogImage.color;
            c.a = exposure * maxFogAlpha;
            fogImage.color = c;
        }

        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = exposure * maxVignetteAlpha;
            vignetteImage.color = c;
        }
    }

    private static Sprite CreateVignetteSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxR = center.magnitude;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                float edge = Mathf.SmoothStep(0.45f, 1f, d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
