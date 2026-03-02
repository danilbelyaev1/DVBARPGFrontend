using DVBARPG.Game.Animation;
using DVBARPG.Net.Network;
using UnityEngine;

namespace DVBARPG.Game.Skills.Presentation
{
    public sealed class SkillPresentationDriver : MonoBehaviour
    {
        [Header("Каталог")]
        [Tooltip("Каталог презентаций по SkillId.")]
        [SerializeField] private SkillPresentationCatalog catalog;

        [Header("Анимации")]
        [Tooltip("Драйвер анимаций способностей.")]
        [SerializeField] private PlayerAbilityAnimationDriver animationDriver;
        [Tooltip("Если в презентации задан AnimationTrigger — использовать его.")]
        [SerializeField] private bool preferPresentationTrigger = true;

        [Header("Сокеты")]
        [Tooltip("Локатор сокетов для VFX.")]
        [SerializeField] private SkillSocketLocator sockets;
        [Tooltip("Корень, куда спавнить VFX, если не привязаны к сокету.")]
        [SerializeField] private Transform vfxRoot;

        [Header("Экипированные скиллы")]
        [Tooltip("SkillId атакующего слота (серверный).")]
        [SerializeField] private string attackSkillId = "";
        [Tooltip("SkillId поддержки A (серверный).")]
        [SerializeField] private string supportASkillId = "";
        [Tooltip("SkillId поддержки B (серверный).")]
        [SerializeField] private string supportBSkillId = "";
        [Tooltip("SkillId мувмент-скилла (если сервер не отправил).")]
        [SerializeField] private string movementFallbackSkillId = "";

        [Header("Логи")]
        [Tooltip("Логировать, если SkillId не найден в каталоге.")]
        [SerializeField] private bool logMissingSkills = false;

        private NetworkSessionRunner _net;
        private bool _lastMovementActive;

        public void SetEquippedSkills(string attackId, string supportAId, string supportBId, string movementId)
        {
            attackSkillId = attackId ?? "";
            supportASkillId = supportAId ?? "";
            supportBSkillId = supportBId ?? "";
            movementFallbackSkillId = movementId ?? "";
        }

        public void SetCatalog(SkillPresentationCatalog newCatalog)
        {
            catalog = newCatalog;
        }

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;
            if (_net != null)
            {
                _net.Snapshot += OnSnapshot;
            }
        }

        private void OnDisable()
        {
            if (_net != null)
            {
                _net.Snapshot -= OnSnapshot;
            }
        }

        private void OnSnapshot(SnapshotEnvelope snap)
        {
            if (snap == null) return;

            if (snap.Player.AttackAnimTriggered)
            {
                PlaySkillEvent(attackSkillId, SkillVfxEventType.CastStart);
            }

            if (snap.Player.MovementActive && !_lastMovementActive)
            {
                var movementId = ResolveMovementSkillId(snap.Player.MovementSkillId);
                PlaySkillEvent(movementId, SkillVfxEventType.MovementStart);
            }
            else if (!snap.Player.MovementActive && _lastMovementActive)
            {
                var movementId = ResolveMovementSkillId(snap.Player.MovementSkillId);
                PlaySkillEvent(movementId, SkillVfxEventType.MovementStop);
            }

            _lastMovementActive = snap.Player.MovementActive;
        }

        public void PlaySkillEvent(string skillId, SkillVfxEventType eventType)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return;

            if (catalog != null && catalog.TryGet(skillId, out var presentation) && presentation != null)
            {
                PlayAnimation(presentation, skillId);
                SpawnVfx(presentation, eventType);
                return;
            }

            if (animationDriver != null)
            {
                animationDriver.PlaySkill(skillId);
            }

            if (logMissingSkills)
            {
                Debug.LogWarning($"SkillPresentationDriver: no presentation for skill '{skillId}'.");
            }
        }

        private void PlayAnimation(SkillPresentation presentation, string skillId)
        {
            if (animationDriver == null || presentation == null) return;

            if (preferPresentationTrigger && !string.IsNullOrWhiteSpace(presentation.AnimationTrigger))
            {
                animationDriver.PlayTrigger(presentation.AnimationTrigger);
                return;
            }

            animationDriver.PlaySkill(skillId);
        }

        private void SpawnVfx(SkillPresentation presentation, SkillVfxEventType eventType)
        {
            var list = presentation?.Vfx;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var binding = list[i];
                if (binding == null) continue;
                if (binding.EventType != eventType) continue;
                SpawnVfxBinding(binding);
            }
        }

        private void SpawnVfxBinding(SkillVfxBinding binding)
        {
            if (binding.Prefab == null) return;

            var socket = sockets != null ? sockets.GetSocket(binding.SocketName) : transform;
            var parent = binding.AttachToSocket ? socket : (vfxRoot != null ? vfxRoot : transform);

            Transform instance;
            if (binding.AttachToSocket)
            {
                instance = Instantiate(binding.Prefab, parent, false);
                instance.localPosition = binding.LocalOffset;
                instance.localEulerAngles = binding.LocalEuler;
            }
            else
            {
                var pos = socket != null ? socket.TransformPoint(binding.LocalOffset) : transform.position;
                var rot = binding.UseSocketRotation && socket != null
                    ? socket.rotation * Quaternion.Euler(binding.LocalEuler)
                    : Quaternion.Euler(binding.LocalEuler);
                instance = Instantiate(binding.Prefab, pos, rot, parent);
            }

            instance.localScale = binding.LocalScale;

            if (binding.LifetimeSec > 0.001f)
            {
                Destroy(instance.gameObject, binding.LifetimeSec);
            }
        }

        private string ResolveMovementSkillId(string snapshotMovementId)
        {
            if (!string.IsNullOrWhiteSpace(snapshotMovementId)) return snapshotMovementId;
            return movementFallbackSkillId;
        }
    }
}
