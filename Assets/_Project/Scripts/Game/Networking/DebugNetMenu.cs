using UnityEngine;

namespace BooTrayal.Game.Networking
{
    /// <summary>Temporary IMGUI menu for network testing. Replaced by the UI Toolkit menus in M6.</summary>
    public class DebugNetMenu : MonoBehaviour
    {
        string _code = "";
        GUIStyle _label;

        void OnGUI()
        {
            var svc = ConnectionService.Instance;
            if (svc == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };

            GUILayout.BeginArea(new Rect(20, 20, 520, 420), GUI.skin.box);
            GUILayout.Label($"BOO-TRAYAL v{Application.version} — {svc.PlayerDisplayName}", _label);
            GUILayout.Label($"สถานะ: {svc.Status}", _label);
            if (!string.IsNullOrEmpty(svc.RoomCode))
                GUILayout.Label($"รหัสห้อง: {svc.RoomCode}   ผู้เล่น: {svc.ConnectedCount}", _label);
            if (!string.IsNullOrEmpty(svc.LastMessageKey))
                GUILayout.Label(NetMessages.Thai(svc.LastMessageKey), _label);

            bool idle = svc.Status == NetStatus.Offline || svc.Status == NetStatus.Failed;
            GUI.enabled = idle;
            if (GUILayout.Button("สร้างห้อง (Create Room)", GUILayout.Height(36))) _ = svc.CreateRoomAsync(8);
            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code, 12, GUILayout.Height(36), GUILayout.Width(200));
            if (GUILayout.Button("เข้าห้อง (Join)", GUILayout.Height(36))) _ = svc.JoinRoomAsync(_code);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("เล่นคนเดียว (Solo)", GUILayout.Height(36))) svc.StartLocalHost();
            GUI.enabled = !idle;
            if (GUILayout.Button("ออกจากห้อง (Leave)", GUILayout.Height(36))) _ = svc.LeaveAsync();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(svc.RoomCode) && svc.Status == NetStatus.Hosting &&
                GUILayout.Button("คัดลอกรหัส (Copy code)", GUILayout.Height(30)))
                GUIUtility.systemCopyBuffer = svc.RoomCode;
            GUILayout.EndArea();
        }
    }
}
