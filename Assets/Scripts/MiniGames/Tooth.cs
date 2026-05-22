using UnityEngine;
using System;

namespace PartyMiniGames.MiniGames
{
    public class Tooth : MonoBehaviour
    {
        public int ToothIndex { get; set; }
        public bool IsPressed { get; private set; }
        public bool IsBadTooth { get; set; }

        public event Action<Tooth> OnToothClicked;

        public Sprite pressedSprite;

        private SpriteRenderer _spriteRenderer;
        private Sprite _normalSprite;
        private Color _normalColor = Color.white;
        private Color _badToothRevealColor = new Color(1f, 0.2f, 0.2f, 1f);
        private Vector3 _normalScale;
        private bool _interactable = true;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _normalScale = transform.localScale;
            if (_spriteRenderer != null)
                _normalSprite = _spriteRenderer.sprite;
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
        }

        private void OnMouseDown()
        {
            if (!_interactable || IsPressed) return;
            OnToothClicked?.Invoke(this);
        }

        private void OnMouseEnter()
        {
            if (!_interactable || IsPressed) return;
            transform.localScale = _normalScale * 1.1f;
        }

        private void OnMouseExit()
        {
            if (!_interactable || IsPressed) return;
            transform.localScale = _normalScale;
        }

        public void Press()
        {
            IsPressed = true;
            _interactable = false;
            if (_spriteRenderer != null)
            {
                if (pressedSprite != null)
                    _spriteRenderer.sprite = pressedSprite;
                else
                    _spriteRenderer.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
            transform.localScale = _normalScale * 0.85f;
        }

        public void RevealAsBad()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = _badToothRevealColor;
            transform.localScale = _normalScale * 1.3f;
        }

        public void SetColor(Color color)
        {
            _normalColor = color;
            if (_spriteRenderer != null && !IsPressed)
                _spriteRenderer.color = color;
        }
    }
}