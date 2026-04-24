using System.Reflection;
using UnityEngine;

public class SmokeHealthReceiver : MonoBehaviour
{
    public float health = 100f;

    [Header("Debug")]
    [SerializeField] private bool logPostureInfoOnDamage = true;
    [SerializeField] private bool immuneToSmokeWhileCrouched = true;
    
    [Header("UI Reference")]
    [SerializeField] private EndPanelController endPanelController;

    private CharacterController characterController;
    private CapsuleCollider capsuleCollider;
    private Component firstPersonController;
    private PropertyInfo isCrouchedProperty;
    private FieldInfo isCrouchedField;
    private bool gameOverLogged;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        firstPersonController = GetComponent("FirstPersonController");

        if (firstPersonController != null)
        {
            isCrouchedProperty = firstPersonController.GetType().GetProperty(
                "IsCrouched",
                BindingFlags.Instance | BindingFlags.Public
            );
            isCrouchedField = firstPersonController.GetType().GetField(
                "isCrouched",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
        }

        if (endPanelController == null)
        {
            endPanelController = Object.FindAnyObjectByType<EndPanelController>(FindObjectsInactive.Include);
            
            if (endPanelController != null)
                Debug.Log($"[SmokeHealthReceiver] Found EndPanelController on '{endPanelController.gameObject.name}'");
            else
                Debug.LogWarning("[SmokeHealthReceiver] Could not find EndPanelController in the scene!");
        }
    }

    public void TakeSmokeDamage(float damageAmount)
    {
        ApplyEnvironmentalDamage(damageAmount, ignoreWhenCrouched: true, sourceLabel: "Smoke");
        if (damageAmount > 0f)
        {
            GameAudioManager.Instance?.TryPlayCough(transform.position);
        }
    }

    public void TakeFlameDamage(float damageAmount)
    {
        ApplyEnvironmentalDamage(damageAmount, ignoreWhenCrouched: false, sourceLabel: "Flame");
        if (damageAmount > 0f)
        {
            GameAudioManager.Instance?.TryPlayFireHurt(transform.position);
        }
    }

    private void ApplyEnvironmentalDamage(
        float damageAmount,
        bool ignoreWhenCrouched,
        string sourceLabel
    )
    {
        if (damageAmount <= 0f || gameOverLogged)
        {
            return;
        }

        if (ignoreWhenCrouched && immuneToSmokeWhileCrouched && IsPlayerCrouched())
        {
            return;
        }

        health -= damageAmount;
        var stats = GameplaySessionStats.Instance;
        if (stats != null)
        {
            if (sourceLabel == "Smoke")
                stats.RegisterSmokeDamage(damageAmount);
            else if (sourceLabel == "Flame")
                stats.RegisterFireDamage(damageAmount, transform.position);
        }

        if (logPostureInfoOnDamage)
        {
            float ccHeight = characterController != null ? characterController.height : -1f;
            float capsuleHeight = capsuleCollider != null ? capsuleCollider.height : -1f;
            bool? isCrouched = TryGetCrouchedState();

            Debug.Log(
                $"[SmokeHealthReceiver] Source={sourceLabel} | Damage={damageAmount:F3} | Health={health:F2} | CharacterController.height={ccHeight:F2} | CapsuleCollider.height={capsuleHeight:F2} | IsCrouched={isCrouched?.ToString() ?? "unknown"}"
            );
        }

        if (health <= 0f)
        {
            gameOverLogged = true;
            Debug.Log("[SmokeHealthReceiver] Player Died. Preparing EndPanel (hidden) and letting DeathPanel handle visuals.");
            
            // 1. Prepare the EndPanel (calculate stats) but keep it HIDDEN for now
            if (endPanelController != null)
            {
                // false = reachedGoal (player died)
                // false = activateGameObject (stay hidden until DeathPanel transition)
                endPanelController.Show(false, false);
            }
            else
            {
                Debug.LogWarning("[SmokeHealthReceiver] EndPanelController not found to prepare stats.");
            }

            // Note: DeathPanel script handles its own activation in its Update loop 
            // by checking playerHealth.health <= 0.
        }
    }

    public bool IsPlayerCrouched()
    {
        return TryGetCrouchedState() == true;
    }

    private bool? TryGetCrouchedState()
    {
        if (firstPersonController == null)
        {
            return null;
        }

        if (isCrouchedProperty != null)
        {
            object propertyValue = isCrouchedProperty.GetValue(firstPersonController);
            if (propertyValue is bool crouchedFromProperty)
            {
                return crouchedFromProperty;
            }
        }

        if (isCrouchedField != null)
        {
            object fieldValue = isCrouchedField.GetValue(firstPersonController);
            if (fieldValue is bool crouchedFromField)
            {
                return crouchedFromField;
            }
        }

        return null;
    }
}
