using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class TcpJsonClient : MonoBehaviour
{
    [Header("TCP Settings")]
    public string host = "127.0.0.1";
    public int port = 8765;
    public bool connectOnStart = true;

    [Header("Diagnostics")]
    public bool logConnection = true;
    public bool logLines = false;

    private Thread _thread;
    private volatile bool _running;
    private TcpClient _client;
    private StreamReader _reader;

    
    public readonly ConcurrentQueue<string> IncomingLines = new ConcurrentQueue<string>();

    void Start()
    {
        if (connectOnStart)
            StartClient();
    }

    public void StartClient()
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(RunClient) { IsBackground = true };
        _thread.Start();
    }

    public void StopClient()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        _client = null;
    }

    private void RunClient()
    {
        while (_running)
        {
            try
            {
                _client = new TcpClient();
                _client.NoDelay = true;
                _client.Connect(host, port);
                using (var ns = _client.GetStream())
                using (var sr = new StreamReader(ns))
                {
                    _reader = sr;
                    if (logConnection) Debug.Log($"[TcpJsonClient] Connected {host}:{port}");
                    string line;
                    while (_running && (line = sr.ReadLine()) != null)
                    {
                        if (logLines) Debug.Log($"[TcpJsonClient] {line}");
                        IncomingLines.Enqueue(line);
                    }
                }
            }
            catch (Exception e)
            {
                if (logConnection)
                    Debug.LogWarning($"[TcpJsonClient] {e.GetType().Name}: {e.Message}. Reconnecting in 1s...");
                Thread.Sleep(1000);
            }
        }
        if (logConnection) Debug.Log("[TcpJsonClient] Stopped.");
    }

    void OnDestroy()
    {
        StopClient();
        try { _thread?.Join(200); } catch { }
    }
}
