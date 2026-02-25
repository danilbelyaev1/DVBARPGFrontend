using DVBARPG.Core.Services;
using DVBARPG.Net.Local;
using DVBARPG.Net.Mock;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DVBARPG.Core
{
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public ServiceRegistry Services { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var go = new GameObject("[GameRoot]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Services = new ServiceRegistry();
            RegisterCoreServices();
        }

        private void Start()
        {
            var active = SceneManager.GetActiveScene();
            if (active.name == "Bootstrap")
            {
                SceneManager.LoadScene("Login");
            }
        }

        private void RegisterCoreServices()
        {
            Services.Register<IAuthService>(new MockAuthService());
            Services.Register<IProfileService>(new MockProfileService());
            var useNetworkSession = true;
            if (useNetworkSession)
            {
                var go = new GameObject("[NetworkSession]");
                DontDestroyOnLoad(go);
                var net = go.AddComponent<DVBARPG.Net.Network.NetworkSessionRunner>();
                Services.Register<ISessionService>(net);
            }
            else
            {
                Services.Register<ISessionService>(new LocalSessionRunner());
            }
            Services.Register<ICombatService>(new LocalCombatService());
            Services.Register<IInventoryService>(new LocalInventoryService());
            Services.Register<IMarketService>(new LocalMarketService());
            Services.Register<IStatService>(new LocalStatService());
            Services.Register<IItemRollService>(new LocalItemRollService());
        }
    }
}
