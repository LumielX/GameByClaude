using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BooTrayal.Core.Rules;
using BooTrayal.Core.Voice;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using BooTrayal.Game.Settings;
using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace BooTrayal.Game.Voice
{
    /// <summary>
    /// In-game voice (GDD 13): Vivox positional chat between the living, the whole room during meetings, a private channel
    /// for mini ghosts. Only in online (Relay) rooms; Solo/localhost and -novoice have none. Failures never break the game:
    /// voice turns itself off and says why in Settings.
    /// </summary>
    public class VoiceService : MonoBehaviour
    {
        const float ApplyInterval = 0.25f;

        public static VoiceService Instance { get; private set; }

        public bool Active { get; private set; }
        public string LastError { get; private set; }
        public IReadOnlyCollection<ulong> Speaking => _speaking;

        readonly HashSet<string> _joined = new HashSet<string>();
        readonly HashSet<ulong> _speaking = new HashSet<ulong>();
        ConnectionService _svc;
        string _code;
        string _transmit;
        bool _busy;
        bool _initialized;
        bool _muted;
        float _timer;
        double _lastSend = double.NegativeInfinity;
        float _appliedVolume = -1f;

        void Awake() => Instance = this;

        void Start()
        {
            _svc = GetComponent<ConnectionService>();
            if (_svc != null) _svc.Changed += OnConnectionChanged;
        }

        void OnDestroy()
        {
            if (_svc != null) _svc.Changed -= OnConnectionChanged;
            if (Instance == this) Instance = null;
        }

        bool WantVoice =>
            _svc != null && !_svc.Args.NoVoice &&
            (_svc.Status == NetStatus.Hosting || _svc.Status == NetStatus.Connected) &&
            !string.IsNullOrEmpty(_svc.RoomCode) && _svc.RoomCode != ConnectionService.LocalCode;

        void OnConnectionChanged()
        {
            if (!WantVoice) LastError = null; // a new room gets a fresh try
            if (WantVoice && _code != _svc.RoomCode) _ = StartAsync(_svc.RoomCode);
            else if (!WantVoice && _code != null) _ = StopAsync();
        }

        async Task StartAsync(string code)
        {
            if (_busy) return;
            _busy = true;
            _code = code;
            try
            {
                if (!_initialized)
                {
                    await VivoxService.Instance.InitializeAsync();
                    _initialized = true;
                }
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    var net = NetworkManager.Singleton;
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        // The NGO client id lets the HUD map Vivox speakers back to players.
                        DisplayName = net != null ? net.LocalClientId.ToString() : "0",
                        ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
                    });
                }
                Active = true;
                LastError = null;
                Debug.Log($"VOICE_LOGGED_IN room={code}");
            }
            catch (Exception e) { Fail(e); }
            finally { _busy = false; }
        }

        async Task StopAsync()
        {
            Active = false;
            _code = null;
            _speaking.Clear();
            if (!_initialized) return;
            while (_busy) await Task.Yield();
            _busy = true;
            try
            {
                if (_joined.Count > 0) await VivoxService.Instance.LeaveAllChannelsAsync();
                if (VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LogoutAsync();
            }
            catch (Exception e) { Debug.LogWarning($"VOICE_STOP_WARNING {e.Message}"); }
            finally
            {
                _joined.Clear();
                _transmit = null;
                _busy = false;
                Debug.Log("VOICE_STOPPED");
            }
        }

        void Fail(Exception e)
        {
            Active = false;
            LastError = e.Message;
            Debug.LogWarning($"VOICE_FAILED reason={e.GetType().Name}: {e.Message}");
        }

        void Update()
        {
            // Events can arrive before the room code is known (joining clients), so also re-check here.
            if (!_busy && _svc != null)
            {
                if (WantVoice && _code != _svc.RoomCode && LastError == null) _ = StartAsync(_svc.RoomCode);
                else if (!WantVoice && _code != null) _ = StopAsync();
            }
            if (!Active) return;
            ApplyPushToTalk();
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f || _busy) return;
            _timer = ApplyInterval;
            _ = ApplyAsync();
        }

        static PlayerLife LocalLife()
        {
            var net = NetworkManager.Singleton;
            var po = net != null && net.LocalClient != null ? net.LocalClient.PlayerObject : null;
            return po != null ? po.GetComponent<PlayerLife>() : null;
        }

        void ApplyPushToTalk()
        {
            bool mute = false;
            if (GameSettings.PushToTalk)
            {
                var life = LocalLife();
                var input = life != null ? PlayerInputSource.Get(life.gameObject) : null;
                mute = input == null || !input.TalkHeld;
            }
            if (mute == _muted) return;
            _muted = mute;
            if (mute) VivoxService.Instance.MuteInputDevice();
            else VivoxService.Instance.UnmuteInputDevice();
        }

        async Task ApplyAsync()
        {
            _busy = true;
            try
            {
                var life = LocalLife();
                var state = life != null ? life.State.Value : LifeState.Disconnected;
                bool meeting = NightDirector.Instance != null && NightDirector.Instance.MeetingRunning;
                var desired = VoiceRules.Joined(state).Select(c => (c, VoiceRules.ChannelName(_code, c))).ToList();

                foreach (var (channel, name) in desired)
                {
                    if (_joined.Contains(name)) continue;
                    if (channel == VoiceChannel.Positional)
                        await VivoxService.Instance.JoinPositionalChannelAsync(name, ChatCapability.AudioOnly,
                            new Channel3DProperties(VoiceRules.AudibleDistance, VoiceRules.ConversationalDistance, 1f, AudioFadeModel.InverseByDistance));
                    else
                        await VivoxService.Instance.JoinGroupChannelAsync(name, ChatCapability.AudioOnly);
                    _joined.Add(name);
                    _appliedVolume = -1f;
                    Debug.Log($"VOICE_JOINED channel={name}");
                }
                foreach (var name in _joined.Where(n => desired.All(d => d.Item2 != n)).ToList())
                {
                    await VivoxService.Instance.LeaveChannelAsync(name);
                    _joined.Remove(name);
                    Debug.Log($"VOICE_LEFT channel={name}");
                }

                var to = VoiceRules.TransmitTo(state, meeting);
                var transmit = to.HasValue ? VoiceRules.ChannelName(_code, to.Value) : null;
                if (transmit != _transmit && (transmit == null || _joined.Contains(transmit)))
                {
                    if (transmit == null) await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    else await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.Single, transmit);
                    _transmit = transmit;
                    Debug.Log($"VOICE_TRANSMIT channel={transmit ?? "-"}");
                }

                if (!Mathf.Approximately(_appliedVolume, GameSettings.VoiceVolume))
                {
                    _appliedVolume = GameSettings.VoiceVolume;
                    int v = Mathf.RoundToInt(Mathf.Lerp(-50f, 50f, GameSettings.VoiceVolume));
                    foreach (var name in _joined) await VivoxService.Instance.SetChannelVolumeAsync(name, v);
                }

                UpdatePosition(life);
                ReadSpeakers(life);
            }
            catch (Exception e) { Fail(e); }
            finally { _busy = false; }
        }

        void UpdatePosition(PlayerLife life)
        {
            var pos = VoiceRules.ChannelName(_code, VoiceChannel.Positional);
            if (life == null || !_joined.Contains(pos)) return;
            var head = life.transform.Find("HeadPivot");
            VivoxService.Instance.Set3DPosition(head != null ? head.gameObject : life.gameObject, pos);
        }

        void ReadSpeakers(PlayerLife life)
        {
            _speaking.Clear();
            var channels = VivoxService.Instance.ActiveChannels;
            foreach (var name in _joined)
            {
                if (!channels.TryGetValue(name, out var participants)) continue;
                foreach (var p in participants)
                {
                    if (!p.SpeechDetected) continue;
                    if (p.IsSelf)
                    {
                        // Loud talking is a noise the Krasue hears (GDD 6.4); quiet talk is safe.
                        if (name == _transmit && life != null && Time.realtimeSinceStartupAsDouble - _lastSend >= VoiceRules.ClientSendInterval)
                        {
                            float level = VoiceRules.LoudnessFromEnergy(p.AudioEnergy);
                            if (level > 0f)
                            {
                                _lastSend = Time.realtimeSinceStartupAsDouble;
                                life.GetComponent<PlayerNoise>()?.ReportVoice(level);
                            }
                        }
                        continue;
                    }
                    if (ulong.TryParse(p.DisplayName, out var id)) _speaking.Add(id);
                }
            }
        }
    }
}
