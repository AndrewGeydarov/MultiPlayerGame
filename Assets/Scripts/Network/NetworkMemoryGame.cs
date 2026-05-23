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
    /// Сетевая версия мини-игры "Память".
    ///
    /// Архитектура: server-authoritative.
    ///   - Сервер хранит маппинг карт (IconIndex для каждой позиции) и генерирует его.
    ///   - Клиент кликает карту → RequestFlipServerRpc(cardIndex).
    ///   - Сервер проверяет: чей ход, можно ли перевернуть → FlipCardClientRpc (всем).
    ///   - При совпадении: MatchClientRpc (всем), очки обновляются.
    ///   - При несовпадении: после паузы FlipBackClientRpc, ход меняется.
    ///   - В конце: ReportResultClientRpc / ReportDrawClientRpc.
    ///
    /// Имена игроков синхронизируются через NetworkVariable.
    /// </summary>
    public class NetworkMemoryGame : NetworkBehaviour
    {
        // ── Сетевые переменные ──────────────────────────────────────────────────

        private NetworkVariable<int> _currentPlayerIndex =
            new NetworkVariable<int>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<int> _pairsFoundP1 =
            new NetworkVariable<int>(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private NetworkVariable<int> _pairsFoundP2 =
            new NetworkVariable<int>(0,
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

        // ── Серверное состояние ─────────────────────────────────────────────────

        // Маппинг: cardIndex → iconIndex (только на сервере, генерируется один раз).
        private int[] _cardIcons;
        private int    _totalPairs;
        private int    _totalPairsFound;

        // Два выбранных индекса в текущем ходе (-1 = не выбран).
        private int _firstFlippedIndex  = -1;
        private int _secondFlippedIndex = -1;
        private bool _waitingForMismatch = false;

        // ── Клиентское состояние ────────────────────────────────────────────────

        private List<Card> _cards = new List<Card>();
        private HudUI _hud;
        private bool _bound = false;

        // ── OnNetworkSpawn ──────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _currentPlayerIndex.OnValueChanged += (_, __) => UpdateTurnIndicator();
            _pairsFoundP1.OnValueChanged       += (_, __) => UpdateScoreDisplay();
            _pairsFoundP2.OnValueChanged       += (_, __) => UpdateScoreDisplay();
            _player1Name.OnValueChanged        += (_, __) => SyncNamesToGameManager();
            _player2Name.OnValueChanged        += (_, __) => SyncNamesToGameManager();

            BindSceneObjects();

            if (IsServer)
            {
                string hostName = NetworkBootstrap.Instance != null
                    ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 1";
                _player1Name.Value = string.IsNullOrEmpty(hostName) ? "Игрок 1" : hostName;

                _currentPlayerIndex.Value = 0;
                _pairsFoundP1.Value = 0;
                _pairsFoundP2.Value = 0;
                _roundOver.Value = false;
                _firstFlippedIndex = -1;
                _secondFlippedIndex = -1;
                _totalPairsFound = 0;
                _waitingForMismatch = false;

                // Генерируем маппинг на сервере после короткой задержки,
                // чтобы карты успели появиться у клиентов.
                StartCoroutine(GenerateAndSendIconsDelayed());
            }
            else
            {
                string myName = NetworkBootstrap.Instance != null
                    ? NetworkBootstrap.Instance.LocalPlayerName : "Игрок 2";
                SubmitPlayerNameServerRpc(myName);
            }

            SyncNamesToGameManager();
            UpdateTurnIndicator();
            UpdateScoreDisplay();
        }

        // ── Привязка сцены ──────────────────────────────────────────────────────

        private void BindSceneObjects()
        {
            if (_bound) return;

            // Отключаем локальный MemoryGame, чтобы он не мешал.
            var localGame = FindAnyObjectByType<MemoryGame>();
            if (localGame != null) localGame.enabled = false;

            _hud = FindAnyObjectByType<HudUI>();

            // Собираем все карты в сцене (они уже расставлены MemoryGame'ом или сценой).
            _cards.Clear();
            var allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);

            // Сортируем по позиции (сверху-вниз, слева-направо) — детерминированный порядок.
            System.Array.Sort(allCards, (a, b) =>
            {
                float dy = b.transform.position.y - a.transform.position.y;
                if (Mathf.Abs(dy) > 0.1f) return dy > 0 ? -1 : 1;
                float dx = a.transform.position.x - b.transform.position.x;
                return dx < 0 ? -1 : (dx > 0 ? 1 : 0);
            });

            for (int i = 0; i < allCards.Length; i++)
            {
                allCards[i].SetInteractable(false); // пока не придут иконки с сервера
                _cards.Add(allCards[i]);
            }

            _bound = true;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _bound = false;
            _cards.Clear();
        }

        // ── Генерация иконок (сервер) ────────────────────────────────────────────

        private IEnumerator GenerateAndSendIconsDelayed()
        {
            // Ждём пока карты появятся в сцене (LocalGame их создаёт в Start).
            yield return new WaitForSeconds(0.5f);

            // Пересчитываем карты на сервере.
            var allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
            int cardCount = allCards.Length;
            if (cardCount == 0)
            {
                Debug.LogError("[NetworkMemoryGame] Карты не найдены в сцене!");
                yield break;
            }

            _totalPairs = cardCount / 2;

            // Генерируем перемешанные пары.
            var iconPool = new List<int>();
            for (int i = 0; i < _totalPairs; i++) { iconPool.Add(i); iconPool.Add(i); }
            for (int i = iconPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = iconPool[i]; iconPool[i] = iconPool[j]; iconPool[j] = tmp;
            }

            _cardIcons = iconPool.ToArray();

            // Передаём маппинг клиентам через ClientRpc.
            // int[] нельзя напрямую в ClientRpc — упакуем в строку через join.
            string iconsStr = string.Join(",", _cardIcons);
            SendIconsClientRpc(iconsStr);
        }

        [ClientRpc]
        private void SendIconsClientRpc(string iconsStr)
        {
            // Применяем иконки к картам.
            string[] parts = iconsStr.Split(',');
            int[] icons = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                int.TryParse(parts[i], out icons[i]);

            // Ищем спрайты в MemoryGame (он ещё существует в сцене, просто disabled).
            MemoryGame memGame = FindAnyObjectByType<MemoryGame>(FindObjectsInactive.Include);

            var allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
            System.Array.Sort(allCards, (a, b) =>
            {
                float dy = b.transform.position.y - a.transform.position.y;
                if (Mathf.Abs(dy) > 0.1f) return dy > 0 ? -1 : 1;
                float dx = a.transform.position.x - b.transform.position.x;
                return dx < 0 ? -1 : (dx > 0 ? 1 : 0);
            });

            _cards.Clear();
            for (int i = 0; i < allCards.Length && i < icons.Length; i++)
            {
                int iconIdx = icons[i];
                Sprite backSp  = memGame != null ? memGame.cardBackSprite  : null;
                Sprite frontSp = (memGame != null && memGame.cardFrontSprites != null
                                  && iconIdx < memGame.cardFrontSprites.Length)
                    ? memGame.cardFrontSprites[iconIdx] : null;

                allCards[i].Initialize(iconIdx, backSp, frontSp);
                allCards[i].SetInteractable(true);
                _cards.Add(allCards[i]);
            }

            // На сервере _cards тоже обновляется.
            if (IsServer && _cardIcons == null)
            {
                _cardIcons = icons;
                _totalPairs = icons.Length / 2;
            }

            UpdateTurnIndicator();
        }

        // ── Клик по карте ───────────────────────────────────────────────────────

        private void Update()
        {
            if (!Application.isPlaying || !IsSpawned) return;
            if (_roundOver.Value || _waitingForMismatch) return;

            if (!TryGetPointerDown(out Vector2 screenPos)) return;

            var cam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (cam == null) return;

            Vector3 world = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            Collider2D hit = Physics2D.OverlapPoint(world);
            if (hit == null) return;

            Card clicked = hit.GetComponent<Card>() ?? hit.GetComponentInParent<Card>();
            if (clicked == null) return;

            int idx = _cards.IndexOf(clicked);
            if (idx < 0) return;

            if (clicked.IsFlipped || clicked.IsMatched) return;

            // Проверяем, что это наш ход.
            int myIndex = NetworkBootstrap.Instance != null
                ? NetworkBootstrap.Instance.LocalPlayerIndex : 0;
            if (_currentPlayerIndex.Value != myIndex)
            {
                if (_hud != null)
                {
                    _hud.ShowMessage("Не ваш ход");
                    StartCoroutine(HideMessageAfter(0.7f));
                }
                return;
            }

            RequestFlipServerRpc(idx);
        }

        private bool TryGetPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
#endif
            if (Input.GetMouseButtonDown(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }
            return false;
        }

        // ── ServerRpc: запрос перевернуть карту ────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void RequestFlipServerRpc(int cardIndex,
            ServerRpcParams rpcParams = default(ServerRpcParams))
        {
            if (_roundOver.Value || _waitingForMismatch) return;
            if (cardIndex < 0 || _cardIcons == null || cardIndex >= _cardIcons.Length) return;

            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int senderPlayer = senderClientId == NetworkManager.ServerClientId ? 0 : 1;
            if (senderPlayer != _currentPlayerIndex.Value) return;

            // Карта уже выбрана?
            if (cardIndex == _firstFlippedIndex) return;

            if (_firstFlippedIndex < 0)
            {
                // Первый выбор хода.
                _firstFlippedIndex = cardIndex;
                FlipCardClientRpc(cardIndex);
            }
            else
            {
                // Второй выбор — проверяем совпадение.
                _secondFlippedIndex = cardIndex;
                FlipCardClientRpc(cardIndex);

                int icon1 = _cardIcons[_firstFlippedIndex];
                int icon2 = _cardIcons[_secondFlippedIndex];

                if (icon1 == icon2)
                {
                    // Совпадение!
                    _totalPairsFound++;
                    if (_currentPlayerIndex.Value == 0)
                        _pairsFoundP1.Value++;
                    else
                        _pairsFoundP2.Value++;

                    MatchCardsClientRpc(_firstFlippedIndex, _secondFlippedIndex,
                        _currentPlayerIndex.Value);

                    _firstFlippedIndex  = -1;
                    _secondFlippedIndex = -1;

                    if (_totalPairsFound >= _totalPairs)
                    {
                        _roundOver.Value = true;
                        StartCoroutine(EndRoundFlow());
                    }
                    // При совпадении ход остаётся у того же игрока — не меняем _currentPlayerIndex.
                }
                else
                {
                    // Несовпадение — ждём и переворачиваем обратно.
                    int f = _firstFlippedIndex;
                    int s = _secondFlippedIndex;
                    _firstFlippedIndex  = -1;
                    _secondFlippedIndex = -1;
                    _waitingForMismatch = true;

                    StartCoroutine(MismatchFlow(f, s));
                }
            }
        }

        private IEnumerator MismatchFlow(int idx1, int idx2)
        {
            yield return new WaitForSeconds(1.0f);
            FlipBackClientRpc(idx1, idx2);
            _currentPlayerIndex.Value = 1 - _currentPlayerIndex.Value;
            _waitingForMismatch = false;
        }

        private IEnumerator EndRoundFlow()
        {
            yield return new WaitForSeconds(1.5f);

            int p1 = _pairsFoundP1.Value;
            int p2 = _pairsFoundP2.Value;

            if (p1 == p2)
            {
                ShowMessageClientRpc("Ничья!");
                yield return new WaitForSeconds(1.5f);
                ReportDrawClientRpc();
            }
            else
            {
                int winner = p1 > p2 ? 0 : 1;
                string name = _player1Name.Value.ToString();
                if (winner == 1) name = _player2Name.Value.ToString();
                ShowMessageClientRpc($"{name} победил! ({p1} – {p2})");
                yield return new WaitForSeconds(1.5f);
                ReportResultClientRpc(winner);
            }

            yield return null;
            if (NetworkBootstrap.Instance != null)
                NetworkBootstrap.Instance.HostReturnToMainMenu();
        }

        // ── ClientRpc'ы: визуальные обновления ─────────────────────────────────

        [ClientRpc]
        private void FlipCardClientRpc(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count) return;
            var card = _cards[cardIndex];
            if (card == null || card.IsFlipped || card.IsMatched) return;
            card.FlipToFront();
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCardFlip();
        }

        [ClientRpc]
        private void FlipBackClientRpc(int idx1, int idx2)
        {
            FlipBackOne(idx1);
            FlipBackOne(idx2);
            // Блокировку снимаем после анимации через короткую паузу.
            StartCoroutine(ReenableCardsAfter(0.3f));
        }

        private void FlipBackOne(int idx)
        {
            if (idx < 0 || idx >= _cards.Count) return;
            var card = _cards[idx];
            if (card != null && !card.IsMatched) card.FlipToBack();
        }

        private IEnumerator ReenableCardsAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var c in _cards)
                if (c != null && !c.IsMatched) c.SetInteractable(true);
        }

        [ClientRpc]
        private void MatchCardsClientRpc(int idx1, int idx2, int playerIndex)
        {
            MatchOne(idx1, playerIndex);
            MatchOne(idx2, playerIndex);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCardMatch();
        }

        private void MatchOne(int idx, int playerIndex)
        {
            if (idx < 0 || idx >= _cards.Count) return;
            var card = _cards[idx];
            if (card == null) return;
            Color color = GameManager.Instance != null
                ? GameManager.Instance.GetPlayerColor(playerIndex) : Color.white;
            card.MarkMatched(playerIndex, color);
        }

        [ClientRpc]
        private void ShowMessageClientRpc(string msg)
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud != null) _hud.ShowMessage(msg);
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

        // ── Имена игроков ───────────────────────────────────────────────────────

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
            UpdateTurnIndicator();
            UpdateScoreDisplay();
        }

        // ── HUD ─────────────────────────────────────────────────────────────────

        private void UpdateTurnIndicator()
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null || GameManager.Instance == null) return;

            int idx = _currentPlayerIndex.Value;
            string name  = GameManager.Instance.GetPlayerName(idx);
            Color  color = GameManager.Instance.GetPlayerColor(idx);

            int myIndex = NetworkBootstrap.Instance != null
                ? NetworkBootstrap.Instance.LocalPlayerIndex : 0;
            string suffix = (idx == myIndex) ? " (ваш ход)" : " (ждём…)";

            _hud.SetTurnText(name + suffix, color);
        }

        private void UpdateScoreDisplay()
        {
            if (_hud == null) _hud = FindAnyObjectByType<HudUI>();
            if (_hud == null || GameManager.Instance == null) return;
            _hud.SetPairsScoreText(
                _pairsFoundP1.Value, _pairsFoundP2.Value,
                GameManager.Instance.GetPlayerName(0),
                GameManager.Instance.GetPlayerName(1));
        }

        private IEnumerator HideMessageAfter(float sec)
        {
            yield return new WaitForSeconds(sec);
            if (_hud != null) _hud.HideMessage();
        }
    }
}
