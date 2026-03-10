using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using DVBARPG.Core.Services;
using DVBARPG.Net.Commands;
using DVBARPG.Tools;
using UnityEngine;

namespace DVBARPG.Net.Network
{
    public sealed class NetworkSessionRunner : MonoBehaviour, ISessionService
    {
        public bool IsConnected { get; private set; }
        /// <summary>Есть ли активный instance_start (игровой ран запущен).</summary>
        public bool HasInstance => _instanceStarted;
        public event Action<SnapshotEnvelope> Snapshot;
        public event Action<int, Vector2, float> MoveSent;
        /// <summary>Вызывается один раз при завершении забега: true = смерть (HP=0), false = досрочный выход (finish).</summary>
        public event Action<bool> RunEnded;

        [Header("Отладка UDP")]
        [Tooltip("Включить логирование отправки/приёма UDP пакетов.")]
        [SerializeField] private bool logUdpTraffic = true;

        private UdpClient _udp;
        private IPEndPoint _remoteEndPoint;
        private CancellationTokenSource _cts;
        private Task _recvTask;
        private Task _resendTask;
        private Task _keepaliveTask;
        private readonly ConcurrentQueue<SnapshotEnvelope> _snapshots = new();
        private int _seq;
        private int _packetSeq;
        private int _expectedServerPacketSeq;
        private int _lastAckFromServer;
        private readonly ConcurrentDictionary<int, PendingPacket> _pending = new();
        private string _serverUrl = "udp://127.0.0.1:8081";
        
        private bool _connectOk;
        private bool _instanceStarted;
        private readonly List<SnapshotEnvelope> _buffer = new();
        private float _serverToLocalOffsetMs;
        private bool _hasTimeOffset;
        private readonly object _bufferLock = new();
        private Guid? _sessionId;
        private string _pendingMapId = "default";
        private float _avgPingMs;
        private bool _runEndedFired;

        private readonly TimeSpan _resendInterval = TimeSpan.FromMilliseconds(200);
        private const int MaxRetries = 10;

        public void Disconnect()
        {
            try
            {
                _cts?.Cancel();
                _udp?.Dispose();
            }
            catch { }
            _udp = null;
            _cts = null;
            _sessionId = null;
            _connectOk = false;
            _instanceStarted = false;
            IsConnected = false;
        }

        public void Connect(AuthSession session, string mapId, string serverUrl)
        {
            if (session == null) return;
            Disconnect();

            _serverUrl = string.IsNullOrWhiteSpace(serverUrl) ? _serverUrl : serverUrl;
            _pendingMapId = string.IsNullOrWhiteSpace(mapId) ? "default" : mapId;

            _cts = new CancellationTokenSource();
            _udp = new UdpClient(0);
            _remoteEndPoint = ParseEndpoint(_serverUrl);

            DebugLog($"NetworkSessionRunner: UDP connect to {_remoteEndPoint}");

            _recvTask = Task.Run(async () => await ReceiveLoopAsync(_cts.Token));
            _resendTask = Task.Run(async () => await ResendLoopAsync(_cts.Token));
            _keepaliveTask = Task.Run(async () => await KeepaliveLoopAsync(_cts.Token));

            var connectEnv = new CommandEnvelope
            {
                Type = "connect",
                Seq = NextSeq(),
                Token = session.Token,
                CharacterId = session.CharacterId,
                SeasonId = session.SeasonId
            };
            DebugLog($"NetworkSessionRunner: sending connect CharacterId={connectEnv.CharacterId} SeasonId={connectEnv.SeasonId} TokenLength={session.Token?.Length ?? 0}");
            SendReliable(connectEnv);
        }

        public void Send(IClientCommand command)
        {
            if (!IsConnected || _udp == null) return;
            if (!_connectOk || !_instanceStarted) return;

            if (command is CmdDebug debug)
            {
                var seq = NextSeq();
                var env = new CommandEnvelope
                {
                    Type = debug.Type,
                    Seq = seq,
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f),
                    StatPatch = debug.StatPatch,
                    Skills = debug.Skills,
                    CombatLoadout = debug.CombatLoadout,
                    ReplaceSkills = debug.ReplaceSkills
                };
                if (debug.HasPosition)
                {
                    env.X = debug.Position.x;
                    env.Y = debug.Position.y;
                }

                DebugLog($"NetworkSessionRunner: sending debug command type={env.Type} " +
                         $"statPatchCount={(env.StatPatch != null ? env.StatPatch.Count : 0)} " +
                         $"skillsCount={(env.Skills != null ? env.Skills.Count : 0)} " +
                         $"hasLoadout={env.CombatLoadout != null} replaceSkills={env.ReplaceSkills}");

                SendReliable(env);
                return;
            }

            if (command is CmdMove move)
            {
                var seq = NextSeq();
                MoveSent?.Invoke(seq, new Vector2(move.Direction.x, move.Direction.z), move.DeltaTime);
                SendUnreliable(new CommandEnvelope
                {
                    Type = "move",
                    Seq = seq,
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f),
                    X = move.Direction.x,
                    Y = move.Direction.z
                });
                return;
            }

