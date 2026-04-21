using UnityEngine;

public class Bot : MonoBehaviour
{
    [SerializeField] private float turnSpeed = 360f;
    [SerializeField] private float destinationReachedDistance = 0.25f;
    [SerializeField] private Vector2 moveDurationRange = new Vector2(0.8f, 2.5f);
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(0.3f, 1.4f);
    [SerializeField] private float throttleResponsiveness = 4f;
    [SerializeField] private Vector2 throttleTargetRange = new Vector2(0.55f, 1f);
    [SerializeField] private float hitReactForce = 4f;

    [Header("Wander Spread")]
    [SerializeField] private float minTravelDistance = 5f;
    [SerializeField] private int destinationCandidateCount = 6;

    [Header("Collision Reaction")]
    [SerializeField] private float collisionRedirectDistance = 3f;
    [SerializeField] private Vector2 collisionPauseRange = new Vector2(0.15f, 0.45f);
    [SerializeField] private float collisionTurnJitterDegrees = 45f;
    [SerializeField] private float collisionReactionCooldown = 0.25f;
    [SerializeField] private float wallHitDetectDistance = 0.02f;

    private float moveSpeed;
    private Vector3 destination;
    private float lockedY;

    private bool isMoving;
    private float stateTimer;
    private float currentThrottle;
    private float targetThrottle;

    // Hit reaction
    private bool isHitReacting;
    private float hitReactTimer;
    private Vector3 hitReactVelocity;

    // Collision reaction cooldown so bumps don't spam
    private float collisionCooldownTimer;

    private Animator animator;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    public void Initialize(float moveSpeed)
    {
        this.moveSpeed = moveSpeed;
        lockedY = transform.position.y;

        animator = GetComponentInChildren<Animator>();

        // Prevent gravity/collision forces from nudging the bot off the play plane.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezePositionY
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationZ;
        }

        // Keep the position that Control spawned us at — it handles even spatial
        // distribution via best-candidate sampling. Just clamp in case of slight drift.
        transform.position = ScreenBoundsUtility.ClampToVisibleWorld(transform.position);
        destination = GetRandomPointOnScreen(lockedY);
        FaceDestination();

