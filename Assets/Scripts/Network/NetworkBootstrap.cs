using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Менеджер сетевого подключения. Создаёт NetworkManager (Netcode for GameObjects)
    /// с Unity Transport (UTP) и хранит его между сценами.
    ///
    /// Логика подключения:
    ///  - Хост (Host) запускает встроенный сервер и одновременно играет как Player 0.
    ///  - Клиент (Client) подключается к хосту по IP:Port и играет как Player 1.
    ///
    /// Архитектура для решения проблемы "сцены не отредактированы в Editor":
    ///  - Не используем in-scene NetworkObject'ы (для них нужны GlobalObjectIdHash из Editor'a).
    ///  - Вместо этого создаём runtime prefab'ы (через NetworkPrefabRegistry) и спавним их
    ///    через NetworkManager после загрузки сцены мини-игры.
    ///  - Сцена грузится через NetworkSceneManager (синхронно у всех).
    ///  - После загрузки сервер спавнит сетевой prefab нужной игры; клиенты получают его автоматически.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        // Локальное имя игрока (выбирается в меню перед подключением).
        public string LocalPlayerName { get; set; } = "Игрок";

        // Параметры подключения (хранятся между сценами).
        public string JoinIpAddress { get; set; } = "127.0.0.1";
        public ushort JoinPort { get; set; } = 7777;

        // Признак, что соединение уже было запущено — чтобы не запускать снова при перезагрузке сцены.
        public bool IsHost { get; private set; }
        public bool IsClient { get; private set; }

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private NetworkPrefabRegistry _prefabs;

        // Имя сцены, которая загружается следующей; используется, чтобы понять, какой prefab спавнить.
        private string _pendingMiniGameScene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureNetworkManager();
            EnsurePrefabRegistry();
            RegisterPrefabsIntoManager();

            // Добавляем диагностический оверлей (F1) — для демонстрации мультиплеера.
            if (GetComponent<NetworkDebugOverlay>() == null)
                gameObject.AddComponent<NetworkDebugOverlay>();
        }

        /// <summary>
        /// Создаёт NetworkManager + UnityTransport на этом объекте, если их ещё нет.
        /// </summary>
        private void EnsureNetworkManager()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = gameObject.AddComponent<NetworkManager>();

            // NetworkConfig может быть null у свежесозданного NetworkManager — создаём вручную.
            if (_networkManager.NetworkConfig == null)
                _networkManager.NetworkConfig = new NetworkConfig();

            _transport = GetComponent<UnityTransport>();
            if (_transport == null)
                _transport = gameObject.AddComponent<UnityTransport>();

            // Привязываем транспорт.
            _networkManager.NetworkConfig.NetworkTransport = _transport;

            // Включаем синхронизированную загрузку сцен (хост -> все клиенты).
            _networkManager.NetworkConfig.EnableSceneManagement = true;

            // Разрешаем разные prefab-листы между клиентом и сервером (т.к. часть prefab'ов
            // регистрируется динамически).
            _networkManager.NetworkConfig.ForceSamePrefabs = false;

            // Инициализируем список Prefabs, если он null (в NGO 2.x иногда так бывает).
            if (_networkManager.NetworkConfig.Prefabs == null)
                _networkManager.NetworkConfig.Prefabs = new Unity.Netcode.NetworkPrefabs();

            // По умолчанию слушаем на всех интерфейсах. Конкретный IP подставляется в StartHost/StartClient.
            _transport.SetConnectionData("0.0.0.0", JoinPort, "0.0.0.0");
        }

        private void EnsurePrefabRegistry()
        {
            _prefabs = NetworkPrefabRegistry.Instance;
            if (_prefabs == null)
            {
                var go = new GameObject("NetworkPrefabRegistry");
                _prefabs = go.AddComponent<NetworkPrefabRegistry>();
            }
        }

        private void RegisterPrefabsIntoManager()
        {
            if (_prefabs != null && _networkManager != null)
                _prefabs.RegisterAllPrefabs(_networkManager);
        }

        /// <summary>
        /// Запускает Host (= Server + локальный Client). Используется первым игроком.
        /// </summary>
        public bool StartHost(ushort port = 7777)
        {
            JoinPort = port;
            _transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");

            // Регистрируем prefab'ы (на случай если регистрация при Awake не прошла).
            RegisterPrefabsIntoManager();

            bool success = _networkManager.StartHost();
            if (success)
            {
                IsHost = true;
                IsClient = false;
                Debug.Log($"[NetworkBootstrap] Host запущен на порту {port}.");

                EnsureLocalGameManager();
                if (Core.GameManager.Instance != null)
                {
                    string p1 = string.IsNullOrEmpty(LocalPlayerName) ? "Игрок 1" : LocalPlayerName;
                    Core.GameManager.Instance.SetPlayers(p1, "Игрок 2");
                }

                // Подписываемся на завершение загрузки сцены, чтобы спавнить prefab мини-игры.
                _networkManager.SceneManager.OnLoadEventCompleted += OnNetworkSceneLoadCompleted;
            }
            else
            {
                Debug.LogError("[NetworkBootstrap] Не удалось запустить Host.");
            }
            return success;
        }

        /// <summary>
        /// Подключается как Client к указанному IP:Port. Используется вторым игроком.
        /// </summary>
        public bool StartClient(string ipAddress, ushort port = 7777)
        {
            JoinIpAddress = ipAddress;
            JoinPort = port;
            _transport.SetConnectionData(ipAddress, port);

            RegisterPrefabsIntoManager();

            bool success = _networkManager.StartClient();
            if (success)
            {
                IsHost = false;
                IsClient = true;
                Debug.Log($"[NetworkBootstrap] Клиент подключается к {ipAddress}:{port}.");

                EnsureLocalGameManager();
                if (Core.GameManager.Instance != null)
                {
                    string p2 = string.IsNullOrEmpty(LocalPlayerName) ? "Игрок 2" : LocalPlayerName;
                    Core.GameManager.Instance.SetPlayers("Игрок 1", p2);
                }
            }
            else
            {
                Debug.LogError($"[NetworkBootstrap] Не удалось подключиться к {ipAddress}:{port}.");
            }
            return success;
        }

        private void EnsureLocalGameManager()
        {
            if (Core.GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<Core.GameManager>();
            }
        }

        /// <summary>
        /// Хост вызывает этот метод вместо прямого SceneManager.LoadScene.
        /// Сцена синхронизированно загружается у всех клиентов; после загрузки сервер спавнит
        /// сетевой prefab нужной мини-игры.
        /// </summary>
        public void HostLoadMiniGameScene(string sceneName)
        {
            if (_networkManager == null || !_networkManager.IsServer)
            {
                Debug.LogWarning("[NetworkBootstrap] Только сервер (хост) может стартовать мини-игру.");
                return;
            }

            _pendingMiniGameScene = sceneName;
            _networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// Хост вызывает при возврате в меню — синхронно вернёт всех в MainMenu.
        /// </summary>
        public void HostReturnToMainMenu()
        {
            if (_networkManager == null || !_networkManager.IsServer) return;
            _pendingMiniGameScene = null;
            _networkManager.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        /// <summary>
        /// Серверный коллбек после полной загрузки сцены у всех клиентов: время спавнить prefab мини-игры.
        /// </summary>
        private void OnNetworkSceneLoadCompleted(string sceneName, LoadSceneMode mode,
                                                 System.Collections.Generic.List<ulong> clientsCompleted,
                                                 System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            if (!_networkManager.IsServer) return;

            GameObject prefab = null;
            if (sceneName == "Crocodile"     && _prefabs != null) prefab = _prefabs.CrocodilePrefab;
            else if (sceneName == "TowerBuilder" && _prefabs != null) prefab = _prefabs.TowerPrefab;
            else if (sceneName == "Memory"   && _prefabs != null) prefab = _prefabs.MemoryPrefab;

            if (prefab == null) return;

            // Спавним prefab на сервере. NGO сам разошлёт его клиентам.
            var instance = Object.Instantiate(prefab);
            instance.SetActive(true);
            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true); // true = объект уничтожится при выгрузке сцены.
            }
        }

        /// <summary>
        /// Разрывает текущее соединение (используется при возврате в главное меню "выйти из сети").
        /// </summary>
        public void Shutdown()
        {
            if (_networkManager != null)
            {
                if (_networkManager.SceneManager != null)
                {
                    _networkManager.SceneManager.OnLoadEventCompleted -= OnNetworkSceneLoadCompleted;
                }
                if (_networkManager.IsListening)
                    _networkManager.Shutdown();
            }
            IsHost = false;
            IsClient = false;
        }

        /// <summary>
        /// Индекс локального игрока: 0 для хоста, 1 для клиента.
        /// Используется для определения, какой Player управляется на этом устройстве.
        /// </summary>
        public int LocalPlayerIndex
        {
            get
            {
                if (_networkManager == null) return 0;
                if (_networkManager.IsHost || _networkManager.IsServer) return 0;
                return 1;
            }
        }

        /// <summary>
        /// True, если на этом устройстве запущен сервер (= хост).
        /// </summary>
        public bool IsServerInstance => _networkManager != null && _networkManager.IsServer;

        /// <summary>
        /// Сколько клиентов сейчас подключено (включая хоста).
        /// </summary>
        public int ConnectedClientCount
        {
            get
            {
                if (_networkManager == null || !_networkManager.IsListening) return 0;
                return _networkManager.ConnectedClientsIds.Count;
            }
        }
    }
}