            if (command is CmdSlotToggle toggle)
            {
                var seq = NextSeq();
                SendReliable(new CommandEnvelope
                {
                    Type = "slot_toggle",
                    Seq = seq,
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f),
                    Slot = toggle.Slot,
                    Enabled = toggle.Enabled
                });
                return;
            }

            if (command is CmdStop)
            {
                var seq = NextSeq();
                SendUnreliable(new CommandEnvelope
                {
                    Type = "stop",
                    Seq = seq,
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f)
                });
                return;
            }

            if (command is CmdFinish)
            {
                SendReliable(new CommandEnvelope
                {
                    Type = "finish",
                    Seq = NextSeq(),
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f)
                });
                if (!_runEndedFired)
                {
                    _runEndedFired = true;
                    RunEnded?.Invoke(false);
                }
                return;
            }

            if (command is CmdPickup pickup)
            {
                SendReliable(new CommandEnvelope
                {
                    Type = "pickup",
                    Seq = NextSeq(),
                    ClientTimeMs = (long)(Time.unscaledTime * 1000f),
                    DropIndex = pickup.DropIndex
                });
            }
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("NetworkSessionRunner.Update"))
            {
                while (_snapshots.TryDequeue(out var snap))
            {
                Snapshot?.Invoke(snap);

                if (!_runEndedFired && snap?.Player != null && snap.Player.MaxHp > 0 && snap.Player.Hp <= 0)
                {
                    _runEndedFired = true;
                    RunEnded?.Invoke(true);
                }

                lock (_bufferLock)
                {
                    _buffer.Add(snap);
                    if (_buffer.Count > 30) _buffer.RemoveAt(0);
                    if (!_hasTimeOffset)
                    {
                        _serverToLocalOffsetMs = Time.unscaledTime * 1000f - snap.ServerTimeMs;
                        _hasTimeOffset = true;
                    }
                }

                }
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
                var halfRttMs = _avgPingMs > 0f ? _avgPingMs * 0.5f : 0f;
                renderTimeMs = serverNowMs - interpolationDelayMs + halfRttMs;

                // Prevent getting stuck too far behind if offset was learned from a delayed packet.
                var last = _buffer[_buffer.Count - 1];
                var maxBehindMs = Mathf.Max(100f, interpolationDelayMs * 2f);
                if (last.ServerTimeMs - renderTimeMs > maxBehindMs)
                {
                    renderTimeMs = last.ServerTimeMs - maxBehindMs;
                }

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
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udp.ReceiveAsync();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    DebugLogWarning($"NetworkSessionRunner: recv failed {e.GetType().Name} {e.Message}");
                    continue;
                }

                var json = Encoding.UTF8.GetString(result.Buffer);
                if (logUdpTraffic)
                {
                    if (!IsIdleSnapshot(json) && !IsNetStats(json) && !IsEmptyMonstersSnapshot(json))
                    {
                        DebugLog($"UDP RECV: {json}");
                    }
                }
                TryHandleMessage(json);
            }
        }

        private static bool IsIdleSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            if (!json.Contains("\"type\":\"snapshot\"")) return false;
            return json.Contains("\"state\":\"idle\"");
        }

        private static bool IsNetStats(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            return json.Contains("\"type\":\"net_stats\"");
        }

        private static bool IsEmptyMonstersSnapshot(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            if (!json.Contains("\"type\":\"snapshot\"")) return false;
            return json.Contains("\"monsters\":[]");
        }

        private void TryHandleMessage(string json)
        {
            try
            {
                var baseEnv = JsonConvert.DeserializeObject<UdpEnvelopeBase>(json, NetProtocol.JsonSettings);
                if (baseEnv == null) return;

                if (baseEnv.Ack > _lastAckFromServer)
                {
                    _lastAckFromServer = baseEnv.Ack;
                    CleanupPending();
                }

                if (baseEnv.Type == "ack")
                {
                    return;
                }

                if (baseEnv.Reliable)
                {
                    if (!AcceptReliable(baseEnv.PacketSeq))
                    {
                        return;
                    }
                }

                switch (baseEnv.Type)
                {
                    case "snapshot":
                        var snap = JsonConvert.DeserializeObject<SnapshotEnvelope>(json, NetProtocol.JsonSettings);
                        if (snap != null) _snapshots.Enqueue(snap);
                        break;
                    case "connect_ok":
                        _connectOk = true;
                        TrySendStart();
                        break;
                    case "instance_start":
                        _instanceStarted = true;
                        _runEndedFired = false;
                        break;
                    case "error":
                        var err = JsonConvert.DeserializeObject<ErrorEnvelope>(json, NetProtocol.JsonSettings);
                        if (err != null)
                        {
                            DebugLogWarning($"NetworkSessionRunner: server error {err.Code} {err.Message}");
                            if (string.Equals(err.Code, "auth_failed", StringComparison.OrdinalIgnoreCase))
                            {
                                DebugLogWarning("NetworkSessionRunner: auth_failed — runtime не смог провалидировать сессию через Laravel. Проверьте: 1) Laravel запущен; 2) у runtime-server задан BACKEND_BASE_URL и он доступен (из Docker — использовать host.docker.internal); 3) BACKEND_API_KEY совпадает с Laravel; 4) в логе «sending connect» TokenLength > 0, characterId/seasonId — от выбранного персонажа. Подробно: PROJECT_OVERVIEW.md, раздел «Устранение auth_failed».");
                            }
                        }
                        DebugLogWarning($"NetworkSessionRunner: full server error response: {json}");
                        break;
                    case "hello":
                        var hello = JsonConvert.DeserializeObject<HelloEnvelope>(json, NetProtocol.JsonSettings);
                        if (hello != null)
                        {
                            _sessionId = hello.SessionId;
                            IsConnected = true;
                        }
                        break;
                    case "net_stats":
                        var stats = JsonConvert.DeserializeObject<NetworkStatsEnvelope>(json, NetProtocol.JsonSettings);
                        if (stats != null)
                        {
                            _avgPingMs = stats.AvgPingMs;
                        }
                        break;
                }

            }
            catch
            {
            }
        }

        private bool AcceptReliable(int packetSeq)
        {
            var expected = _expectedServerPacketSeq + 1;
            if (packetSeq < expected)
            {
                return false;
            }

            if (packetSeq > expected)
            {
                return false;
            }

            _expectedServerPacketSeq = packetSeq;
            return true;
        }

        private void TrySendStart()
        {
            if (!_connectOk || _sessionId == null) return;

            SendReliable(new CommandEnvelope
            {
                Type = "start",
                Seq = NextSeq(),
                MapId = _pendingMapId
            });
        }

        private int NextSeq() => Interlocked.Increment(ref _seq);

        private void SendReliable(CommandEnvelope cmd)
        {
            cmd.SessionId = _sessionId;
            cmd.Reliable = true;
            cmd.PacketSeq = Interlocked.Increment(ref _packetSeq);
            cmd.Ack = _expectedServerPacketSeq;

            var json = JsonConvert.SerializeObject(cmd, NetProtocol.JsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            _pending[cmd.PacketSeq] = new PendingPacket
            {
                Payload = bytes,
                LastSentUtc = DateTime.UtcNow,
                Retries = 0
            };

            if (logUdpTraffic && cmd.Type != "move")
            {
                DebugLog($"UDP SEND: {json}");
            }
            _ = _udp.SendAsync(bytes, bytes.Length, _remoteEndPoint);
        }

        private void SendUnreliable(CommandEnvelope cmd)
        {
            cmd.SessionId = _sessionId;
            cmd.Reliable = false;
            cmd.PacketSeq = 0;
            cmd.Ack = _expectedServerPacketSeq;

            var json = JsonConvert.SerializeObject(cmd, NetProtocol.JsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            if (logUdpTraffic && cmd.Type != "move")
            {
                DebugLog($"UDP SEND: {json}");
            }
            _ = _udp.SendAsync(bytes, bytes.Length, _remoteEndPoint);
        }

        private async Task ResendLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                foreach (var kvp in _pending)
                {
                    var seq = kvp.Key;
                    var pending = kvp.Value;
                    if (now - pending.LastSentUtc < _resendInterval)
                    {
                        continue;
                    }

                    if (pending.Retries >= MaxRetries)
                    {
                        _pending.TryRemove(seq, out _);
                        continue;
                    }

                    pending.Retries++;
                    pending.LastSentUtc = now;
                    await _udp.SendAsync(pending.Payload, pending.Payload.Length, _remoteEndPoint);
                }

                try
                {
                    await Task.Delay(50, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task KeepaliveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_udp != null && _sessionId != null)
                {
                    var ack = new AckEnvelope
                    {
                        SessionId = _sessionId,
                        Ack = _expectedServerPacketSeq,
                        Reliable = false
                    };

                    var json = JsonConvert.SerializeObject(ack, NetProtocol.JsonSettings);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _udp.SendAsync(bytes, bytes.Length, _remoteEndPoint);
                }

                try
                {
                    await Task.Delay(500, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private void CleanupPending()
        {
            var ack = _lastAckFromServer;
            foreach (var seq in _pending.Keys)
            {
                if (seq <= ack)
                {
                    _pending.TryRemove(seq, out _);
                }
            }
        }

        private static IPEndPoint ParseEndpoint(string serverUrl)
        {
            if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 8081;
                return new IPEndPoint(Dns.GetHostAddresses(host)[0], port);
            }

            var parts = serverUrl.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var p))
            {
                return new IPEndPoint(Dns.GetHostAddresses(parts[0])[0], p);
            }

            return new IPEndPoint(Dns.GetHostAddresses(serverUrl)[0], 8081);
        }

        private void OnDestroy()
        {
            try
            {
                _cts?.Cancel();
                _udp?.Dispose();
            }
            catch
            {
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DebugLog(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void DebugLogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }
    }

    internal sealed class PendingPacket
    {
        public byte[] Payload;
        public DateTime LastSentUtc;
        public int Retries;
    }
}
