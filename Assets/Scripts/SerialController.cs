using UnityEngine;

#if !UNITY_WEBGL
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
#endif

/// <summary>
/// Reads joystick data from an ESP32 over a serial (USB) connection and
/// feeds it into PhoneInputManager so Player.cs can consume it.
///
/// Expected serial protocol (newline-terminated):
///   Movement : "P1:0.50:-0.30"   (player index, x, y — all floats in [-1, 1])
///   Action   : "P1:ACT"
///   Player 2 : replace "P1" with "P2"
///
/// Only active when GameConfig.Mode == ControlMode.ESP32.
/// Configure portName and baudRate via the Inspector.
/// </summary>
public class SerialController : MonoBehaviour
{
#if !UNITY_WEBGL

    [SerializeField] private string portName  = "COM3";   // e.g. /dev/tty.usbserial-* on Mac
    [SerializeField] private int    baudRate  = 115200;
    [SerializeField] private bool   logRawLines = false;  // turn on to see exactly what the ESP32 sends

    private SerialPort            _port;
    private Thread                _readThread;
    private bool                  _running;
    private readonly Queue<string> _queue = new Queue<string>();
    private readonly object       _lock  = new object();

    /// <summary>
    /// Overrides portName/baudRate before Start() runs. Use from a scene
    /// manager that spawns this component at runtime.
    /// </summary>
    public void Configure(string port, int baud)
    {
        portName = port;
        baudRate = baud;
    }

    private void Start()
    {
        if (GameConfig.Instance?.Mode != ControlMode.ESP32) return;

        try
        {
            _port = new SerialPort(portName, baudRate) { ReadTimeout = 100 };
            _port.Open();
            _running = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true };
            _readThread.Start();
            Debug.Log($"[Serial] Opened {portName} at {baudRate} baud");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Serial] Failed to open {portName}: {e.Message}");
        }
    }

    private void ReadLoop()
    {
        while (_running && _port != null && _port.IsOpen)
        {
            try
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                    lock (_lock) { _queue.Enqueue(line.Trim()); }
            }
            catch (System.TimeoutException) { /* normal */ }
            catch { break; }
        }
    }

    private void Update()
    {
        lock (_lock)
        {
            while (_queue.Count > 0)
                ParseLine(_queue.Dequeue());
        }
    }

    /// <summary>
    /// Accepts any of:
    ///   "P1:0.50:-0.30"        → movement only
    ///   "P1:ACT" / "P1:BTN"    → button only (also PRESS, DOWN, or "1")
    ///   "P1:0.50:-0.30:1"      → movement + button in one frame
    /// Player tag and keywords are case-insensitive.
    /// </summary>
    private void ParseLine(string line)
    {
        if (logRawLines) Debug.Log($"[Serial] {line}");

        string[] parts = line.Split(':');
        if (parts.Length < 2) return;

        int playerIndex = PlayerIndex(parts[0]);
        if (playerIndex < 0) return;

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var floatStyle = System.Globalization.NumberStyles.Float;

        // "P1:<action>"
        if (parts.Length == 2)
        {
            if (IsActionToken(parts[1]))
                PhoneInputManager.Instance?.TriggerAction(playerIndex);
            return;
        }

        // "P1:x:y" or "P1:x:y:<action>"
        if (float.TryParse(parts[1], floatStyle, inv, out float x)
            && float.TryParse(parts[2], floatStyle, inv, out float y))
        {
            PhoneInputManager.Instance?.SetMovement(playerIndex, x, y);

            if (parts.Length >= 4 && IsActionToken(parts[3]))
                PhoneInputManager.Instance?.TriggerAction(playerIndex);
        }
    }

    private static int PlayerIndex(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return -1;
        tag = tag.Trim().ToUpperInvariant();
        if (tag == "P1" || tag == "1") return 0;
        if (tag == "P2" || tag == "2") return 1;
        return -1;
    }

    private static bool IsActionToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        token = token.Trim().ToUpperInvariant();
        return token == "ACT" || token == "BTN" || token == "PRESS"
            || token == "DOWN" || token == "1" || token == "TRUE";
    }

    private void OnDestroy()
    {
        _running = false;
        try { _port?.Close(); } catch { /* ignored */ }
    }

#endif
}
