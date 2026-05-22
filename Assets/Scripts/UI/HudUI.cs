using UnityEngine;
using UnityEngine.UIElements;
using PartyMiniGames.Core;

namespace PartyMiniGames.UI
{
    public class HudUI : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _root;
        private Label _turnLabel;
        private Label _scoreLabel;
        private Label _timerLabel;
        private VisualElement _messageOverlay;
        private Label _messageText;

        private void OnEnable()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) return;
            _root = _document.rootVisualElement;

            _turnLabel = _root.Q<Label>("turn-label");
            _scoreLabel = _root.Q<Label>("score-label");
            _timerLabel = _root.Q<Label>("timer-label");
            _messageOverlay = _root.Q<VisualElement>("message-overlay");
            _messageText = _root.Q<Label>("message-text");

            HideMessage();
        }

        public void SetTurnText(string playerName, Color playerColor)
        {
            if (_turnLabel == null) return;
            _turnLabel.text = $"\u0425\u043e\u0434: {playerName}";
            _turnLabel.style.color = new StyleColor(playerColor);
        }

        public void SetScoreText(string scoreText)
        {
            if (_scoreLabel == null) return;
            _scoreLabel.text = scoreText;
        }

        public void SetTimerText(string timerText)
        {
            if (_timerLabel == null) return;
            _timerLabel.text = timerText;
        }

        public void SetTimerColor(Color color)
        {
            if (_timerLabel == null) return;
            _timerLabel.style.color = new StyleColor(color);
        }

        public void SetTowerScoreText(float height1, float height2, string name1, string name2, Color color1, Color color2)
        {
            if (_scoreLabel == null) return;
            int p1Blocks = Mathf.RoundToInt(height1);
            int p2Blocks = Mathf.RoundToInt(height2);
            _scoreLabel.text = $"{name1}: {p1Blocks} блок. | {name2}: {p2Blocks} блок.";
        }

        public void SetPairsScoreText(int pairs1, int pairs2, string name1, string name2)
        {
            if (_scoreLabel == null) return;
            _scoreLabel.text = $"\u041f\u0430\u0440\u044b: {name1} {pairs1} - {pairs2} {name2}";
        }

        public void ShowMessage(string message)
        {
            if (_messageOverlay == null || _messageText == null) return;
            _messageText.text = message;
            _messageOverlay.style.display = DisplayStyle.Flex;
        }

        public void HideMessage()
        {
            if (_messageOverlay == null) return;
            _messageOverlay.style.display = DisplayStyle.None;
        }

        public void HideTurnLabel()
        {
            if (_turnLabel == null) return;
            _turnLabel.style.display = DisplayStyle.None;
        }

        public void ShowTurnLabel()
        {
            if (_turnLabel == null) return;
            _turnLabel.style.display = DisplayStyle.Flex;
        }
    }
}