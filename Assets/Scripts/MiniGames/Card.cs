using UnityEngine;
using System;
using System.Collections;

namespace PartyMiniGames.MiniGames
{
    public class Card : MonoBehaviour
    {
        public int IconIndex { get; set; }
        public bool IsFlipped { get; private set; }
        public bool IsMatched { get; private set; }
        public int MatchedByPlayer { get; private set; } = -1;

        public event Action<Card> OnCardClicked;

        private SpriteRenderer _backRenderer;
        private SpriteRenderer _frontRenderer;
        private bool _interactable = true;
        private bool _animating = false;

        public void Initialize(int iconIndex, Sprite backSprite, Sprite frontSprite)
        {
            IconIndex = iconIndex;
            IsFlipped = false;
            IsMatched = false;
            MatchedByPlayer = -1;
            _interactable = true;
            _animating = false;

            var toDelete = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
                toDelete.Add(child.gameObject);
            foreach (var go in toDelete)
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }

            float cardScale = 0.18f;

            GameObject backObj = new GameObject("Back");
            backObj.transform.SetParent(transform, false);
            backObj.transform.localScale = new Vector3(cardScale, cardScale, 1f);
            _backRenderer = backObj.AddComponent<SpriteRenderer>();
            _backRenderer.sprite = backSprite;
            _backRenderer.sortingOrder = 2;

            GameObject frontObj = new GameObject("Front");
            frontObj.transform.SetParent(transform, false);
            _frontRenderer = frontObj.AddComponent<SpriteRenderer>();
            _frontRenderer.sprite = frontSprite;
            _frontRenderer.sortingOrder = 2;

            float frontScale = cardScale;
            if (backSprite != null && frontSprite != null)
            {
                float backW = backSprite.bounds.size.x;
                float frontW = frontSprite.bounds.size.x;
                if (frontW > 0f) frontScale = cardScale * (backW / frontW) * 0.9f;
            }
            frontObj.transform.localScale = new Vector3(frontScale, frontScale, 1f);

            frontObj.SetActive(false);

            var collider = GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.2f, 1.6f);
        }

        private void OnMouseDown() { }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
        }

        public void FlipToFront()
        {
            if (IsFlipped) return;
            StartCoroutine(FlipAnimation(true));
        }

        public void FlipToBack()
        {
            if (!IsFlipped) return;
            StartCoroutine(FlipAnimation(false));
        }

        private IEnumerator FlipAnimation(bool toFront)
        {
            _animating = true;
            float duration = 0.25f;
            float elapsed = 0f;
            Vector3 originalScale = transform.localScale;

            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                float scaleX = Mathf.Lerp(1f, 0f, t);
                transform.localScale = new Vector3(scaleX * originalScale.x, originalScale.y, originalScale.z);
                yield return null;
            }

            transform.localScale = new Vector3(0f, originalScale.y, originalScale.z);

            if (toFront)
            {
                _backRenderer.gameObject.SetActive(false);
                _frontRenderer.gameObject.SetActive(true);
                IsFlipped = true;
            }
            else
            {
                _backRenderer.gameObject.SetActive(true);
                _frontRenderer.gameObject.SetActive(false);
                IsFlipped = false;
            }

            elapsed = 0f;
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                float scaleX = Mathf.Lerp(0f, 1f, t);
                transform.localScale = new Vector3(scaleX * originalScale.x, originalScale.y, originalScale.z);
                yield return null;
            }

            transform.localScale = originalScale;
            _animating = false;
        }

        public void MarkMatched(int playerIndex, Color playerColor)
        {
            IsMatched = true;
            MatchedByPlayer = playerIndex;
            _interactable = false;
        }

        private void OnMouseEnter() { }
        private void OnMouseExit() { }
    }
}