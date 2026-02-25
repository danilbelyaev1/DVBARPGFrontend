using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using UnityEngine;

namespace DVBARPG.Net.Network
{
    public sealed class NetworkSessionRunner : MonoBehaviour, ISessionService
    {
        public bool IsConnected { get; private set; }
        public event Action<SnapshotEnvelope> Snapshot;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private Task _recvTask;
        private readonly ConcurrentQueue<SnapshotEnvelope> _snapshots = new();
        private int _seq;
        private string _serverUrl = "ws://localhost:8080/ws";
        private float _lastMoveLog;

        public void Connect(AuthSession session, string mapId, string serverUrl)
        {
            if (IsConnected) return;
            if (session == null) return;

            _serverUrl = string.IsNullOrWhiteSpace(serverUrl) ? _serverUrl : serverUrl;
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            Debug.Log($"NetworkSessionRunner: connecting to {_serverUrl}");

            _recvTask = Task.Run(async () =>
            {
                try
                {
                    await _socket.ConnectAsync(new Uri(_serverUrl), _cts.Token);
                    IsConnected = true;
                    Debug.Log("NetworkSessionRunner: connected");

                    await SendEnvelopeAsync(new CommandEnvelope
                    {
                        Type = "connect",
                        Seq = NextSeq(),
                        Token = session.Token,
                        CharacterId = session.CharacterId,
                        SeasonId = session.SeasonId
                    });

                    await SendEnvelopeAsync(new CommandEnvelope
                    {
                        Type = "start",
                        Seq = NextSeq(),
                        MapId = string.IsNullOrWhiteSpace(mapId) ? "default" : mapId
                    });

                    await ReceiveLoopAsync(_cts.Token);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"NetworkSessionRunner: {e.GetType().Name} {e.Message}");
                }
                finally
                {
                    IsConnected = false;
                }
            });
        }

        public void RegisterLocalMover(DVBARPG.Core.Simulation.ILocalMover mover)
        {
        }

        public void Send(IClientCommand command)
        {
            if (!IsConnected || _socket == null || _socket.State != WebSocketState.Open) return;

            if (command is CmdMove move)
            {
                if (Time.unscaledTime - _lastMoveLog > 1f)
                {
                    Debug.Log($"NetworkSessionRunner: send move ({move.Direction.x:0.00},{move.Direction.z:0.00})");
                    _lastMoveLog = Time.unscaledTime;
                }
                _ = SendEnvelopeAsync(new CommandEnvelope
                {
                    Type = "move",
                    Seq = NextSeq(),
                    X = move.Direction.x,
                    Y = move.Direction.z
                });
                return;
            }

            if (command is CmdStop)
            {
                _ = SendEnvelopeAsync(new CommandEnvelope
                {
                    Type = "stop",
                    Seq = NextSeq()
                });
            }
        }

        private void Update()
        {
            while (_snapshots.TryDequeue(out var snap))
            {
                Snapshot?.Invoke(snap);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            while (_socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new System.IO.MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                Debug.Log($"WS RECV: {json}");
                TryHandleMessage(json);
            }
        }

        private void TryHandleMessage(string json)
        {
            try
            {
                var baseEnv = JsonConvert.DeserializeObject<BaseEnvelope>(json, NetProtocol.JsonSettings);
                if (baseEnv == null) return;

                switch (baseEnv.Type)
                {
                    case "snapshot":
                        var snap = JsonConvert.DeserializeObject<SnapshotEnvelope>(json, NetProtocol.JsonSettings);
                        if (snap != null) _snapshots.Enqueue(snap);
                        break;
                    case "hello":
                    case "connect_ok":
                    case "instance_start":
                    case "error":
                        break;
                }
            }
            catch
            {
            }
        }

        private int NextSeq() => Interlocked.Increment(ref _seq);

        private Task SendEnvelopeAsync(CommandEnvelope cmd)
        {
            var json = JsonConvert.SerializeObject(cmd, NetProtocol.JsonSettings);
            Debug.Log($"WS SEND: {json}");
            var bytes = Encoding.UTF8.GetBytes(json);
            return _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void OnDestroy()
        {
            try
            {
                _cts?.Cancel();
                _socket?.Dispose();
            }
            catch
            {
            }
        }
    }
}
