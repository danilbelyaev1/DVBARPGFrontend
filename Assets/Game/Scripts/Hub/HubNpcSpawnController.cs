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
    /// Загружает список NPC с бэка и спавнит визуалы в хабе. Позиция: объект на сцене с именем = <c>code</c>, иначе <see cref="fallbackSpawns"/>.
    /// </summary>
    public sealed class HubNpcSpawnController : MonoBehaviour
    {
        [Tooltip("Родитель для инстансов (пустой объект в сцене).")]
        [SerializeField] private Transform spawnRoot;

        [Tooltip("Визуал NPC (например HumanMale_Character_FREE). Должен иметь коллайдер или будет добавлен капсула.")]
        [SerializeField] private GameObject npcVisualPrefab;

        [SerializeField] private HubNpcFallbackSpawn[] fallbackSpawns =
        {
            new HubNpcFallbackSpawn { npcCode = "npc_hub_merchant", localPosition = new Vector3(4f, 0f, 6f), yRotationDegrees = 180f },
        };

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

            var root = spawnRoot != null ? spawnRoot : transform;
            foreach (var npc in snap.Npcs)
            {
                if (npc == null || string.IsNullOrWhiteSpace(npc.Code))
                {
                    continue;
                }

                SpawnOne(npc, root);
            }
        }

        private void SpawnOne(NpcInfo npc, Transform root)
        {
            if (npcVisualPrefab == null)
            {
                Debug.LogWarning("[HubNpcSpawnController] npcVisualPrefab не назначен.");
                return;
            }

            ResolveTransform(npc, root, out var pos, out var rot);
            var go = Instantiate(npcVisualPrefab, pos, rot, root);
            go.name = $"RuntimeNpc_{npc.Code}";

            var actor = go.GetComponent<HubNpcActor>();
            if (actor == null)
            {
                actor = go.AddComponent<HubNpcActor>();
            }

            actor.Bind(npc);

            if (go.GetComponentInChildren<Collider>() == null)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.height = 2f;
                cap.radius = 0.35f;
                cap.center = new Vector3(0f, 1f, 0f);
            }
        }

        private void ResolveTransform(NpcInfo npc, Transform root, out Vector3 position, out Quaternion rotation)
        {
            if (TryFindSceneNpcAnchor(npc.Code, out position, out var yaw))
            {
                rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }

            foreach (var fb in fallbackSpawns)
            {
                if (fb == null || string.IsNullOrWhiteSpace(fb.npcCode))
                {
                    continue;
                }

                if (string.Equals(fb.npcCode.Trim(), npc.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    position = root.TransformPoint(fb.localPosition);
                    rotation = Quaternion.Euler(0f, fb.yRotationDegrees, 0f);
                    return;
                }
            }

            position = root.position + new Vector3(2f, 0f, 2f);
            rotation = Quaternion.identity;
        }

        private static bool TryFindSceneNpcAnchor(string npcCode, out Vector3 worldPosition, out float yawDegrees)
        {
            worldPosition = default;
            yawDegrees = 0f;
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
            Transform found = null;
            var count = 0;
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                foreach (var t in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, code, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                        found ??= t;
                    }
                }
            }

            if (found == null)
            {
                return false;
            }

            if (count > 1)
            {
                Debug.LogWarning($"[HubNpcSpawnController] Несколько объектов «{code}» на сцене ({count}), используется первый.");
            }

            worldPosition = found.position;
            yawDegrees = found.eulerAngles.y;
            return true;
        }
    }

    [System.Serializable]
    public sealed class HubNpcFallbackSpawn
    {
        public string npcCode;
        public Vector3 localPosition;
        public float yRotationDegrees;
    }
}
