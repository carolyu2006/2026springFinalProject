using UnityEngine;

// Reads a BLE HID gamepad (ESP32 running the BleGamepad library) via Unity's
// legacy Input API and feeds PhoneInputManager so Player.ControlScheme.ESP32
// works identically to the old serial path.
//
// Firmware sends:
//   - Left Thumbstick X/Y on the gamepad's first two axes
//   - BUTTON_1 on joystick button 0
public class BleGamepadBridge : MonoBehaviour
{
    [SerializeField] private float deadzone = 0.15f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool logJoysticks = true;

    private bool _prevButtonP1;
    private bool _prevButtonP2;
    private float _nameLogTimer;

    private void Update()
    {
        if (logJoysticks)
        {
            _nameLogTimer -= Time.unscaledDeltaTime;
            if (_nameLogTimer <= 0f)
            {
                _nameLogTimer = 2f;
                var names = Input.GetJoystickNames();
                for (int i = 0; i < names.Length; i++)
                    if (!string.IsNullOrEmpty(names[i]))
                        Debug.Log($"[BleGamepadBridge] Joystick {i + 1}: '{names[i]}'");
            }
        }

        var pim = PhoneInputManager.Instance;
        if (pim == null) return;

        // Axes — legacy Input's "Horizontal"/"Vertical" already map to the
        // gamepad's primary thumbstick on joystick 1. If there is no joystick,
        // these may pick up keyboard arrows; that's harmless for this scene.
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        if (invertY) y = -y;
        if (Mathf.Abs(x) < deadzone) x = 0f;
        if (Mathf.Abs(y) < deadzone) y = 0f;
        pim.SetMovement(0, x, y);

        // Button — Joystick 1 is P1, Joystick 2 (if present) is P2.
        bool p1 = Input.GetKey(KeyCode.Joystick1Button0);
        if (p1 && !_prevButtonP1) pim.TriggerAction(0);
        _prevButtonP1 = p1;

        bool p2 = Input.GetKey(KeyCode.Joystick2Button0);
        if (p2 && !_prevButtonP2) pim.TriggerAction(1);
        _prevButtonP2 = p2;
    }
}
