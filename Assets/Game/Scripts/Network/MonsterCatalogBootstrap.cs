using UnityEngine;
using UnityEngine.SceneManagement;

namespace DVBARPG.Game.Network
{
    public static class MonsterCatalogBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            var scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, "Run", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Object.FindFirstObjectByType<MonsterCatalogClient>() != null)
            {
                return;
            }

            var go = new GameObject("MonsterCatalogClient");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<MonsterCatalogClient>();
        }
    }
}
