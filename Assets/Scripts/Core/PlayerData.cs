using UnityEngine;

namespace PartyMiniGames.Core
{
    [System.Serializable]
    public class PlayerData
    {
        public string PlayerName;
        public Color PlayerColor;
        public int Score;

        public PlayerData(string name, Color color)
        {
            PlayerName = name;
            PlayerColor = color;
            Score = 0;
        }
    }
}