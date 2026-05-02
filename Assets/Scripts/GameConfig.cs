using UnityEngine;

public enum ControlMode { Keyboard, Phone, ESP32 }

public enum GameLanguage { Default, English }

/// <summary>
/// Persistent singleton that stores the chosen control mode and room state.
/// Survives scene loads. If the game starts directly (not via ControlSelectScene),
/// Mode defaults to Keyboard.
/// </summary>
public class GameConfig : MonoBehaviour
{
    public static GameConfig Instance { get; private set; }

    public ControlMode Mode = ControlMode.Keyboard;
    public GameLanguage Language = GameLanguage.Default;
    public string RoomCode = "";
    // Empty = auto: JoystickNetworkManager will try its candidate list in order.
    // Set explicitly (in the inspector) to pin a single backend.
    public string ServerBase = "";
    public int ConnectedPhoneCount = 0;

    // Display names for the two player slots, captured from the phone lobby.
    // Index 0 = player 1, index 1 = player 2. Null/empty means "no name known".
    public string[] PlayerNames = new string[2];

    // Per-slot scheme set by ArduinoStartScene. Default (null) = fall back to Mode.
    public Player.ControlScheme[] PlayerSchemes = new Player.ControlScheme[2];
    public bool[] PlayerSchemeAssigned = new bool[2];

    // For the hybrid scene: which serial "P1"/"P2" index (0 or 1) feeds each slot.
    // -1 means "no remap — use the slot index directly".
    public int[] EspIndexForSlot = new int[] { -1, -1 };

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call from any scene to guarantee a GameConfig exists.
    /// Safe to call multiple times.
    /// </summary>
    public static void EnsureExists()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameConfig");
            go.AddComponent<GameConfig>();
        }
    }
}