        EnterMovingState();
    }

    /// <summary>
    /// Called when a player punches this bot. The bot flinches away.
    /// </summary>
    public void ReactToHit(Vector3 attackerPosition)
    {
        Vector3 awayDir = (transform.position - attackerPosition).normalized;
        hitReactVelocity = awayDir * hitReactForce;
        isHitReacting = true;
        hitReactTimer = 0.4f;
        if (animator != null) animator.SetTrigger(HitHash);
    }

    private void Update()
    {
        // Hit reaction: slide away briefly
        if (isHitReacting)
        {
            hitReactTimer -= Time.deltaTime;
            hitReactVelocity = Vector3.Lerp(hitReactVelocity, Vector3.zero, 5f * Time.deltaTime);
            Vector3 reactPos = transform.position + hitReactVelocity * Time.deltaTime;
            reactPos.y = lockedY;
            transform.position = ScreenBoundsUtility.ClampToVisibleWorld(reactPos);
            if (hitReactTimer <= 0f)
                isHitReacting = false;
            if (animator != null) animator.SetBool(IsMovingHash, false);
            return;
        }

        if (collisionCooldownTimer > 0f)
            collisionCooldownTimer -= Time.deltaTime;

        Vector3 currentPosition = transform.position;
        Vector3 flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);
        Vector3 flatDestination = new Vector3(destination.x, 0f, destination.z);
        float distanceToDestination = Vector3.Distance(flatCurrent, flatDestination);

        if (distanceToDestination <= destinationReachedDistance)
        {
            destination = GetRandomPointOnScreen(currentPosition.y);
            flatDestination = new Vector3(destination.x, 0f, destination.z);
        }

        // Joystick-like state machine: alternate moving/pausing in random intervals.
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            if (isMoving)
            {
                EnterPausedState();
            }
            else
            {
                EnterMovingState();
            }
        }

        // Smoothly ramp throttle toward target so motion eases in/out like a joystick push.
        currentThrottle = Mathf.MoveTowards(
            currentThrottle,
            targetThrottle,
            throttleResponsiveness * Time.deltaTime);

        Vector3 moveDirection = (flatDestination - flatCurrent).normalized;
        float effectiveSpeed = moveSpeed * currentThrottle;
        Vector3 nextPosition = Vector3.MoveTowards(currentPosition, destination, effectiveSpeed * Time.deltaTime);
        nextPosition.y = lockedY;
        Vector3 clamped = ScreenBoundsUtility.ClampToVisibleWorld(nextPosition);
        transform.position = clamped;

        // If the clamp pushed us inward while we were actively trying to move outward,
        // treat it as a wall bump and redirect — like a human realizing they walked into an edge.
        bool pushingIntoWall = isMoving
            && currentThrottle > 0.1f
            && (clamped - nextPosition).sqrMagnitude > wallHitDetectDistance * wallHitDetectDistance;
        if (pushingIntoWall)
        {
            Vector3 inwardHint = clamped - nextPosition; // points away from the wall
            RedirectAwayFrom(clamped - inwardHint);
        }

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (animator != null)
            animator.SetBool(IsMovingHash, isMoving && currentThrottle > 0.05f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        RedirectAwayFrom(collision.transform.position);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collisionCooldownTimer <= 0f)
            RedirectAwayFrom(collision.transform.position);
    }

    /// <summary>
    /// Pick a new destination roughly away from the given obstacle position,
    /// with a small random turn angle and a brief startled pause.
    /// </summary>
    private void RedirectAwayFrom(Vector3 obstaclePosition)
    {
        if (collisionCooldownTimer > 0f) return;
        if (isHitReacting) return;

        collisionCooldownTimer = collisionReactionCooldown;

        Vector3 awayDir = transform.position - obstaclePosition;
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.0001f)
            awayDir = -transform.forward;
        awayDir.Normalize();

        float jitter = Random.Range(-collisionTurnJitterDegrees, collisionTurnJitterDegrees);
        awayDir = Quaternion.AngleAxis(jitter, Vector3.up) * awayDir;

        Vector3 candidate = transform.position + awayDir * collisionRedirectDistance;
        candidate.y = destination.y;
        destination = ScreenBoundsUtility.ClampToVisibleWorld(candidate);

        // Brief startled pause before committing to the new direction.
        isMoving = false;
        stateTimer = Random.Range(collisionPauseRange.x, collisionPauseRange.y);
        targetThrottle = 0f;
    }

    private void EnterMovingState()
    {
        isMoving = true;
        stateTimer = Random.Range(moveDurationRange.x, moveDurationRange.y);
        targetThrottle = Random.Range(throttleTargetRange.x, throttleTargetRange.y);
    }

    private void EnterPausedState()
    {
        isMoving = false;
        stateTimer = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
        targetThrottle = 0f;
    }

    /// <summary>
    /// Pick a random destination, sampling several candidates and preferring
    /// ones that are far from the current position. Prevents short in-place
    /// wandering that causes bots to clump near wherever they started.
    /// </summary>
    private Vector3 GetRandomPointOnScreen(float worldY)
    {
        Vector3 origin = transform.position;
        float minDistSq = minTravelDistance * minTravelDistance;

        Vector3 best = ScreenBoundsUtility.GetRandomPointInsideVisibleWorld(worldY);
        float bestDistSq = (best - origin).sqrMagnitude;

        for (int i = 1; i < destinationCandidateCount; i++)
        {
            if (bestDistSq >= minDistSq) break;
            Vector3 candidate = ScreenBoundsUtility.GetRandomPointInsideVisibleWorld(worldY);
            float distSq = (candidate - origin).sqrMagnitude;
            if (distSq > bestDistSq)
            {
                best = candidate;
                bestDistSq = distSq;
            }
        }

        return best;
    }

    private void FaceDestination()
    {
        Vector3 direction = destination - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}