using UnityEngine;
using System.Collections.Generic;

namespace PartyMiniGames.MiniGames
{
    public class TowerInstance : MonoBehaviour
    {
        public int PlayerIndex { get; set; }
        public float TowerHeight { get; private set; }
        public bool IsEliminated { get; private set; }
        public int BlocksPlaced => _blocksPlaced;

        [Header("\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438")]
        public float initialBlockWidth = 3f;
        public float blockHeight = 0.4f;
        public float oscillateSpeed = 3f;
        public float baseY = -4f;
        public bool useSolidColorBlocks = true;

        public Sprite blockSprite;

        private float _currentBlockWidth;
        private float _currentBlockCenterX;
        private GameObject _movingBlock;
        private SpriteRenderer _movingBlockRenderer;
        private float _oscillateDirection = 1f;
        [Header("Input and movement")]
        public float oscillateRange = 3.5f;

        private float _oscillateRange = 3.5f;
        private List<GameObject> _placedBlocks = new List<GameObject>();
        private bool _active = true;
        private int _blocksPlaced = 0;
        private Color _blockColor;
        private static Sprite _solidBlockSprite;
        private GameObject _baseBlock;
        private float _baseTopY;

        public void Initialize(int playerIndex, Color color)
        {
            ClearGeneratedBlocks(keepBase: true);
            PlayerIndex = playerIndex;
            _blockColor = color;
            _currentBlockWidth = initialBlockWidth;
            _currentBlockCenterX = 0f;
            TowerHeight = 0f;
            IsEliminated = false;
            _blocksPlaced = 0;
            _oscillateRange = Mathf.Max(0.25f, oscillateRange);

            if (!TryBindExistingBaseBlock())
                CreateBaseBlock();
            SpawnMovingBlock();
        }

        private void ClearGeneratedBlocks(bool keepBase)
        {
            _placedBlocks.Clear();
            _movingBlock = null;
            _movingBlockRenderer = null;
            _baseBlock = null;

            var toDelete = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (keepBase && child.name == "Base")
                {
                    _baseBlock = child.gameObject;
                    continue;
                }
                toDelete.Add(child.gameObject);
            }

            foreach (var go in toDelete)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
        }

        private bool TryBindExistingBaseBlock()
        {
            if (_baseBlock == null)
            {
                var existing = transform.Find("Base");
                if (existing != null) _baseBlock = existing.gameObject;
            }
            if (_baseBlock == null) return false;

            var sr = _baseBlock.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (sr.sprite == null)
                    sr.sprite = GetBlockSprite();
                ConfigureRendererForPixelArt(sr);
            }

            _currentBlockCenterX = _baseBlock.transform.localPosition.x;
            baseY = _baseBlock.transform.localPosition.y;

            float measuredWidth = initialBlockWidth;
            if (sr != null && sr.bounds.size.x > 0.001f)
                measuredWidth = sr.bounds.size.x;
            else
                measuredWidth = Mathf.Max(0.1f, _baseBlock.transform.localScale.x);

            _currentBlockWidth = measuredWidth;
            initialBlockWidth = measuredWidth;

            float baseHeight = blockHeight * 0.5f;
            _baseTopY = _baseBlock.transform.localPosition.y + baseHeight * 0.5f;

