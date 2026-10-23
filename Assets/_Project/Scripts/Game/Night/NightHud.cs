using System.Linq;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Night
{
    /// <summary>
    /// Temporary IMGUI HUD for the night loop (timer, tasks, role, interact hint, start button, vote panel, summary).
    /// M6 replaces it with UI Toolkit + Localization.
    /// </summary>
    public class NightHud : MonoBehaviour
    {
        GUIStyle _label, _big;
        bool _cursorFreedForVote;

        void OnGUI()
        {
            var d = NightDirector.Instance;
            var net = NetworkManager.Singleton;
            if (d == null || net == null || !net.IsConnectedClient && !net.IsServer) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true, fontStyle = FontStyle.Bold };
            var me = net.LocalClient != null ? net.LocalClient.PlayerObject : null;
            var life = me != null ? me.GetComponent<PlayerLife>() : null;

            GUILayout.BeginArea(new Rect(Screen.width - 440, 20, 420, 360), GUI.skin.box);
            switch (d.Phase.Value)
            {
                case SessionPhase.Lobby:
                    GUILayout.Label($"ล็อบบี้ (Lobby) — ผู้เล่น {net.ConnectedClientsIds.Count} คน", _big);
                    if (net.IsServer)
                    {
                        if (GUILayout.Button("เริ่มเกม (Start)", GUILayout.Height(40))) d.StartGame();
                    }
                    else GUILayout.Label("รอ host กดเริ่มเกม (Waiting for host)", _label);
                    break;
                case SessionPhase.InNight:
                    DrawNight(d, life);
                    break;
                default:
                    GUILayout.Label(d.Phase.Value == SessionPhase.GameOver ? "จบเกม (Game over)" : "สรุปคืน (Summary)", _big);
                    GUILayout.Label(d.LastSummary, _label);
                    break;
            }
            GUILayout.EndArea();

            DrawRole(d);
            DrawMeeting(d, net, life);
            if (Time.time < d.MeetingResultUntil)
                GUI.Label(new Rect(Screen.width * 0.5f - 300, 90, 600, 60), d.LastMeetingResult, _big);
            var actions = me != null ? me.GetComponent<PlayerActions>() : null;
            if (actions != null && actions.Aimed != null)
                GUI.Label(new Rect(Screen.width * 0.5f - 150, Screen.height * 0.5f + 30, 300, 30), $"[E] {actions.Aimed.Label}", _label);
        }

        void DrawNight(NightDirector d, PlayerLife life)
        {
            int s = Mathf.CeilToInt(d.SecondsLeft.Value);
            GUILayout.Label($"คืนที่ {d.NightNumber.Value}/{d.NightsTotal.Value}   เวลา {s / 60}:{s % 60:00}", _big);
            GUILayout.Label($"ปิดผนึก (Seals): {d.SealsDone.Value}/{d.SealsTotal.Value}", _label);
            GUILayout.Label($"ของพิธี (Ritual items): {d.RitualDelivered.Value}/{d.RitualRequired.Value}" +
                            (d.SealsDone.Value >= d.SealsTotal.Value ? "  → ขนไปแท่นบูชาข้างศาล" : ""), _label);
            if (d.GateOpen.Value) GUILayout.Label("ประตูรั้วเปิดแล้ว! หนีออกไป (Gate open — escape!)", _big);
            if (life == null) return;
            if (life.IsMiniGhost) GUILayout.Label("คุณเป็นผีจิ๋ว: Space/Ctrl ลอย, Z บู้ — ให้เพื่อนพาดวงวิญญาณไปศาล", _label);
            else if (life.State.Value == LifeState.Escaped) GUILayout.Label("หนีรอดแล้ว! (Escaped)", _label);
            else if (life.Grabbed.Value) GUILayout.Label("ผีจับตัวไว้!!", _big);
        }

        void DrawRole(NightDirector d)
        {
            if (!d.HasRole || d.Phase.Value != SessionPhase.InNight) return;
            string text = d.MyRole == Role.Spy
                ? "คุณคือ ร่างทรง (Spy) — Q ป่วนปริศนาใกล้ ๆ, X ดับไฟโซน" +
                  (d.MyPartners.Count > 0 ? $"  เพื่อนร่างทรง: {string.Join(", ", d.MyPartners.Select(PlayerAvatar.NameOf))}" : "")
                : "คุณคือ ลูกทีม (Crew) — ปิดผนึก ทำพิธี แล้วหนี!";
            GUI.Label(new Rect(20, Screen.height - 50, Screen.width - 40, 40), text, _label);
        }

        void DrawMeeting(NightDirector d, NetworkManager net, PlayerLife life)
        {
            var phase = d.MeetPhase.Value;
            bool canVote = phase == MeetingPhase.Vote && life != null && life.IsAlive;
            if (canVote != _cursorFreedForVote)
            {
                _cursorFreedForVote = canVote;
                PlayerInputReader.SetCursorFree(canVote);
            }
            if (phase == MeetingPhase.None || phase == MeetingPhase.Closed) return;

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 220, 150, 440, 420), GUI.skin.box);
            int t = Mathf.CeilToInt(d.MeetTimeLeft.Value);
            GUILayout.Label(phase == MeetingPhase.Discuss ? $"ประชุม! คุยกัน {t} วิ (Discuss)" : $"โหวต {t} วิ (Vote)", _big);
            if (canVote)
            {
                var actions = net.LocalClient.PlayerObject.GetComponent<PlayerActions>();
                foreach (var other in FindObjectsByType<PlayerLife>().Where(p => p.IsAlive).OrderBy(p => p.OwnerClientId))
                    if (GUILayout.Button($"ไล่ {PlayerAvatar.NameOf(other.OwnerClientId)}", GUILayout.Height(32)))
                        actions.RequestVote(other.OwnerClientId);
                if (GUILayout.Button("ข้าม (Skip)", GUILayout.Height(32))) actions.RequestVote(null);
            }
            GUILayout.EndArea();
        }
    }
}
