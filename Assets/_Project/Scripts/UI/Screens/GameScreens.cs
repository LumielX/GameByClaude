using System.Linq;
using System.Text;
using BooTrayal.Core.Fear;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using BooTrayal.Game.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace BooTrayal.UI.Screens
{
    static class Local
    {
        public static NetworkObject Player =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;

        public static T Get<T>() where T : Component
        {
            var p = Player;
            return p != null ? p.GetComponent<T>() : null;
        }

        public static string Time(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>In-night HUD (mockup "HUD ระหว่างคืน"): timer, tasks, stamina, held item, role, interact hint, banners.</summary>
    public class HudScreen : UIScreen
    {
        Label _night, _timer, _seals, _ritual, _escape, _hintText, _banner, _toast, _adrenaline, _heldName, _role, _speaking;
        int _speakingKey = int.MinValue;
        VisualElement _hint, _bannerRow, _toastRow, _staminaFill, _held;

        protected override void Bind()
        {
            _night = View.Q<Label>("night");
            _timer = View.Q<Label>("timer");
            _seals = View.Q<Label>("seals");
            _ritual = View.Q<Label>("ritual");
            _escape = View.Q<Label>("escape");
            _hint = View.Q<VisualElement>("hint");
            _hintText = View.Q<Label>("hint-text");
            _bannerRow = View.Q<VisualElement>("banner-row");
            _banner = View.Q<Label>("banner");
            _toastRow = View.Q<VisualElement>("toast-row");
            _toast = View.Q<Label>("toast");
            _adrenaline = View.Q<Label>("adrenaline");
            _staminaFill = View.Q<VisualElement>("stamina-fill");
            _held = View.Q<VisualElement>("held");
            _heldName = View.Q<Label>("held-name");
            _role = View.Q<Label>("role");
            _speaking = View.Q<Label>("speaking");
        }

        public override void Refresh()
        {
            var d = NightDirector.Instance;
            if (d == null) return;
            _night.text = L.Format("hud.night", d.NightNumber.Value, d.NightsTotal.Value);
            _timer.text = Local.Time(d.SecondsLeft.Value);
            _seals.text = L.Format("hud.seals", d.SealsDone.Value, d.SealsTotal.Value);
            _ritual.text = L.Format("hud.ritual", d.RitualDelivered.Value, d.RitualRequired.Value);
            _escape.text = d.GateOpen.Value ? L.Get("hud.gate_open") : L.Get("hud.escape");
            SetClass(_escape, "accent", d.GateOpen.Value);
            SetClass(_escape, "faint", !d.GateOpen.Value);

            var motor = Local.Get<PlayerMotor>();
            if (motor != null)
            {
                _staminaFill.style.width = Length.Percent(100f * motor.Stamina.Value / Stamina.Max);
                var adr = motor.Adrenaline.State;
                _adrenaline.text = adr == AdrenalineState.Adrenaline ? L.Get("hud.adrenaline") : adr == AdrenalineState.Winded ? L.Get("hud.winded") : "";
            }

            var held = Local.Get<PlayerHands>()?.HeldItem;
            _held.style.display = held != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (held != null) _heldName.text = L.Has("item." + held.ItemId) ? L.Get("item." + held.ItemId) : L.Get("item.item");

            var aimed = Local.Get<PlayerActions>()?.Aimed;
            _hint.style.display = aimed != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (aimed != null) _hintText.text = PartText(aimed);

            var life = Local.Get<PlayerLife>();
            string banner = life == null ? "" : life.Grabbed.Value ? L.Get("hud.grabbed")
                : life.IsMiniGhost ? L.Get("hud.minighost")
                : life.State.Value == LifeState.Escaped ? L.Get("hud.escaped") : "";
            _banner.text = banner;
            _bannerRow.style.display = banner.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            bool toast = Time.time < d.MeetingResultUntil && d.LastMeetingResult != null;
            _toastRow.style.display = toast ? DisplayStyle.Flex : DisplayStyle.None;
            if (toast) _toast.text = ResultText(d.LastMeetingResult);

            if (!d.HasRole) _role.text = "";
            else if (d.MyRole == Role.Spy)
                _role.text = L.Get("hud.role.spy") + (d.MyPartners.Count > 0
                    ? "  ·  " + L.Format("hud.partners", string.Join(", ", d.MyPartners.Select(PlayerAvatar.NameOf))) : "");
            else _role.text = L.Get("hud.role.crew");
            _role.style.display = _role.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // Rebuilt only when the set of speakers changes, not every frame someone talks.
            var voice = BooTrayal.Game.Voice.VoiceService.Instance;
            int key = 17;
            if (voice != null) foreach (var id in voice.Speaking) key = unchecked(key * 31 + (int)id + 1);
            if (key != _speakingKey)
            {
                _speakingKey = key;
                _speaking.text = voice != null && voice.Speaking.Count > 0
                    ? string.Join("\n", voice.Speaking.OrderBy(id => id).Select(id => L.Format("hud.speaking", PlayerAvatar.NameOf(id))))
                    : "";
            }
        }

        static string PartText(Interactable part)
        {
            if (part.Station is SafeStation)
                return part.Part == SafeStation.ClearPart ? L.Get("part.clear") : L.Format("part.digit", part.Part);
            if (part.Station is FuseStation) return L.Get("part.fuse");
            if (part.Station is ShrineBell) return L.Get("part.bell");
            return part.Label;
        }

        public static string ResultText(MeetingResultData r)
        {
            if (!r.Ejected) return L.Get("result.none");
            return L.Format(r.WasSpy ? "result.correct" : "result.wrong", PlayerAvatar.NameOf(r.Target));
        }
    }

    /// <summary>Meeting overlay (mockup "ประชุม / โหวต"): who rang, timer, one button per living player, skip.</summary>
    public class VoteScreen : UIScreen
    {
        Label _caller, _timer, _cannot;
        VisualElement _people, _fill;
        Button _skip;
        string _signature;
        ulong? _myVote;
        bool _voted;
        MeetingPhase _lastPhase;
        float _phaseLength = 1f;

        protected override void Bind()
        {
            _caller = View.Q<Label>("caller");
            _timer = View.Q<Label>("timer");
            _cannot = View.Q<Label>("cannot");
            _people = View.Q<VisualElement>("people");
            _fill = View.Q<VisualElement>("timebar-fill");
            _skip = Btn("skip", () => Vote(null));
        }

        public override void OnShow()
        {
            _signature = null;
            _voted = false;
            _myVote = null;
        }

        void Vote(ulong? target)
        {
            var actions = Local.Get<PlayerActions>();
            if (actions == null) return;
            actions.RequestVote(target);
            _voted = true;
            _myVote = target;
            _signature = null;
        }

        public override void Refresh()
        {
            var d = NightDirector.Instance;
            if (d == null) return;
            var phase = d.MeetPhase.Value;
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _phaseLength = Mathf.Max(1f, d.MeetTimeLeft.Value);
                _signature = null;
            }
            _caller.text = L.Format("meeting.called_by", PlayerAvatar.NameOf(d.MeetCaller.Value));
            int t = Mathf.CeilToInt(d.MeetTimeLeft.Value);
            _timer.text = phase == MeetingPhase.Discuss ? L.Format("meeting.discuss", t) : L.Format("meeting.vote", t);
            _fill.style.width = Length.Percent(100f * Mathf.Clamp01(d.MeetTimeLeft.Value / _phaseLength));

            var life = Local.Get<PlayerLife>();
            bool canVote = phase == MeetingPhase.Vote && life != null && life.IsAlive;
            _cannot.style.display = life != null && !life.IsAlive ? DisplayStyle.Flex : DisplayStyle.None;
            _skip.SetEnabled(canVote);
            SetClass(_skip, "btn-selected", _voted && !_myVote.HasValue);

            var alive = Object.FindObjectsByType<PlayerLife>().Where(p => p.IsSpawned && p.IsAlive).OrderBy(p => p.OwnerClientId).ToList();
            var sig = new StringBuilder($"{L.Current}{canVote}{_voted}{_myVote}");
            foreach (var p in alive) sig.Append('|').Append(p.OwnerClientId).Append(PlayerAvatar.NameOf(p.OwnerClientId));
            if (sig.ToString() == _signature) return;
            _signature = sig.ToString();
            _people.Clear();
            foreach (var p in alive)
            {
                ulong id = p.OwnerClientId;
                var b = new Button(() => Vote(id)) { text = L.Format("meeting.vote_for", PlayerAvatar.NameOf(id)) };
                b.AddToClassList("btn");
                b.AddToClassList("semibold");
                b.style.width = Length.Percent(31);
                b.style.height = 72;
                b.style.marginRight = 14;
                b.style.marginBottom = 14;
                b.style.borderLeftColor = PlayerAvatar.Palette[(int)(id % (ulong)PlayerAvatar.Palette.Length)];
                b.style.borderLeftWidth = 10;
                if (_voted && _myVote == id) b.AddToClassList("btn-selected");
                b.SetEnabled(canVote);
                _people.Add(b);
            }
            Root.Prepare(_people);
        }
    }

    /// <summary>End of night (winner, Spy reveal, coins) and end of game (ranking) — mockup "สรุปคืน".</summary>
    public class SummaryScreen : UIScreen
    {
        Label _night, _winner, _reason, _spyLabel, _spyName, _sabotage, _next, _tonightCol;
        VisualElement _spyAvatar;
        ScrollView _rows;
        object _shown;
        Core.Localization.Language _shownLang;

        protected override void Bind()
        {
            _night = View.Q<Label>("night");
            _winner = View.Q<Label>("winner");
            _reason = View.Q<Label>("reason");
            _spyLabel = View.Q<Label>("spy-label");
            _spyName = View.Q<Label>("spy-name");
            _sabotage = View.Q<Label>("sabotage");
            _next = View.Q<Label>("next");
            _tonightCol = View.Q<Label>("col-tonight");
            _spyAvatar = View.Q<VisualElement>("spy-avatar");
            _rows = View.Q<ScrollView>("rows");
        }

        public override void OnShow() => _shown = null;

        public override void Refresh()
        {
            var d = NightDirector.Instance;
            if (d == null) return;
            bool over = d.Phase.Value == SessionPhase.GameOver;
            object data = over ? (object)d.LastRanking : d.LastSummary;
            if (data == null || (data == _shown && L.Current == _shownLang)) return;
            _shown = data;
            _shownLang = L.Current;
            var s = d.LastSummary;

            if (s != null)
            {
                _night.text = L.Format("summary.night_end", s.Night);
                _winner.text = L.Get("summary.win." + s.Winner);
                _reason.text = L.Has("reason." + s.Reason) ? L.Get("reason." + s.Reason) : s.Reason;
                bool spy = s.Spies.Length > 0;
                _spyLabel.style.display = spy ? DisplayStyle.Flex : DisplayStyle.None;
                _spyName.text = spy ? string.Join(", ", s.Spies.Select(PlayerAvatar.NameOf)) : L.Get("summary.no_spy");
                _sabotage.text = spy ? L.Format("summary.sabotage", s.Sabotage, s.Required) : "";
                _spyAvatar.style.display = spy ? DisplayStyle.Flex : DisplayStyle.None;
                if (spy) _spyAvatar.style.backgroundColor = PlayerAvatar.Palette[(int)(s.Spies[0] % (ulong)PlayerAvatar.Palette.Length)];
            }
            if (over) _winner.text = L.Get("gameover.title");
            _next.text = over ? L.Get("gameover.next") : L.Get("summary.next");
            _tonightCol.style.visibility = over ? Visibility.Hidden : Visibility.Visible;

            _rows.Clear();
            if (over && d.LastRanking != null)
            {
                var r = d.LastRanking;
                for (int i = 0; i < r.Ids.Length; i++) _rows.Add(Row($"{i + 1}. {PlayerAvatar.NameOf(r.Ids[i])}", "", r.Scores[i].ToString()));
            }
            else if (s != null)
            {
                var order = Enumerable.Range(0, s.Ids.Length).OrderByDescending(i => s.Coins[i]).ThenBy(i => s.Ids[i]);
                foreach (var i in order) _rows.Add(Row(PlayerAvatar.NameOf(s.Ids[i]), "+" + s.Coins[i], s.Totals[i].ToString()));
            }
            Root.Prepare(_rows);
        }

        static VisualElement Row(string name, string tonight, string total)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");
            var a = new Label(name);
            a.AddToClassList("semibold");
            var b = new Label(tonight);
            b.AddToClassList("right");
            b.AddToClassList("accent");
            var c = new Label(total);
            c.AddToClassList("right");
            row.Add(a);
            row.Add(b);
            row.Add(c);
            return row;
        }
    }

    public class PauseMenu : UIScreen
    {
        protected override void Bind()
        {
            Btn("resume", () => Root.Paused = false);
            Btn("settings", () => Root.OpenSide("settings"));
            Btn("howto", () => Root.OpenSide("howto"));
            Btn("leave", () =>
            {
                Root.Paused = false;
                var svc = ConnectionService.Instance;
                if (svc != null) _ = svc.LeaveAsync();
            });
        }
    }
}
