using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyMiniGames.UI
{
    /// <summary>
    /// Создаёт NetworkLobbyUI при запуске приложения (DontDestroyOnLoad).
    /// Раньше лобби создавалось только если оно уже было в сцене; теперь его нет в сцене
    /// (мы её не редактируем), и инициализатор создаёт UI прямо в runtime.
    /// </summary>
    public static class LobbyAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureLobby()
        {
            // Создаём только один раз; после этого DontDestroyOnLoad сохранит объект.
            if (Object.FindAnyObjectByType<NetworkLobbyUI>() != null) return;

            var go = new GameObject("NetworkLobbyUI");
            go.AddComponent<NetworkLobbyUI>();
            // Awake создаст UIDocument и PanelSettings программно, и подпишется на SceneManager.sceneLoaded.

            // На первой загрузке (MainMenu) хочется сразу открыть лобби.
            var lobby = go.GetComponent<NetworkLobbyUI>();
            if (lobby != null && SceneManager.GetActiveScene().name == "MainMenu")
            {
                lobby.Show();
            }
        }
    }
}
