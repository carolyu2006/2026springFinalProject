using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndSceneManager : MonoBehaviour
{
    [Header("Scene Flow")]
    [SerializeField] private Button restartButton;
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string arduinoStartSceneName = "ArduinoStartScene";
    [SerializeField] private string phoneStartSceneName = "PhoneStartScene";
    [Tooltip("Ignore attack-button restart for this many seconds after the EndScene loads, so the press that ended the game doesn't immediately bounce back.")]
    [SerializeField] private float restartInputDelay = 1.0f;

    [Header("Text 1 (slides in, holds, slides out)")]
    [SerializeField] private RectTransform text1Chinese;
    [SerializeField] private RectTransform text1English;

    [Header("Text 2 (slides in and stays)")]
    [SerializeField] private RectTransform text2Chinese;
    [SerializeField] private RectTransform text2English;

    [Header("Winner Reveal")]
    [SerializeField] private GameObject player1Image;
    [SerializeField] private GameObject player1Winner;
    [SerializeField] private GameObject player2Image;
    [SerializeField] private GameObject player2Winner;
    [SerializeField] private RectTransform bandOrange;
    [SerializeField] private RectTransform bandGreen;
    [SerializeField] private float winBandWidth = 4000f;
    [SerializeField] private float bandExpandDuration = 0.35f;

    [Header("Player Names (shown in Phone mode)")]
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;
    [Tooltip("-2 = use GameManager.WinnerPlayerIndex, -1 = draw, 0 = player 1, 1 = player 2. For testing the EndScene standalone.")]
    [SerializeField] private int debugWinnerOverride = -2;

    [Header("Audio (optional for now)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip text1Audio;
    [SerializeField] private AudioClip text2Audio;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private float bgmDelay = 0f;
    [SerializeField] private bool bgmLoop = true;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.4f;

    [Header("Ending Sting")]
    [SerializeField] private AudioSource endingSource;
    [SerializeField] private AudioClip endingClip;
    [SerializeField] private float endingDelay = 0f;
    [SerializeField, Range(0f, 1f)] private float endingVolume = 1f;

    [Header("Timing")]
    [Tooltip("How long each text slide takes (smaller = snappier).")]
    [SerializeField] private float slideDuration = 0.25f;
    [Tooltip("How long text 1 stays on-screen before sliding out.")]
    [SerializeField] private float holdDuration = 0.15f;
    [SerializeField] private float offScreenOffset = 2000f;
    [Tooltip("How long text 2 stays before the winner is revealed (used when no text2Audio is set).")]
    [SerializeField] private float text2FallbackHold = 0.5f;

    private float _enabledTime;
    private bool _restarting;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        _enabledTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (_restarting) return;
        if (Time.unscaledTime - _enabledTime < restartInputDelay) return;
        if (AnyAttackPressed()) OnRestartClicked();
    }

    private bool AnyAttackPressed()
    {
        if (Input.GetKeyDown(KeyCode.R)) return true;
        if (Input.GetKeyDown(KeyCode.Slash)) return true;
        if (Input.GetKeyDown(KeyCode.Space)) return true;
        if (Input.GetKeyDown(KeyCode.Return)) return true;

        var pim = PhoneInputManager.Instance;
        if (pim != null)
        {
            if (pim.ConsumeAction(0)) return true;
            if (pim.ConsumeAction(1)) return true;
        }
        return false;
    }

    private void Start()
    {
        SetupPlayerNames();
        audioSource = EnsureDedicatedAudioSource(audioSource);
        StartBgm();
        StartEndingSting();
        StartCoroutine(PlaySequence());
    }

    private void StartBgm()
    {
        if (bgmClip == null) return;
        bgmSource = EnsureDedicatedAudioSource(bgmSource);
        bgmSource.clip = bgmClip;
        bgmSource.loop = bgmLoop;
        bgmSource.volume = bgmVolume;
        if (bgmDelay > 0f) bgmSource.PlayDelayed(bgmDelay);
        else bgmSource.Play();
    }

    private void StartEndingSting()
    {
        if (endingClip == null) return;
        endingSource = EnsureDedicatedAudioSource(endingSource);
        endingSource.clip = endingClip;
        endingSource.loop = false;
        endingSource.volume = endingVolume;
        if (endingDelay > 0f) endingSource.PlayDelayed(endingDelay);
        else endingSource.Play();
    }

    // Returns an AudioSource that's not already used by audioSource/bgmSource/
    // endingSource — creates a new one if needed. Keeps each clip on its own
    // component so volumes / loop flags don't collide.
    private AudioSource EnsureDedicatedAudioSource(AudioSource current)
    {
        if (current != null) return current;
        foreach (var src in GetComponents<AudioSource>())
        {
            if (src == audioSource || src == bgmSource || src == endingSource) continue;
            return src;
        }
        var added = gameObject.AddComponent<AudioSource>();
        added.playOnAwake = false;
        return added;
    }

    private void SetupPlayerNames()
    {
        var cfg = GameConfig.Instance;
        bool showNames = cfg != null && (cfg.Mode == ControlMode.Phone || cfg.Mode == ControlMode.ESP32);
        string name1 = null;
        string name2 = null;
        if (showNames)
        {
            var names = cfg.PlayerNames;
            if (names != null)
            {
                if (names.Length > 0) name1 = names[0];
                if (names.Length > 1) name2 = names[1];
            }
        }
        ApplyName(player1NameText, name1);
        ApplyName(player2NameText, name2);
    }

    private static void ApplyName(TMP_Text label, string name)
    {
        if (label == null) return;
        if (string.IsNullOrEmpty(name))
        {
            label.gameObject.SetActive(false);
        }
        else
        {
            label.text = name;
            label.gameObject.SetActive(true);
        }
    }

    private IEnumerator PlaySequence()
    {
        Vector2 t1cRest = text1Chinese.anchoredPosition;
        Vector2 t1eRest = text1English.anchoredPosition;
        Vector2 t2cRest = text2Chinese.anchoredPosition;
        Vector2 t2eRest = text2English.anchoredPosition;

        Vector2 offL = Vector2.left * offScreenOffset;
        Vector2 offR = Vector2.right * offScreenOffset;

        // Make sure the winner "winner" sprites stay hidden until announced.
        if (player1Winner != null) player1Winner.SetActive(false);
        if (player2Winner != null) player2Winner.SetActive(false);
        if (player1Image != null) player1Image.SetActive(true);
        if (player2Image != null) player2Image.SetActive(true);

        // Hide text 2 until its turn.
        text2Chinese.gameObject.SetActive(false);
        text2English.gameObject.SetActive(false);

        // Park text 1 off-screen before anything renders.
        text1Chinese.gameObject.SetActive(true);
        text1English.gameObject.SetActive(true);
        text1Chinese.anchoredPosition = t1cRest + offL;
        text1English.anchoredPosition = t1eRest + offR;

        // Text 1 slides in: chinese from left, english from right.
        yield return RunParallel(
            Slide(text1Chinese, t1cRest + offL, t1cRest, slideDuration),
            Slide(text1English, t1eRest + offR, t1eRest, slideDuration));

        PlayClip(text1Audio);
        float text1Wait = holdDuration;
        if (text1Audio != null) text1Wait = Mathf.Max(holdDuration, text1Audio.length);
        yield return new WaitForSeconds(text1Wait);

        // Text 1 exits the opposite sides.
        yield return RunParallel(
            Slide(text1Chinese, t1cRest, t1cRest + offR, slideDuration),
            Slide(text1English, t1eRest, t1eRest + offL, slideDuration));

        text1Chinese.gameObject.SetActive(false);
        text1English.gameObject.SetActive(false);

        // Text 2 slides in and stays.
        text2Chinese.gameObject.SetActive(true);
        text2English.gameObject.SetActive(true);
        text2Chinese.anchoredPosition = t2cRest + offL;
        text2English.anchoredPosition = t2eRest + offR;
        yield return RunParallel(
            Slide(text2Chinese, t2cRest + offL, t2cRest, slideDuration),
            Slide(text2English, t2eRest + offR, t2eRest, slideDuration));

        float waitForAudio = text2FallbackHold;
        if (text2Audio != null)
        {
            PlayClip(text2Audio);
            waitForAudio = text2Audio.length;
        }
        yield return new WaitForSeconds(waitForAudio);

        RevealWinner();
    }

    private void RevealWinner()
    {
        int winner = debugWinnerOverride == -2 ? GameManager.WinnerPlayerIndex : debugWinnerOverride;
        if (winner == 0)
        {
            if (player1Image != null) player1Image.SetActive(false);
            if (player1Winner != null) player1Winner.SetActive(true);
            if (bandOrange != null) StartCoroutine(ExpandWidth(bandOrange, winBandWidth, bandExpandDuration));
        }
        else if (winner == 1)
        {
            if (player2Image != null) player2Image.SetActive(false);
            if (player2Winner != null) player2Winner.SetActive(true);
            if (bandGreen != null) StartCoroutine(ExpandWidth(bandGreen, winBandWidth, bandExpandDuration));
        }
        // For a draw (winner < 0) both players keep their default image and no band expands.
    }

    private IEnumerator ExpandWidth(RectTransform rt, float targetWidth, float duration)
    {
        Vector2 size = rt.sizeDelta;
        float startWidth = size.x;
        if (duration <= 0f)
        {
            size.x = targetWidth;
            rt.sizeDelta = size;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            size.x = Mathf.Lerp(startWidth, targetWidth, eased);
            rt.sizeDelta = size;
            yield return null;
        }
        size.x = targetWidth;
        rt.sizeDelta = size;
    }

    private IEnumerator RunParallel(IEnumerator a, IEnumerator b)
    {
        Coroutine ca = StartCoroutine(a);
        Coroutine cb = StartCoroutine(b);
        yield return ca;
        yield return cb;
    }

    private IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        rt.anchoredPosition = from;
        if (duration <= 0f)
        {
            rt.anchoredPosition = to;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private void PlayClip(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    private void OnRestartClicked()
    {
        if (_restarting) return;
        _restarting = true;
        SceneManager.LoadScene(ResolveStartScene());
    }

    private string ResolveStartScene()
    {
        var cfg = GameConfig.Instance;
        if (cfg != null)
        {
            if (cfg.Mode == ControlMode.ESP32 && !string.IsNullOrEmpty(arduinoStartSceneName))
                return arduinoStartSceneName;
            if (cfg.Mode == ControlMode.Phone && !string.IsNullOrEmpty(phoneStartSceneName))
                return phoneStartSceneName;
        }
        return startSceneName;
    }
}
