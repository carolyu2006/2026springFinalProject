using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages phone-joystick networking for the display (game) side.
/// Creates a room, announces the room code, then listens for
/// player_input / player_action messages and feeds them into PhoneInputManager.
///
/// Persists across scenes (DontDestroyOnLoad).
/// </summary>
public class JoystickNetworkManager : MonoBehaviour
{
    public static JoystickNetworkManager Instance { get; private set; }

    public event Action<string>      OnRoomCodeReceived;
    public event Action<int, string> OnPlayerConnected;
    public event Action<int>         OnPlayerDisconnected;

    [Serializable]
    private class CreateRoomResponse { public string roomCode; }

    [Serializable]
    private class InboundMsg
    {
        public string type;
        public int    playerId;
        public float  x;
        public float  y;
        public string name;
    }

    public enum BackendTarget { Production, Localhost, Custom }

    [Header("Backend")]
    [Tooltip("Which backend to create the room on. Phones must be able to reach the same URL.")]
    [SerializeField] private BackendTarget backend = BackendTarget.Production;

    [Tooltip("Used when Backend = Custom. For a real phone on Wi-Fi use your laptop's LAN IP, e.g. ws://192.168.1.42:7001")]
    [SerializeField] private string customServerBase = "ws://192.168.1.42:7001";

    private const string ProductionServerBase = "wss://orango.ing";
    private const string LocalhostServerBase  = "ws://localhost:7001";

    private bool subscribed;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        TrySubscribe();
    }

    private void Start() => TrySubscribe();

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (WebSocketClient.Instance == null) return;
        WebSocketClient.Instance.OnMessageReceived += HandleMessage;
        subscribed = true;
        Debug.Log("[Joystick] Subscribed to WebSocketClient.OnMessageReceived");
    }

    /// <summary>
    /// Creates a room on the backend and connects the WebSocket.
    /// Call this from StartSceneManager when Phone mode is selected.
    /// </summary>
    public void CreateRoomAndConnect()
    {
        StartCoroutine(CreateRoomRoutine());
    }

    private string ResolveServerBase()
    {
        // GameConfig.ServerBase, if set programmatically, still wins.
        string pinned = GameConfig.Instance?.ServerBase;
        if (!string.IsNullOrEmpty(pinned)) return pinned;

        switch (backend)
        {
            case BackendTarget.Localhost: return LocalhostServerBase;
            case BackendTarget.Custom:    return customServerBase;
            default:                      return ProductionServerBase;
        }
    }

    private IEnumerator CreateRoomRoutine()
    {
        string[] candidates = { ResolveServerBase() };

        foreach (string serverBase in candidates)
        {
            string httpBase = serverBase.Replace("ws://", "http://").Replace("wss://", "https://");
            string url      = $"{httpBase}/gowiththecrowd/room/create";

            Debug.Log($"[Joystick] Trying {url}");
            var req = UnityWebRequest.Get(url);
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Joystick] {serverBase} unreachable: {req.error}");
                continue;
            }

            var resp = JsonUtility.FromJson<CreateRoomResponse>(req.downloadHandler.text);
            if (resp == null || string.IsNullOrEmpty(resp.roomCode))
            {
                Debug.LogWarning($"[Joystick] {serverBase} returned bad payload: {req.downloadHandler.text}");
                continue;
            }

            string code = resp.roomCode;
            if (GameConfig.Instance != null)
            {
                GameConfig.Instance.ServerBase = serverBase;
                GameConfig.Instance.RoomCode   = code;
            }

            Debug.Log($"[Joystick] Connected to {serverBase}, room {code}");
            // Make sure we're subscribed before the display socket opens —
            // otherwise an early roster-replay message would be dropped.
            TrySubscribe();
            // Start the display WebSocket FIRST so the server is ready to fan
            // out player_connected to us before any phone scans the code.
            WebSocketClient.Instance.Connect(code, serverBase);
            OnRoomCodeReceived?.Invoke(code);
            yield break;
        }

        Debug.LogError("[Joystick] All backends unreachable: " + string.Join(", ", candidates));
    }

    private void HandleMessage(string json)
    {
        var msg = JsonUtility.FromJson<InboundMsg>(json);
        if (msg == null)
        {
            Debug.LogWarning($"[Joystick] Failed to parse: {json}");
            return;
        }

        switch (msg.type)
        {
            case "player_connected":
                if (GameConfig.Instance != null)
                    GameConfig.Instance.ConnectedPhoneCount++;
                Debug.Log($"[Joystick] Player {msg.playerId} connected ({msg.name})");
                OnPlayerConnected?.Invoke(msg.playerId, msg.name);
                break;

            case "player_disconnected":
                if (GameConfig.Instance != null && GameConfig.Instance.ConnectedPhoneCount > 0)
                    GameConfig.Instance.ConnectedPhoneCount--;
                Debug.Log($"[Joystick] Player {msg.playerId} disconnected");
                OnPlayerDisconnected?.Invoke(msg.playerId);
                break;

            case "player_input":
                // playerId is 1-based; PhoneInputManager uses 0-based index
                PhoneInputManager.Instance?.SetMovement(msg.playerId - 1, msg.x, msg.y);
                break;

            case "player_action":
                PhoneInputManager.Instance?.TriggerAction(msg.playerId - 1);
                break;
        }
    }
}
