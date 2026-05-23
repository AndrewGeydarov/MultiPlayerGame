using UnityEngine;
using System.Collections;
using Unity.Netcode;
using PartyMiniGames.Core;
using PartyMiniGames.UI;
using PartyMiniGames.MiniGames;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Сетевая версия мини-игры "Башня".
    ///
    /// Исправление: теперь каждый дроп блока синхронизируется через ClientRpc,
    /// и башня соперника визуально обновляется на ОБОИХ экранах.
    ///
    /// Данные о дропе, которые нужно синхронизировать:
    ///   - dropX          — где находился блок по X в момент нажатия
    ///   - overlapWidth   — ширина итогового блока (после обрезки)
    ///   - overlapCenterX — центр итогового блока по X
    ///   - success        — попал ли игрок
    ///
    /// Вместо вызова DropBlock() на удалённой башне (который зависит от
    /// локального состояния физики/движения) мы передаём результат и
    /// воспроизводим его через PlaceBlockVisual().
    /// </summary>
    public class NetworkTowerGame : NetworkBehaviour
    {
        [Header("Настройки раунда")]
        public float roundDurationSeconds = 30f;

        private NetworkVariable<float> _timeRemaining =
            new NetworkVariable<float>(30f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<int> _blocksP1 =
            new NetworkVariable<int>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private NetworkVariable<int> _blocksP2 =
            new NetworkVariable<int>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<bool> _stoppedP1 =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> _stoppedP2 =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<bool> _roundOver =
            new NetworkVariable<bool>(false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<Unity.Collections.FixedString64Bytes> _player1Name =
            new NetworkVariable<Unity.Collections.FixedString64Bytes>("Игрок 1",
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<Unity.Collections.FixedString64Bytes> _player2Name =
            new NetworkVariable<Unity.Collections.FixedString64Bytes>("Игрок 2",
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private TowerInstance _tower1;
        private TowerInstance _tower2;
        private HudUI _hud;
        private bool _localPlayerStopped = false;
        private bool _bound = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _blocksP1.OnValueChanged += (_, __) => UpdateScoreDisplay();
            _blocksP2.OnValueChanged += (_, __) => UpdateScoreDisplay();
            _timeRemaining.OnValueChanged += (_, __) => UpdateTimerDisplay();
            _player1Name.OnValueChanged += (_, __) => SyncNamesToGameManager();
            _player2Name.OnValueChanged += (_, __) => SyncNamesToGameManager();

            BindSceneObjects();

            if (IsServer)
            {
                _timeRemaining.Value = roundDurationSeconds;
                _roundOver.Value = false;
                _blocksP1.Value = 0;
                _blocksP2.Value = 0;
                _stoppedP1.Value = false;
                _stoppedP2.Value = false;

                string hostName = NetworkBootstrap.Instance != null
                    ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 1";
                _player1Name.Value = string.IsNullOrEmpty(hostName) ? "Игрок 1" : hostName;
            }
            else
            {
                string myName = NetworkBootstrap.Instance != null
                    ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 2";
                SubmitPlayerNameServerRpc(myName);
            }

            SyncNamesToGameManager();
            UpdateScoreDisplay();
            UpdateTimerDisplay();
        }

        private void BindSceneObjects()
        {
            if (_bound) return;

            var localGame = FindAnyObjectByType<TowerGame>();
            if (localGame != null) localGame.enabled = false;

            _hud = FindAnyObjectByType<HudUI>();
            if (_hud != null) _hud.HideTurnLabel();

            var towers = FindObjectsByType<TowerInstance>(FindObjectsSortMode.None);
            System.Array.Sort(towers, (a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            if (towers.Length >= 1) _tower1 = towers[0];
            if (towers.Length >= 2) _tower2 = towers[1];

            Color c1 = GameManager.Instance != null
                ? GameManager.Instance.GetPlayerColor(0) : new Color(1f, 0.42f, 0.42f);
            Color c2 = GameManager.Instance != null
                ? GameManager.Instance.GetPlayerColor(1) : new Color(0.306f, 0.804f, 0.769f);

            if (_tower1 != null) _tower1.Initialize(0, c1);
            if (_tower2 != null) _tower2.Initialize(1, c2);

            _bound = true;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _bound = false;
            _tower1 = null;
            _tower2 = null;
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsSpawned) return;

            if (IsServer && !_roundOver.Value)
            {
                _timeRemaining.Value = Mathf.Max(0f, _timeRemaining.Value - Time.deltaTime);
                if (_timeRemaining.Value <= 0f)
                {
                    EndRound();
                    return;
                }
            }

            if (_roundOver.Value) return;
            if (_localPlayerStopped) return;

            if (PollDropInput())
            {
                int myIndex = NetworkBootstrap.Instance != null
                    ? NetworkBootstrap.Instance.LocalPlayerIndex : 0;
                TryDropLocal(myIndex);
            }
        }

        private bool PollDropInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.spaceKey.wasPressedThisFrame
                       || keyboard.enterKey.wasPressedThisFrame
                       || keyboard.numpadEnterKey.wasPressedThisFrame;
            }
#endif
            return Input.GetKeyDown(KeyCode.Space)
                   || Input.GetKeyDown(KeyCode.Return)
                   || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        /// <summary>
        /// Локально выполняем дроп блока на СВОЕЙ башне, затем передаём
        /// результат всем остальным через ServerRpc → ClientRpc, чтобы они
        /// тоже увидели изменения на экране.
        /// </summary>
        private void TryDropLocal(int myIndex)
        {
            TowerInstance myTower = myIndex == 0 ? _tower1 : _tower2;
            if (myTower == null) return;
            if (myTower.IsEliminated)
            {
                _localPlayerStopped = true;
                return;
            }

            // Выполняем дроп локально и получаем результат.
            DropResult result = myTower.DropBlockWithResult();

            // Отправляем серверу: счёт + визуальные данные для синхронизации соперника.
            ReportDropServerRpc(
                myIndex,
                result.Success,
                myTower.BlocksPlaced,
                result.PlacedCenterX,
                result.PlacedWidth,
                result.PlacedY,
                result.Color);

            if (!result.Success)
                _localPlayerStopped = true;
        }

        /// <summary>
        /// Сервер принимает результат дропа, обновляет счёт и рассылает
        /// ClientRpc для визуального обновления башни соперника у всех.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ReportDropServerRpc(
            int playerIndex, bool success, int blocksPlaced,
            float placedCenterX, float placedWidth, float placedY,
            Vector3 colorVec,
            ServerRpcParams rpcParams = default(ServerRpcParams))
        {
            if (_roundOver.Value) return;

            if (playerIndex == 0)
            {
                _blocksP1.Value = blocksPlaced;
                if (!success) _stoppedP1.Value = true;
            }
            else if (playerIndex == 1)
            {
                _blocksP2.Value = blocksPlaced;
                if (!success) _stoppedP2.Value = true;
            }

            // Рассылаем всем клиентам команду визуально обновить башню.
            // Вызывающий клиент пропустит это (уже сделал локально).
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            SyncBlockDropClientRpc(playerIndex, success, placedCenterX, placedWidth, placedY, colorVec, senderClientId);

            if (_stoppedP1.Value && _stoppedP2.Value)
                EndRound();
        }

        /// <summary>
        /// Приходит всем клиентам. Тот, кто сам сделал дроп, пропускает
        /// (у него башня уже обновлена локально). Остальные воспроизводят
        /// визуальный результат на башне противника.
        /// </summary>
        [ClientRpc]
        private void SyncBlockDropClientRpc(
            int playerIndex, bool success,
            float placedCenterX, float placedWidth, float placedY,
            Vector3 colorVec, ulong senderClientId)
        {
            // Пропускаем: тот клиент, который сам сделал дроп (уже обновлён).
            ulong myClientId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId : 0;
            if (myClientId == senderClientId) return;

            TowerInstance remoteTower = playerIndex == 0 ? _tower1 : _tower2;
            if (remoteTower == null) return;

            Color blockColor = new Color(colorVec.x, colorVec.y, colorVec.z, 1f);
            remoteTower.PlaceBlockVisual(placedCenterX, placedWidth, placedY, blockColor, success);
        }

        private void EndRound()
        {
            if (!IsServer || _roundOver.Value) return;
            _roundOver.Value = true;
            StartCoroutine(EndRoundFlow());
        }

        private IEnumerator EndRoundFlow()
        {
            int p1 = _blocksP1.Value;
            int p2 = _blocksP2.Value;
            ShowEndMessageClientRpc(p1, p2);

            yield return new WaitForSeconds(2.5f);

            if (p1 == p2)
                ReportDrawClientRpc();
            else
                ReportResultClientRpc(p1 > p2 ? 0 : 1);

            yield return null;

            if (NetworkBootstrap.Instance != null)
                NetworkBootstrap.Instance.HostReturnToMainMenu();
        }

        [ClientRpc]
        private void ShowEndMessageClientRpc(int p1, int p2)
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null) return;

            if (p1 == p2)
                _hud.ShowMessage($"Ничья: {p1} – {p2}");
            else
            {
                int winner = p1 > p2 ? 0 : 1;
                string name = GameManager.Instance != null
                    ? GameManager.Instance.GetPlayerName(winner) : "?";
                _hud.ShowMessage($"{name} победил! ({p1} – {p2})");
            }
        }

        [ClientRpc]
        private void ReportResultClientRpc(int winnerIndex)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReportResultLocal(winnerIndex);
        }

        [ClientRpc]
        private void ReportDrawClientRpc()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReportDrawLocal();
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
            GameManager.Instance.SetPlayers(
                _player1Name.Value.ToString(),
                _player2Name.Value.ToString());

            if (_tower1 != null)
                _tower1.Initialize(0, GameManager.Instance.GetPlayerColor(0));
            if (_tower2 != null)
                _tower2.Initialize(1, GameManager.Instance.GetPlayerColor(1));

            UpdateScoreDisplay();
        }

        private void UpdateScoreDisplay()
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null || GameManager.Instance == null) return;

            string n1 = GameManager.Instance.GetPlayerName(0);
            string n2 = GameManager.Instance.GetPlayerName(1);
            _hud.SetScoreText($"{n1}: {_blocksP1.Value} блок. | {n2}: {_blocksP2.Value} блок.");
        }

        private void UpdateTimerDisplay()
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null) return;

            float t = Mathf.Max(0f, _timeRemaining.Value);
            int totalSeconds = Mathf.FloorToInt(t);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            int tenths = Mathf.FloorToInt((t - totalSeconds) * 10f);
            _hud.SetTimerText($"Время: {minutes:00}:{seconds:00}.{tenths}");
            _hud.SetTimerColor(t <= 10f ? new Color(1f, 0.45f, 0.45f) : Color.white);
        }
    }

    /// <summary>
    /// Результат дропа блока — нужен для передачи визуальных данных по сети.
    /// </summary>
    public struct DropResult
    {
        public bool Success;
        public float PlacedCenterX;
        public float PlacedWidth;
        public float PlacedY;
        public Vector3 Color; // Vector3(r,g,b) — Color не сериализуется в RPC напрямую
    }
}
