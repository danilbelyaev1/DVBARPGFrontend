using System.Collections.Generic;
using DVBARPG.Net.Network;
using UnityEngine;
using DVBARPG.Game.World;
namespace DVBARPG.Game.Player
{
    public sealed class NetworkPlayerReplicator : MonoBehaviour
    {
        public static Transform PlayerTransform { get; private set; }
        public static int CurrentHp { get; private set; } = -1;
        public static int MaxHp { get; private set; } = -1;

        private NetworkSessionRunner _net;
        private float _lastLog;
        [Header("Сеть")]
        [Tooltip("Задержка интерполяции (мс).")]
        [SerializeField] private float interpolationDelayMs = 100f;
        [Tooltip("Макс. время экстраполяции (мс).")]
        [SerializeField] private float maxExtrapolationMs = 120f;
        [Header("Предсказание")]
        [Tooltip("Локальное предсказание движения игрока.")]
        [SerializeField] private bool enablePrediction = true;
        [Tooltip("Скорость движения для предсказания (должна совпадать с серверной).")]
        [SerializeField] private float predictedMoveSpeed = 4.5f;
        [Header("Поворот")]
        [Tooltip("Скорость сглаживания поворота.")]
        [SerializeField] private float rotationLerp = 12f;

        private struct PendingInput
        {
            public int Seq;
            public Vector2 Dir;
            public float Dt;
        }

        private readonly List<PendingInput> _pending = new();
        private bool _hasServerPos;
        private Vector3 _predictedPos;
        private Vector3 _targetForward = Vector3.forward;

        private void OnEnable()
        {
            PlayerTransform = transform;
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
                _net.MoveSent += OnMoveSent;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
                _net.MoveSent -= OnMoveSent;
            }

            if (PlayerTransform == transform) PlayerTransform = null;
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            CurrentHp = snap.Player.Hp;
            MaxHp = snap.Player.MaxHp;

            if (enablePrediction)
            {
                _hasServerPos = true;
                var serverPos = new Vector3(snap.Player.X, 0f, snap.Player.Y);
                _predictedPos = serverPos;

                // Drop acknowledged inputs
                var ack = snap.AckSeq;
                _pending.RemoveAll(p => p.Seq <= ack);

                // Re-apply pending inputs
                for (int i = 0; i < _pending.Count; i++)
                {
                    var p = _pending[i];
                    var dir = p.Dir;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                    _predictedPos += new Vector3(dir.x, 0f, dir.y) * predictedMoveSpeed * p.Dt;
                }

                _predictedPos.y = SampleHeight(_predictedPos);
                transform.position = _predictedPos;
            }

            if (Time.unscaledTime - _lastLog > 1f)
            {
                Debug.Log($"NetworkPlayerReplicator: snapshot hp {snap.Player.Hp}/{snap.Player.MaxHp}");
                _lastLog = Time.unscaledTime;
            }
        }

        private void OnMoveSent(int seq, Vector2 dir, float dt)
        {
            if (!enablePrediction) return;

            var p = new PendingInput { Seq = seq, Dir = dir, Dt = dt };
            _pending.Add(p);

            if (!_hasServerPos)
            {
                _predictedPos = transform.position;
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                var norm = dir;
                if (norm.sqrMagnitude > 1f) norm.Normalize();
                // Локально двигаем игрока сразу, не дожидаясь снапшота.
                _predictedPos += new Vector3(norm.x, 0f, norm.y) * predictedMoveSpeed * dt;
                _targetForward = new Vector3(norm.x, 0f, norm.y);
                _predictedPos.y = SampleHeight(_predictedPos);
                transform.position = _predictedPos;
            }
        }

        private void Update()
        {
            if (_net == null) return;

            if (!enablePrediction)
            {
                float renderTime = 0f;
                if (_net.TryGetSnapshotsForRender(interpolationDelayMs, out var from, out var to, out renderTime))
                {
                    var fromPos = new Vector3(from.Player.X, 0f, from.Player.Y);
                    var toPos = new Vector3(to.Player.X, 0f, to.Player.Y);

                    if (renderTime <= to.ServerTimeMs)
                    {
                        float t = 0f;
                        var dt = to.ServerTimeMs - from.ServerTimeMs;
                        if (dt > 0)
                        {
                            t = Mathf.Clamp01((float)((renderTime - from.ServerTimeMs) / dt));
                        }

                        // Интерполяция для удалённого игрока (без предсказания).
                        var pos = Vector3.Lerp(fromPos, toPos, t);
                        pos.y = SampleHeight(pos);
                        transform.position = pos;
                    }
                    else if (_net.TryGetLastTwoSnapshots(out var prevSnap, out var lastSnap))
                    {
                        var lastPos = new Vector3(lastSnap.Player.X, 0f, lastSnap.Player.Y);
                        var prevPos = new Vector3(prevSnap.Player.X, 0f, prevSnap.Player.Y);
                        var dtMs = lastSnap.ServerTimeMs - prevSnap.ServerTimeMs;
                        if (dtMs > 0)
                        {
                            var vel = (lastPos - prevPos) / (dtMs / 1000f);
                            var extraMs = Mathf.Min((float)(renderTime - lastSnap.ServerTimeMs), maxExtrapolationMs);
                            var pos = lastPos + vel * (extraMs / 1000f);
                            pos.y = SampleHeight(pos);
                            transform.position = pos;
                        }
                    }
                }
            }

            // Smooth facing
            if (_targetForward.sqrMagnitude > 0.0001f)
            {
                var desired = Quaternion.LookRotation(_targetForward, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, rotationLerp * Time.deltaTime);
            }
        }

        private float SampleHeight(Vector3 worldPos)
        {
            return UnifiedHeightSampler.SampleHeight(worldPos);
        }
    }
}
