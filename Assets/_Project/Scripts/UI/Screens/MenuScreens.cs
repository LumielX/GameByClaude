using System.Collections.Generic;
using BooTrayal.Core.Localization;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace BooTrayal.UI.Screens
{
    /// <summary>Language pills (title screen and settings).</summary>
    static class LanguagePills
    {
        public static void Bind(VisualElement view)
        {
            view.Q<Button>("lang-th")?.RegisterCallback<ClickEvent>(_ => GameSettings.SetLanguage(Language.Thai));
            view.Q<Button>("lang-en")?.RegisterCallback<ClickEvent>(_ => GameSettings.SetLanguage(Language.English));
        }

        public static void Refresh(VisualElement view)
        {
            Set(view.Q<Button>("lang-th"), UIRoot.L.Current == Language.Thai);
            Set(view.Q<Button>("lang-en"), UIRoot.L.Current == Language.English);
        }

        static void Set(Button b, bool on)
        {
            if (b == null) return;
            if (on) b.AddToClassList("pill-on");
            else b.RemoveFromClassList("pill-on");
        }
    }

    public class TitleScreen : UIScreen
    {
        Label _version;

        protected override void Bind()
        {
            Btn("play", () => Root.GoMenu("play"));
            Btn("howto", () => Root.OpenSide("howto"));
            Btn("settings", () => Root.OpenSide("settings"));
            Btn("quit", Application.Quit);
            LanguagePills.Bind(View);
            _version = View.Q<Label>("version");
        }

        public override void Refresh()
        {
            _version.text = L.Format("menu.version", Application.version);
            LanguagePills.Refresh(View);
        }
    }

    public class PlayScreen : UIScreen
    {
        TextField _name, _code;
        Label _status;
        Button _host, _join, _solo;

        protected override void Bind()
        {
            Btn("back", () => Root.GoMenu("title"));
            _host = Btn("host", () => Root.GoMenu("create"));
            _join = Btn("join", Join);
            _solo = Btn("solo", Solo);
            _name = View.Q<TextField>("name");
            _code = View.Q<TextField>("code");
            _status = View.Q<Label>("status");
            _name.RegisterValueChangedCallback(e => ConnectionService.Instance?.SetPlayerName(e.newValue));
            _code.RegisterValueChangedCallback(e =>
            {
                var up = e.newValue.ToUpperInvariant();
                if (up != e.newValue) _code.SetValueWithoutNotify(up);
            });
            _code.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Join(); });
        }

        public override void OnShow()
        {
            var svc = ConnectionService.Instance;
            if (svc != null) _name.SetValueWithoutNotify(svc.PlayerDisplayName);
        }

        void Join()
        {
            var svc = ConnectionService.Instance;
            if (svc != null) _ = svc.JoinRoomAsync(_code.value);
        }

        void Solo()
        {
            var svc = ConnectionService.Instance;
            if (svc == null) return;
            svc.HostConfig = new GameConfig { MaxPlayers = 1, SpyEnabled = false };
            svc.StartLocalHost();
        }

        public override void Refresh()
        {
            var svc = ConnectionService.Instance;
            if (svc == null) return;
            bool idle = svc.Status == NetStatus.Offline || svc.Status == NetStatus.Failed;
            _host.SetEnabled(idle);
            _join.SetEnabled(idle);
            _solo.SetEnabled(idle);
            _status.text = L.Get("status." + svc.Status);
        }
    }

    public class CreateRoomScreen : UIScreen
    {
        readonly GameConfig _cfg = new GameConfig();
        readonly List<Button> _counts = new List<Button>();
        Button[] _spy;
        DropdownField _length, _nights, _difficulty;
        Label _summary, _status;
        Button _submit;

        protected override void Bind()
        {
            Btn("back", () => Root.GoMenu("play"));
            _submit = Btn("submit", Submit);
            var counts = View.Q<VisualElement>("counts");
            for (int n = RoomRules.MinPlayers; n <= RoomRules.MaxPlayers; n++)
            {
                int value = n;
                var b = new Button(() => _cfg.MaxPlayers = value) { text = n.ToString() };
                b.AddToClassList("btn");
                b.AddToClassList("btn-small");
                b.AddToClassList("heavy");
                b.style.minWidth = 60;
                counts.Add(b);
                _counts.Add(b);
            }
            Root.Prepare(counts);
            _spy = new[]
            {
                Btn("spy-0", () => _cfg.SpyEnabled = false),
                Btn("spy-1", () => { _cfg.SpyEnabled = true; _cfg.RequestedSpies = 1; }),
                Btn("spy-2", () => { _cfg.SpyEnabled = true; _cfg.RequestedSpies = 2; })
            };
            _length = View.Q<DropdownField>("length");
            _nights = View.Q<DropdownField>("nights");
            _difficulty = View.Q<DropdownField>("difficulty");
            _length.RegisterValueChangedCallback(_ => _cfg.NightMinutes = RoomRules.NightMinuteOptions[Mathf.Max(0, _length.index)]);
            _nights.RegisterValueChangedCallback(_ => _cfg.Nights = RoomRules.NightOptions[Mathf.Max(0, _nights.index)]);
            _difficulty.RegisterValueChangedCallback(_ => _cfg.Difficulty = (Difficulty)Mathf.Max(0, _difficulty.index));
            _summary = View.Q<Label>("summary");
            _status = View.Q<Label>("status");
        }

        void Submit()
        {
            var svc = ConnectionService.Instance;
            if (svc == null) return;
            if (RoomRules.Validate(_cfg).Count > 0) return;
            svc.HostConfig = new GameConfig
            {
                MaxPlayers = _cfg.MaxPlayers, SpyEnabled = _cfg.SpyEnabled, RequestedSpies = _cfg.RequestedSpies,
                NightMinutes = _cfg.NightMinutes, Nights = _cfg.Nights, Difficulty = _cfg.Difficulty
            };
            _ = svc.CreateRoomAsync(_cfg.MaxPlayers);
        }

        public override void Refresh()
        {
            for (int i = 0; i < _counts.Count; i++) SetClass(_counts[i], "btn-on", RoomRules.MinPlayers + i == _cfg.MaxPlayers);
            int spyChoice = !_cfg.SpyEnabled ? 0 : _cfg.RequestedSpies >= 2 ? 2 : 1;
            for (int i = 0; i < _spy.Length; i++) SetClass(_spy[i], "btn-on-purple", i == spyChoice);
            _spy[2].SetEnabled(_cfg.MaxPlayers >= 7);
            if (_cfg.MaxPlayers < 7 && _cfg.RequestedSpies > 1) _cfg.RequestedSpies = 1;

            SetChoices(_length, Options(RoomRules.NightMinuteOptions, "create.minutes"), System.Array.IndexOf(RoomRules.NightMinuteOptions, _cfg.NightMinutes));
            SetChoices(_nights, Options(RoomRules.NightOptions, "create.nights.n"), System.Array.IndexOf(RoomRules.NightOptions, _cfg.Nights));
            SetChoices(_difficulty, new List<string> { L.Get("diff.Easy"), L.Get("diff.Normal"), L.Get("diff.Hard") }, (int)_cfg.Difficulty);

            string spy = !_cfg.SpyEnabled || _cfg.MaxPlayers < RoomRules.SpyMinPlayers
                ? L.Get("create.summary.spy.none")
                : L.Format("create.summary.spy.n", RoomRules.MaxSpiesAllowed(_cfg.MaxPlayers) < _cfg.RequestedSpies ? 1 : _cfg.RequestedSpies);
            _summary.text = L.Format("create.summary", _cfg.NightMinutes, _cfg.Nights, spy);

            var svc = ConnectionService.Instance;
            bool idle = svc == null || svc.Status == NetStatus.Offline || svc.Status == NetStatus.Failed;
            _submit.SetEnabled(idle);
            _status.text = svc != null && !idle ? L.Get("status." + svc.Status) : "";
        }

        static List<string> Options(int[] values, string key)
        {
            var list = new List<string>();
            foreach (var v in values) list.Add(UIRoot.L.Format(key, v));
            return list;
        }

        static void SetChoices(DropdownField field, List<string> choices, int index)
        {
            if (field.choices == null || field.choices.Count != choices.Count || !field.choices.TrueForAll(choices.Contains))
                field.choices = choices;
            index = Mathf.Clamp(index, 0, choices.Count - 1);
            if (field.value != choices[index]) field.SetValueWithoutNotify(choices[index]);
        }
    }

    public class SettingsScreen : UIScreen
    {
        Slider _volume, _sensitivity;
        Toggle _reduced;

        protected override void Bind()
        {
            Btn("back", () => Root.CloseSide());
            _volume = View.Q<Slider>("volume");
            _sensitivity = View.Q<Slider>("sensitivity");
            _reduced = View.Q<Toggle>("reduced");
            _sensitivity.lowValue = GameSettings.MinSensitivity;
            _sensitivity.highValue = GameSettings.MaxSensitivity;
            _volume.RegisterValueChangedCallback(e => GameSettings.SetVolume(e.newValue));
            _sensitivity.RegisterValueChangedCallback(e => GameSettings.SetSensitivity(e.newValue));
            _reduced.RegisterValueChangedCallback(e => GameSettings.SetReducedEffects(e.newValue));
            LanguagePills.Bind(View);
        }

        public override void OnShow()
        {
            _volume.SetValueWithoutNotify(GameSettings.Volume);
            _sensitivity.SetValueWithoutNotify(GameSettings.Sensitivity);
            _reduced.SetValueWithoutNotify(GameSettings.ReducedEffects);
        }

        public override void Refresh() => LanguagePills.Refresh(View);
    }

    public class HowToScreen : UIScreen
    {
        protected override void Bind() => Btn("back", () => Root.CloseSide());
    }

    public class ErrorPopup : UIScreen
    {
        Label _message;
        Button _download;

        protected override void Bind()
        {
            _message = View.Q<Label>("message");
            _download = Btn("download", () => Application.OpenURL(NetMessages.ReleasesUrl));
            Btn("close", () => Root.CloseError());
        }

        public override void Refresh()
        {
            var key = Root.ErrorKey;
            if (key == null) return;
            _message.text = L.Has("net." + key) ? L.Get("net." + key) : L.Get("net.connect_failed");
            _download.style.display = key == "version_mismatch" ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
