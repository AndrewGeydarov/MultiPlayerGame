using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PartyMiniGames.Core;
using PartyMiniGames.UI;

namespace PartyMiniGames.MiniGames
{
    [ExecuteAlways]
    public class CrocodileGame : MonoBehaviour
    {
        [Header("\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438")]
        public int toothCount = 8;
        public float arcRadius = 3f;
        public float arcStartAngle = 10f;
        public float arcEndAngle = 170f;
        public bool useSimpleTeeth = false;
        public bool buildInEditMode = true;
        public bool useSceneLayoutOnly = true;

        [Header("\u0421\u043f\u0440\u0430\u0439\u0442\u044b")]
        public Sprite toothSprite;
        public Sprite toothPressedSprite;
        public Sprite crocodileMouthSprite;

        private List<Tooth> _teeth = new List<Tooth>();
        private int _badToothIndex;
        private int _currentPlayerIndex = 0;
        private int _safeTeethPressed = 0;
        private bool _roundOver = false;
        private HudUI _hud;
        private static Sprite _simpleToothSprite;
        private static Sprite _simplePressedToothSprite;

        private void Start()
        {
            if (!Application.isPlaying) return;

            // В сетевом режиме игру ведёт NetworkCrocodileGame — отключаем локальную, чтобы не было
            // двойной обработки кликов и конфликта логики.
            var nb = PartyMiniGames.Network.NetworkBootstrap.Instance;
            if (nb != null && (nb.IsHost || nb.IsClient))
            {
                enabled = false;
                return;
            }

            EnsureGameManager();
            LoadSpritesIfNeeded();
            _hud = FindAnyObjectByType<HudUI>();
            if (!TryBindExistingTeeth() && !useSceneLayoutOnly)
                CreateTeeth();
            if (_teeth.Count == 0) return;
            SelectBadTooth();
            UpdateTurnIndicator();
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            if (Application.isPlaying || !buildInEditMode || !gameObject.scene.IsValid()) return;
            if (useSceneLayoutOnly) return;
            EnsureEditorLayout();
        }
#endif

        private void LoadSpritesIfNeeded()
        {
            if (toothSprite == null) toothSprite = Resources.Load<Sprite>("Sprites/tooth");
            if (toothPressedSprite == null) toothPressedSprite = Resources.Load<Sprite>("Sprites/tooth_pressed");
            if (crocodileMouthSprite == null) crocodileMouthSprite = Resources.Load<Sprite>("Sprites/crocodile_mouth");
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

        private void CreateTeeth()
        {
            _teeth.Clear();

            for (int i = 0; i < toothCount; i++)
            {
                float t = (float)i / (toothCount - 1);
                float angle = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;

                float x = Mathf.Cos(angle) * arcRadius;
                float y = Mathf.Sin(angle) * arcRadius - 1.5f;

                GameObject toothObj = new GameObject($"Tooth_{i}");
                toothObj.transform.position = new Vector3(x, y, 0f);
                toothObj.transform.SetParent(transform);

                var sr = toothObj.AddComponent<SpriteRenderer>();
                sr.sprite = useSimpleTeeth ? GetSimpleToothSprite() : toothSprite;
                sr.color = Color.white;
                sr.sortingOrder = 1;
                toothObj.transform.localScale = useSimpleTeeth
                    ? new Vector3(0.34f, 0.42f, 1f)
                    : new Vector3(0.36f, 0.36f, 1f);

                var collider = toothObj.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.2f, 1.4f);

                var tooth = toothObj.AddComponent<Tooth>();
                tooth.ToothIndex = i;
                tooth.pressedSprite = useSimpleTeeth ? GetSimplePressedToothSprite() : toothPressedSprite;
                tooth.OnToothClicked -= OnToothClicked;
                tooth.OnToothClicked += OnToothClicked;

                _teeth.Add(tooth);
            }

            CreateCrocodileBackground();
        }

