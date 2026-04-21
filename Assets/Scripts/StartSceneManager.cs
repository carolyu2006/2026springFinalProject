using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// StartScene manager for Keyboard / ESP32 modes.
/// Both players must ready up to start the game:
///   Keyboard — P1 presses R (WASD side), P2 presses / (Arrows side).
///   ESP32    — each player presses their joystick action button.
/// Phone mode has its own scene (PhoneStartScene) — see PhoneStartSceneManager.
/// </summary>
public class StartSceneManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "InstructionScene";

    [Header("Per-Player UI — Status (the WAITING… label under the image)")]
    [SerializeField] private TMP_Text player1StatusText;
    [SerializeField] private TMP_Text player2StatusText;

    [Header("Per-Player UI — Hint chatbox (the 'Press the joystick to start!' word)")]
    [SerializeField] private TMP_Text player1HintText;
    [SerializeField] private TMP_Text player2HintText;

    [Header("Per-Player UI — Chatbox root (hidden once that player is ready)")]
    [SerializeField] private GameObject player1Textbox;
    [SerializeField] private GameObject player2Textbox;

    const KeyCode P1_READY_KEY = KeyCode.R;
    const KeyCode P2_READY_KEY = KeyCode.Slash;

    const string WAITING_LABEL = "WAITING...";
    const string READY_LABEL = "READY!";

    private bool p1Ready;
    private bool p2Ready;
    private bool loading;

    private void Awake()
    {
        GameConfig.EnsureExists();
    }

    private void Start()
    {
        SetHintsForMode();
        RefreshStatus();
    }

    private void Update()
    {
        if (loading) return;

        ControlMode mode = GameConfig.Instance != null ? GameConfig.Instance.Mode : ControlMode.Keyboard;

        if (mode == ControlMode.ESP32)
        {
            var pim = PhoneInputManager.Instance;
            if (pim != null)
            {
                if (!p1Ready && pim.ConsumeAction(0)) { p1Ready = true; RefreshStatus(); }
                if (!p2Ready && pim.ConsumeAction(1)) { p2Ready = true; RefreshStatus(); }
            }
        }
        else
        {
            if (!p1Ready && Input.GetKeyDown(P1_READY_KEY)) { p1Ready = true; RefreshStatus(); }
            if (!p2Ready && Input.GetKeyDown(P2_READY_KEY)) { p2Ready = true; RefreshStatus(); }
        }

        if (p1Ready && p2Ready)
        {
            loading = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private void SetHintsForMode()
    {
        ControlMode mode = GameConfig.Instance != null ? GameConfig.Instance.Mode : ControlMode.Keyboard;

        string p1Hint, p2Hint;
        if (mode == ControlMode.Keyboard)
        {
            p1Hint = "Press button [R] to start!";
            p2Hint = "Press button [/] to start!";
        }
        else
        {
            p1Hint = "Press the joystick to start!";
            p2Hint = "Press the joystick to start!";
        }

        if (player1HintText != null) player1HintText.text = p1Hint;
        if (player2HintText != null) player2HintText.text = p2Hint;
    }

    private void RefreshStatus()
    {
        if (player1StatusText != null) player1StatusText.text = p1Ready ? READY_LABEL : WAITING_LABEL;
        if (player2StatusText != null) player2StatusText.text = p2Ready ? READY_LABEL : WAITING_LABEL;
        if (player1Textbox != null) player1Textbox.SetActive(!p1Ready);
        if (player2Textbox != null) player2Textbox.SetActive(!p2Ready);
    }
}
