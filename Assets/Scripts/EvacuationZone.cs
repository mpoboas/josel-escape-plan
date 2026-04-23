using UnityEngine;

/// <summary>
/// Script to be attached to the escape zones (triggers).
/// When the player enters the zone, it triggers the end panel.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EvacuationZone : MonoBehaviour
{
    [Header("End Panel Reference")]
    [Tooltip("Assign the EndPanel object that has the EndPanelController script.")]
    public EndPanelController endPanelController;

    private void OnTriggerEnter(Collider other)
    {
        // Verify if the collider belongs to the Player
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[Evacuation] Player entered {gameObject.name}. Triggering EndPanel.");

            if (endPanelController != null)
            {
                endPanelController.Show(true);
            }
            else
            {
                // Fallback: try to find the controller in the scene specifically if not assigned
                endPanelController = Object.FindAnyObjectByType<EndPanelController>(FindObjectsInactive.Include);
                
                if (endPanelController != null)
                {
                    Debug.Log($"[Evacuation] Found EndPanelController on '{endPanelController.gameObject.name}'");
                    endPanelController.Show(true);
                }
                else
                {
                    Debug.LogError("[Evacuation] EndPanelController not found. Please assign it in the inspector.");
                }
            }
        }
    }

    private void Awake()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[Evacuation] Collider on {gameObject.name} was not a trigger. Fixing automatically.");
            col.isTrigger = true;
        }

        if (endPanelController == null)
        {
            endPanelController = Object.FindAnyObjectByType<EndPanelController>(FindObjectsInactive.Include);
            if (endPanelController != null)
                Debug.Log($"[Evacuation] Resolved EndPanelController to '{endPanelController.gameObject.name}'");
        }
    }
}
