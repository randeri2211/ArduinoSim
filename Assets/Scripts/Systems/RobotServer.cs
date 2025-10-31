using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using UnityEngine;
using Unity.Burst;
using Unity.Entities;

[UpdateAfter(typeof(ProximitySensorSystem))]
public static class RobotServerRuntime
{
    public static readonly ConcurrentQueue<string> Commands = new();

    static TcpListener _listener;
    static Thread _thread;
    static volatile bool _running;

    // Track the most recent client connection
    static TcpClient _currentClient;
    static readonly object _clientLock = new();

    public static void Start(int port = 7001)
    {
        if (_running) return;
        _running = true;
        _thread = new Thread(() => ServerLoop(port)) { IsBackground = true, Name = "RobotServer" };
        _thread.Start();
    }

    public static void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _thread?.Join(250); } catch { }
        _listener = null;
        _thread = null;
    }

    static void ServerLoop(int port)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            Debug.Log($"[RobotServer] Listening on 127.0.0.1:{port}");

            var buf = new byte[4096];

            while (_running)
            {
                // Wait for a client
                using var client = _listener.AcceptTcpClient();
                lock (_clientLock) _currentClient = client;
                Debug.Log("[RobotServer] accepted client");

                using var stream = client.GetStream();
                var sb = new StringBuilder();

                while (_running && client.Connected)
                {
                    int n = 0;
                    try
                    {
                        n = stream.Read(buf, 0, buf.Length); // blocks
                        if (n <= 0) break; // graceful close (FIN)
                    }
                    catch (IOException)
                    {
                        // Expected when client closes abruptly (RST). Treat as normal.
                        break;
                    }
                    catch (SocketException)
                    {
                        // Network hiccup/abort — also treat as disconnect.
                        break;
                    }

                    sb.Append(Encoding.UTF8.GetString(buf, 0, n));

                    int newline;
                    // parse complete lines
                    while ((newline = sb.ToString().IndexOf('\n')) >= 0)
                    {
                        string line = sb.ToString(0, newline).Trim();
                        sb.Remove(0, newline + 1);
                        if (line.Length > 0)
                            Commands.Enqueue(line);
                    }
                }

                Debug.Log("[RobotServer] client closed");
                lock (_clientLock) _currentClient = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RobotServer] Exception: {e}");
        }
    }

    // 📨 Send message back to the most recent client
    public static void Send(string msg)
    {
        lock (_clientLock)
        {
            if (_currentClient == null || !_currentClient.Connected) return;
            try
            {
                var data = Encoding.UTF8.GetBytes(msg + "\n");
                _currentClient.GetStream().Write(data, 0, data.Length);
                Debug.Log($"[RobotServer] Sent: {msg}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RobotServer] Send failed: {e.Message}");
            }
        }
    }
}
