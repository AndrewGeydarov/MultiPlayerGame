using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Регистратор сетевых prefab'ов для динамического спавна.
    ///
    /// Подход:
    ///   1) Создаём шаблонные GameObject'ы (с NetworkObject + игровым скриптом) в коде.
    ///   2) Через рефлексию задаём им фиксированные GlobalObjectIdHash значения (одинаковые
    ///      на хосте и клиенте — гарантируется константами).
    ///   3) Регистрируем handler'ы через NetworkManager.PrefabHandler.AddHandler(uint, ...).
    ///      Эта перегрузка не требует, чтобы prefab был в asset database — она привязывает
    ///      handler напрямую к GlobalObjectIdHash, что нам и нужно.
    ///
    /// Почему так, а не через "in-scene placed NetworkObject":
    ///   In-scene объекты получают GlobalObjectIdHash, генерируемый Unity Editor'ом, и его
    ///   нельзя задать программно без сохранения сцены. Это значит, без открытия Unity Editor
    ///   мы не можем подготовить рабочие сцены. Через AddHandler(uint, ...) мы обходим
    ///   это ограничение полностью.
    /// </summary>
    public class NetworkPrefabRegistry : MonoBehaviour
    {
        public static NetworkPrefabRegistry Instance { get; private set; }

        // Заранее заданные хэши для каждого prefab'а — должны быть одинаковыми на хосте и клиенте.
        // Поскольку код одинаковый, константы тоже совпадают автоматически.
        public const uint CROCODILE_PREFAB_HASH = 0x1C2C0001;
        public const uint TOWER_PREFAB_HASH     = 0x1C2C0002;
        public const uint MEMORY_PREFAB_HASH    = 0x1C2C0003;

        private GameObject _crocodilePrefab;
        private GameObject _towerPrefab;
        private GameObject _memoryPrefab;

        // Запоминаем зарегистрированные handler'ы для последующей отписки.
        private readonly Dictionary<uint, INetworkPrefabInstanceHandler> _registered =
            new Dictionary<uint, INetworkPrefabInstanceHandler>();

        public GameObject CrocodilePrefab => _crocodilePrefab;
        public GameObject TowerPrefab     => _towerPrefab;
        public GameObject MemoryPrefab    => _memoryPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreatePrefabTemplates();
        }

        /// <summary>
        /// Создаёт шаблоны prefab'ов как обычные GameObject'ы. Они не активны и
        /// "хранятся" во вселенной DontDestroyOnLoad — используются только как
        /// заготовка для клонирования.
        /// </summary>
        private void CreatePrefabTemplates()
        {
            _crocodilePrefab = CreatePrefabTemplate<NetworkCrocodileGame>("NetworkCrocodile_Template", CROCODILE_PREFAB_HASH);
            _towerPrefab     = CreatePrefabTemplate<NetworkTowerGame>("NetworkTower_Template",     TOWER_PREFAB_HASH);
            _memoryPrefab    = CreatePrefabTemplate<NetworkMemoryGame>("NetworkMemory_Template",   MEMORY_PREFAB_HASH);
        }

        private GameObject CreatePrefabTemplate<T>(string name, uint hash) where T : NetworkBehaviour
        {
            var go = new GameObject(name);
            go.SetActive(false); // шаблон неактивен, чтобы у него не работали ни Update, ни OnEnable
            DontDestroyOnLoad(go);

            var netObj = go.AddComponent<NetworkObject>();
            ForceSetGlobalObjectIdHash(netObj, hash);

            go.AddComponent<T>();

            return go;
        }

        /// <summary>
        /// Принудительно задаёт NetworkObject.GlobalObjectIdHash через рефлексию.
        /// Это поле в NGO помечено как [SerializeField] internal uint GlobalObjectIdHash.
        /// В runtime'е OnValidate() не вызывается, поэтому наше значение остаётся.
        /// </summary>
        private static void ForceSetGlobalObjectIdHash(NetworkObject netObj, uint hash)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var type = typeof(NetworkObject);

            var field = type.GetField("GlobalObjectIdHash", flags);
            if (field != null)
            {
                field.SetValue(netObj, hash);
                return;
            }

            var prop = type.GetProperty("GlobalObjectIdHash", flags);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(netObj, hash);
                return;
            }

            Debug.LogError("[NetworkPrefabRegistry] Не нашли поле GlobalObjectIdHash через рефлексию. " +
                           "Возможно, API NGO изменилось. Сетевой режим работать не будет.");
        }

        /// <summary>
        /// Регистрирует все шаблоны в указанном NetworkManager. Должно быть вызвано
        /// ДО StartHost/StartClient, чтобы хэши были известны обеим сторонам.
        /// </summary>
        public void RegisterAllPrefabs(NetworkManager nm)
        {
            if (nm == null) return;

            // Если регистрировались раньше — отписываемся.
            foreach (var kv in _registered)
            {
                nm.PrefabHandler.RemoveHandler(kv.Key);
            }
            _registered.Clear();

            RegisterOne(nm, _crocodilePrefab, CROCODILE_PREFAB_HASH);
            RegisterOne(nm, _towerPrefab,     TOWER_PREFAB_HASH);
            RegisterOne(nm, _memoryPrefab,    MEMORY_PREFAB_HASH);
        }

        private void RegisterOne(NetworkManager nm, GameObject prefab, uint hash)
        {
            if (prefab == null) return;

            var handler = new RuntimePrefabHandler(prefab);

            // Используем перегрузку AddHandler(uint, ...) — она не валидирует prefab
            // как asset из AssetDatabase, что критично для runtime-созданных GameObject'ов.
            bool ok = nm.PrefabHandler.AddHandler(hash, handler);
            if (ok)
            {
                _registered[hash] = handler;
                Debug.Log($"[NetworkPrefabRegistry] Зарегистрирован prefab '{prefab.name}' с хэшем {hash:X8}.");
            }
            else
            {
                Debug.LogWarning($"[NetworkPrefabRegistry] AddHandler(uint) вернул false для '{prefab.name}'. " +
                                 "Возможно, handler уже зарегистрирован.");
            }
        }

        /// <summary>
        /// Возвращает зарегистрированный шаблон по хэшу. Используется сервером для спавна.
        /// </summary>
        public GameObject GetPrefabByHash(uint hash)
        {
            if (hash == CROCODILE_PREFAB_HASH) return _crocodilePrefab;
            if (hash == TOWER_PREFAB_HASH)     return _towerPrefab;
            if (hash == MEMORY_PREFAB_HASH)    return _memoryPrefab;
            return null;
        }
    }

    /// <summary>
    /// Handler для INetworkPrefabInstanceHandler. NGO вызывает Instantiate на КЛИЕНТАХ,
    /// когда сервер шлёт сообщение о спавне prefab'a. Сервер сам создаёт инстанс отдельно
    /// (через Object.Instantiate + NetworkObject.Spawn).
    /// </summary>
    public class RuntimePrefabHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _template;

        public RuntimePrefabHandler(GameObject template)
        {
            _template = template;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            // Клонируем шаблон и активируем клон (сам шаблон остаётся неактивным).
            var instance = Object.Instantiate(_template, position, rotation);
            instance.SetActive(true);
            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject != null && networkObject.gameObject != null)
                Object.Destroy(networkObject.gameObject);
        }
    }
}
