using System;
using System.Collections;
using BooTrayal.Core.Localization;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.UI
{
    /// <summary>
    /// -uismoke: opens every screen and overlay in Thai and English (menus, error, lobby + chat + ready, HUD, pause,
    /// meeting vote, night summary, game-over ranking) and logs UI_SCREEN markers; with -uishots DIR also saves PNGs.
    /// Plays a real Solo game on the local transport to reach the in-game screens.
    /// </summary>
    public static class UISmokeRunner
    {
        static readonly Language[] Languages = { Language.Thai, Language.English };

        public static IEnumerator Run(UIRoot root)
        {
            var svc = ConnectionService.Instance;
            string shots = svc.Args.UiShots;
            if (!string.IsNullOrEmpty(shots)) System.IO.Directory.CreateDirectory(shots);
            yield return new WaitForSeconds(1.5f);
            Debug.Log("UI_SMOKE_START");

            foreach (var name in new[] { "title", "play", "create", "settings", "howto" })
            {
                if (name == "settings" || name == "howto") root.OpenSide(name);
                else root.GoMenu(name);
                yield return Visit(root, name, shots);
                if (name == "settings" || name == "howto") root.CloseSide();
            }
            root.GoMenu("play");
            _ = svc.JoinRoomAsync("AB1");
            yield return null;
            yield return null;
            Debug.Log($"UI_BADCODE error={root.ErrorKey}");
            root.CloseError();
            root.ShowError("version_mismatch");
            yield return Visit(root, "error", shots);
            root.CloseError();

            svc.HostConfig = new GameConfig { MaxPlayers = 1, SpyEnabled = false };
            svc.StartLocalHost();
            yield return Until(() => UIRoot.InSession && NightDirector.Instance.Phase.Value == SessionPhase.Lobby && Local<PlayerLobby>() != null, 30f);
            yield return Visit(root, "lobby", shots);
            Cursor("lobby");

            var lobby = Local<PlayerLobby>();
            int before = PlayerLobby.ChatLog.Count;
            lobby.SendChat("  สวัสดี hello ​ ");
            yield return Until(() => PlayerLobby.ChatLog.Count > before, 10f);
            Debug.Log(PlayerLobby.ChatLog.Count > before ? $"UI_CHAT_OK text={PlayerLobby.ChatLog[PlayerLobby.ChatLog.Count - 1].Text}" : "UI_CHAT_FAILED");
            lobby.RequestReady(true);
            yield return Until(() => NightDirector.Instance.AllReady, 10f);
            Debug.Log($"UI_READY allReady={NightDirector.Instance.AllReady}");
            NightDirector.Instance.StartGame();
            yield return Until(() => NightDirector.Instance.InNight, 15f);
            yield return new WaitForSeconds(1.5f);
            yield return Visit(root, "hud", shots);
            Cursor("hud");

            root.Paused = true;
            yield return Visit(root, "pause", shots);
            Cursor("pause");
            root.Paused = false;

            NightHooks.RingBell?.Invoke(NetworkManager.Singleton.LocalClientId);
            yield return Until(() => NightDirector.Instance.MeetingRunning, 10f);
            yield return Visit(root, "vote", shots);
            Cursor("vote");
            yield return Until(() => NightDirector.Instance.MeetPhase.Value == MeetingPhase.Vote, 20f);
            Local<PlayerActions>().RequestVote(null);
            yield return Until(() => !NightDirector.Instance.MeetingRunning, 10f);
            Debug.Log("UI_VOTE_DONE");

            yield return Until(() => NightDirector.Instance.Phase.Value == SessionPhase.Summary, 90f);
            yield return new WaitForSeconds(0.5f);
            yield return Visit(root, "summary", shots, "summary");
            Cursor("summary");
            yield return Until(() => NightDirector.Instance.Phase.Value == SessionPhase.GameOver, 90f);
            yield return new WaitForSeconds(0.5f);
            yield return Visit(root, "summary", shots, "ranking");

            UIRoot.L.Set(Language.Thai);
            Debug.Log("UI_SMOKE_OK");
            yield return new WaitForSeconds(1f);
            if (Application.isBatchMode || !string.IsNullOrEmpty(shots)) Application.Quit();
        }

        static IEnumerator Visit(UIRoot root, string screen, string shots, string label = null)
        {
            foreach (var lang in Languages)
            {
                UIRoot.L.Set(lang);
                yield return null;
                yield return null;
                var s = root.Screen(screen);
                bool visible = s != null && s.Visible;
                Debug.Log($"UI_SCREEN name={label ?? screen} lang={lang} visible={visible} main={root.MainScreen}");
                if (!string.IsNullOrEmpty(shots))
                {
                    yield return new WaitForSeconds(0.4f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, $"{label ?? screen}-{(lang == Language.Thai ? "th" : "en")}.png"));
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }

        static void Cursor(string screen) => Debug.Log($"UI_CURSOR screen={screen} free={PlayerInputReader.CursorFree}");

        static IEnumerator Until(Func<bool> condition, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                bool ok;
                try { ok = condition(); }
                catch (NullReferenceException) { ok = false; }
                if (ok) yield break;
                yield return null;
            }
            Debug.Log("UI_SMOKE_TIMEOUT");
        }

        static T Local<T>() where T : Component
        {
            var nm = NetworkManager.Singleton;
            var po = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            return po != null ? po.GetComponent<T>() : null;
        }
    }
}
