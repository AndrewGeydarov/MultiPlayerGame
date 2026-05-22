using UnityEngine;
using System.Collections;
using PartyMiniGames.Core;
using PartyMiniGames.UI;

namespace PartyMiniGames.MiniGames
{
    [ExecuteAlways]
    public class TowerGame : MonoBehaviour
    {
        [Header("\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438")]
        public float towerSeparation = 12f;
        // 0 or below = no drop cap, round ends by timer or miss.
        public int maxDrops = 0;
        public float roundDurationSeconds = 20f;

        [Header("\u0421\u043f\u0440\u0430\u0439\u0442\u044b")]
        public Sprite blockSprite;
        public Sprite fallbackTowerBlockSprite;

        [Header("Scene visuals")]
        public Color backgroundColor = new Color(0.08f, 0.12f, 0.2f, 1f);
        public Color groundColor = new Color(0.16f, 0.23f, 0.34f, 1f);
        public Color midLineColor = new Color(1f, 1f, 1f, 0.35f);
        public Color sidePanelColor = new Color(0.25f, 0.35f, 0.5f, 0.16f);
        public Vector2 backgroundSize = new Vector2(28f, 16f);
        public Vector2 groundSize = new Vector2(9f, 0.9f);
        public Vector2 groundOffset = new Vector2(0f, -3.7f);
        public Vector2 sidePanelSize = new Vector2(4f, 14f);
        public float cameraY = -0.5f;
        public float cameraOrthoSize = 7f;
        public bool buildInEditMode = true;
        public bool useSceneLayoutOnly = true;

        private TowerInstance _tower1;
        private TowerInstance _tower2;
        private int _dropsRemaining;
        private bool _roundOver = false;
        private HudUI _hud;
        private static Sprite _solidSprite;
        private float _timeRemaining;

        private void Start()
        {
            if (!Application.isPlaying) return;

            // В сетевом режиме башней управляет NetworkTowerGame.
            var nb = PartyMiniGames.Network.NetworkBootstrap.Instance;
            if (nb != null && (nb.IsHost || nb.IsClient))
            {
                enabled = false;
                return;
            }

            EnsureGameManager();
            LoadSpritesIfNeeded();
            EnsureCameraSetup();
            _hud = FindAnyObjectByType<HudUI>();
            _dropsRemaining = maxDrops > 0 ? maxDrops : int.MaxValue;
            _timeRemaining = roundDurationSeconds;
            if (!useSceneLayoutOnly)
                CreateSceneVisuals();

            if (_hud != null)
            {
                _hud.HideTurnLabel();
            }

            CreateTowers();
            UpdateScoreDisplay();
            UpdateTimerDisplay();
            if (!useSceneLayoutOnly)
                CreateDividerIfMissing();
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
            if (blockSprite == null) blockSprite = Resources.Load<Sprite>("Sprites/block");
            if (blockSprite == null) blockSprite = Resources.Load<Sprite>("Sprites/tower_block");
            if (blockSprite == null && fallbackTowerBlockSprite != null) blockSprite = fallbackTowerBlockSprite;
        }

        private void CreateSceneVisuals()
        {
            if (transform.Find("BackgroundRect") == null)
            {
                CreateColoredRect("BackgroundRect", Vector3.zero, backgroundSize, backgroundColor, -10);
            }
            if (transform.Find("LeftPanel") == null)
            {
                CreateColoredRect("LeftPanel", new Vector3(-10f, 0f, 0f), sidePanelSize, sidePanelColor, -9);
            }
            if (transform.Find("RightPanel") == null)
            {
                CreateColoredRect("RightPanel", new Vector3(10f, 0f, 0f), sidePanelSize, sidePanelColor, -9);
            }

            if (transform.Find("Ground_P1") == null)
            {
                CreateColoredRect(
                    "Ground_P1",
                    new Vector3(-towerSeparation * 0.5f, groundOffset.y, 0f),
                    groundSize,
                    groundColor,
                    -1
                );
            }

            if (transform.Find("Ground_P2") == null)
            {
                CreateColoredRect(
                    "Ground_P2",
                    new Vector3(towerSeparation * 0.5f, groundOffset.y, 0f),
                    groundSize,
                    groundColor,
                    -1
                );
            }
        }

        private void EnsureCameraSetup()
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.orthographic = true;
            cam.orthographicSize = cameraOrthoSize;
            cam.backgroundColor = backgroundColor;

