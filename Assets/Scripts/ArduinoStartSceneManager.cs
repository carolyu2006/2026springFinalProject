using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// StartScene variant for ESP32 play with a keyboard fallback.
/// Any of four sources can claim a slot: R key, / key, ESP32 P1 button, ESP32 P2 button.
/// First press fills player 0, next press from a *different* source fills player 1.
/// Each slot remembers which input claimed it (via GameConfig.PlayerSchemes) so the
/// gameplay scene can drive that slot with the right controls.
/// </summary>
public class ArduinoStartSceneManager : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string nextSceneName = "InstructionScene";
    [SerializeField] private string englishNextSceneName = "EnglishInstructionScene";

    [Header("Language Selection")]
    [Tooltip("Initial language. Players can flip it before they ready up.")]
    [SerializeField] private GameLanguage initialLanguage = GameLanguage.Default;
    [Tooltip("Pressing this UI button selects Chinese.")]
    [SerializeField] private Button chineseButton;
    [Tooltip("Pressing this UI button selects English.")]
    [SerializeField] private Button englishButton;
    [Tooltip("Optional label that displays the current language.")]
    [SerializeField] private TMP_Text languageLabel;
    [Tooltip("Keyboard key that toggles language — handy if no UI buttons are wired up.")]
    [SerializeField] private KeyCode languageToggleKey = KeyCode.Tab;

    [Header("Per-Player UI — Status (the WAITING… label under the image)")]
    [SerializeField] private TMP_Text player1StatusText;
    [SerializeField] private TMP_Text player2StatusText;

    [Header("Per-Player UI — Name (optional, shows the joystick's BLE name)")]
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;

    [Header("Per-Player UI — Hint chatbox")]
    [SerializeField] private TMP_Text player1HintText;
    [SerializeField] private TMP_Text player2HintText;

    [Header("Per-Player UI — Chatbox root (hidden once that player is ready)")]
    [SerializeField] private GameObject player1Textbox;
    [SerializeField] private GameObject player2Textbox;

    [Header("Per-Player UI — Character image (swaps pending → ready)")]
    [SerializeField] private Image player1Image;
    [SerializeField] private Image player2Image;

    [Header("Character Sprites")]
    [SerializeField] private Sprite pendingSprite;
    [SerializeField] private Sprite readySprite;

    private enum JoinSource { None, KeyR, KeySlash, Esp32P1, Esp32P2 }

    const KeyCode P1_READY_KEY = KeyCode.R;
    const KeyCode P2_READY_KEY = KeyCode.Slash;

    const string WAITING_LABEL = "WAITING...";
    const string READY_PREFIX = "Hi ";
    const string HINT_TEXT = "Press the joystick button to join!";
    const string PLAYER1_NAME = "CrowCode";
    const string PLAYER2_NAME = "OpenCrow";

    private readonly JoinSource[] _slotSource = new JoinSource[2] { JoinSource.None, JoinSource.None };
    private bool loading;

    private void Awake()
    {
        GameConfig.EnsureExists();
        GameConfig.Instance.Mode = ControlMode.ESP32;
        GameConfig.Instance.Language = initialLanguage;
        EnsureInputBridge();
    }

    private void EnsureInputBridge()
    {
        if (PhoneInputManager.Instance == null)
        {
            var pim = new GameObject("PhoneInputManager");
            pim.AddComponent<PhoneInputManager>();
        }

        if (FindObjectOfType<BleGamepadBridge>() == null)
        {
            var go = new GameObject("BleGamepadBridge");
            go.AddComponent<BleGamepadBridge>();
            DontDestroyOnLoad(go);
        }
    }

    private void Start()
    {
        if (player1HintText != null) player1HintText.text = HINT_TEXT;
        if (player2HintText != null) player2HintText.text = HINT_TEXT;

        if (chineseButton != null) chineseButton.onClick.AddListener(() => SetLanguage(GameLanguage.Default));
        if (englishButton != null) englishButton.onClick.AddListener(() => SetLanguage(GameLanguage.English));

        RefreshLanguageUI();
        RefreshStatus();
    }

    private void Update()
    {
        if (loading) return;

        if (Input.GetKeyDown(languageToggleKey))
        {
            var current = GameConfig.Instance != null ? GameConfig.Instance.Language : GameLanguage.Default;
            SetLanguage(current == GameLanguage.English ? GameLanguage.Default : GameLanguage.English);
        }

        JoinSource pressed = PollJoinSource();
        if (pressed != JoinSource.None)
        {
            TryClaimSlot(pressed);
        }

        if (_slotSource[0] != JoinSource.None && _slotSource[1] != JoinSource.None)
        {
            loading = true;
            SceneManager.LoadScene(NextSceneForLanguage());
        }
    }

    private string NextSceneForLanguage()
    {
        bool english = GameConfig.Instance != null && GameConfig.Instance.Language == GameLanguage.English;
        return english && !string.IsNullOrEmpty(englishNextSceneName) ? englishNextSceneName : nextSceneName;
    }

    private void SetLanguage(GameLanguage lang)
    {
        if (GameConfig.Instance != null) GameConfig.Instance.Language = lang;
        RefreshLanguageUI();
    }

    private void RefreshLanguageUI()
    {
        var lang = GameConfig.Instance != null ? GameConfig.Instance.Language : initialLanguage;
        if (languageLabel != null)
            languageLabel.text = lang == GameLanguage.English ? "Language: English" : "语言: 中文";

        // Visually nudge the active button: dim the inactive one. Only runs if
        // both buttons are wired — partial setup is left untouched.
        if (chineseButton != null && englishButton != null)
        {
            SetButtonActive(chineseButton, lang == GameLanguage.Default);
            SetButtonActive(englishButton, lang == GameLanguage.English);
        }
    }

    private static void SetButtonActive(Button btn, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = active ? 1f : 0.45f;
            img.color = c;
        }
    }

    /// <summary>
    /// Returns the first join source that fired this frame, or None.
    /// Priority is arbitrary — one press per frame is the realistic case.
    /// </summary>
    private JoinSource PollJoinSource()
    {
        if (Input.GetKeyDown(P1_READY_KEY)) return JoinSource.KeyR;
        if (Input.GetKeyDown(P2_READY_KEY)) return JoinSource.KeySlash;

        var pim = PhoneInputManager.Instance;
        if (pim != null)
        {
            if (pim.ConsumeAction(0)) return JoinSource.Esp32P1;
            if (pim.ConsumeAction(1)) return JoinSource.Esp32P2;
        }
        return JoinSource.None;
    }

    private void TryClaimSlot(JoinSource source)
    {
        // Each source is locked to a specific slot, so the Orango One pad
        // (resolved by BleGamepadBridge by device name) always becomes
        // CrowCode/left and Orango Two always becomes OpenCrow/right —
        // independent of which one presses to join first.
        int slot = SlotFor(source);
        if (slot < 0) return;

        // Slot already claimed by some other source — ignore the press.
        if (_slotSource[slot] != JoinSource.None) return;

        _slotSource[slot] = source;

        var cfg = GameConfig.Instance;
        if (cfg != null)
        {
            cfg.PlayerSchemes[slot] = SchemeFor(source, slot);
            cfg.PlayerSchemeAssigned[slot] = true;
            cfg.EspIndexForSlot[slot] = EspSerialIndex(source);
            cfg.PlayerNames[slot] = slot == 0 ? PLAYER1_NAME : PLAYER2_NAME;
        }

        RefreshStatus();
    }

    private static int SlotFor(JoinSource source)
    {
        switch (source)
        {
            case JoinSource.KeyR:     return 0;  // keyboard P1 fallback
            case JoinSource.Esp32P1:  return 0;  // Orango One → CrowCode (left)
            case JoinSource.KeySlash: return 1;  // keyboard P2 fallback
            case JoinSource.Esp32P2:  return 1;  // Orango Two → OpenCrow (right)
        }
        return -1;
    }

    private static int EspSerialIndex(JoinSource source)
    {
        if (source == JoinSource.Esp32P1) return 0;
        if (source == JoinSource.Esp32P2) return 1;
        return -1;
    }

    private static Player.ControlScheme SchemeFor(JoinSource source, int slot)
    {
        switch (source)
        {
            case JoinSource.KeyR: return Player.ControlScheme.WASD;
            case JoinSource.KeySlash: return Player.ControlScheme.ArrowKeys;
            case JoinSource.Esp32P1:
            case JoinSource.Esp32P2: return Player.ControlScheme.ESP32;
        }
        // Fallback shouldn't happen — default keeps the slot playable.
        return slot == 0 ? Player.ControlScheme.WASD : Player.ControlScheme.ArrowKeys;
    }

    private void RefreshStatus()
    {
        bool p1Ready = _slotSource[0] != JoinSource.None;
        bool p2Ready = _slotSource[1] != JoinSource.None;
        var names = GameConfig.Instance != null ? GameConfig.Instance.PlayerNames : null;
        string p1Name = names != null ? (names[0] ?? PLAYER1_NAME) : PLAYER1_NAME;
        string p2Name = names != null ? (names[1] ?? PLAYER2_NAME) : PLAYER2_NAME;
        if (player1StatusText != null) player1StatusText.text = p1Ready ? READY_PREFIX + p1Name : WAITING_LABEL;
        if (player2StatusText != null) player2StatusText.text = p2Ready ? READY_PREFIX + p2Name : WAITING_LABEL;
        if (player1Textbox != null) player1Textbox.SetActive(!p1Ready);
        if (player2Textbox != null) player2Textbox.SetActive(!p2Ready);
        SetSprite(player1Image, p1Ready ? readySprite : pendingSprite);
        SetSprite(player2Image, p2Ready ? readySprite : pendingSprite);

        if (player1NameText != null) player1NameText.text = "";
        if (player2NameText != null) player2NameText.text = "";
    }

    private static void SetSprite(Image img, Sprite sprite)
    {
        if (img != null && sprite != null) img.sprite = sprite;
    }
}