        private bool TryBindExistingTeeth()
        {
            _teeth.Clear();

            for (int i = 0; i < toothCount; i++)
            {
                Transform toothTransform = transform.Find($"Tooth_{i}");
                if (toothTransform == null) return false;

                var sr = toothTransform.GetComponent<SpriteRenderer>();
                var tooth = toothTransform.GetComponent<Tooth>();
                if (sr == null || tooth == null) return false;

                sr.sprite = useSimpleTeeth ? GetSimpleToothSprite() : toothSprite;
                sr.color = Color.white;
                sr.sortingOrder = 1;

                var collider = toothTransform.GetComponent<BoxCollider2D>();
                if (collider == null) collider = toothTransform.gameObject.AddComponent<BoxCollider2D>();

                tooth.ToothIndex = i;
                tooth.pressedSprite = useSimpleTeeth ? GetSimplePressedToothSprite() : toothPressedSprite;
                tooth.OnToothClicked -= OnToothClicked;
                tooth.OnToothClicked += OnToothClicked;

                _teeth.Add(tooth);
            }

            var bg = transform.Find("CrocodileMouth");
            if (bg == null)
            {
                CreateCrocodileBackground();
            }
            else
            {
                var bgSr = bg.GetComponent<SpriteRenderer>();
                if (bgSr == null) bgSr = bg.gameObject.AddComponent<SpriteRenderer>();
                bgSr.sprite = crocodileMouthSprite;
                bgSr.sortingOrder = 0;
            }

            return true;
        }

        private void EnsureEditorLayout()
        {
            LoadSpritesIfNeeded();

            if (TryBindExistingTeeth()) return;

            ClearGeneratedObjects();
            CreateTeeth();
        }

        [ContextMenu("Rebuild Crocodile Layout")]
        private void RebuildCrocodileLayout()
        {
            if (Application.isPlaying) return;

            LoadSpritesIfNeeded();
            ClearGeneratedObjects();
            CreateTeeth();
        }

        private void ClearGeneratedObjects()
        {
            var toDelete = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Tooth_") || child.name == "CrocodileMouth")
                    toDelete.Add(child.gameObject);
            }

            foreach (var obj in toDelete)
            {
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
        }

        private void CreateCrocodileBackground()
        {
            if (crocodileMouthSprite == null) return;

            GameObject bg = new GameObject("CrocodileMouth");
            bg.transform.position = new Vector3(0f, -0.5f, 0.1f);
            bg.transform.SetParent(transform);
            bg.transform.localScale = new Vector3(1.8f, 1.8f, 1f);

            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = crocodileMouthSprite;
            sr.sortingOrder = 0;
        }

        private void SelectBadTooth()
        {
            _badToothIndex = Random.Range(0, toothCount);
            _teeth[_badToothIndex].IsBadTooth = true;
        }

        private void OnToothClicked(Tooth tooth)
        {
            if (_roundOver) return;

            // Защита от повторных вызовов: если зуб уже нажат, выходим (например, если бы
            // обработчик клика когда-то вызвался дважды — раньше из-за этого менялся ход
            // не на того игрока).
            if (tooth.IsPressed) return;

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayToothClick();

            if (tooth.IsBadTooth)
            {
                StartCoroutine(HandleBite(tooth));
            }
            else
            {
                tooth.Press();
                _safeTeethPressed++;

                if (_safeTeethPressed >= toothCount - 1)
                {
                    int winnerIndex = _currentPlayerIndex;
                    StartCoroutine(HandleWin(winnerIndex));
                    return;
                }

                _currentPlayerIndex = 1 - _currentPlayerIndex;
                UpdateTurnIndicator();
            }
        }

        // Update НАМЕРЕННО оставлен пустым (раньше тут шёл повторный опрос мыши через
        // Physics2D.OverlapPoint, который вызывал OnToothClicked второй раз на каждый клик
        // и ломал переключение хода). Клики обрабатываются через Tooth.OnMouseDown → событие
        // OnToothClicked → метод выше.
        private void Update()
        {
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

        private static Sprite GetSimpleToothSprite()
        {
            if (_simpleToothSprite != null) return _simpleToothSprite;

            const int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = new Color(1f, 0.97f, 0.86f, 1f);
            var outline = new Color(0.2f, 0.14f, 0.06f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);
            }

            // Draw a rounded triangular tooth shape on a transparent texture.
            for (int y = 0; y < size; y++)
            {
                float t = Mathf.Clamp01(y / (float)(size - 1));
                float width = Mathf.Lerp(18f, 42f, 1f - Mathf.Pow(t, 1.35f));
                float cx = size * 0.5f;
                float left = cx - width * 0.5f;
                float right = cx + width * 0.5f;

                for (int x = 0; x < size; x++)
                {
                    if (x < left || x > right)
                        continue;

                    bool isOutline =
                        (x - left < 2f) ||
                        (right - x < 2f) ||
                        (y < 2) ||
                        Mathf.Abs(Mathf.Abs(x - cx) - width * 0.5f) < 2f;

                    tex.SetPixel(x, y, isOutline ? outline : fill);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _simpleToothSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.08f), 96f);
            return _simpleToothSprite;
        }

