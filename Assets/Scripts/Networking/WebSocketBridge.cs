using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

/// <summary>
/// Cross-platform WebSocket: uses browser-native WS in WebGL builds,
/// System.Net.WebSockets in Editor / Standalone.
/// </summary>
public class WebSocketBridge
{
    private Uri _uri;
    public string Error { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR

    [DllImport("__Internal")] private static extern int  WebSocket_Create  (string url);
    [DllImport("__Internal")] private static extern int  WebSocket_GetState(int id);
    [DllImport("__Internal")] private static extern void WebSocket_Send    (int id, string msg);
    [DllImport("__Internal")] private static extern string WebSocket_Recv  (int id);
    [DllImport("__Internal")] private static extern void WebSocket_Close   (int id);
    [DllImport("__Internal")] private static extern string WebSocket_GetError(int id);

    private int _id;

    public WebSocketBridge(Uri uri) { _uri = uri; }

    public IEnumerator Connect()
    {
        _id = WebSocket_Create(_uri.ToString());
        while (WebSocket_GetState(_id) == 0)
            yield return null;
        if (WebSocket_GetState(_id) != 1)
            Error = WebSocket_GetError(_id) ?? "Connection failed";
    }

    public void SendString(string msg) => WebSocket_Send(_id, msg);
    public string Recv()              => WebSocket_Recv(_id);
    public void Close()               => WebSocket_Close(_id);

#else

    private ClientWebSocket _cws;
    private CancellationTokenSource _cts;
    private readonly Queue<string> _messages = new Queue<string>();

    public WebSocketBridge(Uri uri)
    {
        _uri = uri;
        _cws = new ClientWebSocket();
        // Cloudflare's WebSocket edge sometimes hangs the upgrade if there's
        // no User-Agent. Setting one lets the handshake complete on Mono.
        try { _cws.Options.SetRequestHeader("User-Agent", "Unity-WebSocketBridge/1.0"); }
        catch (Exception) { /* some platforms reject custom headers; safe to ignore */ }
        _cts = new CancellationTokenSource();
    }

    public IEnumerator Connect()
    {
        const float ConnectTimeoutSeconds = 10f;
        var task = _cws.ConnectAsync(_uri, _cts.Token);
        float elapsed = 0f;
        while (!task.IsCompleted)
        {
            elapsed += UnityEngine.Time.unscaledDeltaTime;
            if (elapsed > ConnectTimeoutSeconds)
            {
                Error = $"Connect timed out after {ConnectTimeoutSeconds}s ({_uri})";
                _cts.Cancel();
                yield break;
            }
            yield return null;
        }

        if (task.IsFaulted)
        {
            Error = task.Exception?.InnerException?.Message
                 ?? task.Exception?.Message
                 ?? "Connection failed (faulted)";
            yield break;
        }

        if (task.IsCanceled)
        {
            Error = "Connection canceled";
            yield break;
        }

        if (_cws.State != WebSocketState.Open)
        {
            Error = $"Socket not open after connect (state={_cws.State})";
            yield break;
        }

        Task.Run(ReceiveLoop);
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        try
        {
            while (_cws.State == WebSocketState.Open)
            {
                var result = await _cws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                lock (_messages) { _messages.Enqueue(msg); }
            }
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
    }

    public void SendString(string msg)
    {
        if (_cws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(msg);
        _ = _cws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    public string Recv()
    {
        lock (_messages)
        {
            return _messages.Count > 0 ? _messages.Dequeue() : null;
        }
    }

    public void Close()
    {
        _cts.Cancel();
        if (_cws.State == WebSocketState.Open)
            _ = _cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

#endif
}
