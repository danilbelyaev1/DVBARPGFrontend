using DVBARPG.Net.Commands;
using DVBARPG.Net.Network;
using UnityEngine;
using UnityEngine.UI;

namespace DVBARPG.UI.Dev
{
    public sealed class DevCommandsPanel : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Корень панели (включается/выключается).")]
        [SerializeField] private GameObject contentRoot;
        [Tooltip("Кнопка-переключатель панели.")]
        [SerializeField] private Button toggleButton;
        [Tooltip("Кнопка: очистить мобов.")]
        [SerializeField] private Button clearMobsButton;
        [Tooltip("Кнопка: спавн мили моба.")]
        [SerializeField] private Button spawnMeleeButton;
        [Tooltip("Кнопка: спавн рейндж моба.")]
        [SerializeField] private Button spawnRangedButton;
        [Tooltip("Кнопка: бессмертие ВКЛ.")]
        [SerializeField] private Button immortalOnButton;
        [Tooltip("Кнопка: бессмертие ВЫКЛ.")]
        [SerializeField] private Button immortalOffButton;

        [Header("Поведение")]
        [Tooltip("Показывать панель при старте.")]
        [SerializeField] private bool showOnStart = true;
        [Tooltip("Горячая клавиша для показа/скрытия.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [Tooltip("Смещение точки спавна мобов от игрока вперёд.")]
        [SerializeField] private float spawnForwardOffset = 2.0f;

        private NetworkSessionRunner _net;

        private void Awake()
        {
            SetVisible(showOnStart);
        }

        private void OnEnable()
        {
            var session = DVBARPG.Core.GameRoot.Instance.Services.Get<DVBARPG.Core.Services.ISessionService>();
            _net = session as NetworkSessionRunner;

            if (toggleButton != null) toggleButton.onClick.AddListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.AddListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.AddListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.AddListener(OnSpawnRanged);
            if (immortalOnButton != null) immortalOnButton.onClick.AddListener(OnImmortalOn);
            if (immortalOffButton != null) immortalOffButton.onClick.AddListener(OnImmortalOff);
        }

        private void OnDisable()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleVisible);
            if (clearMobsButton != null) clearMobsButton.onClick.RemoveListener(OnClearMobs);
            if (spawnMeleeButton != null) spawnMeleeButton.onClick.RemoveListener(OnSpawnMelee);
            if (spawnRangedButton != null) spawnRangedButton.onClick.RemoveListener(OnSpawnRanged);
            if (immortalOnButton != null) immortalOnButton.onClick.RemoveListener(OnImmortalOn);
            if (immortalOffButton != null) immortalOffButton.onClick.RemoveListener(OnImmortalOff);
        }

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                ToggleVisible();
            }
        }

        private void OnClearMobs() => SendDebug("debug_clear_mobs");
        private void OnSpawnMelee() => SendDebug("debug_spawn_melee", usePlayerPos: true);
        private void OnSpawnRanged() => SendDebug("debug_spawn_ranged", usePlayerPos: true);
        private void OnImmortalOn() => SendDebug("debug_immortal_on");
        private void OnImmortalOff() => SendDebug("debug_immortal_off");

        private void SendDebug(string type, bool usePlayerPos = false)
        {
            if (_net == null) return;

            var cmd = new CmdDebug { Type = type };
            if (usePlayerPos)
            {
                var tr = DVBARPG.Game.Player.NetworkPlayerReplicator.PlayerTransform;
                if (tr != null)
                {
                    var pos = tr.position + tr.forward * spawnForwardOffset;
                    cmd.HasPosition = true;
                    cmd.Position = new Vector2(pos.x, pos.z);
                }
            }

            _net.Send(cmd);
        }

        private void ToggleVisible()
        {
            if (contentRoot == null) return;
            contentRoot.SetActive(!contentRoot.activeSelf);
        }

        private void SetVisible(bool visible)
        {
            if (contentRoot != null) contentRoot.SetActive(visible);
        }
    }
}
