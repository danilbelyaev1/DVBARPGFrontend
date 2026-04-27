using System;
using System.Collections;
using DVBARPG.Core;
using DVBARPG.Core.Services;
using DVBARPG.UI.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DVBARPG.Game.Hub
{
    /// <summary>
    /// Загружает список NPC с бэка и привязывает данные к уже существующим объектам NPC на сцене.
    /// Объект должен иметь имя, совпадающее с <c>NpcInfo.Code</c>.
    /// </summary>
    public sealed class HubNpcSpawnController : MonoBehaviour
    {
        private void Start()
        {
            StripLegacyHudShopButton();
            StartCoroutine(CoSpawn());
        }

        private static void StripLegacyHudShopButton()
        {
            var menu = GameObject.Find("HUD/RightTop/Background/ActionBar/menu");
            if (menu == null)
            {
                return;
            }

            foreach (var t in menu.GetComponents<NpcShopPanelToggle>())
            {
                Destroy(t);
            }
        }

        private IEnumerator CoSpawn()
        {
            var services = GameRoot.Instance?.Services;
            var profile = services?.Get<IProfileService>();
            var session = profile?.CurrentAuth;
            var mvp = services?.Get<IRuntimeMvpService>();
            var shopState = services?.Get<ShopState>();
            var sessionState = services?.Get<SessionState>();
            if (profile == null || session == null || mvp == null || shopState == null || sessionState == null)
            {
                yield break;
            }

            var mapId = ActHubResolver.ResolveHubMapCode(sessionState, services.Get<CampaignState>());
            RuntimeNpcListSnapshot snap = null;
            var done = false;
            mvp.FetchNpcs(session, mapId, r =>
            {
                snap = r;
                done = true;
            });
            while (!done)
            {
                yield return null;
            }

            if (snap == null || !snap.Ok || snap.Npcs == null || snap.Npcs.Length == 0)
            {
                yield break;
            }

            shopState.Npcs = snap.Npcs;
            foreach (var npc in snap.Npcs)
            {
                if (npc == null || string.IsNullOrWhiteSpace(npc.Code))
                {
                    continue;
                }

                BindExistingNpc(npc);
            }
        }

        private static void BindExistingNpc(NpcInfo npc)
        {
            if (!TryFindSceneNpcAnchor(npc.Code, out var anchor, out var duplicateCount))
            {
                Debug.LogWarning($"[HubNpcSpawnController] NPC anchor '{npc.Code}' not found in scene. Skipped.");
                return;
            }

            if (duplicateCount > 1)
            {
                Debug.LogWarning($"[HubNpcSpawnController] Multiple NPC anchors '{npc.Code}' found ({duplicateCount}), binding first.");
            }

            var actor = anchor.GetComponent<HubNpcActor>();
            if (actor == null)
            {
                actor = anchor.gameObject.AddComponent<HubNpcActor>();
            }

            actor.Bind(npc);

            if (anchor.GetComponentInChildren<Collider>() == null)
            {
                var cap = anchor.gameObject.AddComponent<CapsuleCollider>();
                cap.height = 2f;
                cap.radius = 0.35f;
                cap.center = new Vector3(0f, 1f, 0f);
            }
        }

        private static bool TryFindSceneNpcAnchor(string npcCode, out Transform anchor, out int count)
        {
            anchor = null;
            count = 0;
            if (string.IsNullOrWhiteSpace(npcCode))
            {
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            var code = npcCode.Trim();
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                foreach (var t in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, code, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                        anchor ??= t;
                    }
                }
            }

            if (anchor == null)
            {
                return false;
            }
            return true;
        }
    }
}
