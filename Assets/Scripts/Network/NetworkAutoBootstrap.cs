using UnityEngine;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Создаёт NetworkBootstrap при старте приложения, чтобы не нужно было руками класть его в сцену.
    /// (Он DontDestroyOnLoad — живёт всё время.)
    /// </summary>
    public static class NetworkAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureNetworkBootstrap()
        {
            if (NetworkBootstrap.Instance != null) return;

            // PrefabRegistry создаётся первым, чтобы его шаблоны были готовы до NetworkManager.
            if (NetworkPrefabRegistry.Instance == null)
            {
                var regGo = new GameObject("NetworkPrefabRegistry");
                regGo.AddComponent<NetworkPrefabRegistry>();
            }

            var go = new GameObject("NetworkBootstrap");
            go.AddComponent<NetworkBootstrap>();
        }
    }
}
