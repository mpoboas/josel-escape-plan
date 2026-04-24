using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerFootstepAudio : MonoBehaviour
{
    [Header("Cadence")]
    [SerializeField] private float minSpeedToStep = 0.2f;
    [SerializeField] private float walkStepInterval = 0.52f;
    [SerializeField] private float sprintStepInterval = 0.36f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Rigidbody rb;
    private FirstPersonController firstPersonController;
    private float nextStepTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        firstPersonController = GetComponent<FirstPersonController>();
    }

    private void Update()
    {
        if (GameAudioManager.Instance == null || Time.timeScale <= 0f)
        {
            return;
        }

        if (!IsGrounded())
        {
            return;
        }

        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        if (horizontalSpeed < minSpeedToStep)
        {
            return;
        }

        if (Time.time < nextStepTime)
        {
            return;
        }

        float stepInterval = ShouldUseSprintCadence() ? sprintStepInterval : walkStepInterval;
        nextStepTime = Time.time + Mathf.Max(0.1f, stepInterval);
        GameAudioManager.Instance.PlayFootstepConcrete();
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.05f;
        return Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private bool ShouldUseSprintCadence()
    {
        if (firstPersonController == null)
        {
            return false;
        }

        bool sprintHeld = firstPersonController.enableSprint && Input.GetKey(firstPersonController.sprintKey);
        bool crouched = firstPersonController.IsCrouched;
        return sprintHeld && !crouched;
    }
}
