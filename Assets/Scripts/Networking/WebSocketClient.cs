using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton WebSocket client that persists across scenes.
/// Connects as the display role, auto-reconnects on disconnect.
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    public static WebSocketClient Instance { get; private set; }

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    private WebSocketBridge _ws;
    private string _serverUrl;
    private bool _connected;
    private bool _shouldReconnect;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Connect(string roomCode, string serverBase)
    {
        _serverUrl = $"{serverBase}/gowiththecrowd/room/{roomCode}?role=display";
        _shouldReconnect = true;
        StartCoroutine(ConnectRoutine());
    }

    private IEnumerator ConnectRoutine()
    {
        Debug.Log($"[WS] Connecting to {_serverUrl}");
        _ws = new WebSocketBridge(new Uri(_serverUrl));
        yield return StartCoroutine(_ws.Connect());

        if (_ws.Error != null)
        {
            Debug.LogError($"[WS] Connection error: {_ws.Error}");
            if (_shouldReconnect)
            {
                yield return new WaitForSeconds(2f);
                StartCoroutine(ConnectRoutine());
            }
            yield break;
        }

        _connected = true;
        Debug.Log($"[WS] Connected to {_serverUrl}");
        OnConnected?.Invoke();

        while (_connected && _ws.Error == null)
        {
            string msg;
            while ((msg = _ws.Recv()) != null)
            {
                Debug.Log($"[WS] Recv: {msg}");
                OnMessageReceived?.Invoke(msg);
            }
            yield return null;
        }

        _connected = false;
        Debug.Log("[WS] Disconnected");
        OnDisconnected?.Invoke();

        if (_shouldReconnect)
        {
            yield return new WaitForSeconds(2f);
            StartCoroutine(ConnectRoutine());
        }
    }

    public void Send(string json)
    {
        if (_connected && _ws != null)
            _ws.SendString(json);
    }

    public void Disconnect()
    {
        _shouldReconnect = false;
        _connected = false;
        _ws?.Close();
    }

    private void OnDestroy() => Disconnect();
}