            var collider = _baseBlock.GetComponent<BoxCollider2D>();
            if (collider == null) collider = _baseBlock.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(_currentBlockWidth, baseHeight);
            collider.offset = Vector2.zero;
            return true;
        }

        private void CreateBaseBlock()
        {
            GameObject baseBlock = new GameObject("Base");
            baseBlock.transform.SetParent(transform);
            baseBlock.transform.localPosition = new Vector3(0f, baseY, 0f);
            _baseBlock = baseBlock;

            var sr = baseBlock.AddComponent<SpriteRenderer>();
            sr.sprite = GetBlockSprite();
            sr.color = new Color(_blockColor.r * 0.6f, _blockColor.g * 0.6f, _blockColor.b * 0.6f, 1f);
            sr.sortingOrder = 0;
            ConfigureRendererForPixelArt(sr);
            float baseHeight = blockHeight * 0.5f;
            SetBlockWorldSize(baseBlock.transform, sr.sprite, initialBlockWidth, baseHeight);
            _baseTopY = baseY + baseHeight * 0.5f;

            var collider = baseBlock.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(initialBlockWidth, baseHeight);
            collider.offset = Vector2.zero;
        }

        private void SpawnMovingBlock()
        {
            if (!_active) return;

            float yPos = _baseTopY + blockHeight * 0.5f + _blocksPlaced * blockHeight;
            float spawnHeight = yPos + 1.5f;

            _movingBlock = new GameObject($"Block_{_blocksPlaced}");
            _movingBlock.transform.SetParent(transform);
            _movingBlock.transform.localPosition = new Vector3(0f, spawnHeight, 0f);

            _movingBlockRenderer = _movingBlock.AddComponent<SpriteRenderer>();
            _movingBlockRenderer.sprite = GetBlockSprite();

            float hueShift = (_blocksPlaced * 0.05f) % 1f;
            Color.RGBToHSV(_blockColor, out float h, out float s, out float v);
            _movingBlockRenderer.color = Color.HSVToRGB((h + hueShift) % 1f, s, v);
            _movingBlockRenderer.sortingOrder = 1;
            ConfigureRendererForPixelArt(_movingBlockRenderer);
            SetBlockWorldSize(_movingBlock.transform, _movingBlockRenderer.sprite, _currentBlockWidth, blockHeight);

            var collider = _movingBlock.GetComponent<BoxCollider2D>();
            if (collider == null) collider = _movingBlock.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(_currentBlockWidth, blockHeight);
            collider.offset = Vector2.zero;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (!_active || _movingBlock == null) return;

            Vector3 pos = _movingBlock.transform.localPosition;
            pos.x += _oscillateDirection * oscillateSpeed * Time.deltaTime;

            float halfRange = _oscillateRange;
            if (pos.x > halfRange)
            {
                pos.x = halfRange;
                _oscillateDirection = -1f;
            }
            else if (pos.x < -halfRange)
            {
                pos.x = -halfRange;
                _oscillateDirection = 1f;
            }

            _movingBlock.transform.localPosition = pos;
        }

        public bool DropBlock()
        {
            if (!_active || _movingBlock == null) return false;

            float dropX = _movingBlock.transform.localPosition.x;
            float dropLeft = dropX - _currentBlockWidth / 2f;
            float dropRight = dropX + _currentBlockWidth / 2f;

            float baseLeft = _currentBlockCenterX - _currentBlockWidth / 2f;
            float baseRight = _currentBlockCenterX + _currentBlockWidth / 2f;

            if (_blocksPlaced > 0)
            {
                baseLeft = _currentBlockCenterX - _currentBlockWidth / 2f;
                baseRight = _currentBlockCenterX + _currentBlockWidth / 2f;
            }
            else
            {
                baseLeft = -initialBlockWidth / 2f;
                baseRight = initialBlockWidth / 2f;
            }

            float overlapLeft = Mathf.Max(dropLeft, baseLeft);
            float overlapRight = Mathf.Min(dropRight, baseRight);
            float overlapWidth = overlapRight - overlapLeft;

            if (overlapWidth <= 0.01f)
            {
                IsEliminated = true;
                _active = false;

                if (_movingBlockRenderer != null)
                    _movingBlockRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

                if (Core.AudioManager.Instance != null)
                    Core.AudioManager.Instance.PlayBlockCut();

                return false;
            }

            float overhangLeft = Mathf.Max(0f, baseLeft - dropLeft);
            float overhangRight = Mathf.Max(0f, dropRight - baseRight);

            if (overhangLeft > 0.01f || overhangRight > 0.01f)
            {
                CreateOverhangPiece(dropX, overhangLeft, overhangRight);

                if (Core.AudioManager.Instance != null)
                    Core.AudioManager.Instance.PlayBlockCut();
            }

            float yPos = _baseTopY + blockHeight * 0.5f + _blocksPlaced * blockHeight;
            float newCenterX = (overlapLeft + overlapRight) / 2f;

            _movingBlock.transform.localPosition = new Vector3(newCenterX, yPos, 0f);
            SetBlockWorldSize(_movingBlock.transform, _movingBlockRenderer.sprite, overlapWidth, blockHeight);
            var blockCollider = _movingBlock.GetComponent<BoxCollider2D>();
            if (blockCollider == null) blockCollider = _movingBlock.AddComponent<BoxCollider2D>();
            blockCollider.size = new Vector2(overlapWidth, blockHeight);
            blockCollider.offset = Vector2.zero;
            _placedBlocks.Add(_movingBlock);

            _currentBlockWidth = overlapWidth;
            _currentBlockCenterX = newCenterX;
            _blocksPlaced++;
            TowerHeight = _blocksPlaced * blockHeight;

            if (Core.AudioManager.Instance != null)
                Core.AudioManager.Instance.PlayBlockDrop();

            _movingBlock = null;
            _movingBlockRenderer = null;

            SpawnMovingBlock();
            return true;
        }

        private void CreateOverhangPiece(float dropX, float overhangLeft, float overhangRight)
        {
            float yPos = _baseTopY + blockHeight * 0.5f + _blocksPlaced * blockHeight;

            if (overhangRight > 0.01f)
            {
                GameObject piece = new GameObject("Overhang");
                piece.transform.SetParent(transform);
                float pieceX = dropX + _currentBlockWidth / 2f - overhangRight / 2f;
                piece.transform.localPosition = new Vector3(pieceX, yPos, 0f);
                piece.transform.localScale = new Vector3(overhangRight, blockHeight, 1f);
                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = GetBlockSprite();
                sr.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                sr.sortingOrder = 0;
                ConfigureRendererForPixelArt(sr);
                SetBlockWorldSize(piece.transform, sr.sprite, overhangRight, blockHeight);
                var rb = piece.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                Destroy(piece, 2f);
            }

            if (overhangLeft > 0.01f)
            {
                GameObject piece = new GameObject("Overhang");
                piece.transform.SetParent(transform);
                float pieceX = dropX - _currentBlockWidth / 2f + overhangLeft / 2f;
                piece.transform.localPosition = new Vector3(pieceX, yPos, 0f);
                piece.transform.localScale = new Vector3(overhangLeft, blockHeight, 1f);
                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = GetBlockSprite();
                sr.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                sr.sortingOrder = 0;
                ConfigureRendererForPixelArt(sr);
                SetBlockWorldSize(piece.transform, sr.sprite, overhangLeft, blockHeight);
                var rb = piece.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                Destroy(piece, 2f);
            }
        }

        private Sprite GetBlockSprite()
        {
            if (useSolidColorBlocks)
                return GetSolidBlockSprite();

            if (blockSprite != null) return blockSprite;
            int w = 32, h = 32;
            Texture2D tex = new Texture2D(w, h);
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite GetSolidBlockSprite()
        {
            if (_solidBlockSprite != null) return _solidBlockSprite;

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _solidBlockSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _solidBlockSprite;
        }

        private static void ConfigureRendererForPixelArt(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.maskInteraction = SpriteMaskInteraction.None;
        }

        private static void SetBlockWorldSize(Transform blockTransform, Sprite sprite, float targetWidth, float targetHeight)
        {
            float spriteWidth = 1f;
            float spriteHeight = 1f;

            if (sprite != null)
            {
                Vector2 size = sprite.bounds.size;
                if (size.x > 0.0001f) spriteWidth = size.x;
                if (size.y > 0.0001f) spriteHeight = size.y;
            }

            blockTransform.localScale = new Vector3(targetWidth / spriteWidth, targetHeight / spriteHeight, 1f);
        }

        public void Deactivate()
        {
            _active = false;
            if (_movingBlock != null)
                _movingBlock.SetActive(false);
        }

        /// <summary>
        /// Выполняет дроп и возвращает визуальные данные результата для сетевой синхронизации.
        /// Вызывается только на СВОЕЙ башне локально; результат передаётся через RPC сопернику.
        /// </summary>
        public Network.DropResult DropBlockWithResult()
        {
            var result = new Network.DropResult();

            if (!_active || _movingBlock == null)
            {
                result.Success = false;
                return result;
            }

            // Сохраняем текущий цвет блока перед дропом.
            Color blockColor = _movingBlockRenderer != null
                ? _movingBlockRenderer.color : _blockColor;
            result.Color = new Vector3(blockColor.r, blockColor.g, blockColor.b);

            bool success = DropBlock();
            result.Success = success;

            // После DropBlock() последний поставленный блок — последний в _placedBlocks.
            if (success && _placedBlocks.Count > 0)
            {
                var lastBlock = _placedBlocks[_placedBlocks.Count - 1];
                if (lastBlock != null)
                {
                    result.PlacedCenterX = lastBlock.transform.localPosition.x;
                    result.PlacedY       = lastBlock.transform.localPosition.y;
                    // Ширину вычисляем из localScale (SetBlockWorldSize устанавливает scale как width).
                    Sprite sp = GetBlockSprite();
                    float spriteW = (sp != null && sp.bounds.size.x > 0.0001f)
                        ? sp.bounds.size.x : 1f;
                    result.PlacedWidth = lastBlock.transform.localScale.x * spriteW;
                }
            }
            else if (!success)
            {
                result.PlacedCenterX = _movingBlock != null
                    ? _movingBlock.transform.localPosition.x : 0f;
                result.PlacedWidth   = _currentBlockWidth;
                result.PlacedY       = _baseTopY + blockHeight * 0.5f + _blocksPlaced * blockHeight;
            }

            return result;
        }

        /// <summary>
        /// Воспроизводит визуальный результат дропа на УДАЛЁННОЙ башне (башне соперника).
        /// Не выполняет физику/логику — только рисует блок по переданным координатам.
        /// Вызывается ClientRpc'ом на стороне соперника.
        /// </summary>
        public void PlaceBlockVisual(float centerX, float width, float y,
                                     Color blockColor, bool success)
        {
            if (!success)
            {
                // Промах у соперника — гасим его движущийся блок.
                if (_movingBlock != null && _movingBlockRenderer != null)
                {
                    _movingBlockRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    _active = false;
                    IsEliminated = true;
                }
                return;
            }

            // Убираем текущий движущийся блок (он будет пересоздан ниже как "поставленный").
            if (_movingBlock != null)
            {
                Destroy(_movingBlock);
                _movingBlock = null;
                _movingBlockRenderer = null;
            }

            // Создаём блок на переданных координатах.
            var placed = new GameObject($"BlockRemote_{_blocksPlaced}");
            placed.transform.SetParent(transform);
            placed.transform.localPosition = new Vector3(centerX, y, 0f);

            var sr = placed.AddComponent<SpriteRenderer>();
            sr.sprite = GetBlockSprite();
            sr.color = blockColor;
            sr.sortingOrder = 1;
            ConfigureRendererForPixelArt(sr);
            SetBlockWorldSize(placed.transform, sr.sprite, width, blockHeight);

            _placedBlocks.Add(placed);
            _currentBlockWidth  = width;
            _currentBlockCenterX = centerX;
            _blocksPlaced++;
            TowerHeight = _blocksPlaced * blockHeight;

            // Создаём следующий движущийся блок для визуализации.
            SpawnMovingBlock();
        }
    }
}