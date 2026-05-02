using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public enum ControlScheme
    {
        WASD,
        ArrowKeys,
        Phone,   // input from PhoneInputManager (WebSocket joystick)
        ESP32    // input from PhoneInputManager (fed by SerialController)
    }

    [Header("Player Identity")]
    [SerializeField] private int playerIndex; // 0 or 1

    [Header("Player Movement")]
    [SerializeField] private ControlScheme controlScheme = ControlScheme.WASD;
    [SerializeField] private float moveSpeed = 4f;

    [Header("Combat")]
    [SerializeField] private float punchRange = 1.5f;
    [SerializeField] private float punchCooldown = 3f;
    [SerializeField] private float stunDuration = 1.5f;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float punchWindup = 0.08f;
    [SerializeField] private float punchStrike = 0.1f;
    [SerializeField] private float punchRecovery = 0.14f;
    [SerializeField] private float punchLunge = 0.5f;

    // State
    private bool isStunned;
    private float stunTimer;
    private float punchTimer;
    private bool isPunching;
    private int orangeCount;
    private Vector3 knockbackVelocity;

    // Locked while playing the "Steal" reaction after losing an orange to a punch.
    private bool isStolenReacting;
    private float stolenReactionTimer;

    // Cached body for punch animation
    private Transform bodyTransform;
    private Vector3 bodyBaseScale;
    private Vector3 bodyBasePosition;

    private Animator animator;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    // Lazily-loaded shared punch SFX so runtime-added Players (e.g. the
    // InstructionScene demo) get sound without inspector setup.
    private static AudioClip hitSfxClip;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 1f;
    private AudioSource sfxSource;

    // Keyboard punch keys
    private KeyCode punchKey;
    private bool prevPunchPressed;

    public int PlayerIndex => playerIndex;
    public int OrangeCount => orangeCount;
    public bool HasOrange => orangeCount > 0;
    public bool IsStunned => isStunned;
    // The position this Player wanted at the end of Update. The
    // InstructionScene's character.prefab has an Animator on the root that
    // would otherwise revert our writes every frame, so the scene manager
    // re-applies this in LateUpdate.
    public Vector3 IntendedPosition { get; private set; }

    public void Initialize(int index, ControlScheme newControlScheme, float newMoveSpeed)
    {
        playerIndex = index;
        controlScheme = newControlScheme;
        moveSpeed = newMoveSpeed;
        punchKey = controlScheme == ControlScheme.WASD ? KeyCode.R : KeyCode.Slash;
        IntendedPosition = transform.position;
    }

    private void Start()
    {
        // punchKey is only used for keyboard schemes; Phone/ESP32 use ConsumeAction()
        punchKey = controlScheme == ControlScheme.WASD ? KeyCode.R : KeyCode.Slash;

        bodyTransform = transform.Find("Cube");
        if (bodyTransform != null)
        {
            bodyBaseScale = bodyTransform.localScale;
            bodyBasePosition = bodyTransform.localPosition;
        }

        animator = GetComponentInChildren<Animator>();
        IntendedPosition = transform.position;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        // Locked out while the Steal reaction plays — no movement, no punching.
        if (isStolenReacting)
        {
            stolenReactionTimer -= Time.deltaTime;
            if (stolenReactionTimer <= 0f)
            {
                isStolenReacting = false;
                if (animator != null) animator.Play("Idle", 0, 0f);
            }
            if (animator != null) animator.SetBool(IsMovingHash, false);
            return;
        }

        // Handle stun
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 5f * Time.deltaTime);
            Vector3 knockDelta = knockbackVelocity * Time.deltaTime;
            transform.position = MoveWithCollision(transform.position, new Vector3(knockDelta.x, 0f, knockDelta.z));
            IntendedPosition = transform.position;

            if (stunTimer <= 0f)
            {
                isStunned = false;
                knockbackVelocity = Vector3.zero;
            }
            if (animator != null) animator.SetBool(IsMovingHash, false);
            return;
        }

        // Movement
        Vector2 input = ReadMovementInput();
        Vector3 move = new Vector3(input.x, 0f, input.y) * (moveSpeed * Time.deltaTime);
        transform.position = MoveWithCollision(transform.position, move);
        IntendedPosition = transform.position;

        if (animator != null) animator.SetBool(IsMovingHash, input.sqrMagnitude > 0.01f);

        // Face movement direction
        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 faceDir = new Vector3(input.x, 0f, input.y);
            transform.rotation = Quaternion.LookRotation(faceDir, Vector3.up);
        }

        // Punch cooldown
        if (punchTimer > 0f)
            punchTimer -= Time.deltaTime;

        // Action button = punch (orange pickup is automatic on contact)
        bool actionPressed = ReadActionInput();
        if (actionPressed && punchTimer <= 0f)
        {
            Punch();
        }
    }

    private void GrabOrange(Orange orange)
    {
        SetOrangeCount(orangeCount + 1);
        orange.PickUp(transform);
    }

    public void DropOrange()
    {
        if (orangeCount <= 0) return;
        Orange orange = GetComponentInChildren<Orange>();
        if (orange != null)
            orange.Drop();
        SetOrangeCount(orangeCount - 1);
    }

    private void SetOrangeCount(int newCount)
    {
        if (newCount < 0) newCount = 0;
        if (newCount == orangeCount) return;
        orangeCount = newCount;
        BroadcastOrangeCount();
    }

    private void BroadcastOrangeCount()
    {
        // playerIndex is 0-based in Unity but 1-based on the worker / phones.
        int wirePlayerId = playerIndex + 1;
        string json = "{\"type\":\"orange_count\",\"playerId\":" + wirePlayerId
                    + ",\"count\":" + orangeCount + "}";
        if (WebSocketClient.Instance != null)
            WebSocketClient.Instance.Send(json);
    }

    /// <summary>
    /// Detaches one orange from this player without returning it to the scene.
    /// Used when another player steals via a punch. Plays the Steal reaction
    /// on the victim and locks them out of moving/punching for the clip.
    /// </summary>
    private Orange TakeOrangeAway()
    {
        if (orangeCount <= 0) return null;
        Orange orange = GetComponentInChildren<Orange>();
        if (orange == null) return null;
        SetOrangeCount(orangeCount - 1);
        PlayStolenReaction();
        return orange;
    }

    public void PlayStolenReaction()
    {
        isStolenReacting = true;
        stolenReactionTimer = 0.6f; // safe default; overridden once the clip starts
        if (animator != null)
        {
            animator.Play("Steal", 0, 0f);
            StartCoroutine(SyncStolenLockToClip());
        }
    }

    private IEnumerator SyncStolenLockToClip()
    {
        yield return null; // let the Animator switch into the Steal state
        if (animator != null && isStolenReacting)
        {
            float length = animator.GetCurrentAnimatorStateInfo(0).length;
            if (length > 0f) stolenReactionTimer = length;
        }
    }

    private void Punch()
    {
        punchTimer = punchCooldown;
        if (animator != null) animator.SetTrigger(HitHash);
        PlayHitSfx();
        if (!isPunching)
            StartCoroutine(PunchRoutine());
    }

    private void PlayHitSfx()
    {
        if (hitSfxClip == null)
            hitSfxClip = Resources.Load<AudioClip>("SFX/hit");
        if (hitSfxClip == null) return;
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f; // 2D — always full volume regardless of listener position
        }
        sfxSource.PlayOneShot(hitSfxClip, hitSfxVolume);
    }

    private IEnumerator PunchRoutine()
    {
        isPunching = true;
        bool hitResolved = false;

        Vector3 windupScale = new Vector3(bodyBaseScale.x * 0.85f, bodyBaseScale.y * 0.85f, bodyBaseScale.z * 0.7f);
        Vector3 strikeScale = new Vector3(bodyBaseScale.x * 0.9f, bodyBaseScale.y * 0.9f, bodyBaseScale.z * 1.6f);
        Vector3 windupPos = bodyBasePosition + Vector3.back * 0.15f;
        Vector3 strikePos = bodyBasePosition + Vector3.forward * punchLunge;

        // Wind-up: pull back
        for (float t = 0f; t < punchWindup; t += Time.deltaTime)
        {
            float k = t / punchWindup;
            ApplyBody(Vector3.Lerp(bodyBaseScale, windupScale, k), Vector3.Lerp(bodyBasePosition, windupPos, k));
            yield return null;
        }

        // Strike: lunge forward, resolve hit mid-strike
        for (float t = 0f; t < punchStrike; t += Time.deltaTime)
        {
            float k = t / punchStrike;
            ApplyBody(Vector3.Lerp(windupScale, strikeScale, k), Vector3.Lerp(windupPos, strikePos, k));
            if (!hitResolved && k >= 0.4f)
            {
                hitResolved = true;
                ResolvePunchHit();
            }
            yield return null;
        }

        if (!hitResolved)
            ResolvePunchHit();

        // Recovery: return to rest
        for (float t = 0f; t < punchRecovery; t += Time.deltaTime)
        {
            float k = t / punchRecovery;
            ApplyBody(Vector3.Lerp(strikeScale, bodyBaseScale, k), Vector3.Lerp(strikePos, bodyBasePosition, k));
            yield return null;
        }

        ApplyBody(bodyBaseScale, bodyBasePosition);
        isPunching = false;
    }

    private void ApplyBody(Vector3 scale, Vector3 pos)
    {
        if (bodyTransform == null) return;
        bodyTransform.localScale = scale;
        bodyTransform.localPosition = pos;
    }

    private void ResolvePunchHit()
    {
        // Only one player can be stunned per exchange: if our own punch is
        // already mid-strike when the opponent's hit lands on us, ours fizzles.
        if (isStolenReacting || isStunned) return;

        Vector3 center = transform.position + transform.forward * punchRange;
        Collider[] hits = Physics.OverlapSphere(center, punchRange * 0.8f);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            Player otherPlayer = hit.GetComponentInParent<Player>();
            if (otherPlayer != null && otherPlayer != this)
            {
                if (otherPlayer.HasOrange)
                {
                    // TakeOrangeAway already triggers the Steal reaction.
                    Orange stolen = otherPlayer.TakeOrangeAway();
                    if (stolen != null)
                    {
                        SetOrangeCount(orangeCount + 1);
                        stolen.PickUp(transform);
                    }
                }
                else
                {
                    // Nothing to steal, but still lock the victim into the reaction.
                    otherPlayer.PlayStolenReaction();
                }
                return;
            }

            Bot bot = hit.GetComponentInParent<Bot>();
            if (bot != null)
            {
                if (!bot.IsDead) bot.Die(transform.position);
                return;
            }

            Orange ground = hit.GetComponent<Orange>();
            if (ground != null && !ground.IsHeld)
            {
                GrabOrange(ground);
                return;
            }
        }
    }

    // Slide-along-wall movement: tries full delta, then X-only, then Z-only.
    private Vector3 MoveWithCollision(Vector3 current, Vector3 delta)
    {
        // Walls in the instruction scenes are rotated on Y, so the axis-aligned
        // slide tests below can leave the player slightly inside one. Without
        // this push-out, every slide candidate stays overlapping and the
        // player gets permanently stuck against the wall.
        Vector3 pushOut = ComputeWallPushOut(current);
        if (pushOut.sqrMagnitude > 0f)
            current = ScreenBoundsUtility.ClampToVisibleWorld(current + pushOut);

        Vector3 full = ScreenBoundsUtility.ClampToVisibleWorld(current + delta);
        if (CanOccupy(full)) return full;

        Vector3 xOnly = ScreenBoundsUtility.ClampToVisibleWorld(current + new Vector3(delta.x, 0f, 0f));
        if (CanOccupy(xOnly)) return xOnly;

        Vector3 zOnly = ScreenBoundsUtility.ClampToVisibleWorld(current + new Vector3(0f, 0f, delta.z));
        if (CanOccupy(zOnly)) return zOnly;

        return current;
    }

    // Computes a world-space offset that pushes `pos` out of every wall whose
    // OBB overlaps the probe. Uses the wall's world axes and the probe's
    // projected radius along each, so it works for rotated walls where the
    // probe center sits outside but the probe corners poke in.
    private Vector3 ComputeWallPushOut(Vector3 pos)
    {
        Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.45f);
        Collider[] hits = Physics.OverlapBox(pos, halfExtents, transform.rotation);
        Vector3 push = Vector3.zero;
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<Player>() != null) continue;
            if (hit.GetComponentInParent<Bot>() != null) continue;

            BoxCollider box = hit as BoxCollider;
            if (box == null) continue;

            Transform wt = box.transform;
            Vector3 wallCenter = wt.TransformPoint(box.center);
            Vector3 wallRight = wt.right;
            Vector3 wallForward = wt.forward;
            Vector3 lossy = wt.lossyScale;
            float wallHalfX = box.size.x * 0.5f * Mathf.Abs(lossy.x);
            float wallHalfZ = box.size.z * 0.5f * Mathf.Abs(lossy.z);

            Vector3 pr = transform.right;
            Vector3 pu = transform.up;
            Vector3 pf = transform.forward;
            float probeRadX = halfExtents.x * Mathf.Abs(Vector3.Dot(pr, wallRight))
                            + halfExtents.y * Mathf.Abs(Vector3.Dot(pu, wallRight))
                            + halfExtents.z * Mathf.Abs(Vector3.Dot(pf, wallRight));
            float probeRadZ = halfExtents.x * Mathf.Abs(Vector3.Dot(pr, wallForward))
                            + halfExtents.y * Mathf.Abs(Vector3.Dot(pu, wallForward))
                            + halfExtents.z * Mathf.Abs(Vector3.Dot(pf, wallForward));

            Vector3 d = pos - wallCenter;
            float distX = Vector3.Dot(d, wallRight);
            float distZ = Vector3.Dot(d, wallForward);
            float penX = (wallHalfX + probeRadX) - Mathf.Abs(distX);
            float penZ = (wallHalfZ + probeRadZ) - Mathf.Abs(distZ);
            if (penX <= 0f || penZ <= 0f) continue;

            Vector3 worldPush = (penX < penZ)
                ? wallRight * ((penX + 0.01f) * Mathf.Sign(distX))
                : wallForward * ((penZ + 0.01f) * Mathf.Sign(distZ));
            worldPush.y = 0f;
            push += worldPush;
        }
        return push;
    }

    private bool CanOccupy(Vector3 pos)
    {
        Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.45f);
        Collider[] hits = Physics.OverlapBox(pos, halfExtents, transform.rotation);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            // Let players and bots overlap — only world geometry blocks.
            if (hit.GetComponentInParent<Player>() != null) continue;
            if (hit.GetComponentInParent<Bot>() != null) continue;
            return false;
        }
        return true;
    }

    public void ApplyStun(Vector3 knockback, float duration)
    {
        isStunned = true;
        stunTimer = duration;
        knockbackVelocity = knockback;
        if (animator != null) animator.SetTrigger(HitHash);
    }

    // ESP32 serial sends as "P1"/"P2" (indices 0/1). When the hybrid start scene
    // pairs a single "P1" controller with slot 1, we need to read from serial
    // index 0, not 1.
    private int EspInputIndex()
    {
        var cfg = GameConfig.Instance;
        if (cfg != null
            && playerIndex >= 0 && playerIndex < cfg.EspIndexForSlot.Length
            && cfg.EspIndexForSlot[playerIndex] >= 0)
        {
            return cfg.EspIndexForSlot[playerIndex];
        }
        return playerIndex;
    }

    private Vector2 ReadMovementInput()
    {
        switch (controlScheme)
        {
            case ControlScheme.Phone:
                if (PhoneInputManager.Instance != null)
                    return PhoneInputManager.Instance.GetMovement(playerIndex);
                return Vector2.zero;
            case ControlScheme.ESP32:
                if (PhoneInputManager.Instance != null)
                    return PhoneInputManager.Instance.GetMovement(EspInputIndex());
                return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        switch (controlScheme)
        {
            case ControlScheme.WASD:
                if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.D)) horizontal += 1f;
                if (Input.GetKey(KeyCode.S)) vertical -= 1f;
                if (Input.GetKey(KeyCode.W)) vertical += 1f;
                break;

            case ControlScheme.ArrowKeys:
                if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
                if (Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
                break;
        }

        Vector2 raw = new Vector2(horizontal, vertical);
        return raw.sqrMagnitude > 1f ? raw.normalized : raw;
    }

    private bool ReadActionInput()
    {
        switch (controlScheme)
        {
            case ControlScheme.Phone:
                if (PhoneInputManager.Instance != null)
                    return PhoneInputManager.Instance.ConsumeAction(playerIndex);
                return false;
            case ControlScheme.ESP32:
                if (PhoneInputManager.Instance != null)
                    return PhoneInputManager.Instance.ConsumeAction(EspInputIndex());
                return false;
        }

        bool pressed = Input.GetKey(punchKey);
        bool justPressed = pressed && !prevPunchPressed;
        prevPunchPressed = pressed;
        return justPressed;
    }

}
