using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using PartyMiniGames.Core;
using PartyMiniGames.UI;
using PartyMiniGames.MiniGames;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Сетевая версия мини-игры "Крокодил".
    ///
    /// Прицепляется к runtime prefab'у, который спавнится сервером после загрузки сцены Crocodile.
    /// При OnNetworkSpawn находит существующие в сцене зубы (Tooth_0..Tooth_7), отключает
    /// локальный CrocodileGame, и берёт управление на себя.
    ///
    /// Архитектура: server-authoritative.
    ///  - Сервер хранит: какой зуб плохой, чей сейчас ход, какие зубы нажаты.
    ///  - Клиент посылает RequestPressToothServerRpc.
    ///  - Сервер проверяет валидность и либо нажимает зуб у всех (ClientRpc), либо запускает укус.
    ///
    /// Исправление бага локальной версии: ход переключался "дважды" из-за того,
    /// что Tooth.OnMouseDown И CrocodileGame.Update оба обрабатывали клик. Здесь обработка
    /// идёт только через Tooth.OnMouseDown → событие OnToothClicked → ServerRpc.
    /// </summary>
    public class NetworkCrocodileGame : NetworkBehaviour
    {
        [Header("Настройки")]
        public int toothCount = 8;

        // Чей сейчас ход: 0 = хост (Player 1), 1 = клиент (Player 2). Меняет только сервер.
        private NetworkVariable<int> _currentPlayerIndex =
            new NetworkVariable<int>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        // Имена игроков синхронизируются через сетевые переменные.
        private NetworkVariable<Unity.Collections.FixedString64Bytes> _player1Name =
            new NetworkVariable<Unity.Collections.FixedString64Bytes>("Игрок 1",
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<Unity.Collections.FixedString64Bytes> _player2Name =
            new NetworkVariable<Unity.Collections.FixedString64Bytes>("Игрок 2",
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        // Признак, что раунд завершён.
        private NetworkVariable<bool> _roundOver =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private List<Tooth> _teeth = new List<Tooth>();
        private int _badToothIndex = -1; // только на сервере
        private int _safeTeethPressed = 0;
        private HudUI _hud;
        private bool _bound = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Жизненный цикл сетевого объекта управляется NGO через destroyWithScene=true:
            // он будет уничтожен при выгрузке сцены автоматически. DontDestroyOnLoad не нужен.

            _currentPlayerIndex.OnValueChanged += (_, __) => UpdateTurnIndicator();
            _roundOver.OnValueChanged += OnRoundOverChanged;
            _player1Name.OnValueChanged += (_, __) => SyncNamesToGameManager();
            _player2Name.OnValueChanged += (_, __) => SyncNamesToGameManager();

            // Привязываемся к существующим зубам в сцене.
            BindSceneObjects();

            if (IsServer)
            {
                // Сервер выбирает плохой зуб и стартует первый ход.
                _badToothIndex = Random.Range(0, _teeth.Count > 0 ? _teeth.Count : toothCount);
                _currentPlayerIndex.Value = 0;
                _safeTeethPressed = 0;

                string hostName = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 1";
                _player1Name.Value = string.IsNullOrEmpty(hostName) ? "Игрок 1" : hostName;
            }
            else
            {
                // Клиент сообщает серверу своё имя.
                string myName = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 2";
                SubmitPlayerNameServerRpc(myName);
            }

            SyncNamesToGameManager();
            UpdateTurnIndicator();
        }

        /// <summary>
        /// Ищет в загруженной сцене зубы (Tooth_0..Tooth_7) и подписывается на их клики.
        /// Также отключает локальный CrocodileGame, чтобы он не мешал.
        /// </summary>
        private void BindSceneObjects()
        {
            if (_bound) return;

            // Отключаем локальный CrocodileGame, если он есть в сцене.
            var localGame = FindAnyObjectByType<CrocodileGame>();
            if (localGame != null)
            {
                localGame.enabled = false;
            }

            _hud = FindAnyObjectByType<HudUI>();

            // Находим все Tooth-компоненты в сцене.
            _teeth.Clear();
            var allTeeth = FindObjectsByType<Tooth>(FindObjectsSortMode.None);

            // Сортируем по ToothIndex, чтобы порядок был стабильным.
            // Если ToothIndex не выставлен (== 0 у всех), используем порядок по имени.
            System.Array.Sort(allTeeth, (a, b) =>
            {
                int ai = a.ToothIndex;
                int bi = b.ToothIndex;
                if (ai != bi) return ai.CompareTo(bi);
                return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
            });

            for (int i = 0; i < allTeeth.Length; i++)
            {
                var t = allTeeth[i];
                t.ToothIndex = i; // Перезаписываем, чтобы был детерминированный индекс.
                t.OnToothClicked -= OnLocalToothClicked;
                t.OnToothClicked += OnLocalToothClicked;
                _teeth.Add(t);
            }

            _bound = true;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            foreach (var t in _teeth)
            {
                if (t != null)
                    t.OnToothClicked -= OnLocalToothClicked;
            }
            _teeth.Clear();
            _bound = false;
        }

        /// <summary>
        /// Локальный клик игрока по зубу. Шлёт запрос серверу.
        /// </summary>
        private void OnLocalToothClicked(Tooth tooth)
        {
            if (_roundOver.Value) return;
            if (tooth == null || tooth.IsPressed) return;

            int myIndex = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.LocalPlayerIndex : 0;
            if (_currentPlayerIndex.Value != myIndex)
            {
                if (_hud != null)
                {
                    _hud.ShowMessage("Не ваш ход");
                    StartCoroutine(HideMessageAfter(0.7f));
                }
                return;
            }

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayToothClick();

            RequestPressToothServerRpc(tooth.ToothIndex);
        }

        private IEnumerator HideMessageAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_hud != null) _hud.HideMessage();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPressToothServerRpc(int toothIndex, ServerRpcParams rpcParams = default(ServerRpcParams))
        {
            if (_roundOver.Value) return;
            if (toothIndex < 0 || toothIndex >= _teeth.Count) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            int senderPlayerIndex = senderId == NetworkManager.ServerClientId ? 0 : 1;

            if (senderPlayerIndex != _currentPlayerIndex.Value) return;
            if (_teeth[toothIndex] == null || _teeth[toothIndex].IsPressed) return;

            if (toothIndex == _badToothIndex)
            {
                _roundOver.Value = true;
                int winnerIndex = 1 - senderPlayerIndex;
                ShowBiteClientRpc(toothIndex, senderPlayerIndex);
                StartCoroutine(FinishWithResultAfterDelay(winnerIndex, 2.5f));
            }
            else
            {
                _safeTeethPressed++;
                PressToothClientRpc(toothIndex);

                // Если все безопасные зубы нажаты — победил тот, кто нажал последний.
                if (_safeTeethPressed >= _teeth.Count - 1)
                {
                    _roundOver.Value = true;
                    int winnerIndex = senderPlayerIndex;
                    StartCoroutine(FinishWithResultAfterDelay(winnerIndex, 2f));
                    return;
                }

                // Передаём ход.
                _currentPlayerIndex.Value = 1 - senderPlayerIndex;
            }
        }

        [ClientRpc]
        private void PressToothClientRpc(int toothIndex)
        {
            if (toothIndex < 0 || toothIndex >= _teeth.Count) return;
            if (_teeth[toothIndex] != null)
                _teeth[toothIndex].Press();
        }

        [ClientRpc]
        private void ShowBiteClientRpc(int toothIndex, int loserPlayerIndex)
        {
            if (toothIndex < 0 || toothIndex >= _teeth.Count) return;

            Tooth tooth = _teeth[toothIndex];
            if (tooth == null) return;

            tooth.IsBadTooth = true;
            tooth.RevealAsBad();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBite();

            string loserName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(loserPlayerIndex) : "?";
            if (_hud != null)
                _hud.ShowMessage($"Укус! {loserName} проиграл!");

            StartCoroutine(ShakeAnimation(tooth.gameObject, 0.8f));

            foreach (var t in _teeth)
                if (t != null) t.SetInteractable(false);
        }

        private IEnumerator FinishWithResultAfterDelay(int winnerIndex, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsServer) yield break;

            ReportResultClientRpc(winnerIndex);

            // Даём один кадр клиентам обработать ClientRpc, потом грузим MainMenu.
            // Когда сцена выгрузится, NetworkObject уничтожится автоматически (destroyWithScene=true).
            yield return null;

            if (NetworkBootstrap.Instance != null)
                NetworkBootstrap.Instance.HostReturnToMainMenu();
        }

        [ClientRpc]
        private void ReportResultClientRpc(int winnerIndex)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReportResultLocal(winnerIndex);
        }

        private IEnumerator ShakeAnimation(GameObject obj, float duration)
        {
            if (obj == null) yield break;
            Vector3 originalPos = obj.transform.position;
            float elapsed = 0f;
            float intensity = 0.15f;

            while (elapsed < duration && obj != null)
            {
                float x = originalPos.x + Random.Range(-intensity, intensity);
                float y = originalPos.y + Random.Range(-intensity, intensity);
                obj.transform.position = new Vector3(x, y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (obj != null) obj.transform.position = originalPos;
        }

        private void OnRoundOverChanged(bool oldVal, bool newVal)
        {
            if (newVal)
            {
                foreach (var t in _teeth)
                    if (t != null) t.SetInteractable(false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitPlayerNameServerRpc(string name)
        {
            string clean = string.IsNullOrWhiteSpace(name) ? "Игрок 2" : name.Trim();
            if (clean.Length > 32) clean = clean.Substring(0, 32);
            _player2Name.Value = clean;
        }

        private void SyncNamesToGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
            GameManager.Instance.SetPlayers(_player1Name.Value.ToString(), _player2Name.Value.ToString());
            UpdateTurnIndicator();
        }

        private void UpdateTurnIndicator()
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null || GameManager.Instance == null) return;

            int idx = _currentPlayerIndex.Value;
            string playerName = GameManager.Instance.GetPlayerName(idx);
            Color playerColor = GameManager.Instance.GetPlayerColor(idx);

            int myIndex = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.LocalPlayerIndex : 0;
            string suffix = (idx == myIndex) ? " (ваш ход)" : " (ждём…)";

            _hud.SetTurnText(playerName + suffix, playerColor);
        }
    }
}
