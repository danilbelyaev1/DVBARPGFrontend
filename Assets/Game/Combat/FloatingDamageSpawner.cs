using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Net.Events;
using UnityEngine;

namespace DVBARPG.Game.Combat
{
    public sealed class FloatingDamageSpawner : MonoBehaviour
    {
        [SerializeField] private TextMesh textPrefab;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] private float floatSpeed = 1.5f;
        [SerializeField] private float lifetime = 0.8f;

        private ICombatService _combat;

        private void OnEnable()
        {
            _combat = GameRoot.Instance.Services.Get<ICombatService>();
            _combat.Damage += OnDamage;
        }

        private void OnDisable()
        {
            if (_combat != null) _combat.Damage -= OnDamage;
        }

        private void OnDamage(EvtDamage evt)
        {
            if (textPrefab == null)
            {
                Debug.LogWarning("FloatingDamageSpawner: Text Prefab is not assigned.");
                return;
            }
            if (!CombatEntity.TryGetTransform(evt.TargetId, out var target))
            {
                Debug.LogWarning($"FloatingDamageSpawner: Target not found for id '{evt.TargetId}'.");
                return;
            }

            var spawnPos = target.position + worldOffset;
            var tm = Instantiate(textPrefab, spawnPos, Quaternion.identity);
            tm.text = evt.IsCrit ? $"{evt.Amount}!" : evt.Amount.ToString();
            tm.color = evt.IsCrit ? new Color(1f, 0.8f, 0.2f) : Color.white;

            var mover = tm.GetComponent<FloatingDamageText>();
            if (mover == null) mover = tm.gameObject.AddComponent<FloatingDamageText>();
            mover.Init(floatSpeed, lifetime);
        }
    }
}