            Vector3 camPos = cam.transform.position;
            camPos.x = 0f;
            camPos.y = cameraY;
            camPos.z = -10f;
            cam.transform.position = camPos;
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

        private void CreateTowers()
        {
            if (_tower1 == null)
            {
                var existing = transform.Find("Tower_P1");
                if (existing != null) _tower1 = existing.GetComponent<TowerInstance>();
            }
            if (_tower2 == null)
            {
                var existing = transform.Find("Tower_P2");
                if (existing != null) _tower2 = existing.GetComponent<TowerInstance>();
            }

            if (_tower1 == null)
            {
                if (useSceneLayoutOnly) return;
                GameObject t1Obj = new GameObject("Tower_P1");
                t1Obj.transform.SetParent(transform);
                t1Obj.transform.localPosition = new Vector3(-towerSeparation / 2f, 0f, 0f);
                _tower1 = t1Obj.AddComponent<TowerInstance>();
            }

            if (_tower2 == null)
            {
                if (useSceneLayoutOnly) return;
                GameObject t2Obj = new GameObject("Tower_P2");
                t2Obj.transform.SetParent(transform);
                t2Obj.transform.localPosition = new Vector3(towerSeparation / 2f, 0f, 0f);
                _tower2 = t2Obj.AddComponent<TowerInstance>();
            }

            if (_tower1.blockSprite == null)
                _tower1.blockSprite = blockSprite;
            if (_tower2.blockSprite == null)
                _tower2.blockSprite = blockSprite;

            Color p1Color = GameManager.Instance != null ? GameManager.Instance.GetPlayerColor(0) : new Color(1f, 0.42f, 0.42f);
            Color p2Color = GameManager.Instance != null ? GameManager.Instance.GetPlayerColor(1) : new Color(0.306f, 0.804f, 0.769f);
            _tower1.Initialize(0, p1Color);
            _tower2.Initialize(1, p2Color);
        }

        private void EnsureEditorLayout()
        {
            LoadSpritesIfNeeded();
            EnsureCameraSetup();
            CreateSceneVisuals();
            CreateDividerIfMissing();
            CreateTowers();
        }

        private void CreateDividerIfMissing()
        {
            if (transform.Find("Divider") == null)
                CreateDivider();
        }

        [ContextMenu("Rebuild Tower Layout")]
        private void RebuildTowerLayout()
        {
            if (Application.isPlaying) return;
            ClearLayoutChildren();
            EnsureEditorLayout();
        }

        private void ClearLayoutChildren()
        {
            var toDelete = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
                toDelete.Add(child.gameObject);

            foreach (var go in toDelete)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            _tower1 = null;
            _tower2 = null;
        }

        private void CreateTowers_Legacy()
        {
            GameObject t1Obj = new GameObject("Tower_P1");
            t1Obj.transform.SetParent(transform);
            t1Obj.transform.localPosition = new Vector3(-towerSeparation / 2f, 0f, 0f);
            _tower1 = t1Obj.AddComponent<TowerInstance>();
            _tower1.blockSprite = blockSprite;
            _tower1.initialBlockWidth = 2.8f;
            _tower1.blockHeight = 0.7f;
            _tower1.oscillateSpeed = 4f;
            _tower1.oscillateRange = 1.8f;
            _tower1.baseY = -3.2f;

            Color p1Color = GameManager.Instance != null ? GameManager.Instance.GetPlayerColor(0) : new Color(1f, 0.42f, 0.42f);
            _tower1.Initialize(0, p1Color);

            GameObject t2Obj = new GameObject("Tower_P2");
            t2Obj.transform.SetParent(transform);
            t2Obj.transform.localPosition = new Vector3(towerSeparation / 2f, 0f, 0f);
            _tower2 = t2Obj.AddComponent<TowerInstance>();
            _tower2.blockSprite = blockSprite;
            _tower2.initialBlockWidth = 2.8f;
            _tower2.blockHeight = 0.7f;
            _tower2.oscillateSpeed = 4f;
            _tower2.oscillateRange = 1.8f;
            _tower2.baseY = -3.2f;

            Color p2Color = GameManager.Instance != null ? GameManager.Instance.GetPlayerColor(1) : new Color(0.306f, 0.804f, 0.769f);
            _tower2.Initialize(1, p2Color);
        }

