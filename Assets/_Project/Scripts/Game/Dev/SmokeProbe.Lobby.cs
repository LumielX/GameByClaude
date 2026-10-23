using System.Collections;
using System.Linq;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using UnityEngine;

namespace BooTrayal.Game.Dev
{
    /// <summary>
    /// Lobby bots for tools/unity/lobby-smoke.ps1. lobbyhost: readies up alone (Start must stay blocked by the client
    /// who is not ready), counts the client's chat lines, then kicks it. lobbyclient: sends two lines in one frame
    /// (the second must be dropped by the 1 s rate limit) and never readies.
    /// </summary>
    public partial class SmokeProbe
    {
        IEnumerator LobbyHost()
        {
            while (FindObjectsByType<PlayerLobby>().Count(p => p.IsSpawned) < 2) yield return null;
            var me = LocalPlayer.GetComponent<PlayerLobby>();
            me.RequestReady(true);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"PROBE_LOBBY allReady={D.AllReady}");
            D.StartGame();
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"PROBE_LOBBY phase={D.Phase.Value}");

            float end = Time.time + 15f;
            while (Time.time < end && !PlayerLobby.ChatLog.Any(l => l.From == 1)) yield return null;
            yield return new WaitForSeconds(2.5f);
            Debug.Log($"PROBE_CHAT count={PlayerLobby.ChatLog.Count(l => l.From == 1)}");
            ConnectionService.Instance.Kick(1);
        }

        IEnumerator LobbyClient()
        {
            yield return new WaitForSeconds(1f);
            var me = LocalPlayer.GetComponent<PlayerLobby>();
            me.SendChat("first line");
            me.SendChat("second line, too soon");
            Debug.Log("PROBE_CHAT_SENT count=2");
        }
    }
}
