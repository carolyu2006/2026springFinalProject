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

    // Substring matched (case-insensitive) against Input.GetJoystickNames() to
    // decide which physical pad drives which player slot, so pairing order
    // doesn't determine identity.
    [SerializeField] private string player1DeviceName = "Orango One";
    [SerializeField] private string player2DeviceName = "Orango Two";

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

        ResolveJoystickNumbers(out int p1JoyNum, out int p2JoyNum);

        FeedSlot(pim, 0, p1JoyNum, ref _prevButtonP1);
        FeedSlot(pim, 1, p2JoyNum, ref _prevButtonP2);
    }

    // Returns 1-based Unity joystick numbers for P1 and P2 by matching device
    // names. Falls back to OS enumeration order (1→P1, 2→P2) when neither name
    // matches; if only one matches, the other slot takes the remaining number.
    private void ResolveJoystickNumbers(out int p1JoyNum, out int p2JoyNum)
    {
        int p1 = FindJoystickByName(player1DeviceName);
        int p2 = FindJoystickByName(player2DeviceName);

        if (p1 == 0 && p2 == 0)
        {
            p1JoyNum = 1;
            p2JoyNum = 2;
            return;
        }

        if (p1 == 0) p1 = (p2 == 1) ? 2 : 1;
        if (p2 == 0) p2 = (p1 == 1) ? 2 : 1;

        p1JoyNum = p1;
        p2JoyNum = p2;
    }

    private static int FindJoystickByName(string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        var names = Input.GetJoystickNames();
        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i])
                && names[i].IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return i + 1;
        }
        return 0;
    }

    private void FeedSlot(PhoneInputManager pim, int slot, int joyNum, ref bool prevButton)
    {
        if (joyNum < 1 || joyNum > 2)
        {
            // Only J1*/J2* axes are defined in InputManager. A joystick number
            // outside that range means the matching pad isn't connected on a
            // slot we can read; leave the player idle rather than guessing.
            pim.SetMovement(slot, 0f, 0f);
            prevButton = false;
            return;
        }

        string axisX = joyNum == 1 ? "J1Horizontal" : "J2Horizontal";
        string axisY = joyNum == 1 ? "J1Vertical"   : "J2Vertical";
        KeyCode button = joyNum == 1 ? KeyCode.Joystick1Button0 : KeyCode.Joystick2Button0;

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
