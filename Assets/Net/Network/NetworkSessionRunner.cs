using System;
using System.Collections.Generic;
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
        public event Action BufferUpdated;
        public event Action<int, Vector2, float> MoveSent;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private Task _recvTask;
        // Сырые снапшоты приходят из сети, обрабатываем их в Update на главном потоке.
        private readonly ConcurrentQueue<SnapshotEnvelope> _snapshots = new();
        private int _seq;
        private string _serverUrl = "ws://localhost:8080/ws";
        private float _lastMoveLog;
        private bool _connectOk;
        private bool _instanceStarted;
        // Общий буфер снапшотов для интерполяции/экстраполяции.
        private readonly List<SnapshotEnvelope> _buffer = new();
        private float _serverToLocalOffsetMs;
        private bool _hasTimeOffset;
        private readonly object _bufferLock = new();

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
                    Debug.LogWarning($"NetworkSessionRunner: {e.GetType().Name} {e.Message}\n{e.StackTrace}");
                }
                finally
                {
                    IsConnected = false;
                    Debug.Log("NetworkSessionRunner: disconnected");
                }
            });
        }

        public void Send(IClientCommand command)
        {
            if (!IsConnected || _socket == null || _socket.State != WebSocketState.Open) return;
            if (!_connectOk || !_instanceStarted) return;

            if (command is CmdMove move)
            {
                if (Time.unscaledTime - _lastMoveLog > 1f)
                {
                    Debug.Log($"NetworkSessionRunner: send move ({move.Direction.x:0.00},{move.Direction.z:0.00})");
                    _lastMoveLog = Time.unscaledTime;
                }
                var seq = NextSeq();
                MoveSent?.Invoke(seq, new Vector2(move.Direction.x, move.Direction.z), move.DeltaTime);
                _ = SendEnvelopeAsync(new CommandEnvelope
                {
                    Type = "move",
                    Seq = seq,
                    X = move.Direction.x,
                    Y = move.Direction.z
                });
                return;
            }

            if (command is CmdStop)
            {
                var seq = NextSeq();
                _ = SendEnvelopeAsync(new CommandEnvelope
                {
                    Type = "stop",
                    Seq = seq
                });
            }
        }

        private void Update()
        {
            while (_snapshots.TryDequeue(out var snap))
            {
                Snapshot?.Invoke(snap);

                lock (_bufferLock)
                {
                    // Кладём снапшот в буфер для интерполяции.
                    _buffer.Add(snap);
                    if (_buffer.Count > 30) _buffer.RemoveAt(0);
                    if (!_hasTimeOffset)
                    {
                        // Вычисляем смещение времени: serverTime -> localTime.
                        _serverToLocalOffsetMs = Time.unscaledTime * 1000f - snap.ServerTimeMs;
                        _hasTimeOffset = true;
                    }
                }

                BufferUpdated?.Invoke();
            }
        }

        public bool TryGetSnapshotsForRender(float interpolationDelayMs, out SnapshotEnvelope from, out SnapshotEnvelope to, out float renderTimeMs)
        {
            from = null;
            to = null;
            renderTimeMs = 0f;

            lock (_bufferLock)
            {
                if (_buffer.Count == 0 || !_hasTimeOffset) return false;

                var localNowMs = Time.unscaledTime * 1000f;
                var serverNowMs = localNowMs - _serverToLocalOffsetMs;
                // Отрисовываем чуть в прошлом, чтобы стабильно интерполировать.
                renderTimeMs = serverNowMs - interpolationDelayMs;

                for (int i = 0; i < _buffer.Count; i++)
                {
                    var s = _buffer[i];
                    if (s.ServerTimeMs <= renderTimeMs) from = s;
                    if (s.ServerTimeMs >= renderTimeMs)
                    {
                        to = s;
                        break;
                    }
                }

                if (from == null) from = _buffer[0];
                if (to == null) to = _buffer[_buffer.Count - 1];
                return true;
            }
        }

        public bool TryGetLastTwoSnapshots(out SnapshotEnvelope prev, out SnapshotEnvelope last)
        {
            prev = null;
            last = null;

            lock (_bufferLock)
            {
                if (_buffer.Count < 2) return false;
                last = _buffer[_buffer.Count - 1];
                prev = _buffer[_buffer.Count - 2];
                return true;
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
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.LogWarning("NetworkSessionRunner: socket closed by server.");
                        return;
                    }
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
                    case "connect_ok":
                        _connectOk = true;
                        break;
                    case "instance_start":
                        _instanceStarted = true;
                        break;
                    case "error":
                        var err = JsonConvert.DeserializeObject<ErrorEnvelope>(json, NetProtocol.JsonSettings);
                        if (err != null)
                        {
                            Debug.LogWarning($"NetworkSessionRunner: server error {err.Code} {err.Message}");
                        }
                        break;
                    case "hello":
                        break;
                }
            }
            catch
            {
            }
        }

        private int NextSeq() => Interlocked.Increment(ref _seq);

        private async Task SendEnvelopeAsync(CommandEnvelope cmd)
        {
            var json = JsonConvert.SerializeObject(cmd, NetProtocol.JsonSettings);
            Debug.Log($"WS SEND: {json}");
            var bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NetworkSessionRunner: send failed {e.GetType().Name} {e.Message}");
            }
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
