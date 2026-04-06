using System;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.Game.Network;
using DVBARPG.Game.Player;
using UnityEngine;

namespace DVBARPG.Game.Run
{
    /// <summary>
    /// В сцене Run: под прямым потомком <see cref="mapsRoot"/> держите объекты с именами = кодам карт
    /// (<c>act1_forest</c> и т.д.). При загрузке включается только тот, чей <c>name</c> совпадает с <see cref="SessionState.MapId"/>.
    /// </summary>
    public sealed class RunMapActivator : MonoBehaviour
    {
        [Tooltip("Родитель корней карт. Если пусто — используется этот GameObject.")]
        [SerializeField] private Transform mapsRoot;

        [Tooltip("Если MapId пустой или нет дочернего объекта с таким именем — включить этот корень (имя ребёнка). Пусто = не включать запасной.")]
        [SerializeField] private string fallbackChildName = "";

        [SerializeField] private bool logWarnings = true;

        [Tooltip("Имена прямых детей, которые никогда не отключаются (игрок, UI мира, камера и т.п.).")]
        [SerializeField] private string[] alwaysActiveChildNames = { "_Systems", "Systems" };

        [Tooltip("Не трогать дочерние объекты, внутри которых есть PlayerInputController / NetworkPlayerReplicator / NetworkMonstersReplicator.")]
        [SerializeField] private bool skipChildrenWithGameplayComponents = true;

        private void Awake()
        {
            var root = mapsRoot != null ? mapsRoot : transform;
            var state = GameRoot.Instance?.Services?.Get<SessionState>();
            var mapId = state?.MapId?.Trim();

            if (string.IsNullOrEmpty(mapId))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        "[RunMapActivator] SessionState.MapId пустой — карта по id не выбрана.",
                        this);
                }

                ApplyExclusiveChild(root, fallbackChildName, logWarnings);
                return;
            }

            if (!ApplyExclusiveChild(root, mapId, false))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[RunMapActivator] Нет дочернего объекта с именем «{mapId}» под «{root.name}». Доступные: {ListChildNames(root)}",
                        this);
                }

                ApplyExclusiveChild(root, fallbackChildName, false);
            }
        }

        private bool ApplyExclusiveChild(Transform root, string activeName, bool warnOnEmptyName)
        {
            var want = activeName?.Trim();
            var anyActivated = false;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (IsAlwaysActiveChild(child))
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                var match = !string.IsNullOrEmpty(want)
                            && string.Equals(child.name, want, StringComparison.OrdinalIgnoreCase);
                child.gameObject.SetActive(match);
                if (match) anyActivated = true;
            }

            if (!anyActivated && !string.IsNullOrEmpty(want) && warnOnEmptyName)
            {
                Debug.LogWarning($"[RunMapActivator] Имя «{want}» не задано или не найдено среди детей.", root);
            }

            return anyActivated;
        }

        private bool IsAlwaysActiveChild(Transform child)
        {
            if (alwaysActiveChildNames != null)
            {
                for (var i = 0; i < alwaysActiveChildNames.Length; i++)
                {
                    var n = alwaysActiveChildNames[i];
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    if (string.Equals(child.name, n.Trim(), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (!skipChildrenWithGameplayComponents) return false;
            return child.GetComponentInChildren<PlayerInputController>(true) != null
                   || child.GetComponentInChildren<NetworkPlayerReplicator>(true) != null
                   || child.GetComponentInChildren<NetworkMonstersReplicator>(true) != null;
        }

        private static string ListChildNames(Transform root)
        {
            if (root.childCount == 0) return "(нет детей)";
            var parts = new string[root.childCount];
            for (var i = 0; i < root.childCount; i++)
            {
                parts[i] = root.GetChild(i).name;
            }

            return string.Join(", ", parts);
        }
    }
}
