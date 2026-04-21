using UnityEngine;

/// <summary>
/// Holds per-player input state fed by either the phone WebSocket client
/// or the ESP32 serial controller. Player.cs reads from here when
/// ControlScheme is Phone or ESP32.
/// </summary>
public class PhoneInputManager : MonoBehaviour
{
    public static PhoneInputManager Instance { get; private set; }

    private class InputState
    {
        public float x;
        public float y;
        public bool actionPending;
    }

    private readonly InputState[] _states = new InputState[2]
    {
        new InputState(),
        new InputState()
    };

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetMovement(int playerIndex, float x, float y)
    {
        if (playerIndex < 0 || playerIndex >= 2) return;
        _states[playerIndex].x = x;
        _states[playerIndex].y = y;
    }

    public void TriggerAction(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 2) return;
        _states[playerIndex].actionPending = true;
    }

    public Vector2 GetMovement(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 2) return Vector2.zero;
        return new Vector2(_states[playerIndex].x, _states[playerIndex].y);
    }

    /// <summary>
    /// Returns true once per pending action, then clears it.
    /// </summary>
    public bool ConsumeAction(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= 2) return false;
        if (_states[playerIndex].actionPending)
        {
            _states[playerIndex].actionPending = false;
            return true;
        }
        return false;
    }
}
