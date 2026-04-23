using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CarryableBox : MonoBehaviour, IInteractable
{
    [Header("Carry Placement")]
    [SerializeField] private float holdDistance = 0.5f;
    [SerializeField] private float holdVerticalOffset = -0.43f;
    [SerializeField] private float releaseDistance = 1.35f;

    [Header("Throw Charge")]
    [SerializeField] private float tapThreshold = 0.15f;
    [SerializeField] private float maxChargeSeconds = 1f;
    [SerializeField] private float maxThrowImpulse = 10.5f;
    [SerializeField] private float upwardThrowBias = 0.15f;

    [Header("Physics")]
    [SerializeField] private float rigidbodyMass = 7f;
    [SerializeField] private float rigidbodyDrag = 0.2f;
    [SerializeField] private float rigidbodyAngularDrag = 0.45f;
    [SerializeField] private float collisionRestoreDelay = 0.2f;

    private Rigidbody rb;
    private Transform holdReference;
    private Transform holderRoot;
    private Collider[] ownColliders;
    private Coroutine restoreCollisionRoutine;

    public bool IsHeld => holdReference != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = rigidbodyMass;
        rb.linearDamping = rigidbodyDrag;
        rb.angularDamping = rigidbodyAngularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        EnsureDynamicColliderCompatibility();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void OnEnable()
    {
        holdReference = null;
        holderRoot = null;
        restoreCollisionRoutine = null;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (!IsHeld || holdReference == null)
        {
            return;
        }

        Vector3 targetPosition = holdReference.position
            + (holdReference.forward * holdDistance)
            + (holdReference.up * holdVerticalOffset);
        transform.position = targetPosition;
        transform.rotation = holdReference.rotation;
    }

    public bool TryPickup(Transform referenceTransform, Transform holderTransform)
    {
        if (IsHeld || referenceTransform == null)
        {
            return false;
        }

        holdReference = referenceTransform;
        holderRoot = holderTransform;

        if (restoreCollisionRoutine != null)
        {
            StopCoroutine(restoreCollisionRoutine);
            restoreCollisionRoutine = null;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        IgnorePlayerCollisions(holderRoot, ignore: true);
        return true;
    }

    public bool Release(float heldSeconds, Vector3 forwardDirection)
    {
        if (!IsHeld)
        {
            return false;
        }

        Vector3 releaseForward = forwardDirection.sqrMagnitude > 0.0001f
            ? forwardDirection.normalized
            : transform.forward;

        float safeReleaseDistance = ComputeSafeReleaseDistance(releaseForward);
        transform.position = holdReference.position
            + (releaseForward * safeReleaseDistance)
            + (Vector3.up * 0.05f);

        holdReference = null;
        rb.isKinematic = false;
        rb.useGravity = true;

        float impulse = CalculateImpulse(heldSeconds);
        if (impulse > 0f)
        {
            Vector3 throwDirection = (releaseForward + (Vector3.up * upwardThrowBias)).normalized;
            rb.AddForce(throwDirection * impulse, ForceMode.Impulse);
        }

        if (holderRoot != null)
        {
            if (restoreCollisionRoutine != null)
            {
                StopCoroutine(restoreCollisionRoutine);
            }
            restoreCollisionRoutine = StartCoroutine(RestorePlayerCollisionsAfterDelay(holderRoot, collisionRestoreDelay));
            holderRoot = null;
        }

        return true;
    }

    public void Interact()
    {
        if (IsHeld)
        {
            return;
        }

        Camera cam = Camera.main;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Transform holder = player != null ? player.transform : null;
        if (cam != null && holder != null)
        {
            TryPickup(cam.transform, holder);
        }
    }

    public string GetInteractText()
    {
        return IsHeld
            ? "Release Box [E]"
            : "Pick Up Box [E]";
    }

    private float CalculateImpulse(float heldSeconds)
    {
        if (heldSeconds <= tapThreshold)
        {
            return 0f;
        }

        float effectiveChargeWindow = Mathf.Max(0.01f, maxChargeSeconds - tapThreshold);
        float normalized = Mathf.Clamp01((heldSeconds - tapThreshold) / effectiveChargeWindow);
        return normalized * maxThrowImpulse;
    }

    private void IgnorePlayerCollisions(Transform playerTransform, bool ignore)
    {
        if (playerTransform == null || ownColliders == null)
        {
            return;
        }

        Collider[] playerColliders = playerTransform.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider own = ownColliders[i];
            if (own == null)
            {
                continue;
            }

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(own, playerCollider, ignore);
            }
        }
    }

    private IEnumerator RestorePlayerCollisionsAfterDelay(Transform playerTransform, float delay)
    {
        yield return new WaitForSeconds(delay);
        IgnorePlayerCollisions(playerTransform, ignore: false);
        restoreCollisionRoutine = null;
    }

    private void EnsureDynamicColliderCompatibility()
    {
        MeshCollider[] meshColliders = GetComponentsInChildren<MeshCollider>(true);
        bool hasMeshCollider = false;
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider mesh = meshColliders[i];
            if (mesh == null)
            {
                continue;
            }

            mesh.convex = true;
            mesh.enabled = true;
            hasMeshCollider = true;
        }

        if (hasMeshCollider)
        {
            BoxCollider rootBoxCollider = GetComponent<BoxCollider>();
            if (rootBoxCollider != null)
            {
                rootBoxCollider.enabled = false;
            }
            return;
        }

        BoxCollider fallback = GetComponent<BoxCollider>();
        if (fallback == null)
        {
            fallback = gameObject.AddComponent<BoxCollider>();
        }

        fallback.center = Vector3.zero;
        fallback.size = Vector3.one;
        fallback.enabled = true;
    }

    private float ComputeSafeReleaseDistance(Vector3 releaseForward)
    {
        float desiredDistance = Mathf.Max(0.1f, releaseDistance);
        if (ownColliders == null || ownColliders.Length == 0 || holdReference == null)
        {
            return desiredDistance;
        }

        Bounds combined = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider c = ownColliders[i];
            if (c == null || !c.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combined = c.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(c.bounds);
            }
        }

        if (!hasBounds)
        {
            return desiredDistance;
        }

        Vector3 castHalfExtents = combined.extents * 0.9f;
        castHalfExtents.x = Mathf.Max(0.03f, castHalfExtents.x);
        castHalfExtents.y = Mathf.Max(0.03f, castHalfExtents.y);
        castHalfExtents.z = Mathf.Max(0.03f, castHalfExtents.z);

        RaycastHit[] hits = Physics.BoxCastAll(
            holdReference.position,
            castHalfExtents,
            releaseForward,
            transform.rotation,
            desiredDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = desiredDistance;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (holderRoot != null && hitTransform.IsChildOf(holderRoot))
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
            }
        }

        return Mathf.Clamp(nearestDistance - 0.05f, 0.1f, desiredDistance);
    }
}
