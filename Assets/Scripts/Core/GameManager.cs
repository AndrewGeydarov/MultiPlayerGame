using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyMiniGames.Core
{
    public enum GameState
    {
        MainMenu,
        NameEntry,
        MiniGamePlay,
        Results
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public PlayerData[] Players { get; private set; } = new PlayerData[2];
        public string WinnerName { get; private set; }
        public bool IsDraw { get; private set; }
        public int WinnerIndex { get; private set; } = -1;
        public string SelectedMiniGame { get; private set; }

        private static readonly Color Player1Color = new Color(1f, 0.42f, 0.42f, 1f);
        private static readonly Color Player2Color = new Color(0.306f, 0.804f, 0.769f, 1f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetPlayers(string name1, string name2)
        {
            Players[0] = new PlayerData(name1, Player1Color);
            Players[1] = new PlayerData(name2, Player2Color);
        }

        public void StartMiniGame(string sceneName)
        {
            SelectedMiniGame = sceneName;
            CurrentState = GameState.MiniGamePlay;
            SceneManager.LoadScene(sceneName);
        }

        public void ReportResult(int winnerIndex)
        {
            WinnerIndex = winnerIndex;
            WinnerName = Players[winnerIndex].PlayerName;
            IsDraw = false;
            CurrentState = GameState.Results;
            SceneManager.LoadScene("MainMenu");
        }

        public void ReportDraw()
        {
            WinnerIndex = -1;
            WinnerName = "";
            IsDraw = true;
            CurrentState = GameState.Results;
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// Только фиксирует результат, не вызывая загрузку сцены.
        /// Используется сетевой версией: сцену MainMenu загрузит NetworkManager.SceneManager.
        /// </summary>
        public void ReportResultLocal(int winnerIndex)
        {
            if (winnerIndex < 0 || winnerIndex >= Players.Length || Players[winnerIndex] == null) return;
            WinnerIndex = winnerIndex;
            WinnerName = Players[winnerIndex].PlayerName;
            IsDraw = false;
            CurrentState = GameState.Results;
        }

        public void ReportDrawLocal()
        {
            WinnerIndex = -1;
            WinnerName = "";
            IsDraw = true;
            CurrentState = GameState.Results;
        }

        /// <summary>
        /// Сбрасывает состояние в MainMenu, не перезагружая сцену.
        /// Используется при возврате из мини-игры в сетевом режиме, где сцена уже загружена через NGO.
        /// </summary>
        public void SetStateMainMenu()
        {
            CurrentState = GameState.MainMenu;
        }

        public void ReturnToMenu()
        {
            CurrentState = GameState.MainMenu;
            SceneManager.LoadScene("MainMenu");
        }

        public void ShowNameEntry(string miniGame)
        {
            SelectedMiniGame = miniGame;
            CurrentState = GameState.NameEntry;
        }

        public void ConfirmNamesAndStart()
        {
            StartMiniGame(SelectedMiniGame);
        }

        public PlayerData GetPlayer(int index)
        {
            if (Players == null || index < 0 || index >= Players.Length || Players[index] == null)
                return null;
            return Players[index];
        }

        public string GetPlayerName(int index)
        {
            var p = GetPlayer(index);
            return p != null ? p.PlayerName : $"\u0418\u0433\u0440\u043e\u043a {index + 1}";
        }

        public Color GetPlayerColor(int index)
        {
            var p = GetPlayer(index);
            return p != null ? p.PlayerColor : Color.white;
        }
    }
}