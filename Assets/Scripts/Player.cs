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
    [SerializeField] private float punchCooldown = 0.5f;
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
    private bool hasOrange;
    private Vector3 knockbackVelocity;

    // Cached body for punch animation
    private Transform bodyTransform;
    private Vector3 bodyBaseScale;
    private Vector3 bodyBasePosition;

    private Animator animator;
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int HitHash = Animator.StringToHash("Hit");

    // Keyboard punch keys
    private KeyCode punchKey;
    private bool prevPunchPressed;

    public int PlayerIndex => playerIndex;
    public bool HasOrange => hasOrange;
    public bool IsStunned => isStunned;

    public void Initialize(int index, ControlScheme newControlScheme, float newMoveSpeed)
    {
        playerIndex = index;
        controlScheme = newControlScheme;
        moveSpeed = newMoveSpeed;
        punchKey = controlScheme == ControlScheme.WASD ? KeyCode.R : KeyCode.Slash;
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
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        // Handle stun
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 5f * Time.deltaTime);
            Vector3 knockDelta = knockbackVelocity * Time.deltaTime;
            transform.position = MoveWithCollision(transform.position, new Vector3(knockDelta.x, 0f, knockDelta.z));

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
        hasOrange = true;
        orange.PickUp(transform);
        GameManager.Instance.SetCarrier(this);
    }

    public void DropOrange()
    {
        if (!hasOrange) return;
        Orange orange = GetComponentInChildren<Orange>();
        if (orange != null)
            orange.Drop();
        hasOrange = false;
        GameManager.Instance.ClearCarrier();
    }

    /// <summary>
    /// Detaches the orange from this carrier without returning it to the scene.
    /// Used when another player steals it.
    /// </summary>
    private Orange TakeOrangeAway()
    {
        if (!hasOrange) return null;
        Orange orange = GetComponentInChildren<Orange>();
        hasOrange = false;
        if (GameManager.Instance != null) GameManager.Instance.ClearCarrier();
        return orange;
    }

    private void Punch()
    {
        punchTimer = punchCooldown;
        if (!isPunching)
            StartCoroutine(PunchRoutine());
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
        Vector3 center = transform.position + transform.forward * punchRange;
        Collider[] hits = Physics.OverlapSphere(center, punchRange * 0.8f);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (hit.transform.IsChildOf(transform)) continue;

            Player otherPlayer = hit.GetComponentInParent<Player>();
            if (otherPlayer != null && otherPlayer != this)
            {
                if (hasOrange)
                {
                    // Carrier punches the other player = instant win
                    GameManager.Instance.InstantWin(this);
                    return;
                }
                if (otherPlayer.HasOrange)
                {
                    // Steal the orange directly from the victim
                    Orange stolen = otherPlayer.TakeOrangeAway();
                    if (stolen != null)
                    {
                        hasOrange = true;
                        stolen.PickUp(transform);
                        GameManager.Instance.SetCarrier(this);
                    }
                    return;
                }
                // Neither has orange — no effect for now
                return;
            }

            Bot bot = hit.GetComponentInParent<Bot>();
            if (bot != null)
            {
                Destroy(bot.gameObject);
                return;
            }

            if (!hasOrange && (GameManager.Instance == null || GameManager.Instance.Carrier == null))
            {
                Orange orange = hit.GetComponent<Orange>();
                if (orange != null && !orange.IsHeld)
                {
                    GrabOrange(orange);
                    return;
                }
            }
        }
    }

    // Slide-along-wall movement: tries full delta, then X-only, then Z-only.
    private Vector3 MoveWithCollision(Vector3 current, Vector3 delta)
    {
        Vector3 full = ScreenBoundsUtility.ClampToVisibleWorld(current + delta);
        if (CanOccupy(full)) return full;

        Vector3 xOnly = ScreenBoundsUtility.ClampToVisibleWorld(current + new Vector3(delta.x, 0f, 0f));
        if (CanOccupy(xOnly)) return xOnly;

        Vector3 zOnly = ScreenBoundsUtility.ClampToVisibleWorld(current + new Vector3(0f, 0f, delta.z));
        if (CanOccupy(zOnly)) return zOnly;

        return current;
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
