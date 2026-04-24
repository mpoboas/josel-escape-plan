using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SmokeVisionEffect : MonoBehaviour
{
    [Header("Trigger To Vision Mapping")]
    [SerializeField] private float particlesForFullEffect = 6f;
    [SerializeField, Range(0f, 1f)] private float minSmokeExposureWhileInside = 0.92f;
    [SerializeField] private float riseSpeed = 6f;
    [SerializeField] private float decaySpeed = 2f;

    [Header("Smoke Visual Strength")]
    [SerializeField] private float maxFogAlpha = 0.96f;
    [SerializeField] private float maxVignetteAlpha = 1f;
    
    [Header("Flame Visual Strength")]
    [SerializeField] private float maxFlameTintAlpha = 0.35f;

    [Header("Start Siren Vignette")]
    [SerializeField] private bool playSirenPulseOnStart = true;
    [SerializeField] private float sirenStartDelay = 0f;
    [SerializeField] private float sirenPulseFrequency = 2.2f;
    [SerializeField] private float sirenPulseMaxAlpha = 0.6f;
    [SerializeField] private Color sirenVignetteColor = new Color(0.95f, 0.02f, 0.02f, 1f);
    [SerializeField] private Material ceillingLightMaterial;
    [SerializeField] private Material ceillingLightEmergencyMaterial;

    private Image fogImage;
    private Image vignetteImage;
    private Image flameTintImage;
    private Image sirenVignetteImage;
    private float smokeTargetExposure;
    private float smokeCurrentExposure;
    private float flameTargetExposure;
    private float flameCurrentExposure;
    private bool suppressSmokeVisual;
    private bool sirenPulseActive;
    private float sirenDelayTimeLeft;
    private float sirenPulseElapsed;
    private bool sirenVisualStarted;
    private bool emergencyLightsActive;
    private readonly Dictionary<Renderer, Material[]> originalCeillingLightMaterials = new Dictionary<Renderer, Material[]>();

    private void Awake()
    {
        CreateOverlay();
    }

    private void Start()
    {
        // The GameManager owns siren/alarm start timing so visual and audio stay in sync.
    }

    private void Update()
    {
        if (suppressSmokeVisual)
        {
            smokeTargetExposure = 0f;
            smokeCurrentExposure = Mathf.MoveTowards(smokeCurrentExposure, 0f, riseSpeed * Time.deltaTime);
        }

        smokeTargetExposure = Mathf.MoveTowards(smokeTargetExposure, 0f, decaySpeed * Time.deltaTime);
        float smokeSpeed = smokeCurrentExposure < smokeTargetExposure ? riseSpeed : decaySpeed;
        smokeCurrentExposure = Mathf.MoveTowards(
            smokeCurrentExposure,
            smokeTargetExposure,
            smokeSpeed * Time.deltaTime
        );

        flameTargetExposure = Mathf.MoveTowards(flameTargetExposure, 0f, decaySpeed * Time.deltaTime);
        float flameSpeed = flameCurrentExposure < flameTargetExposure ? riseSpeed : decaySpeed;
        flameCurrentExposure = Mathf.MoveTowards(
            flameCurrentExposure,
            flameTargetExposure,
            flameSpeed * Time.deltaTime
        );

        ApplyExposure(smokeCurrentExposure, flameCurrentExposure);
        UpdateSirenPulse();
    }

    public void SetParticleExposure(int insideParticleCount)
    {
        SetSmokeExposure(insideParticleCount);
    }

    public void SetSmokeExposure(int insideParticleCount)
    {
        if (suppressSmokeVisual)
        {
            return;
        }

        if (insideParticleCount <= 0)
        {
            return;
        }

        float normalizedFromParticles = Mathf.Clamp01(insideParticleCount / Mathf.Max(1f, particlesForFullEffect));
        float normalized = Mathf.Max(minSmokeExposureWhileInside, normalizedFromParticles);
        smokeTargetExposure = Mathf.Max(smokeTargetExposure, normalized);
    }

    public void SetSmokeVisualSuppressed(bool suppressed)
    {
        suppressSmokeVisual = suppressed;
        if (suppressed)
        {
            smokeTargetExposure = 0f;
        }
    }

    public void SetFlameExposure01(float normalizedExposure)
    {
        if (normalizedExposure <= 0f)
        {
            return;
        }

        flameTargetExposure = Mathf.Max(flameTargetExposure, Mathf.Clamp01(normalizedExposure));
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
        fogImage.color = new Color(0.02f, 0.02f, 0.02f, 0f);

        vignetteImage = CreateFullscreenImage(canvasGO.transform, "SmokeVignette");
        vignetteImage.sprite = CreateVignetteSprite();
        vignetteImage.type = Image.Type.Simple;
        vignetteImage.color = new Color(0.01f, 0.01f, 0.01f, 0f);

        flameTintImage = CreateFullscreenImage(canvasGO.transform, "FlameTint");
        flameTintImage.color = new Color(0.95f, 0.12f, 0.06f, 0f);

        sirenVignetteImage = CreateFullscreenImage(canvasGO.transform, "SirenVignette");
        sirenVignetteImage.sprite = CreateVignetteSprite();
        sirenVignetteImage.type = Image.Type.Simple;
        sirenVignetteImage.color = new Color(sirenVignetteColor.r, sirenVignetteColor.g, sirenVignetteColor.b, 0f);
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

    private void ApplyExposure(float smokeExposure, float flameExposure)
    {
        if (fogImage != null)
        {
            Color c = fogImage.color;
            c.a = smokeExposure * maxFogAlpha;
            fogImage.color = c;
        }

        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = smokeExposure * maxVignetteAlpha;
            vignetteImage.color = c;
        }

        if (flameTintImage != null)
        {
            Color c = flameTintImage.color;
            c.a = flameExposure * maxFlameTintAlpha;
            flameTintImage.color = c;
        }
    }

    /// <summary>
    /// Instantly clears all visual overlays (fog, vignette, flame tint).
    /// </summary>
    public void ClearEffects()
    {
        smokeTargetExposure = 0f;
        smokeCurrentExposure = 0f;
        flameTargetExposure = 0f;
        flameCurrentExposure = 0f;
        StopSirenPulse();
        ApplyExposure(0f, 0f);
        
        if (fogImage != null && fogImage.canvas != null)
        {
            fogImage.canvas.enabled = false;
        }
    }

    public void TriggerSirenPulse()
    {
        TriggerSirenPulse(sirenStartDelay);
    }

    public void TriggerSirenPulse(float startDelaySeconds)
    {
        sirenPulseActive = true;
        sirenDelayTimeLeft = Mathf.Max(0f, startDelaySeconds);
        sirenPulseElapsed = 0f;
        sirenVisualStarted = false;
    }

    public void StopSirenPulse()
    {
        sirenPulseActive = false;
        sirenDelayTimeLeft = 0f;
        sirenPulseElapsed = 0f;
        sirenVisualStarted = false;
        ApplySirenAlpha(0f);
        RestoreCeillingLightMaterials();
        GameAudioManager.Instance?.StopAlarmLoop();
    }

    private void UpdateSirenPulse()
    {
        if (sirenVignetteImage == null)
        {
            return;
        }

        if (!sirenPulseActive)
        {
            ApplySirenAlpha(0f);
            return;
        }

        if (sirenDelayTimeLeft > 0f)
        {
            sirenDelayTimeLeft -= Time.deltaTime;
            ApplySirenAlpha(0f);
            return;
        }

        if (!sirenVisualStarted)
        {
            sirenVisualStarted = true;
            ApplyEmergencyCeillingLightMaterials();
            GameAudioManager.Instance?.StartAlarmLoop();
        }

        sirenPulseElapsed += Time.deltaTime;
        float wave = (Mathf.Sin(sirenPulseElapsed * sirenPulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = wave * sirenPulseMaxAlpha;
        ApplySirenAlpha(alpha);
    }

    private void ApplySirenAlpha(float alpha)
    {
        if (sirenVignetteImage == null)
        {
            return;
        }

        Color c = sirenVignetteImage.color;
        c.r = sirenVignetteColor.r;
        c.g = sirenVignetteColor.g;
        c.b = sirenVignetteColor.b;
        c.a = Mathf.Clamp01(alpha);
        sirenVignetteImage.color = c;
    }

    private void ApplyEmergencyCeillingLightMaterials()
    {
        if (emergencyLightsActive)
        {
            return;
        }

        if (!ResolveCeillingLightMaterials())
        {
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            Material[] shared = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < shared.Length; i++)
            {
                if (IsCeillingLightMaterial(shared[i]))
                {
                    if (!originalCeillingLightMaterials.ContainsKey(r))
                    {
                        originalCeillingLightMaterials[r] = (Material[])shared.Clone();
                    }

                    shared[i] = ceillingLightEmergencyMaterial;
                    changed = true;
                }
            }

            if (changed)
            {
                r.sharedMaterials = shared;
            }
        }

        emergencyLightsActive = true;
    }

    private void RestoreCeillingLightMaterials()
    {
        if (!emergencyLightsActive)
        {
            return;
        }

        foreach (KeyValuePair<Renderer, Material[]> kv in originalCeillingLightMaterials)
        {
            if (kv.Key != null)
            {
                kv.Key.sharedMaterials = kv.Value;
            }
        }

        originalCeillingLightMaterials.Clear();
        emergencyLightsActive = false;
    }

    private bool ResolveCeillingLightMaterials()
    {
        if (ceillingLightMaterial != null && ceillingLightEmergencyMaterial != null)
        {
            return true;
        }

        Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material mat in allMaterials)
        {
            if (mat == null)
            {
                continue;
            }

            if (ceillingLightMaterial == null && MaterialNameEquals(mat, "CeillingLight"))
            {
                ceillingLightMaterial = mat;
            }
            else if (ceillingLightEmergencyMaterial == null && MaterialNameEquals(mat, "CeillingLightEmergency"))
            {
                ceillingLightEmergencyMaterial = mat;
            }
        }

        return ceillingLightMaterial != null && ceillingLightEmergencyMaterial != null;
    }

    private bool IsCeillingLightMaterial(Material mat)
    {
        if (mat == null)
        {
            return false;
        }

        if (ceillingLightMaterial != null && mat == ceillingLightMaterial)
        {
            return true;
        }

        return MaterialNameEquals(mat, "CeillingLight");
    }

    private static bool MaterialNameEquals(Material mat, string expected)
    {
        if (mat == null)
        {
            return false;
        }

        string n = mat.name;
        if (n.EndsWith(" (Instance)"))
        {
            n = n.Substring(0, n.Length - " (Instance)".Length);
        }

        return n == expected;
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