        private static Sprite GetSimplePressedToothSprite()
        {
            if (_simplePressedToothSprite != null) return _simplePressedToothSprite;

            const int width = 96;
            const int height = 64;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var gum = new Color(0.93f, 0.45f, 0.5f, 1f);
            var gumOutline = new Color(0.34f, 0.1f, 0.1f, 1f);
            var tooth = new Color(0.54f, 0.54f, 0.54f, 1f);
            var toothOutline = new Color(0.2f, 0.14f, 0.06f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, clear);
            }

            // Gum ellipse.
            Vector2 c = new Vector2(width * 0.5f, height * 0.28f);
            float rx = width * 0.43f;
            float ry = height * 0.24f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - c.x) / rx;
                    float dy = (y - c.y) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    tex.SetPixel(x, y, d > 0.88f ? gumOutline : gum);
                }
            }

            // Pressed tooth nub.
            for (int y = 16; y < height; y++)
            {
                float t = (y - 16f) / (height - 16f);
                float halfWidth = Mathf.Lerp(11f, 18f, 1f - t);
                float cx = width * 0.5f;
                float left = cx - halfWidth;
                float right = cx + halfWidth;
                for (int x = Mathf.FloorToInt(left); x <= Mathf.CeilToInt(right); x++)
                {
                    if (x < 0 || x >= width) continue;
                    bool isOutline = (x - left < 2f) || (right - x < 2f) || (y < 18);
                    tex.SetPixel(x, y, isOutline ? toothOutline : tooth);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _simplePressedToothSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.15f), 96f);
            return _simplePressedToothSprite;
        }

        private IEnumerator HandleBite(Tooth tooth)
        {
            _roundOver = true;
            SetAllTeethInteractable(false);

            tooth.RevealAsBad();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBite();

            int loserIndex = _currentPlayerIndex;
            int winnerIndex = 1 - loserIndex;
            string loserName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(loserIndex) : "?";

            if (_hud != null)
                _hud.ShowMessage($"\u0423\u043a\u0443\u0441! {loserName} \u043f\u0440\u043e\u0438\u0433\u0440\u0430\u043b!");

            yield return StartCoroutine(ShakeAnimation(tooth.gameObject, 0.8f));
            yield return new WaitForSeconds(1.5f);

            if (GameManager.Instance != null)
                GameManager.Instance.ReportResult(winnerIndex);
        }

        private IEnumerator HandleWin(int winnerIndex)
        {
            _roundOver = true;
            SetAllTeethInteractable(false);

            string winnerName = GameManager.Instance != null ? GameManager.Instance.GetPlayerName(winnerIndex) : "?";

            if (_hud != null)
                _hud.ShowMessage($"{winnerName} \u043f\u043e\u0431\u0435\u0434\u0438\u043b!");

            yield return new WaitForSeconds(2f);

            if (GameManager.Instance != null)
                GameManager.Instance.ReportResult(winnerIndex);
        }

        private IEnumerator ShakeAnimation(GameObject obj, float duration)
        {
            Vector3 originalPos = obj.transform.position;
            float elapsed = 0f;
            float intensity = 0.15f;

            while (elapsed < duration)
            {
                float x = originalPos.x + Random.Range(-intensity, intensity);
                float y = originalPos.y + Random.Range(-intensity, intensity);
                obj.transform.position = new Vector3(x, y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            obj.transform.position = originalPos;
        }

        private void SetAllTeethInteractable(bool interactable)
        {
            foreach (var tooth in _teeth)
                tooth.SetInteractable(interactable);
        }

        private void UpdateTurnIndicator()
        {
            if (_hud == null || GameManager.Instance == null) return;
            string playerName = GameManager.Instance.GetPlayerName(_currentPlayerIndex);
            Color playerColor = GameManager.Instance.GetPlayerColor(_currentPlayerIndex);
            _hud.SetTurnText(playerName, playerColor);
        }
    }
}