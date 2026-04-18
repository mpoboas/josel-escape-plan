using UnityEngine;

/// <summary>
/// Place under <c>Floor N / Signage / …</c>. Picks a sprite from a <see cref="SignageCatalog"/> and shows it
/// flat on the floor so the map ortho camera bakes it into the wall map texture.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MapSignagePlacement : MonoBehaviour
{
    [Tooltip("Shared catalog; assign the SignageCatalog asset (regenerate via Assets menu).")]
    public SignageCatalog catalog;

    [Tooltip("Index into the catalog; use the inspector dropdown when catalog is set.")]
    public int symbolIndex;

    [Tooltip("Approximate width/height in world units after laying the sprite flat.")]
    [Min(0.01f)] public float worldSize = 1f;

    [Tooltip("Slight lift above the floor to reduce z-fighting in the map capture.")]
    public float yOffset = 0.02f;

    private const string VisualChildName = "SignageVisual";

    /// <summary>Draw after typical opaque floor/walls in the map ortho pass (sprite + queue).</summary>
    private const int MapDrawSortingOrder = 30000;

    /// <summary>Late transparent pass so signage draws after most scene geometry in the ortho capture.</summary>
    private const int MapDrawRenderQueue = 3990;

    private Material _overlayMaterial;
    private Sprite _overlaySprite;

    private void Awake()
    {
        ApplyVisual();
    }

    private void Start()
    {
        ApplyVisual();
    }

    private void OnValidate()
    {
        ApplyVisual();
    }

    /// <summary>Rebuilds the child <see cref="SpriteRenderer"/> from current fields.</summary>
    public void ApplyVisual()
    {
        if (catalog == null || catalog.entries == null || catalog.entries.Count == 0)
        {
            ClearVisual();
            return;
        }

        symbolIndex = Mathf.Clamp(symbolIndex, 0, catalog.entries.Count - 1);
        var entry = catalog.entries[symbolIndex];
        if (entry?.sprite == null)
        {
            ClearVisual();
            return;
        }

        var sprite = entry.sprite;
        var visTransform = GetOrCreateVisualTransform();
        var sr = visTransform.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = visTransform.gameObject.AddComponent<SpriteRenderer>();

        if (_overlayMaterial != null && _overlaySprite != sprite)
            DestroyOverlayMaterial();

        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = MapDrawSortingOrder;

        // SpriteRenderer.sharedMaterial can be null before the sprite is set (or in some URP/editor paths); never pass null to new Material().
        if (_overlayMaterial == null)
            _overlayMaterial = CreateOverlayMaterial(sr);

        _overlayMaterial.renderQueue = MapDrawRenderQueue;
        if (_overlayMaterial.HasProperty("_Color"))
            _overlayMaterial.SetColor("_Color", Color.white);
        sr.material = _overlayMaterial;
        _overlaySprite = sprite;

        visTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visTransform.localPosition = Vector3.up * yOffset;

        var maxDim = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y, 0.01f);
        var uniform = worldSize / maxDim;
        visTransform.localScale = Vector3.one * uniform;
    }

    private Transform GetOrCreateVisualTransform()
    {
        var t = transform.Find(VisualChildName);
        if (t != null)
            return t;

        var go = new GameObject(VisualChildName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private void ClearVisual()
    {
        var t = transform.Find(VisualChildName);
        if (t != null)
        {
            if (Application.isPlaying)
                Destroy(t.gameObject);
            else
                DestroyImmediate(t.gameObject);
        }

        DestroyOverlayMaterial();
    }

    private void OnDestroy()
    {
        DestroyOverlayMaterial();
    }

    private void DestroyOverlayMaterial()
    {
        if (_overlayMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(_overlayMaterial);
        else
            DestroyImmediate(_overlayMaterial);
        _overlayMaterial = null;
        _overlaySprite = null;
    }

    private static Material CreateOverlayMaterial(SpriteRenderer sr)
    {
        // Prefer dedicated shader: ZTest Always + late queue so floor/walls never depth-occlude map symbols.
        var overlayShader = Shader.Find("Signage/MapSignageOverlay");
        Material mat;
        if (overlayShader != null)
        {
            mat = new Material(overlayShader);
        }
        else
        {
            Material basis = sr.sharedMaterial;
            if (basis != null)
                mat = new Material(basis);
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Transparent");
                if (shader == null)
                    throw new System.InvalidOperationException(
                        "[MapSignagePlacement] Signage/MapSignageOverlay shader missing and no fallback sprite shader found.");
                mat = new Material(shader);
            }
        }

        mat.name = "MapSignageOverlay";
        mat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        mat.renderQueue = MapDrawRenderQueue;
        return mat;
    }
}
