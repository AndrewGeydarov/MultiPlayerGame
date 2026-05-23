using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using PartyMiniGames.Core;
using PartyMiniGames.Network;
using Unity.Netcode;

namespace PartyMiniGames.UI
{
    /// <summary>
    /// UI-окно "Сетевая игра": выбор Host/Client + ввод имени и IP.
    /// Создаёт собственный UIDocument с PanelSettings программно.
    /// Сам открывается при загрузке сцены MainMenu, если нет активного соединения.
    /// </summary>
    public class NetworkLobbyUI : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private TextField _inputName;
        private TextField _inputIp;
        private TextField _inputPort;
        private Button _btnHost;
        private Button _btnJoin;
        private Button _btnDisconnect;
        private Button _btnClose;
        private Label _lblStatus;
        private Label _lblLocalIp;
        private bool _uiBuilt = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Привязываем PanelSettings отложенно — на момент Awake основное меню ещё может быть не загружено.
            TryAssignPanelSettings();
        }

        /// <summary>
        /// Берём PanelSettings у уже существующего UIDocument в сцене (например, у MainMenu),
        /// и создаём на его основе свой клон с более высоким sortingOrder, чтобы лобби было поверх.
        /// </summary>
        private void TryAssignPanelSettings()
        {
            if (_document == null) return;
            if (_document.panelSettings != null) return;

            // Ищем любой другой UIDocument в сцене с настроенным PanelSettings.
            var allDocs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            PanelSettings sourcePs = null;
            foreach (var d in allDocs)
            {
                if (d == _document) continue;
                if (d.panelSettings != null)
                {
                    sourcePs = d.panelSettings;
                    break;
                }
            }

            if (sourcePs == null)
            {
                // Нет других UIDocument — отложим, попробуем при загрузке MainMenu в OnSceneLoaded.
                return;
            }

            // Клонируем PanelSettings основного меню, чтобы тема и шрифты были унаследованы.
            var ps = ScriptableObject.Instantiate(sourcePs);
            ps.sortingOrder = sourcePs.sortingOrder + 100; // лобби сверху
            ps.clearColor = false;
            ps.name = "LobbyPanelSettings";

            _document.panelSettings = ps;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnEnable()
        {
            BuildUI();
            UpdateState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                if (_document != null && _document.panelSettings == null)
                    TryAssignPanelSettings();
                BuildUI();
                // Если соединение ещё не установлено — открываем лобби автоматически.
                if (NetworkBootstrap.Instance != null &&
                    !NetworkBootstrap.Instance.IsHost &&
                    !NetworkBootstrap.Instance.IsClient)
                {
                    Show();
                }
                else
                {
                    Hide();
                }
                UpdateState();
            }
            else
            {
                // В мини-играх лобби всегда скрыто.
                Hide();
            }
        }

        private void BuildUI()
        {
            if (_uiBuilt && _root != null) return;
            if (_document == null) return;

            // Если PanelSettings ещё не назначены — пробуем сейчас (MainMenu уже мог загрузиться).
            if (_document.panelSettings == null)
                TryAssignPanelSettings();

            _root = _document.rootVisualElement;
            if (_root == null) return;

            _root.Clear();

            // Полупрозрачный оверлей.
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.top = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            _root.pickingMode = PickingMode.Position;
            // Чтобы текст и поля были читаемыми в ConstantPixelSize-режиме.
            _root.style.fontSize = 14;
            _root.style.color = Color.black;

            var card = new VisualElement();
            card.style.width = 520;
            card.style.paddingTop = 24; card.style.paddingBottom = 24;
            card.style.paddingLeft = 28; card.style.paddingRight = 28;
            card.style.backgroundColor = new StyleColor(new Color(0.12f, 0.14f, 0.2f, 1f));
            card.style.borderTopLeftRadius = 12; card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12; card.style.borderBottomRightRadius = 12;
            _root.Add(card);

            var title = new Label("Сетевая игра");
            title.style.fontSize = 22;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 14;
            card.Add(title);

            var subtitle = new Label("Один игрок создаёт игру (Host), второй подключается по IP (Join).");
            subtitle.style.color = new StyleColor(new Color(0.75f, 0.78f, 0.85f, 1f));
            subtitle.style.fontSize = 13;
            subtitle.style.marginBottom = 12;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            card.Add(subtitle);

            _inputName = new TextField("Имя игрока:") { name = "input-player-name", value = "Игрок" };
            _inputName.style.marginBottom = 8;
            card.Add(_inputName);

            _inputIp = new TextField("IP хоста:") { name = "input-ip", value = "127.0.0.1" };
            _inputIp.style.marginBottom = 8;
            card.Add(_inputIp);

            _inputPort = new TextField("Порт:") { name = "input-port", value = "7777" };
            _inputPort.style.marginBottom = 14;
            card.Add(_inputPort);

            // Информация о локальных IP — пригодится тому, кто хостит.
            _lblLocalIp = new Label("");
            _lblLocalIp.style.color = new StyleColor(new Color(0.6f, 0.85f, 0.7f, 1f));
            _lblLocalIp.style.fontSize = 11;
            _lblLocalIp.style.marginBottom = 12;
            _lblLocalIp.style.whiteSpace = WhiteSpace.Normal;
            UpdateLocalIpLabel();
            card.Add(_lblLocalIp);

            var rowButtons = new VisualElement();
            rowButtons.style.flexDirection = FlexDirection.Row;
            rowButtons.style.justifyContent = Justify.SpaceBetween;
            card.Add(rowButtons);

            _btnHost = new Button(OnHostClicked) { text = "Создать игру (Host)" };
            _btnHost.style.flexGrow = 1; _btnHost.style.marginRight = 6;
            rowButtons.Add(_btnHost);

            _btnJoin = new Button(OnJoinClicked) { text = "Подключиться (Join)" };
            _btnJoin.style.flexGrow = 1; _btnJoin.style.marginLeft = 6;
            rowButtons.Add(_btnJoin);

            _lblStatus = new Label("");
            _lblStatus.style.color = new StyleColor(new Color(0.7f, 0.8f, 1f, 1f));
            _lblStatus.style.marginTop = 14;
            _lblStatus.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_lblStatus);

            // Кнопка отключиться (видна только когда подключены).
            _btnDisconnect = new Button(OnDisconnectClicked) { text = "Отключиться" };
            _btnDisconnect.style.marginTop = 10;
            _btnDisconnect.style.display = DisplayStyle.None;
            card.Add(_btnDisconnect);

            // Кнопка закрыть окно (видна когда подключены — чтобы выбрать мини-игру).
            _btnClose = new Button(OnCloseClicked) { text = "Закрыть и выбрать игру" };
            _btnClose.style.marginTop = 6;
            _btnClose.style.display = DisplayStyle.None;
            card.Add(_btnClose);

            _root.style.display = DisplayStyle.None;
            _uiBuilt = true;
        }

        private void Update()
        {
            // Если игра уже в сети — динамически обновляем статус.
            if (_root != null && _root.style.display == DisplayStyle.Flex)
            {
                UpdateState();
            }
        }

        private void UpdateState()
        {
            if (NetworkBootstrap.Instance == null) return;
            var nb = NetworkBootstrap.Instance;

            if (_inputName != null && string.IsNullOrEmpty(_inputName.value))
                _inputName.value = nb.LocalPlayerName;

            bool connected = nb.IsHost || nb.IsClient;
            int clients = nb.ConnectedClientCount;

            if (nb.IsHost)
            {
                SetStatus(clients >= 2
                    ? $"Хост активен. Подключено игроков: {clients}. Можно начинать!"
                    : $"Хост активен на порту {nb.JoinPort}. Ждём второго игрока…");
            }
            else if (nb.IsClient)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
                    SetStatus("Подключение установлено. Ждём начала мини-игры от хоста.");
                else
                    SetStatus($"Подключаемся к {nb.JoinIpAddress}:{nb.JoinPort}…");
            }
            else
            {
                SetStatus("");
            }

            if (_btnHost != null) _btnHost.SetEnabled(!connected);
            if (_btnJoin != null) _btnJoin.SetEnabled(!connected);
            if (_btnDisconnect != null) _btnDisconnect.style.display = connected ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnClose != null) _btnClose.style.display = (nb.IsHost && clients >= 2) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateLocalIpLabel()
        {
            if (_lblLocalIp == null) return;
            string ips = GetLocalIPv4Addresses();
            _lblLocalIp.text = string.IsNullOrEmpty(ips)
                ? "Ваш локальный IP не определён."
                : $"Ваш локальный IP: {ips}  (дайте этот IP второму игроку для подключения)";
        }

        private string GetLocalIPv4Addresses()
        {
            try
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        string s = ua.Address.ToString();
                        if (s == "127.0.0.1") continue;
                        list.Add(s);
                    }
                }
                return string.Join(", ", list);
            }
            catch
            {
                return "";
            }
        }

        private void OnHostClicked()
        {
            string name = _inputName != null ? _inputName.value : "Игрок";
            ushort port = ParsePortOrDefault();

            if (NetworkBootstrap.Instance == null)
            {
                SetStatus("Ошибка: NetworkBootstrap не создан.");
                return;
            }

            NetworkBootstrap.Instance.LocalPlayerName = string.IsNullOrWhiteSpace(name) ? "Игрок 1" : name.Trim();
            if (!NetworkBootstrap.Instance.StartHost(port))
                SetStatus("Не удалось запустить хост. Возможно, порт занят.");

            UpdateState();
        }

        private void OnJoinClicked()
        {
            string name = _inputName != null ? _inputName.value : "Игрок";
            string ip = _inputIp != null ? _inputIp.value : "127.0.0.1";
            ushort port = ParsePortOrDefault();

            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

            if (NetworkBootstrap.Instance == null)
            {
                SetStatus("Ошибка: NetworkBootstrap не создан.");
                return;
            }

            NetworkBootstrap.Instance.LocalPlayerName = string.IsNullOrWhiteSpace(name) ? "Игрок 2" : name.Trim();
            if (!NetworkBootstrap.Instance.StartClient(ip.Trim(), port))
                SetStatus("Не удалось подключиться. Проверьте IP и порт.");

            UpdateState();
        }

        private ushort ParsePortOrDefault()
        {
            if (_inputPort != null && ushort.TryParse(_inputPort.value, out ushort p) && p > 0)
                return p;
            return 7777;
        }

        private void OnDisconnectClicked()
        {
            if (NetworkBootstrap.Instance != null)
                NetworkBootstrap.Instance.Shutdown();
            SetStatus("Соединение разорвано.");
            UpdateState();
        }

        private void OnCloseClicked()
        {
            Hide();
            var mainMenu = FindAnyObjectByType<MainMenuUI>();
            if (mainMenu != null) mainMenu.Show();
        }

        private void SetStatus(string text)
        {
            if (_lblStatus != null) _lblStatus.text = text;
        }

        public void Show()
        {
            BuildUI();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            UpdateLocalIpLabel();
            UpdateState();
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }
    }
}
