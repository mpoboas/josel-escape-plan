using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach this to the Player (or its Camera child).
/// Every frame it sends a raycast from the camera centre;
/// if the hit object (or any of its parents) implements IInteractable,
/// an optional UI prompt is shown and pressing E calls Interact().
///
/// Setup checklist:
///   1. Attach this script to the same GameObject as FirstPersonController
///      (or drag the player camera into the 'playerCamera' field).
///   2. Make sure the door handle has a Collider (any type) and the
///      DoorController on the door root implements IInteractable.
///   3. (Optional) Create a UI Text/TMP element for the interaction hint
///      and drag it into the 'interactPrompt' field.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Ray Settings")]
    [Tooltip("The camera used to cast the interaction ray. " +
             "Leave empty to use Camera.main.")]
    public Camera playerCamera;

    [Tooltip("Maximum distance (metres) within which the player can interact.")]
    public float interactRange = 3f;

    [Tooltip("Layer mask – set to the layer(s) your door handles are on. " +
             "Leave as 'Everything' to hit any collider.")]
    public LayerMask interactMask = ~0;

    [Header("Input")]
    [Tooltip("Key the player presses to interact.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Key the player presses to inspect an object (e.g. check temperature).")]
    public KeyCode inspectKey = KeyCode.R;

    [Header("UI Prompt (optional)")]
    [Tooltip("A UI Text or TMP_Text component that shows the interaction hint. " +
             "Assign in the Inspector; leave empty to skip.")]
    public Text interactPrompt;   // swap for TMP_Text if you use TextMeshPro

    [Header("Box Prompt UI")]
    [SerializeField] private Text boxLookPrompt;
    [SerializeField] private Text boxCarryPrompt;

    [Tooltip("Componente InspectUI para mostrar o toast de inspeção.")]
    public InspectUI inspectUI;

    [Header("Hand feedback (optional)")]
    [Tooltip("Cosmetic first-person hand on E / R. Leave empty to auto-use PlayerHandFeedback on the player camera, if present.")]
    [SerializeField] private PlayerHandFeedback handFeedback;

    [Header("Heat inspect (R)")]
    [Tooltip("Seconds the hand stays at the inspect pose after it reaches it. Total channel time adds move-in and move-out from Player Hand Feedback.")]
    [Min(0f)] public float heatInspectPeakHoldSeconds = 2.5f;

    [Tooltip("Optional UI for the channel bar. If unset, a small bar is created above the Reticle when possible.")]
    [SerializeField] private HeatInspectCooldownUI heatInspectBarUi;

    // ── runtime state ──────────────────────────────────────────────────
    private IInteractable _currentTarget;
    private Coroutine _heatInspectRoutine;
    private bool _heatInspectChannelRunning;
    private CarryableBox _carriedBox;
    private bool _throwChargeActive;
    private float _throwChargeStartTimeUnscaled;

    // ───────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _currentTarget = null;
        _carriedBox = null;
        _throwChargeActive = false;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            Debug.LogError("[PlayerInteraction] No camera found! " +
                           "Assign 'playerCamera' in the Inspector.");

        if (handFeedback == null && playerCamera != null)
            handFeedback = playerCamera.GetComponent<PlayerHandFeedback>();

        if (heatInspectBarUi == null)
        {
            heatInspectBarUi = GetComponent<HeatInspectCooldownUI>();
            if (heatInspectBarUi == null)
                heatInspectBarUi = gameObject.AddComponent<HeatInspectCooldownUI>();
        }

        heatInspectBarUi.AutoSetup(transform);
        heatInspectBarUi.Hide();

        EnsureInteractPromptUi();
        // Hide prompt at startup
        SetPromptVisible(false);
        SetupBoxPromptUi();
        SetBoxLookPromptVisible(false);
        SetBoxCarryPromptVisible(false);
    }

    private void OnDisable()
    {
        _currentTarget = null;
        _carriedBox = null;
        _throwChargeActive = false;
        SetPromptVisible(false);
        SetBoxLookPromptVisible(false);
        SetBoxCarryPromptVisible(false);

        if (_heatInspectRoutine != null)
        {
            StopCoroutine(_heatInspectRoutine);
            _heatInspectRoutine = null;
        }

        _heatInspectChannelRunning = false;
        if (heatInspectBarUi != null)
            heatInspectBarUi.Hide();
    }

    private void Update()
    {
        ScanForInteractable();
        UpdateBoxPromptState();

        if (HandleCarriedBoxInput())
        {
            return;
        }

        if (_currentTarget != null)
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (_currentTarget is CarryableBox carryable)
                {
                    if (playerCamera != null && carryable.TryPickup(playerCamera.transform, transform))
                    {
                        _carriedBox = carryable;
                        _throwChargeActive = false;
                    }
                    return;
                }

                handFeedback?.PlayGesture(PlayerHandFeedback.HandGestureKind.Interact);
                _currentTarget.Interact();
            }

            if (Input.GetKeyDown(inspectKey))
            {
                IInspectable inspectable = (_currentTarget as MonoBehaviour)?.GetComponent<IInspectable>();
                if (inspectable != null && !_heatInspectChannelRunning)
                {
                    if (_heatInspectRoutine != null)
                        StopCoroutine(_heatInspectRoutine);
                    _heatInspectRoutine = StartCoroutine(HeatInspectChannelRoutine(inspectable));
                }
            }
        }
    }

    // ── core raycast ────────────────────────────────────────────────────

    private void ScanForInteractable()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position,
                          playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {
            // Walk up the hierarchy so the collider can be on a child object
            // (e.g. the handle) while IInteractable lives on the door root.
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                SetTarget(interactable);
                return;
            }

            CarryableBox carryable = ResolveCarryableBoxTarget(hit.collider);
            if (carryable != null)
            {
                SetTarget(carryable);
                return;
            }
        }

        // Nothing found – clear target
        SetTarget(null);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private void SetTarget(IInteractable target)
    {
        if (target == _currentTarget) return;

        _currentTarget = target;

        if (_carriedBox != null && _carriedBox.IsHeld)
        {
            return;
        }

        if (_currentTarget is CarryableBox)
        {
            SetPromptVisible(false);
        }
        else if (_currentTarget != null)
        {
            SetPromptText(_currentTarget.GetInteractText());
            SetPromptVisible(true);
        }
        else
        {
            SetPromptVisible(false);
        }
    }

    private void SetPromptText(string text)
    {
        if (interactPrompt != null)
            interactPrompt.text = text;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactPrompt != null)
            interactPrompt.gameObject.SetActive(visible);
    }

    private IEnumerator HeatInspectChannelRoutine(IInspectable inspectable)
    {
        _heatInspectChannelRunning = true;

        try
        {
            float peak = heatInspectPeakHoldSeconds;
            float moveIn = handFeedback != null ? handFeedback.moveInDuration : 0.35f;
            float moveOut = handFeedback != null ? handFeedback.moveOutDuration : 0.35f;
            float total = moveIn + peak + moveOut;

            heatInspectBarUi.AutoSetup(transform);
            heatInspectBarUi.Show();

            handFeedback?.PlayHeatInspectChannel(peak, null);

            for (float elapsed = 0f; elapsed < total;)
            {
                elapsed += Time.deltaTime;
                heatInspectBarUi.SetFillRemaining(1f - Mathf.Clamp01(elapsed / total));
                yield return null;
            }

            heatInspectBarUi.Hide();
            heatInspectBarUi.SetFillRemaining(1f);

            while (handFeedback != null && handFeedback.IsGestureActive)
                yield return null;

            InspectResult result = inspectable.Inspect();
            inspectUI?.ShowToast(result);
        }
        finally
        {
            _heatInspectChannelRunning = false;
            _heatInspectRoutine = null;
        }
    }

    private CarryableBox ResolveCarryableBoxTarget(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        CarryableBox existing = hitCollider.GetComponentInParent<CarryableBox>();
        if (existing != null)
        {
            return existing;
        }

        Transform candidate = hitCollider.transform;
        while (candidate != null)
        {
            if (string.Equals(candidate.name, "Box", System.StringComparison.OrdinalIgnoreCase))
            {
                if (candidate.GetComponentInParent<DoorController>() != null)
                {
                    return null;
                }

                return candidate.gameObject.AddComponent<CarryableBox>();
            }

            candidate = candidate.parent;
        }

        return null;
    }

    private bool HandleCarriedBoxInput()
    {
        if (_carriedBox == null || !_carriedBox.IsHeld)
        {
            _carriedBox = null;
            _throwChargeActive = false;
            return false;
        }

        if (Input.GetKeyDown(interactKey))
        {
            _throwChargeActive = true;
            _throwChargeStartTimeUnscaled = Time.unscaledTime;
        }

        if (_throwChargeActive && Input.GetKeyUp(interactKey))
        {
            float heldSeconds = Mathf.Max(0f, Time.unscaledTime - _throwChargeStartTimeUnscaled);
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            _carriedBox.Release(heldSeconds, forward);
            handFeedback?.PlayGesture(PlayerHandFeedback.HandGestureKind.Interact);

            _carriedBox = null;
            _throwChargeActive = false;
        }

        return true;
    }

    private void SetupBoxPromptUi()
    {
        if (boxLookPrompt != null && boxCarryPrompt != null)
        {
            return;
        }

        Transform uiParent = interactPrompt != null ? interactPrompt.transform.parent : null;
        Canvas hostCanvas = uiParent != null ? uiParent.GetComponentInParent<Canvas>() : null;

        if (hostCanvas == null)
        {
            GameObject canvasGO = new GameObject("BoxPromptCanvas");
            canvasGO.transform.SetParent(transform, false);
            hostCanvas = canvasGO.AddComponent<Canvas>();
            hostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hostCanvas.sortingOrder = 32000;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>().enabled = false;
            uiParent = canvasGO.transform;
        }
        else if (uiParent == null)
        {
            uiParent = hostCanvas.transform;
        }

        if (boxLookPrompt == null)
        {
            boxLookPrompt = CreatePromptText(
                uiParent,
                "BoxLookPrompt",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 34f),
                24,
                TextAnchor.MiddleCenter
            );
        }

        if (boxCarryPrompt == null)
        {
            boxCarryPrompt = CreatePromptText(
                uiParent,
                "BoxCarryPrompt",
                new Vector2(0.5f, 0.25f),
                new Vector2(0.5f, 0.25f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                24,
                TextAnchor.MiddleCenter
            );
        }
    }

    private void EnsureInteractPromptUi()
    {
        if (interactPrompt != null)
        {
            return;
        }

        Transform uiParent = null;
        Canvas hostCanvas = GetComponentInChildren<Canvas>(true);
        if (hostCanvas == null)
        {
            GameObject canvasGO = new GameObject("InteractionPromptCanvas");
            canvasGO.transform.SetParent(transform, false);
            hostCanvas = canvasGO.AddComponent<Canvas>();
            hostCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hostCanvas.sortingOrder = 32000;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>().enabled = false;
            uiParent = canvasGO.transform;
        }
        else
        {
            uiParent = hostCanvas.transform;
        }

        interactPrompt = CreatePromptText(
            uiParent,
            "InteractPrompt",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -30f),
            24,
            TextAnchor.MiddleCenter
        );
    }

    private static Text CreatePromptText(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        int fontSize,
        TextAnchor alignment
    )
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(520f, 44f);

        Text text = go.AddComponent<Text>();
        text.font = LoadBuiltinUiFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Font LoadBuiltinUiFont()
    {
        try
        {
            Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacy != null)
            {
                return legacy;
            }
        }
        catch
        {
        }

        try
        {
            Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (arial != null)
            {
                return arial;
            }
        }
        catch
        {
        }

        Font dynamic = Font.CreateDynamicFontFromOSFont("Arial", 16);
        if (dynamic == null)
        {
            dynamic = Font.CreateDynamicFontFromOSFont("Helvetica", 16);
        }
        return dynamic;
    }

    private void UpdateBoxPromptState()
    {
        bool carryingBox = _carriedBox != null && _carriedBox.IsHeld;
        bool aimingAtBox = !carryingBox && _currentTarget is CarryableBox;

        SetBoxLookPromptVisible(aimingAtBox);
        SetBoxCarryPromptVisible(carryingBox);
    }

    private void SetBoxLookPromptVisible(bool visible)
    {
        if (boxLookPrompt == null)
        {
            return;
        }

        if (visible)
        {
            boxLookPrompt.text = "Press \"E\" to pick up";
        }
        boxLookPrompt.gameObject.SetActive(visible);
    }

    private void SetBoxCarryPromptVisible(bool visible)
    {
        if (boxCarryPrompt == null)
        {
            return;
        }

        if (visible)
        {
            boxCarryPrompt.text = "Press \"E\" to drop";
        }
        boxCarryPrompt.gameObject.SetActive(visible);
    }

    // ── editor visualisation ────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        Gizmos.color = _currentTarget != null ? Color.green : Color.yellow;
        Gizmos.DrawRay(playerCamera.transform.position,
                       playerCamera.transform.forward * interactRange);
    }
#endif
}
