using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PartyMiniGames.Core;
using PartyMiniGames.UI;

namespace PartyMiniGames.MiniGames
{
    [ExecuteAlways]
    public class MemoryGame : MonoBehaviour
    {
        [Header("\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0441\u0435\u0442\u043a\u0438")]
        public int columns = 4;
        public int rows = 3;
        public float cardSpacingX = 1.6f;
        public float cardSpacingY = 2.0f;
        public int pairCount = 6;

        [Header("\u0412\u0440\u0435\u043c\u044f")]
        public float mismatchRevealTime = 1f;
        public bool buildInEditMode = true;
        public bool useSceneLayoutOnly = true;

        [Header("\u0421\u043f\u0440\u0430\u0439\u0442\u044b")]
        public Sprite cardBackSprite;
        public Sprite[] cardFrontSprites;

        private List<Card> _cards = new List<Card>();
        private Card _firstFlipped;
        private Card _secondFlipped;
        private int _currentPlayerIndex = 0;
        private int[] _pairsFound = new int[2];
        private int _totalPairsFound = 0;
        private bool _roundOver = false;
        private bool _waitingForMismatch = false;
        private HudUI _hud;

        private void Start()
        {
            if (!Application.isPlaying) return;
            EnsureGameManager();
            LoadSpritesIfNeeded();
            EnsureValidPairCount();
            _hud = FindAnyObjectByType<HudUI>();
            if (!TryBindExistingGrid() && !useSceneLayoutOnly)
                CreateGrid();
            RandomizeExistingGridIcons();
            if (_cards.Count == 0) return;
            SetAllCardsInteractable(true);
            UpdateTurnIndicator();
            UpdateScoreDisplay();
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (Application.isPlaying || !buildInEditMode || !gameObject.scene.IsValid()) return;
            if (useSceneLayoutOnly) return;
            EnsureEditorLayout();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_roundOver || _waitingForMismatch) return;

            if (!TryGetPointerDown(out Vector2 screenPos)) return;

            var cam = GetGameplayCamera();
            if (cam == null) return;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            Collider2D hit = Physics2D.OverlapPoint(world);
            if (hit == null) return;

            Card clickedCard = hit.GetComponent<Card>();
            if (clickedCard == null)
                clickedCard = hit.GetComponentInParent<Card>();

            if (clickedCard != null)
                OnCardClicked(clickedCard);
        }

