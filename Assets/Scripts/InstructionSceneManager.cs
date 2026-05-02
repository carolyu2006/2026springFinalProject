using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class InstructionSceneManager : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(2, 5)] public string text;
        public AudioClip voiceClip;
        public float holdDuration = 3f;
        public float fadeInDuration = 0.4f;
        public UnityEvent onLineStart;
    }

    [Header("References")]
    [SerializeField] private TextMeshProUGUI scriptText;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private float bgmDelay = 2f;
    [SerializeField] private bool bgmLoop = true;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.3f;

    [Header("Flow")]
    [SerializeField] private string nextSceneName = "GameScene";
    [SerializeField] private float initialDelay = 0.5f;
    [SerializeField] private float endDelay = 1f;

    [Header("Dialogue")]
    [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Intro Walk-In")]
    [SerializeField] private List<Transform> introCharacters = new List<Transform>();
    [SerializeField] private int introLineIndex = 0;
    [SerializeField] private float introWalkDuration = 2f;
    [SerializeField] private float introOffscreenOffsetX = -15f;
    [SerializeField] private float introWalkYaw = 90f;
    [SerializeField] private float introRestYaw = 0f;
    [SerializeField] private string animatorMovingBool = "IsMoving";

    [Header("Control Demo")]
    [SerializeField] private int demoLineIndex = 4;
    [SerializeField] private float demoPlayerMoveSpeed = 4f;
    [SerializeField] private float demoRetryInterval = 4f;
    [SerializeField] private float demoMoveThreshold = 0.15f;

    [Header("Skip")]
    [SerializeField] private float skipHoldDuration = 1.2f;

    [Header("Orange Prop")]
    [SerializeField] private int orangePropLineIndex = 2;
    [SerializeField] private Transform orangeProp;
    [SerializeField] private float orangeBottomY = -7.8f;
    [SerializeField] private float orangeRiseHeight = 10f;
    [SerializeField] private float orangeRiseDuration = 0.5f;
    [SerializeField] private float orangeHoldDuration = 1.5f;
    [SerializeField] private float orangeFallDuration = 0.5f;

    private Vector3[] introTargetPositions;
    private bool demoActivated;
    private float skipHoldTimer;
    private bool skipTriggered;
    private Vector3 orangePropTopPos;
    private bool orangePropPosCaptured;

    // The character.prefab Animator owns the root transform — even with
    // applyRootMotion off it stomps any position we write in Update. We keep
    // the desired transform in these arrays and re-apply it in LateUpdate
    // (which runs after the Animator) so the writes actually stick.
    private Vector3[] introDesiredPositions;
    private Quaternion[] introDesiredRotations;
    private bool introOverrideActive;

    private Transform[] demoPlayerTransforms = new Transform[2];
    private Animator[] demoPlayerAnimators = new Animator[2];
    private Player[] demoPlayers = new Player[2];
    private Vector3[] demoPlayerStartPositions = new Vector3[2];
    private Vector3[] demoPlayerLastPositions = new Vector3[2];
    // Last facing direction (in -Z visual-front convention). Persisted between
    // frames so the character keeps facing the way it last walked instead of
    // snapping to whatever Player.Update set (which uses the +Z convention).
    private Vector3[] demoPlayerFaceDirs = new Vector3[2];

    private void Awake()
    {
        if (scriptText == null)
        {
            var found = GameObject.Find("script");
            if (found != null) scriptText = found.GetComponent<TextMeshProUGUI>();
        }
        if (audioSource == null)
        {
            // Pick the first AudioSource that isn't the BGM source so voice and
            // music don't share a component (and thus a volume).
            foreach (var src in GetComponents<AudioSource>())
            {
                if (src != bgmSource) { audioSource = src; break; }
            }
        }
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = voiceVolume;
        audioSource.playOnAwake = false;

        SetupIntroCharacters();

        if (orangeProp != null)
        {
            orangePropTopPos = orangeProp.position;
            orangePropPosCaptured = true;
            orangeProp.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (scriptText != null) scriptText.text = string.Empty;
        StartBgm();
        StartCoroutine(PlaySequence());
    }

    private void StartBgm()
    {
        if (bgmClip == null) return;
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
        }
        bgmSource.clip = bgmClip;
        bgmSource.loop = bgmLoop;
        bgmSource.volume = bgmVolume;
        if (bgmDelay > 0f)
            bgmSource.PlayDelayed(bgmDelay);
        else
            bgmSource.Play();
    }

    private void Update()
    {
        if (skipTriggered) return;

        bool held = Input.GetKey(KeyCode.R)
                 || Input.GetKey(KeyCode.Slash)
                 || Input.GetKey(KeyCode.Joystick1Button0)
                 || Input.GetKey(KeyCode.Joystick2Button0);

        skipHoldTimer = held ? skipHoldTimer + Time.deltaTime : 0f;

        if (skipHoldTimer >= skipHoldDuration)
        {
            skipTriggered = true;
            StopAllCoroutines();
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void LateUpdate()
    {
        // Re-apply intro/demo character positions after the Animator has run.
        // The character.prefab Animator manages root transform and reverts any
        // writes from Update; LateUpdate executes after the animation pass so
        // these writes actually persist into the next frame.
        if (introOverrideActive && introDesiredPositions != null)
        {
            for (int i = 0; i < introCharacters.Count; i++)
            {
                var c = introCharacters[i];
                if (c == null) continue;
                c.position = introDesiredPositions[i];
                c.rotation = introDesiredRotations[i];
            }
        }

        if (!demoActivated) return;

        // Override Player's rotation based on actual movement direction.
        // The character model's visual front is at local -Z, so transform.forward
        // must point *opposite* to the movement direction for the face to lead.
        for (int i = 0; i < demoPlayerTransforms.Length; i++)
        {
            var t = demoPlayerTransforms[i];
            if (t == null) continue;

            // Re-apply Player.Update's intended position (the Animator on the
            // character.prefab root would otherwise reset it every frame).
            var p = demoPlayers[i];
            if (p != null) t.position = p.IntendedPosition;

            Vector3 delta = t.position - demoPlayerLastPositions[i];
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.00005f)
            {
                demoPlayerFaceDirs[i] = -delta.normalized;
            }
            // Apply the cached facing every frame so the wrong rotation written
            // by Player.Update (which assumes +Z visual front) doesn't flicker
            // through when movement pauses.
            if (demoPlayerFaceDirs[i].sqrMagnitude > 0.0001f)
            {
                t.rotation = Quaternion.LookRotation(demoPlayerFaceDirs[i], Vector3.up);
            }
            demoPlayerLastPositions[i] = t.position;
        }
    }

    // Walks up to the topmost transform so we move the whole prefab even if the
    // Inspector reference happens to be a child of the prefab root.
    private static Transform ResolveRoot(Transform t)
    {
        while (t != null && t.parent != null) t = t.parent;
        return t;
    }

    private void SetupIntroCharacters()
    {
        if (introCharacters == null || introCharacters.Count == 0) return;

        introTargetPositions = new Vector3[introCharacters.Count];
        introDesiredPositions = new Vector3[introCharacters.Count];
        introDesiredRotations = new Quaternion[introCharacters.Count];
        var walkRotation = Quaternion.Euler(0f, introWalkYaw, 0f);

        for (int i = 0; i < introCharacters.Count; i++)
        {
            var c = introCharacters[i];
            if (c == null) continue;
            var root = ResolveRoot(c);
            introCharacters[i] = root;
            introTargetPositions[i] = root.position;

            // Park them off-screen immediately so they don't appear at their
            // target positions during initialDelay before WalkIn starts.
            Vector3 offscreen = root.position;
            offscreen.x += introOffscreenOffsetX;
            root.position = offscreen;
            root.rotation = walkRotation;
            introDesiredPositions[i] = offscreen;
            introDesiredRotations[i] = walkRotation;
        }
        introOverrideActive = true;
    }

    private IEnumerator PlaySequence()
    {
        yield return new WaitForSeconds(initialDelay);

        int promptLineIndex = demoLineIndex + 1;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (i == promptLineIndex && HasAnyDemoPlayerActed())
                continue;

            line.onLineStart?.Invoke();

            if (i == introLineIndex)
                StartCoroutine(WalkInIntroCharacters());

            if (i == demoLineIndex)
                ActivateControlDemo();

            if (i == orangePropLineIndex)
                StartCoroutine(AnimateOrangeProp());

            if (line.voiceClip != null && audioSource != null)
                audioSource.PlayOneShot(line.voiceClip);

            yield return StartCoroutine(ShowLine(line));
            yield return new WaitForSeconds(line.holdDuration);

            if (i == promptLineIndex)
                yield return StartCoroutine(WaitForDemoAction(line));
        }

        yield return new WaitForSeconds(endDelay);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator AnimateOrangeProp()
    {
        if (orangeProp == null || !orangePropPosCaptured) yield break;

        Vector3 bottomPos = new Vector3(orangePropTopPos.x, orangeBottomY, orangePropTopPos.z);
        Vector3 topPos = new Vector3(orangePropTopPos.x, orangeBottomY + orangeRiseHeight, orangePropTopPos.z);

        orangeProp.position = bottomPos;
        orangeProp.gameObject.SetActive(true);

        float t = 0f;
        while (t < orangeRiseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / orangeRiseDuration);
            k = 1f - (1f - k) * (1f - k);
            orangeProp.position = Vector3.Lerp(bottomPos, topPos, k);
            yield return null;
        }
        orangeProp.position = topPos;

        yield return new WaitForSeconds(orangeHoldDuration);

        t = 0f;
        while (t < orangeFallDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / orangeFallDuration);
            k = k * k;
            orangeProp.position = Vector3.Lerp(topPos, bottomPos, k);
            yield return null;
        }
        orangeProp.position = bottomPos;
        orangeProp.gameObject.SetActive(false);
    }

    private IEnumerator WalkInIntroCharacters()
    {
        if (introCharacters == null || introCharacters.Count == 0 || introTargetPositions == null)
            yield break;

        var walkRotation = Quaternion.Euler(0f, introWalkYaw, 0f);
        var startPositions = new Vector3[introCharacters.Count];
        var animators = new Animator[introCharacters.Count];

        for (int i = 0; i < introCharacters.Count; i++)
        {
            var c = introCharacters[i];
            if (c == null) continue;

            Vector3 offscreen = introTargetPositions[i];
            offscreen.x += introOffscreenOffsetX;
            startPositions[i] = offscreen;
            introDesiredPositions[i] = offscreen;
            introDesiredRotations[i] = walkRotation;

            animators[i] = c.GetComponentInChildren<Animator>();
            if (animators[i] != null)
            {
                animators[i].applyRootMotion = false;
                animators[i].SetBool(animatorMovingBool, true);
            }
        }
        introOverrideActive = true;

        yield return null;

        float t = 0f;
        while (t < introWalkDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / introWalkDuration);
            for (int i = 0; i < introCharacters.Count; i++)
            {
                if (introCharacters[i] == null) continue;
                introDesiredPositions[i] = Vector3.Lerp(startPositions[i], introTargetPositions[i], k);
                introDesiredRotations[i] = walkRotation;
            }
            yield return null;
        }

        var restRotation = Quaternion.Euler(0f, introRestYaw, 0f);
        for (int i = 0; i < introCharacters.Count; i++)
        {
            if (introCharacters[i] == null) continue;
            introDesiredPositions[i] = introTargetPositions[i];
            introDesiredRotations[i] = restRotation;
            if (animators[i] != null) animators[i].SetBool(animatorMovingBool, false);
        }
    }

    private void ActivateControlDemo()
    {
        if (demoActivated) return;
        demoActivated = true;

        var candidates = new List<int>();
        for (int i = 0; i < introCharacters.Count; i++)
        {
            if (introCharacters[i] != null) candidates.Add(i);
        }
        if (candidates.Count < 2) return;

        for (int i = 0; i < 2; i++)
        {
            int swap = Random.Range(i, candidates.Count);
            (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
        }

        for (int playerIdx = 0; playerIdx < 2; playerIdx++)
        {
            var charTransform = introCharacters[candidates[playerIdx]];
            if (charTransform == null) continue;

            var player = charTransform.gameObject.AddComponent<Player>();
            var scheme = GetDemoControlScheme(playerIdx);
            player.Initialize(playerIdx, scheme, demoPlayerMoveSpeed);

            demoPlayerTransforms[playerIdx] = charTransform;
            demoPlayerAnimators[playerIdx] = charTransform.GetComponentInChildren<Animator>();
            demoPlayers[playerIdx] = player;
            demoPlayerStartPositions[playerIdx] = charTransform.position;
            demoPlayerLastPositions[playerIdx] = charTransform.position;
            demoPlayerFaceDirs[playerIdx] = charTransform.forward;
        }
    }

    private IEnumerator WaitForDemoAction(DialogueLine line)
    {
        float retryTimer = 0f;
        while (!HasAnyDemoPlayerActed())
        {
            retryTimer += Time.deltaTime;
            if (retryTimer >= demoRetryInterval)
            {
                retryTimer = 0f;
                if (line.voiceClip != null && audioSource != null)
                    audioSource.PlayOneShot(line.voiceClip);
            }
            yield return null;
        }
    }

    private bool HasAnyDemoPlayerActed()
    {
        for (int i = 0; i < demoPlayerTransforms.Length; i++)
        {
            var t = demoPlayerTransforms[i];
            if (t == null) continue;

            if (Vector3.Distance(t.position, demoPlayerStartPositions[i]) > demoMoveThreshold)
                return true;

            var anim = demoPlayerAnimators[i];
            if (anim != null)
            {
                var state = anim.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Hit")) return true;
            }
        }
        return false;
    }

    // Pick whichever control scheme this slot will use in the real game so the
    // tutorial demo actually responds to the phone joystick / punch button (or
    // the ESP32 controller). Falls back to keyboard when no GameConfig is set,
    // e.g. when entering this scene directly from the editor.
    private Player.ControlScheme GetDemoControlScheme(int playerIndex)
    {
        var cfg = GameConfig.Instance;
        if (cfg != null)
        {
            if (cfg.PlayerSchemeAssigned != null
                && playerIndex < cfg.PlayerSchemeAssigned.Length
                && cfg.PlayerSchemeAssigned[playerIndex])
            {
                return cfg.PlayerSchemes[playerIndex];
            }
            switch (cfg.Mode)
            {
                case ControlMode.Phone: return Player.ControlScheme.Phone;
                case ControlMode.ESP32: return Player.ControlScheme.ESP32;
            }
        }
        return playerIndex == 0 ? Player.ControlScheme.WASD : Player.ControlScheme.ArrowKeys;
    }

    private IEnumerator ShowLine(DialogueLine line)
    {
        if (scriptText == null)
            yield break;

        scriptText.text = line.text;

        if (line.fadeInDuration <= 0f)
        {
            SetTextAlpha(1f);
            yield break;
        }

        float t = 0f;
        while (t < line.fadeInDuration)
        {
            t += Time.deltaTime;
            SetTextAlpha(Mathf.Clamp01(t / line.fadeInDuration));
            yield return null;
        }
        SetTextAlpha(1f);
    }

    private void SetTextAlpha(float a)
    {
        var c = scriptText.color;
        c.a = a;
        scriptText.color = c;
    }
}
