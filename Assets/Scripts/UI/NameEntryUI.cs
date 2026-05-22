using UnityEngine;
using UnityEngine.UIElements;
using PartyMiniGames.Core;

namespace PartyMiniGames.UI
{
    public class NameEntryUI : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private TextField _inputPlayer1;
        private TextField _inputPlayer2;
        private VisualElement _overlayRoot;
        private VisualElement _container;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) return;
            _root = _document.rootVisualElement;

            _inputPlayer1 = _root.Q<TextField>("input-player1");
            _inputPlayer2 = _root.Q<TextField>("input-player2");
            _overlayRoot = _root.Q<VisualElement>("root");
            _container = _root.Q<VisualElement>("container");

            ApplySafeLayout();

            var btnStart = _root.Q<Button>("btn-start");
            btnStart?.RegisterCallback<ClickEvent>(evt => OnStartClicked());

            var btnBack = _root.Q<Button>("btn-back");
            btnBack?.RegisterCallback<ClickEvent>(evt => OnBackClicked());

            _root.style.display = DisplayStyle.None;
        }

        private void ApplySafeLayout()
        {
            if (_root != null)
            {
                _root.style.width = new Length(100, LengthUnit.Percent);
                _root.style.height = new Length(100, LengthUnit.Percent);
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.style.width = new Length(100, LengthUnit.Percent);
                _overlayRoot.style.height = new Length(100, LengthUnit.Percent);
                _overlayRoot.style.alignItems = Align.Center;
                _overlayRoot.style.justifyContent = Justify.Center;
                _overlayRoot.style.position = Position.Absolute;
                _overlayRoot.style.left = 0;
                _overlayRoot.style.top = 0;
                _overlayRoot.style.right = 0;
                _overlayRoot.style.bottom = 0;
            }

            if (_container != null)
            {
                _container.style.width = 660;
                _container.style.minHeight = 430;
            }
        }

        private void OnStartClicked()
        {
            if (GameManager.Instance == null) return;

            string name1 = _inputPlayer1?.value?.Trim();
            string name2 = _inputPlayer2?.value?.Trim();

            if (string.IsNullOrEmpty(name1)) name1 = "\u0418\u0433\u0440\u043e\u043a 1";
            if (string.IsNullOrEmpty(name2)) name2 = "\u0418\u0433\u0440\u043e\u043a 2";

            GameManager.Instance.SetPlayers(name1, name2);
            GameManager.Instance.ConfirmNamesAndStart();
        }

        private void OnBackClicked()
        {
            Hide();
            var mainMenuUI = FindAnyObjectByType<MainMenuUI>();
            if (mainMenuUI != null)
            {
                mainMenuUI.Show();
                GameManager.Instance.ReturnToMenu();
            }
        }

        public void Show()
        {
            if (_root != null)
            {
                ApplySafeLayout();
                _root.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }
    }
}