using UnityEngine;
using UnityEngine.UIElements;
using PartyMiniGames.Core;
using PartyMiniGames.Network;

namespace PartyMiniGames.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) return;
            _root = _document.rootVisualElement;

            var btnCrocodile = _root.Q<Button>("btn-crocodile");
            var btnTower = _root.Q<Button>("btn-tower");
            var btnMemory = _root.Q<Button>("btn-memory");

            btnCrocodile?.RegisterCallback<ClickEvent>(evt => OnGameSelected("Crocodile"));
            btnTower?.RegisterCallback<ClickEvent>(evt => OnGameSelected("TowerBuilder"));
            // Memory в сетевой версии пока не реализован — показываем подсказку при клике.
            btnMemory?.RegisterCallback<ClickEvent>(evt => OnMemoryClicked());
        }

        private void OnMemoryClicked()
        {
            // Если сетевой режим активен — сообщаем, что Memory в сети пока не доступен.
            if (NetworkBootstrap.Instance != null &&
                (NetworkBootstrap.Instance.IsHost || NetworkBootstrap.Instance.IsClient))
            {
                Debug.Log("Memory в сетевой версии пока не реализован. Используйте Крокодил или Башню.");
                return;
            }
            OnGameSelected("Memory");
        }

        private void OnGameSelected(string sceneName)
        {
            // ---- НОВЫЙ ПОТОК: сначала сеть ----
            // 1) Если NetworkBootstrap есть и уже подключены — стартуем мини-игру через сетевой менеджер.
            // 2) Если ещё не подключены — открываем лобби (Host/Join).
            // 3) Если NetworkBootstrap отсутствует (старый локальный режим) — старое поведение.
            if (NetworkBootstrap.Instance != null)
            {
                bool isHost = NetworkBootstrap.Instance.IsHost;
                bool isClient = NetworkBootstrap.Instance.IsClient;

                if (isHost)
                {
                    // Хост стартует мини-игру через NetworkBootstrap — все остальные клиенты подтянутся.
                    if (NetworkBootstrap.Instance.ConnectedClientCount < 2)
                    {
                        Debug.Log("Ждём подключения второго игрока, прежде чем стартовать.");
                        return;
                    }
                    NetworkBootstrap.Instance.HostLoadMiniGameScene(sceneName);
                    return;
                }
                if (isClient)
                {
                    // Клиент не может сам стартовать — ждёт, пока хост загрузит сцену.
                    Debug.Log("Только хост может выбрать мини-игру. Ждём…");
                    return;
                }

                // Никто не подключён — открываем лобби.
                var lobby = FindAnyObjectByType<NetworkLobbyUI>();
                if (lobby != null)
                {
                    lobby.Show();
                    Hide();
                    return;
                }
            }

            // ---- Старое локальное поведение (fallback) ----
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.GetPlayer(0) != null)
            {
                GameManager.Instance.StartMiniGame(sceneName);
            }
            else
            {
                GameManager.Instance.ShowNameEntry(sceneName);
                ShowNameEntry();
            }
        }

        private void ShowNameEntry()
        {
            var nameEntryUI = FindAnyObjectByType<NameEntryUI>();
            if (nameEntryUI != null)
            {
                nameEntryUI.Show();
                Hide();
            }
        }

        public void Show()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }
    }
}