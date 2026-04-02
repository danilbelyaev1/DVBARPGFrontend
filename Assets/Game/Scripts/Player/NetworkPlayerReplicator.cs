using System.Collections.Generic;
using System.Collections;
using System.Linq;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Animation;
using DVBARPG.Game.CharacterCreation;
using DVBARPG.Net.Network;
using UnityEngine;
using DVBARPG.Game.World;
using DVBARPG.Tools;
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
        [Tooltip("Максимальное число неподтверждённых инпутов для предсказания (ограничение дрейфа).")]
        [SerializeField] private int maxPendingInputs = 30;
        [Tooltip("Сглаживание позиции при применении предсказанной/серверной позы (0 = сразу, больше = мягче).")]
        [SerializeField] private float positionSmoothing = 12f;
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
        private Coroutine _appearanceRoutine;
        private SidekickAppearanceBuilder _appearanceBuilder;
        private readonly List<GameObject> _runtimeVisualRoots = new();

        private void OnEnable()
        {
            PlayerTransform = transform;
            var root = DVBARPG.Core.GameRoot.Instance;
            if (root == null || root.Services == null) return;
            if (!root.Services.TryGet<DVBARPG.Core.Services.ISessionService>(out var session)) return;
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
                _net.MoveSent += OnMoveSent;
            }

            _appearanceRoutine = StartCoroutine(LoadAndApplySelectedAppearanceRoutine());
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
                _net.MoveSent -= OnMoveSent;
            }

            if (_appearanceRoutine != null)
            {
                StopCoroutine(_appearanceRoutine);
                _appearanceRoutine = null;
            }

            ClearRuntimeVisualRoots();

            if (PlayerTransform == transform) PlayerTransform = null;
        }

        private IEnumerator LoadAndApplySelectedAppearanceRoutine()
        {
            var root = GameRoot.Instance;
            if (root == null) yield break;
            var profile = root.Services.Get<IProfileService>();
            if (profile == null) yield break;

            const float waitTimeout = 8f;
            float elapsed = 0f;
            while (elapsed < waitTimeout &&
                   (string.IsNullOrWhiteSpace(profile.SelectedCharacterId) ||
                    profile.Characters == null || profile.Characters.Length == 0))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (string.IsNullOrWhiteSpace(profile.SelectedCharacterId) || profile.Characters == null) yield break;
            var summary = profile.Characters.FirstOrDefault(c =>
                string.Equals(c?.Id, profile.SelectedCharacterId, System.StringComparison.OrdinalIgnoreCase));
            if (summary == null) yield break;

            var parseOk = RuntimeAppearanceParser.TryParse(summary.Appearance, out var appearance, out _);
            if (!parseOk || appearance == null)
            {
                yield break;
            }

            if (_appearanceBuilder == null) _appearanceBuilder = GetComponent<SidekickAppearanceBuilder>();
            if (_appearanceBuilder == null) _appearanceBuilder = gameObject.AddComponent<SidekickAppearanceBuilder>();

            bool done = false;
            GameObject built = null;
            _appearanceBuilder.BuildAppearance(appearance, go =>
            {
                built = go;
                done = true;
            });

            while (!done) yield return null;
            if (built == null) yield break;

            AttachVisualSafely(built);
        }

        private void AttachVisualSafely(GameObject built)
        {
            if (built == null) return;
            var host = ResolveVisualHost();
            var previousAnimators = host.GetComponentsInChildren<Animator>(true);
            ClearRuntimeVisualRoots();

            // Оставляем корень собранной модели: на нём находится Animator для нового рига.
            built.name = "RuntimeVisual";
            built.transform.SetParent(host, false);
            built.transform.localPosition = Vector3.zero;
            built.transform.localRotation = Quaternion.identity;
            built.transform.localScale = Vector3.one;
            _runtimeVisualRoots.Add(built);

            Animator newAnimator = null;
            for (int i = 0; i < _runtimeVisualRoots.Count && newAnimator == null; i++)
                newAnimator = _runtimeVisualRoots[i] != null ? _runtimeVisualRoots[i].GetComponentInChildren<Animator>(true) : null;
            if (newAnimator != null)
            {
                // Берём контроллер от старого боевого Animator, чтобы параметры Movement/Attack совпадали с геймплеем.
                Animator sourceAnimator = null;
                for (int i = 0; i < previousAnimators.Length; i++)
                {
                    var a = previousAnimators[i];
                    if (a == null || a == newAnimator) continue;
                    if (a.runtimeAnimatorController != null)
                    {
                        sourceAnimator = a;
                        break;
                    }
                }
                if (sourceAnimator != null && sourceAnimator.runtimeAnimatorController != null)
                {
                    newAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                    newAnimator.updateMode = sourceAnimator.updateMode;
                    newAnimator.cullingMode = sourceAnimator.cullingMode;
                    // Если у новой модели нет валидного Avatar, подхватим из текущего рабочего Animator.
                    if (newAnimator.avatar == null && sourceAnimator.avatar != null)
                        newAnimator.avatar = sourceAnimator.avatar;
                    newAnimator.applyRootMotion = false;
                    newAnimator.Rebind();
                    newAnimator.Update(0f);
                }
                newAnimator.enabled = true;

                var movementAnimator = GetComponent<MovementAnimator>();
                if (movementAnimator == null) movementAnimator = host.GetComponent<MovementAnimator>();
                if (movementAnimator != null) movementAnimator.SetAnimatorOverride(newAnimator);

                var abilityAnimator = GetComponent<PlayerAbilityAnimationDriver>();
                if (abilityAnimator == null) abilityAnimator = host.GetComponent<PlayerAbilityAnimationDriver>();
                if (abilityAnimator != null) abilityAnimator.SetAnimatorOverride(newAnimator);

                // На корне Player часто остаётся старый Animator с "чужим" Avatar.
                // Оставляем его выключенным и синхронизируем ссылки для предсказуемости в инспекторе/рантайме.
                var playerRootAnimator = host.GetComponent<Animator>();
                if (playerRootAnimator != null && playerRootAnimator != newAnimator)
                {
                    playerRootAnimator.runtimeAnimatorController = newAnimator.runtimeAnimatorController;
                    playerRootAnimator.avatar = newAnimator.avatar;
                    playerRootAnimator.enabled = false;
                }

                // Отключаем прочие Animator, чтобы не анимировался старый риг.
                var allAnimators = host.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < allAnimators.Length; i++)
                {
                    if (allAnimators[i] != null && allAnimators[i] != newAnimator)
                        allAnimators[i].enabled = false;
                }
            }

            var newSmrSet = new HashSet<SkinnedMeshRenderer>();
            for (int i = 0; i < _runtimeVisualRoots.Count; i++)
            {
                if (_runtimeVisualRoots[i] == null) continue;
                var smrs = _runtimeVisualRoots[i].GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int s = 0; s < smrs.Length; s++) newSmrSet.Add(smrs[s]);
            }
            foreach (var smr in host.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!newSmrSet.Contains(smr))
                    smr.enabled = false;
            }
        }

        private Transform ResolveVisualHost()
        {
            // Если репликатор висит на дочернем Network-узле, визуал нужно цеплять к Player (родителю).
            if (transform.parent != null &&
                string.Equals(transform.name, "Network", System.StringComparison.OrdinalIgnoreCase) &&
                transform.parent.GetComponent<PlayerInputController>() != null)
            {
                return transform.parent;
            }
            return transform;
        }

        private void ClearRuntimeVisualRoots()
        {
            for (int i = 0; i < _runtimeVisualRoots.Count; i++)
            {
                var go = _runtimeVisualRoots[i];
                if (go != null) Destroy(go);
            }
            _runtimeVisualRoots.Clear();
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
                transform.position = ApplySmoothing(transform.position, _predictedPos);
            }
        }

        private void OnMoveSent(int seq, Vector2 dir, float dt)
        {
            if (!enablePrediction) return;

            var p = new PendingInput { Seq = seq, Dir = dir, Dt = dt };
            _pending.Add(p);
            if (maxPendingInputs > 0 && _pending.Count > maxPendingInputs)
            {
                var overflow = _pending.Count - maxPendingInputs;
                _pending.RemoveRange(0, overflow);
            }

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
                transform.position = ApplySmoothing(transform.position, _predictedPos);
            }
        }

        private void Update()
        {
            using (RuntimeProfiler.Sample("NetworkPlayerReplicator.Update"))
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
        }

        private float SampleHeight(Vector3 worldPos)
        {
            return UnifiedHeightSampler.SampleHeight(worldPos);
        }

        private Vector3 ApplySmoothing(Vector3 current, Vector3 target)
        {
            if (positionSmoothing <= 0f) return target;
            var alpha = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            return Vector3.Lerp(current, target, alpha);
        }
    }
}
