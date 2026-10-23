using System.Linq;
using System.Text;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace BooTrayal.UI.Screens
{
    /// <summary>The room before a game (mockup "ล็อบบี้ 8 ช่อง"): 8 slots, Ready, host Start and kick, chat.</summary>
    public class LobbyScreen : UIScreen
    {
        const int Slots = 8;

        VisualElement _row1, _row2;
        ScrollView _chat;
        TextField _input;
        Label _heading, _code, _readyCount;
        Button _copy, _ready, _start;
        string _slotSignature;
        bool _chatDirty = true;
        float _copiedUntil;

        protected override void Bind()
        {
            _row1 = View.Q<VisualElement>("row1");
            _row2 = View.Q<VisualElement>("row2");
            _chat = View.Q<ScrollView>("chat-list");
            _input = View.Q<TextField>("chat-input");
            _heading = View.Q<Label>("heading");
            _code = View.Q<Label>("code");
            _readyCount = View.Q<Label>("ready-count");
            _copy = Btn("copy", Copy);
            _ready = Btn("ready", ToggleReady);
            _start = Btn("start", () => NightDirector.Instance?.StartGame());
            Btn("leave", () => { var svc = ConnectionService.Instance; if (svc != null) _ = svc.LeaveAsync(); });
            Btn("send", Send);
            _input.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Send(); });
            PlayerLobby.ChatChanged += () => _chatDirty = true;
        }

        public override void OnShow()
        {
            _slotSignature = null;
            _chatDirty = true;
        }

        static PlayerLobby Mine()
        {
            var po = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            return po != null ? po.GetComponent<PlayerLobby>() : null;
        }

        void ToggleReady()
        {
            var me = Mine();
            if (me != null) me.RequestReady(!me.Ready.Value);
        }

        void Send()
        {
            var me = Mine();
            if (me != null && !string.IsNullOrWhiteSpace(_input.value)) me.SendChat(_input.value);
            _input.SetValueWithoutNotify("");
            _input.Focus();
        }

        void Copy()
        {
            var svc = ConnectionService.Instance;
            if (svc == null || string.IsNullOrEmpty(svc.RoomCode)) return;
            GUIUtility.systemCopyBuffer = svc.RoomCode;
            _copiedUntil = Time.unscaledTime + 2f;
        }

        public override void Refresh()
        {
            var svc = ConnectionService.Instance;
            var net = NetworkManager.Singleton;
            if (svc == null || net == null) return;
            bool solo = string.IsNullOrEmpty(svc.RoomCode);
            _heading.text = solo ? L.Get("lobby.solo") : L.Get("menu.play");
            _code.text = solo ? "" : $"{L.Get("lobby.code")} {svc.RoomCode}";
            _copy.style.display = solo ? DisplayStyle.None : DisplayStyle.Flex;
            _copy.text = Time.unscaledTime < _copiedUntil ? L.Get("lobby.copied") : L.Get("lobby.copy");

            var players = Object.FindObjectsByType<PlayerLobby>().Where(p => p.IsSpawned).OrderBy(p => p.OwnerClientId).ToList();
            int ready = players.Count(p => p.Ready.Value);
            _readyCount.text = L.Format("lobby.ready_count", ready, players.Count);
            var me = Mine();
            bool meReady = me != null && me.Ready.Value;
            _ready.text = meReady ? L.Get("lobby.unready") : L.Get("lobby.ready");
            SetClass(_ready, "btn-on", meReady);
            _start.style.display = net.IsServer ? DisplayStyle.Flex : DisplayStyle.None;
            _start.SetEnabled(NightDirector.Instance != null && NightDirector.Instance.AllReady);

            var sig = new StringBuilder(L.Current.ToString());
            foreach (var p in players) sig.Append('|').Append(p.OwnerClientId).Append(p.Ready.Value).Append(PlayerAvatar.NameOf(p.OwnerClientId));
            if (sig.ToString() != _slotSignature)
            {
                _slotSignature = sig.ToString();
                RebuildSlots(players, net);
            }
            if (_chatDirty) RebuildChat();
        }

        void RebuildSlots(System.Collections.Generic.List<PlayerLobby> players, NetworkManager net)
        {
            _row1.Clear();
            _row2.Clear();
            for (int i = 0; i < Slots; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("slot");
                var avatar = new VisualElement();
                avatar.AddToClassList("avatar");
                var name = new Label();
                name.AddToClassList("semibold");
                var state = new Label();
                state.AddToClassList("faint");
                slot.Add(avatar);
                slot.Add(name);
                slot.Add(state);
                if (i < players.Count)
                {
                    var p = players[i];
                    ulong id = p.OwnerClientId;
                    avatar.style.backgroundColor = PlayerAvatar.Palette[(int)(id % (ulong)PlayerAvatar.Palette.Length)];
                    string suffix = id == net.LocalClientId ? " " + L.Get("lobby.you") : id == NetworkManager.ServerClientId ? " " + L.Get("lobby.host") : "";
                    name.text = PlayerAvatar.NameOf(id) + suffix;
                    state.text = p.Ready.Value ? L.Get("lobby.state.ready") : L.Get("lobby.state.not_ready");
                    if (p.Ready.Value) state.AddToClassList("accent");
                    if (net.IsServer && id != net.LocalClientId)
                    {
                        var kick = new Button(() => ConnectionService.Instance?.Kick(id)) { text = L.Get("lobby.kick") };
                        kick.AddToClassList("btn");
                        kick.AddToClassList("btn-small");
                        kick.AddToClassList("btn-danger");
                        kick.style.marginTop = 8;
                        slot.Add(kick);
                    }
                }
                else
                {
                    slot.AddToClassList("slot-empty");
                    avatar.style.backgroundColor = new Color(0.16f, 0.13f, 0.22f);
                    name.text = L.Get("lobby.empty");
                    state.text = L.Get("lobby.waiting");
                }
                (i < Slots / 2 ? _row1 : _row2).Add(slot);
            }
            Root.Prepare(_row1);
            Root.Prepare(_row2);
        }

        void RebuildChat()
        {
            _chatDirty = false;
            _chat.Clear();
            foreach (var (from, text) in PlayerLobby.ChatLog)
            {
                var line = new Label($"{PlayerAvatar.NameOf(from)}: {text}");
                line.AddToClassList("wrap");
                line.style.fontSize = 17;
                line.style.marginBottom = 6;
                _chat.Add(line);
            }
            Root.Prepare(_chat);
            _chat.schedule.Execute(() => _chat.scrollOffset = new Vector2(0, float.MaxValue));
        }
    }
}
