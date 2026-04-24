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
        Vector3 releasePosition = holdReference.position
            + (releaseForward * safeReleaseDistance)
            + (Vector3.up * 0.05f);
        transform.position = releasePosition;

        holdReference = null;
        Physics.SyncTransforms();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();

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
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider mesh = meshColliders[i];
            if (mesh == null)
            {
                continue;
            }

            // Dynamic rigidbodies built from many mesh colliders can tunnel in player builds.
            // Keep physics stable by using one fitted BoxCollider instead.
            mesh.enabled = false;
        }

        BoxCollider fallback = GetComponent<BoxCollider>();
        if (fallback == null)
        {
            fallback = gameObject.AddComponent<BoxCollider>();
        }

        FitBoxColliderToRenderers(fallback);
        fallback.enabled = true;
    }

    private void FitBoxColliderToRenderers(BoxCollider colliderToFit)
    {
        if (colliderToFit == null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            colliderToFit.center = Vector3.zero;
            colliderToFit.size = Vector3.one;
            return;
        }

        bool hasBounds = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                localBounds = BuildLocalBoundsFromWorld(renderer.bounds);
                hasBounds = true;
            }
            else
            {
                EncapsulateWorldBoundsInLocal(ref localBounds, renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            colliderToFit.center = Vector3.zero;
            colliderToFit.size = Vector3.one;
            return;
        }

        Vector3 fittedSize = localBounds.size;
        fittedSize.x = Mathf.Max(0.1f, fittedSize.x);
        fittedSize.y = Mathf.Max(0.1f, fittedSize.y);
        fittedSize.z = Mathf.Max(0.1f, fittedSize.z);

        colliderToFit.center = localBounds.center;
        colliderToFit.size = fittedSize;
    }

    private Bounds BuildLocalBoundsFromWorld(Bounds worldBounds)
    {
        Vector3 initialPoint = transform.InverseTransformPoint(worldBounds.center);
        Bounds localBounds = new Bounds(initialPoint, Vector3.zero);
        EncapsulateWorldBoundsInLocal(ref localBounds, worldBounds);
        return localBounds;
    }

    private void EncapsulateWorldBoundsInLocal(ref Bounds localBounds, Bounds worldBounds)
    {
        Vector3 extents = worldBounds.extents;
        Vector3 center = worldBounds.center;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                    localBounds.Encapsulate(localCorner);
                }
            }
        }
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
