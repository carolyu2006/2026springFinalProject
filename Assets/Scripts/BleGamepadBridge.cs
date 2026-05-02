using UnityEngine;

// Reads two BLE HID gamepads (ESP32s running the BleGamepad library) via
// Unity's legacy Input API and feeds each one into a separate
// PhoneInputManager slot, so the two ESP32 controllers drive the two
// in-game characters independently.
//
// Firmware sends:
//   - Left Thumbstick X/Y on the gamepad's first two axes
//   - BUTTON_1 on joystick button 0
//
// The per-joystick axis names ("J1Horizontal", "J2Horizontal", etc.) are
// defined in ProjectSettings/InputManager.asset with joyNum bound to the
// specific joystick number, so two pads no longer get merged together.
public class BleGamepadBridge : MonoBehaviour
{
    [SerializeField] private float deadzone = 0.15f;
    // Most HID gamepads report joystick Y as positive-down, but the game
    // expects positive-up (forward). Default to inverted so the ESP32 pads
    // feel right out of the box.
    [SerializeField] private bool invertY = true;
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

        FeedSlot(pim, 0, "J1Horizontal", "J1Vertical", KeyCode.Joystick1Button0, ref _prevButtonP1);
        FeedSlot(pim, 1, "J2Horizontal", "J2Vertical", KeyCode.Joystick2Button0, ref _prevButtonP2);
    }

    private void FeedSlot(PhoneInputManager pim, int slot, string axisX, string axisY, KeyCode button, ref bool prevButton)
    {
        float x = SafeAxis(axisX);
        float y = SafeAxis(axisY);
        if (invertY) y = -y;
        if (Mathf.Abs(x) < deadzone) x = 0f;
        if (Mathf.Abs(y) < deadzone) y = 0f;
        pim.SetMovement(slot, x, y);

        bool pressed = Input.GetKey(button);
        if (pressed && !prevButton) pim.TriggerAction(slot);
        prevButton = pressed;
    }

    // GetAxisRaw throws if the axis isn't defined in InputManager. Defending
    // against that lets the scene keep working in editors that haven't yet
    // reimported the updated InputManager.asset.
    private static float SafeAxis(string name)
    {
        try { return Input.GetAxisRaw(name); }
        catch { return 0f; }
    }
}
