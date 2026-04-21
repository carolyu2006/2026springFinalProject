using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private string nextSceneName = "InstructionScene";

    [Header("Per-Player UI — Status (the WAITING… label under the image)")]
    [SerializeField] private TMP_Text player1StatusText;
    [SerializeField] private TMP_Text player2StatusText;

    [Header("Per-Player UI — Hint chatbox")]
    [SerializeField] private TMP_Text player1HintText;
    [SerializeField] private TMP_Text player2HintText;

    [Header("Per-Player UI — Chatbox root (hidden once that player is ready)")]
    [SerializeField] private GameObject player1Textbox;
    [SerializeField] private GameObject player2Textbox;

    private enum JoinSource { None, KeyR, KeySlash, Esp32P1, Esp32P2 }

    const KeyCode P1_READY_KEY = KeyCode.R;
    const KeyCode P2_READY_KEY = KeyCode.Slash;

    const string WAITING_LABEL = "WAITING...";
    const string READY_LABEL = "READY!";
    const string HINT_TEXT = "Press the joystick button to join!";

    private readonly JoinSource[] _slotSource = new JoinSource[2] { JoinSource.None, JoinSource.None };
    private bool loading;

    private void Awake()
    {
        GameConfig.EnsureExists();
        GameConfig.Instance.Mode = ControlMode.ESP32;
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
        RefreshStatus();
    }

    private void Update()
    {
        if (loading) return;

        JoinSource pressed = PollJoinSource();
        if (pressed != JoinSource.None)
        {
            TryClaimSlot(pressed);
        }

        if (_slotSource[0] != JoinSource.None && _slotSource[1] != JoinSource.None)
        {
            loading = true;
            SceneManager.LoadScene(nextSceneName);
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
        // Don't let a single source claim both slots.
        if (_slotSource[0] == source || _slotSource[1] == source) return;

        int slot = _slotSource[0] == JoinSource.None ? 0 : 1;
        _slotSource[slot] = source;

        var cfg = GameConfig.Instance;
        if (cfg != null)
        {
            cfg.PlayerSchemes[slot] = SchemeFor(source, slot);
            cfg.PlayerSchemeAssigned[slot] = true;
            cfg.EspIndexForSlot[slot] = EspSerialIndex(source);
        }

        RefreshStatus();
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
        if (player1StatusText != null) player1StatusText.text = p1Ready ? READY_LABEL : WAITING_LABEL;
        if (player2StatusText != null) player2StatusText.text = p2Ready ? READY_LABEL : WAITING_LABEL;
        if (player1Textbox != null) player1Textbox.SetActive(!p1Ready);
        if (player2Textbox != null) player2Textbox.SetActive(!p2Ready);
    }
}
