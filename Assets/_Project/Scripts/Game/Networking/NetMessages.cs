using System.Collections.Generic;

namespace BooTrayal.Game.Networking
{
    /// <summary>Player-facing text for network reason keys. Replaced by Unity Localization tables in M6.</summary>
    public static class NetMessages
    {
        public const string ReleasesUrl = "https://github.com/LumielX/GameByClaude/releases/latest";
        const string Fallback = "connect_failed";

        static readonly Dictionary<string, (string th, string en)> Table = new Dictionary<string, (string, string)>
        {
            { "version_mismatch", ("เกมของคุณเป็นคนละเวอร์ชันกับเจ้าของห้อง กรุณาโหลดเวอร์ชันล่าสุด: " + ReleasesUrl,
                                   "Your game version does not match the host. Please download the latest version: " + ReleasesUrl) },
            { "room_full", ("ห้องเต็มแล้ว (สูงสุด 8 คน)", "The room is full (max 8 players).") },
            { "game_in_progress", ("ห้องนี้กำลังเล่นคืนนี้อยู่ รอให้จบคืนแล้วลองเข้าใหม่นะ", "A night is in progress. Try again when it ends.") },
            { "bad_payload", ("ข้อมูลการเชื่อมต่อผิดพลาด ลองเปิดเกมใหม่", "Connection data was invalid. Please restart the game.") },
            { "host_left", ("เจ้าของห้องออกจากเกมแล้ว กลับไปหน้าเมนู", "The host left the game. Back to the menu.") },
            { "code_invalid", ("รหัสห้องต้องเป็นตัวอักษร/ตัวเลข 6 ตัว", "Room codes are 6 letters or numbers.") },
            { "room_not_found", ("ไม่พบห้องนี้ ตรวจรหัสอีกครั้ง หรือห้องอาจปิดไปแล้ว", "Room not found. Check the code; the room may have closed.") },
            { "services_unavailable", ("เชื่อมต่อเซิร์ฟเวอร์ Unity ไม่ได้ ตรวจอินเทอร์เน็ตแล้วลองใหม่", "Could not reach Unity services. Check your internet and try again.") },
            { "connect_failed", ("เชื่อมต่อไม่สำเร็จ ลองใหม่อีกครั้ง", "Could not connect. Please try again.") },
        };

        public static IReadOnlyCollection<string> Keys => Table.Keys;

        public static string Thai(string key) => Lookup(key).th;

        public static string English(string key) => Lookup(key).en;

        static (string th, string en) Lookup(string key)
        {
            return key != null && Table.TryGetValue(key, out var v) ? v : Table[Fallback];
        }
    }
}
