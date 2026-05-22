using UnityEngine;
using UnityEngine.UIElements;
using PartyMiniGames.Core;

namespace PartyMiniGames.UI
{
    public class ResultsUI : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private Label _winnerLabel;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) return;
            _root = _document.rootVisualElement;

            _winnerLabel = _root.Q<Label>("winner-label");

            var btnBackMenu = _root.Q<Button>("btn-back-menu");
            btnBackMenu?.RegisterCallback<ClickEvent>(evt => OnBackToMenuClicked());

            _root.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Results)
            {
                ShowResults();
            }
        }

        private void Update()
        {
            if (_root == null || GameManager.Instance == null) return;

            if (GameManager.Instance.CurrentState == GameState.Results &&
                _root.style.display != DisplayStyle.Flex)
            {
                ShowResults();
            }
        }

        public void ShowResults()
        {
            if (_root == null) return;

            var mainMenu = FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null) mainMenu.Hide();

            var nameEntry = FindAnyObjectByType<NameEntryUI>();
            if (nameEntry != null) nameEntry.Hide();

            _root.style.display = DisplayStyle.Flex;

            if (GameManager.Instance == null) return;

            if (GameManager.Instance.IsDraw)
            {
                _winnerLabel.text = "\u041d\u0438\u0447\u044c\u044f!";
                _winnerLabel.style.color = new StyleColor(Color.white);
            }
            else
            {
                string name = GameManager.Instance.WinnerName;
                Color winnerColor = GameManager.Instance.GetPlayerColor(GameManager.Instance.WinnerIndex);
                _winnerLabel.text = $"\u041f\u043e\u0431\u0435\u0434\u0438\u0442\u0435\u043b\u044c: {name}!";
                _winnerLabel.style.color = new StyleColor(winnerColor);
            }

            if (AudioManager.Instance != null)
            {
                if (GameManager.Instance.IsDraw)
                    AudioManager.Instance.PlayWin();
                else
                    AudioManager.Instance.PlayWin();
            }
        }

        private void OnBackToMenuClicked()
        {
            _root.style.display = DisplayStyle.None;

            // В сетевом режиме мы уже в MainMenu (хост сам загружает её через NetworkSceneManager),
            // достаточно сбросить состояние и показать главное меню.
            bool inNetwork = false;
            var nb = PartyMiniGames.Network.NetworkBootstrap.Instance;
            if (nb != null && (nb.IsHost || nb.IsClient)) inNetwork = true;

            if (GameManager.Instance != null)
            {
                if (inNetwork)
                {
                    // Просто меняем стейт, без перезагрузки сцены.
                    GameManager.Instance.SetStateMainMenu();
                }
                else
                {
                    GameManager.Instance.ReturnToMenu();
                }
            }

            var mainMenu = FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null) mainMenu.Show();
        }
    }
}