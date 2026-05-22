using UnityEngine;
using Unity.Netcode;

namespace PartyMiniGames.Network
{
    /// <summary>
    /// Маленький диагностический оверлей. Нажмите F1 в игре, чтобы увидеть текущее
    /// состояние сети (host/client, число подключённых, мой индекс, имена игроков).
    /// Полезно при демонстрации, чтобы показать, что мультиплеер реально работает.
    /// </summary>
    public class NetworkDebugOverlay : MonoBehaviour
    {
        private bool _show = false;
        private GUIStyle _style;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                _show = !_show;
        }

        private void OnGUI()
        {
            if (!_show) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    alignment = TextAnchor.UpperLeft,
                    richText = true,
                    padding = new RectOffset(10, 10, 10, 10),
                    normal = { textColor = Color.white }
                };
            }

            var nb = NetworkBootstrap.Instance;
            var nm = NetworkManager.Singleton;

            string sb = "<b>Network Debug (F1 to toggle)</b>\n";
            sb += $"Bootstrap: {(nb != null ? "OK" : "<color=red>missing</color>")}\n";
            if (nb != null)
            {
                sb += $"  IsHost = {nb.IsHost}\n";
                sb += $"  IsClient = {nb.IsClient}\n";
                sb += $"  LocalPlayerName = {nb.LocalPlayerName}\n";
                sb += $"  LocalPlayerIndex = {nb.LocalPlayerIndex}\n";
                sb += $"  JoinIP = {nb.JoinIpAddress}:{nb.JoinPort}\n";
                sb += $"  ConnectedClientCount = {nb.ConnectedClientCount}\n";
            }
            sb += $"NetworkManager: {(nm != null ? "OK" : "<color=red>missing</color>")}\n";
            if (nm != null)
            {
                sb += $"  IsServer = {nm.IsServer}\n";
                sb += $"  IsClient = {nm.IsClient}\n";
                sb += $"  IsHost = {nm.IsHost}\n";
                sb += $"  IsListening = {nm.IsListening}\n";
                sb += $"  IsConnectedClient = {nm.IsConnectedClient}\n";
                if (nm.IsListening && nm.LocalClient != null)
                    sb += $"  LocalClientId = {nm.LocalClientId}\n";
            }
            if (Core.GameManager.Instance != null)
            {
                sb += $"Players: P1 = {Core.GameManager.Instance.GetPlayerName(0)} | P2 = {Core.GameManager.Instance.GetPlayerName(1)}\n";
            }

            GUI.Box(new Rect(10, 10, 380, 290), sb, _style);
        }
    }
}