        private void CreateDivider()
        {
            GameObject divider = new GameObject("Divider");
            divider.transform.SetParent(transform);
            divider.transform.localPosition = new Vector3(0f, 0f, 0f);

            var sr = divider.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(4, 512);
            Color[] pixels = new Color[4 * 512];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = midLineColor;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 512), new Vector2(0.5f, 0.5f), 32f);
            sr.sortingOrder = 10;
        }

        private void CreateColoredRect(string name, Vector3 localPos, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSolidSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _solidSprite;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (_roundOver) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining < 0f) _timeRemaining = 0f;
            UpdateTimerDisplay();
            if (_timeRemaining <= 0f)
            {
                StartCoroutine(HandleTimeUp());
                return;
            }

            PollDropInputs(out bool p1Drop, out bool p2Drop);

            if (p1Drop) HandleDrop(_tower1);
            if (p2Drop) HandleDrop(_tower2);
        }

        private void PollDropInputs(out bool p1Drop, out bool p2Drop)
        {
            p1Drop = false;
            p2Drop = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                p1Drop = keyboard.spaceKey.wasPressedThisFrame;
                p2Drop = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                return;
            }
#endif

            p1Drop = Input.GetKeyDown(KeyCode.Space);
            p2Drop = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private void HandleDrop(TowerInstance tower)
        {
            if (tower.IsEliminated) return;

            // Возвращаемое значение DropBlock игнорируем намеренно: даже если игрок
            // полностью промахнулся, мы НЕ заканчиваем раунд и НЕ показываем "Промах".
            // Tower просто становится IsEliminated и перестаёт расти, а раунд продолжается
            // до окончания таймера.
            tower.DropBlock();

            _dropsRemaining--;
            UpdateScoreDisplay();

            if (maxDrops > 0 && _dropsRemaining <= 0)
            {
                StartCoroutine(HandleTimeUp());
                return;
            }
        }

        // Метод оставлен для совместимости, но больше не вызывается из игрового цикла:
        // "Промах" как сценарий завершения раунда удалён по требованию.
        private IEnumerator HandleMiss()
        {
            _roundOver = true;
            _tower1.Deactivate();
            _tower2.Deactivate();

            yield return new WaitForSeconds(1f);

            DetermineWinner();
        }

        private IEnumerator HandleTimeUp()
        {
            _roundOver = true;
            _tower1.Deactivate();
            _tower2.Deactivate();

            if (_hud != null)
                _hud.ShowMessage("\u0412\u0440\u0435\u043c\u044f \u0432\u044b\u0448\u043b\u043e!");

            yield return new WaitForSeconds(1.5f);

            DetermineWinner();
        }

        private void DetermineWinner()
        {
            if (GameManager.Instance == null) return;

            // По требованию: проигрыша нет. Победителя определяет ТОЛЬКО число поставленных блоков.
            // Если у обоих одинаково — ничья.
            int b1 = _tower1 != null ? _tower1.BlocksPlaced : 0;
            int b2 = _tower2 != null ? _tower2.BlocksPlaced : 0;

            if (b1 == b2)
            {
                GameManager.Instance.ReportDraw();
            }
            else
            {
                int winnerIndex = b1 > b2 ? 0 : 1;
                GameManager.Instance.ReportResult(winnerIndex);
            }
        }

        private void UpdateScoreDisplay()
        {
            if (_hud == null || GameManager.Instance == null) return;

            string name1 = GameManager.Instance.GetPlayerName(0);
            string name2 = GameManager.Instance.GetPlayerName(1);
            int b1 = _tower1 != null ? _tower1.BlocksPlaced : 0;
            int b2 = _tower2 != null ? _tower2.BlocksPlaced : 0;
            _hud.SetScoreText($"{name1}: {b1} блок. | {name2}: {b2} блок.");
        }

        private void UpdateTimerDisplay()
        {
            if (_hud == null) return;
            float t = Mathf.Max(0f, _timeRemaining);
            int totalSeconds = Mathf.FloorToInt(t);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            int tenths = Mathf.FloorToInt((t - totalSeconds) * 10f);

            _hud.SetTimerText($"Время: {minutes:00}:{seconds:00}.{tenths}");
            _hud.SetTimerColor(t <= 10f ? new Color(1f, 0.45f, 0.45f) : Color.white);
        }
    }
}