        private bool TryGetPointerDown(out Vector2 screenPos)
        {
            screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
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

        private Camera GetGameplayCamera()
        {
            if (Camera.main != null) return Camera.main;
            return FindAnyObjectByType<Camera>();
        }

        private void LoadSpritesIfNeeded()
        {
            if (cardBackSprite == null)
                cardBackSprite = Resources.Load<Sprite>("Sprites/card_back");

            if (cardFrontSprites == null || cardFrontSprites.Length == 0)
            {
                cardFrontSprites = new Sprite[]
                {
                    Resources.Load<Sprite>("Sprites/card_front_star"),
                    Resources.Load<Sprite>("Sprites/card_front_diamond"),
                    Resources.Load<Sprite>("Sprites/card_front_clover"),
                    Resources.Load<Sprite>("Sprites/card_front_heart"),
                    Resources.Load<Sprite>("Sprites/card_front_music"),
                    Resources.Load<Sprite>("Sprites/card_front_sun")
                };
            }
        }

        private void EnsureValidPairCount()
        {
            int maxByGrid = Mathf.Max(1, (columns * rows) / 2);
            int maxBySprites = (cardFrontSprites != null && cardFrontSprites.Length > 0) ? cardFrontSprites.Length : 1;
            pairCount = Mathf.Clamp(pairCount, 1, Mathf.Min(maxByGrid, maxBySprites));
        }

        private void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
                GameManager.Instance.SetPlayers("\u0418\u0433\u0440\u043e\u043a 1", "\u0418\u0433\u0440\u043e\u043a 2");
            }
        }

        private void CreateGrid()
        {
            _cards.Clear();

            List<int> iconIndices = new List<int>();
            for (int i = 0; i < pairCount; i++)
            {
                iconIndices.Add(i);
                iconIndices.Add(i);
            }

            for (int i = iconIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = iconIndices[i];
                iconIndices[i] = iconIndices[j];
                iconIndices[j] = temp;
            }

            float totalWidth = (columns - 1) * cardSpacingX;
            float totalHeight = (rows - 1) * cardSpacingY;
            float startX = -totalWidth / 2f;
            float startY = totalHeight / 2f - 0.5f;

            int cardIndex = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (cardIndex >= iconIndices.Count) break;

                    float x = startX + c * cardSpacingX;
                    float y = startY - r * cardSpacingY;

                    GameObject cardObj = new GameObject($"Card_{r}_{c}");
                    cardObj.transform.position = new Vector3(x, y, 0f);
                    cardObj.transform.SetParent(transform);

                    Card card = cardObj.AddComponent<Card>();
                    int iconIdx = iconIndices[cardIndex];
                    Sprite frontSprite = (cardFrontSprites != null && iconIdx < cardFrontSprites.Length)
                        ? cardFrontSprites[iconIdx] : null;
                    card.Initialize(iconIdx, cardBackSprite, frontSprite);
                    card.OnCardClicked -= OnCardClicked;
                    card.OnCardClicked += OnCardClicked;

                    _cards.Add(card);
                    cardIndex++;
                }
            }
        }

        private bool TryBindExistingGrid()
        {
            _cards.Clear();
            int expected = rows * columns;
            int found = 0;

            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Card_")) continue;
                var card = child.GetComponent<Card>();
                if (card == null) return false;
                _cards.Add(card);
                found++;
            }

            if (found != expected) return false;

            foreach (var card in _cards)
            {
                int iconIdx = Mathf.Clamp(card.IconIndex, 0, cardFrontSprites.Length - 1);
                Sprite frontSprite = (cardFrontSprites != null && iconIdx < cardFrontSprites.Length)
                    ? cardFrontSprites[iconIdx] : null;
                card.Initialize(iconIdx, cardBackSprite, frontSprite);
                card.OnCardClicked -= OnCardClicked;
                card.OnCardClicked += OnCardClicked;
            }

            return true;
        }

        private void RandomizeExistingGridIcons()
        {
            if (_cards == null || _cards.Count == 0) return;
            if (cardFrontSprites == null || cardFrontSprites.Length == 0) return;

            int cardCount = _cards.Count;
            int pairsNeeded = Mathf.Min(pairCount, cardCount / 2, cardFrontSprites.Length);
            if (pairsNeeded <= 0) return;

            var iconPool = new List<int>();
            for (int i = 0; i < pairsNeeded; i++)
            {
                iconPool.Add(i);
                iconPool.Add(i);
            }

            for (int i = iconPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = iconPool[i];
                iconPool[i] = iconPool[j];
                iconPool[j] = tmp;
            }

            int assignCount = Mathf.Min(iconPool.Count, _cards.Count);
            for (int i = 0; i < assignCount; i++)
            {
                int iconIdx = iconPool[i];
                _cards[i].Initialize(iconIdx, cardBackSprite, cardFrontSprites[iconIdx]);
                _cards[i].OnCardClicked -= OnCardClicked;
                _cards[i].OnCardClicked += OnCardClicked;
            }
        }

        private void EnsureEditorLayout()
        {
            LoadSpritesIfNeeded();
            if (!TryBindExistingGrid())
            {
                ClearGeneratedGrid();
                CreateGrid();
            }
        }

        [ContextMenu("Rebuild Memory Layout")]
        private void RebuildMemoryLayout()
        {
            if (Application.isPlaying) return;
            LoadSpritesIfNeeded();
            ClearGeneratedGrid();
            CreateGrid();
        }

        private void ClearGeneratedGrid()
        {
            _cards.Clear();
            var toDelete = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Card_"))
                    toDelete.Add(child.gameObject);
            }

            foreach (var go in toDelete)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        private void OnCardClicked(Card card)
        {
            if (_roundOver || _waitingForMismatch) return;
            if (card.IsFlipped || card.IsMatched) return;
            if (_firstFlipped != null && _secondFlipped != null) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCardFlip();

            card.FlipToFront();

            if (_firstFlipped == null)
            {
                _firstFlipped = card;
            }
            else
            {
                _secondFlipped = card;
                SetAllCardsInteractable(false);
                StartCoroutine(CheckMatch());
            }
        }

        private IEnumerator CheckMatch()
        {
            yield return new WaitForSeconds(0.3f);

            if (_firstFlipped.IconIndex == _secondFlipped.IconIndex)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayCardMatch();

                Color playerColor = GameManager.Instance != null
                    ? GameManager.Instance.GetPlayerColor(_currentPlayerIndex)
                    : Color.white;

                _firstFlipped.MarkMatched(_currentPlayerIndex, playerColor);
                _secondFlipped.MarkMatched(_currentPlayerIndex, playerColor);

                _pairsFound[_currentPlayerIndex]++;
                _totalPairsFound++;

                _firstFlipped = null;
                _secondFlipped = null;

                UpdateScoreDisplay();

                if (_totalPairsFound >= pairCount)
                {
                    StartCoroutine(HandleRoundEnd());
                    yield break;
                }

                SetAllCardsInteractable(true);
            }
            else
            {
                _waitingForMismatch = true;
                yield return new WaitForSeconds(mismatchRevealTime);

                _firstFlipped.FlipToBack();
                _secondFlipped.FlipToBack();

                _firstFlipped = null;
                _secondFlipped = null;
                _waitingForMismatch = false;

                _currentPlayerIndex = 1 - _currentPlayerIndex;
                UpdateTurnIndicator();

                yield return new WaitForSeconds(0.3f);
                SetAllCardsInteractable(true);
            }
        }

        private IEnumerator HandleRoundEnd()
        {
            _roundOver = true;
            SetAllCardsInteractable(false);

            yield return new WaitForSeconds(1f);

            if (GameManager.Instance == null) yield break;

            if (_pairsFound[0] == _pairsFound[1])
            {
                if (_hud != null)
                    _hud.ShowMessage("\u041d\u0438\u0447\u044c\u044f!");
                yield return new WaitForSeconds(1.5f);
                GameManager.Instance.ReportDraw();
            }
            else
            {
                int winnerIndex = _pairsFound[0] > _pairsFound[1] ? 0 : 1;
                string winnerName = GameManager.Instance.GetPlayerName(winnerIndex);

                if (_hud != null)
                    _hud.ShowMessage($"{winnerName} \u043f\u043e\u0431\u0435\u0434\u0438\u043b!");

                yield return new WaitForSeconds(1.5f);
                GameManager.Instance.ReportResult(winnerIndex);
            }
        }

        private void SetAllCardsInteractable(bool interactable)
        {
            foreach (var card in _cards)
            {
                if (!card.IsMatched)
                    card.SetInteractable(interactable);
            }
        }

        private void UpdateTurnIndicator()
        {
            if (_hud == null || GameManager.Instance == null) return;
            string playerName = GameManager.Instance.GetPlayerName(_currentPlayerIndex);
            Color playerColor = GameManager.Instance.GetPlayerColor(_currentPlayerIndex);
            _hud.SetTurnText(playerName, playerColor);
        }

        private void UpdateScoreDisplay()
        {
            if (_hud == null || GameManager.Instance == null) return;
            string name1 = GameManager.Instance.GetPlayerName(0);
            string name2 = GameManager.Instance.GetPlayerName(1);
            _hud.SetPairsScoreText(_pairsFound[0], _pairsFound[1], name1, name2);
        }
    }
}