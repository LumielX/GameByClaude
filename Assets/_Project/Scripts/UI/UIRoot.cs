using System;
using System.Collections.Generic;
using BooTrayal.Core.Localization;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using BooTrayal.Game.Settings;
using BooTrayal.UI.Screens;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BooTrayal.UI
{
    /// <summary>One screen (or overlay) built from a UXML template. Its controller binds buttons once and refreshes while shown.</summary>
    public abstract class UIScreen
    {
        public string Name { get; private set; }
        public VisualElement View { get; private set; }
        protected UIRoot Root { get; private set; }
        protected static Localizer L => UIRoot.L;
        public bool Visible => View.style.display == DisplayStyle.Flex;

        public void Init(UIRoot root, string name, VisualElement view)
        {
            Root = root;
            Name = name;
            View = view;
            Bind();
        }

        protected abstract void Bind();
        /// <summary>Every frame while visible, and right after a language change.</summary>
        public virtual void Refresh() { }
        public virtual void OnShow() { }

        protected Button Btn(string name, Action onClick)
        {
            var b = View.Q<Button>(name);
            if (b != null && onClick != null) b.clicked += onClick;
            return b;
        }

        protected static void SetClass(VisualElement e, string cls, bool on)
        {
            if (e == null) return;
            if (on) e.AddToClassList(cls);
            else e.RemoveFromClassList(cls);
        }
    }

    /// <summary>
    /// The whole UI (GDD 11): picks the screen from the connection and night state, owns the cursor and Esc, localizes every
    /// text whose UXML text starts with "@", and switches Thai/English live. Lives on the persistent Services object.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }
        public static readonly Localizer L = new Localizer();

        public PanelSettings Panel;
        public StyleSheet Theme;
        public Font Regular;
        public Font SemiBold;
        public Font Heavy;
        public VisualTreeAsset TitleUxml, PlayUxml, CreateUxml, SettingsUxml, HowToUxml, ErrorUxml;
        public VisualTreeAsset LobbyUxml, HudUxml, VoteUxml, SummaryUxml, PauseUxml;

        readonly Dictionary<string, UIScreen> _screens = new Dictionary<string, UIScreen>();
        readonly List<(TextElement element, string key)> _keyed = new List<(TextElement, string)>();
        VisualElement _root;
        string _menu = "title";
        string _menuBack = "title";
        string _inGameMenu;
        bool _wasInSession;
        NetStatus _lastStatus = NetStatus.Offline;
        bool _cursorFree = true;
        bool _cursorKnown;

        public bool Paused { get; set; }
        public string ErrorKey { get; private set; }
        public string MainScreen { get; private set; } = "";

        void Awake()
        {
            Instance = this;
            L.Load(Language.Thai, Localizer.Parse(Resources.Load<TextAsset>("Lang/th")?.text));
            L.Load(Language.English, Localizer.Parse(Resources.Load<TextAsset>("Lang/en")?.text));
            L.Set(GameSettings.Language);
            L.Changed += Relocalize;
            GameSettings.Changed += () => L.Set(GameSettings.Language);
        }

        void OnDestroy()
        {
            L.Changed -= Relocalize;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var svc = ConnectionService.Instance;
            if (svc != null && svc.Args.UiSmoke) StartCoroutine(UISmokeRunner.Run(this));
        }

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            if (Panel != null) doc.panelSettings = Panel;
            _root = doc.rootVisualElement;
            Build();
        }

        void Build()
        {
            _root.Clear();
            _screens.Clear();
            _keyed.Clear();
            if (Theme != null) _root.styleSheets.Add(Theme);
            _root.AddToClassList("bt-root");
            _root.pickingMode = PickingMode.Ignore;
            if (Regular != null) _root.style.unityFontDefinition = FontDefinition.FromFont(Regular);
            _root.style.unityTextGenerator = TextGeneratorType.Advanced;

            Add(new TitleScreen(), "title", TitleUxml);
            Add(new PlayScreen(), "play", PlayUxml);
            Add(new CreateRoomScreen(), "create", CreateUxml);
            Add(new SettingsScreen(), "settings", SettingsUxml);
            Add(new HowToScreen(), "howto", HowToUxml);
            Add(new LobbyScreen(), "lobby", LobbyUxml);
            Add(new HudScreen(), "hud", HudUxml);
            Add(new SummaryScreen(), "summary", SummaryUxml);
            Add(new VoteScreen(), "vote", VoteUxml);
            Add(new PauseMenu(), "pause", PauseUxml);
            Add(new ErrorPopup(), "error", ErrorUxml);
            Relocalize();
        }

        void Add(UIScreen screen, string name, VisualTreeAsset template)
        {
            if (template == null) return;
            var view = template.Instantiate();
            view.name = name + "-root";
            view.style.position = Position.Absolute;
            view.style.left = view.style.top = view.style.right = view.style.bottom = 0;
            view.style.display = DisplayStyle.None;
            view.pickingMode = PickingMode.Ignore;
            _root.Add(view);
            Prepare(view);
            _screens[name] = screen;
            screen.Init(this, name, view);
        }

        /// <summary>Records "@key" texts and applies weights; call again for elements a controller creates later.</summary>
        public void Prepare(VisualElement view)
        {
            view.Query<TextElement>().ForEach(t =>
            {
                t.style.unityTextGenerator = TextGeneratorType.Advanced;
                if (!string.IsNullOrEmpty(t.text) && t.text.StartsWith("@"))
                {
                    var key = t.text.Substring(1);
                    _keyed.Add((t, key));
                    t.text = L.Get(key);
                }
            });
            if (SemiBold != null) view.Query<VisualElement>(className: "semibold").ForEach(e => e.style.unityFontDefinition = FontDefinition.FromFont(SemiBold));
            if (Heavy != null) view.Query<VisualElement>(className: "heavy").ForEach(e => e.style.unityFontDefinition = FontDefinition.FromFont(Heavy));
        }

        void Relocalize()
        {
            foreach (var (element, key) in _keyed) element.text = L.Get(key);
            foreach (var s in _screens.Values) if (s.Visible) s.Refresh();
        }

        public UIScreen Screen(string name) => _screens.TryGetValue(name, out var s) ? s : null;

        // ---- Navigation ----

        public void GoMenu(string name) => _menu = name;

        /// <summary>Settings or How to play, from the title screen or from the pause menu.</summary>
        public void OpenSide(string screen)
        {
            if (InSession) _inGameMenu = screen;
            else
            {
                _menuBack = _menu;
                _menu = screen;
            }
        }

        public void CloseSide()
        {
            if (InSession) _inGameMenu = null;
            else _menu = _menuBack == "settings" || _menuBack == "howto" ? "title" : _menuBack;
        }

        public void ShowError(string key) => ErrorKey = string.IsNullOrEmpty(key) ? null : key;
        public void CloseError() => ErrorKey = null;

        public static bool InSession
        {
            get
            {
                var svc = ConnectionService.Instance;
                var d = NightDirector.Instance;
                return svc != null && (svc.Status == NetStatus.Hosting || svc.Status == NetStatus.Connected) && d != null && d.IsSpawned;
            }
        }

        void Update()
        {
            if (_root == null) return;
            bool session = InSession;
            if (_wasInSession && !session) OnSessionEnded();
            _wasInSession = session;
            var svc = ConnectionService.Instance;
            if (svc != null && svc.Status != _lastStatus)
            {
                // A failed create/join, a rejection, a kick or the host leaving: say why, in the player's language.
                if ((svc.Status == NetStatus.Offline || svc.Status == NetStatus.Failed) && _lastStatus != NetStatus.Offline &&
                    !string.IsNullOrEmpty(svc.LastMessageKey))
                    ShowError(svc.LastMessageKey);
                _lastStatus = svc.Status;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) OnEscape(session);

            string main;
            bool vote = false;
            var d = NightDirector.Instance;
            if (session)
            {
                if (_inGameMenu != null) main = _inGameMenu;
                else switch (d.Phase.Value)
                {
                    case SessionPhase.Lobby: main = "lobby"; break;
                    case SessionPhase.InNight: main = "hud"; vote = d.MeetingRunning; break;
                    default: main = "summary"; break;
                }
            }
            else
            {
                main = _menu;
                Paused = false;
                _inGameMenu = null;
            }

            MainScreen = main;
            foreach (var kv in _screens)
            {
                bool show = kv.Key == main
                    || (kv.Key == "vote" && vote && _inGameMenu == null)
                    || (kv.Key == "pause" && session && Paused && _inGameMenu == null)
                    || (kv.Key == "error" && ErrorKey != null);
                SetVisible(kv.Value, show);
            }
            foreach (var s in _screens.Values) if (s.Visible) s.Refresh();

            bool free = !(session && main == "hud" && !vote && !Paused && ErrorKey == null);
            if (!_cursorKnown || free != _cursorFree)
            {
                _cursorKnown = true;
                _cursorFree = free;
                if (!Application.isBatchMode) PlayerInputReader.SetCursorFree(free);
            }
        }

        static void SetVisible(UIScreen s, bool show)
        {
            if (s.Visible == show) return;
            s.View.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                s.OnShow();
                s.View.BringToFront();
            }
        }

        void OnEscape(bool session)
        {
            if (ErrorKey != null) { ErrorKey = null; return; }
            if (session)
            {
                if (_inGameMenu != null) _inGameMenu = null;
                else Paused = !Paused;
            }
            else if (_menu != "title") _menu = _menu == "create" ? "play" : "title";
        }

        void OnSessionEnded()
        {
            Paused = false;
            _inGameMenu = null;
            _menu = "play";
        }
    }
}
