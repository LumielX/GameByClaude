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
        const float RetryDelay = 10f;

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
        bool _lost;
        int _generation; // bumped by every stop/failure: async work started before it must not touch the new state
        float _timer;
        float _retryAt;
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
            if (_initialized)
            {
                VivoxService.Instance.ChannelLeft -= OnChannelLeft;
                VivoxService.Instance.LoggedOut -= OnLost;
                VivoxService.Instance.ConnectionFailedToRecover -= OnLost;
            }
            if (Instance == this) Instance = null;
        }

        bool WantVoice =>
            _svc != null && !_svc.Args.NoVoice &&
            (_svc.Status == NetStatus.Hosting || _svc.Status == NetStatus.Connected) &&
            !string.IsNullOrEmpty(_svc.RoomCode) && _svc.RoomCode != ConnectionService.LocalCode;

        void OnConnectionChanged()
        {
            if (!WantVoice) LastError = null; // a new room gets a fresh try
            if (WantVoice && _code != _svc.RoomCode && LastError == null) _ = StartAsync(_svc.RoomCode);
            else if (!WantVoice && _code != null) _ = StopAsync();
        }

        // Vivox can drop a channel or the whole login on its own; forget what it dropped so the next tick rejoins.
        void OnChannelLeft(string name)
        {
            _joined.Remove(name);
            if (_transmit == name) _transmit = null;
        }

        void OnLost()
        {
            if (Active) _lost = true;
        }

        async Task StartAsync(string code)
        {
            if (_busy) return;
            _busy = true;
            int gen = _generation;
            _code = code;
            try
            {
                if (!_initialized)
                {
                    await VivoxService.Instance.InitializeAsync();
                    _initialized = true;
                    VivoxService.Instance.ChannelLeft += OnChannelLeft;
                    VivoxService.Instance.LoggedOut += OnLost;
                    VivoxService.Instance.ConnectionFailedToRecover += OnLost;
                }
                // A login left over from another room carries an old display name (= old NGO client id).
                if (VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LogoutAsync();
                if (gen != _generation) return;
                var net = NetworkManager.Singleton;
                await VivoxService.Instance.LoginAsync(new LoginOptions
                {
                    // The NGO client id lets the HUD map Vivox speakers back to players.
                    DisplayName = net != null ? net.LocalClientId.ToString() : "0",
                    ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
                });
                if (gen != _generation) return; // stopped while logging in: StopAsync logs out after us
                _lost = false;
                Active = true;
                LastError = null;
                Debug.Log($"VOICE_LOGGED_IN room={code}");
            }
            catch (Exception e)
            {
                if (gen == _generation) Fail(e);
            }
            finally { _busy = false; }
        }

        async Task StopAsync()
        {
            _generation++;
            Active = false;
            _code = null;
            _speaking.Clear();
            if (!_initialized) return;
            while (_busy) await Task.Yield();
            _busy = true;
            try { await ShutDownVivoxAsync(); }
            finally
            {
                _busy = false;
                Debug.Log("VOICE_STOPPED");
            }
        }

        /// <summary>Best effort: stop transmitting first, then leave every channel, then log out; each step on its own.</summary>
        async Task ShutDownVivoxAsync()
        {
            var vivox = VivoxService.Instance;
            try { if (vivox.IsLoggedIn && _transmit != null) await vivox.SetChannelTransmissionModeAsync(TransmissionMode.None); }
            catch (Exception e) { Debug.LogWarning($"VOICE_STOP_WARNING transmit {e.Message}"); }
            try { if (vivox.ActiveChannels.Count > 0) await vivox.LeaveAllChannelsAsync(); }
            catch (Exception e) { Debug.LogWarning($"VOICE_STOP_WARNING leave {e.Message}"); }
            try { if (vivox.IsLoggedIn) await vivox.LogoutAsync(); }
            catch (Exception e) { Debug.LogWarning($"VOICE_STOP_WARNING logout {e.Message}"); }
            _joined.Clear();
            _transmit = null;
        }

        /// <summary>
        /// Voice broke: never leave a stale state behind (a dead player still heard on the positional channel), so tear
        /// everything down, show why in Settings and try the room again a little later.
        /// </summary>
        void Fail(Exception e)
        {
            _generation++;
            Active = false;
            _code = null;
            _speaking.Clear();
            LastError = e.Message;
            _retryAt = Time.unscaledTime + RetryDelay;
            Debug.LogWarning($"VOICE_FAILED reason={e.GetType().Name}: {e.Message}");
            _ = TearDownAfterFailAsync();
        }

        async Task TearDownAfterFailAsync()
        {
            while (_busy) await Task.Yield();
            _busy = true;
            try { await ShutDownVivoxAsync(); }
            finally { _busy = false; }
        }

        void Update()
        {
            if (_lost) { _lost = false; Fail(new Exception("voice connection lost")); }
            // Events can arrive before the room code is known (joining clients), so also re-check here.
            if (!_busy && _svc != null)
            {
                bool mayTry = LastError == null || Time.unscaledTime >= _retryAt;
                if (WantVoice && _code != _svc.RoomCode && mayTry) _ = StartAsync(_svc.RoomCode);
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
            int gen = _generation;
            string code = _code;
            var vivox = VivoxService.Instance;
            try
            {
                var life = LocalLife();
                var state = life != null ? life.State.Value : LifeState.Disconnected;
                bool meeting = NightDirector.Instance != null && NightDirector.Instance.MeetingRunning;
                var desired = VoiceRules.Joined(state).Select(c => (c, VoiceRules.ChannelName(code, c))).ToList();
                var to = VoiceRules.TransmitTo(state, meeting);
                var transmit = to.HasValue ? VoiceRules.ChannelName(code, to.Value) : null;

                // Stop talking into a channel we may no longer use before the slow joins (a new mini ghost must not
                // be heard by the living while the ghost channel connects).
                if (_transmit != null && _transmit != transmit)
                {
                    await vivox.SetChannelTransmissionModeAsync(TransmissionMode.None);
                    if (gen != _generation) return;
                    _transmit = null;
                    Debug.Log("VOICE_TRANSMIT channel=-");
                }

                foreach (var (channel, name) in desired)
                {
                    if (_joined.Contains(name)) continue;
                    if (channel == VoiceChannel.Positional)
                        await vivox.JoinPositionalChannelAsync(name, ChatCapability.AudioOnly,
                            new Channel3DProperties(VoiceRules.AudibleDistance, VoiceRules.ConversationalDistance, 1f, AudioFadeModel.InverseByDistance));
                    else
                        await vivox.JoinGroupChannelAsync(name, ChatCapability.AudioOnly);
                    if (gen != _generation) return;
                    _joined.Add(name);
                    _appliedVolume = -1f;
                    Debug.Log($"VOICE_JOINED channel={name}");
                }
                foreach (var name in _joined.Where(n => desired.All(d => d.Item2 != n)).ToList())
                {
                    await vivox.LeaveChannelAsync(name);
                    if (gen != _generation) return;
                    _joined.Remove(name);
                    Debug.Log($"VOICE_LEFT channel={name}");
                }

                if (transmit != null && transmit != _transmit && _joined.Contains(transmit))
                {
                    await vivox.SetChannelTransmissionModeAsync(TransmissionMode.Single, transmit);
                    if (gen != _generation) return;
                    _transmit = transmit;
                    Debug.Log($"VOICE_TRANSMIT channel={transmit}");
                }

                if (!Mathf.Approximately(_appliedVolume, GameSettings.VoiceVolume))
                {
                    _appliedVolume = GameSettings.VoiceVolume;
                    int v = VoiceRules.ChannelVolume(GameSettings.VoiceVolume);
                    foreach (var name in _joined.ToList()) await vivox.SetChannelVolumeAsync(name, v);
                    if (gen != _generation) return;
                }

                UpdatePosition(life, code);
                ReadSpeakers(life, code, meeting);
            }
            catch (Exception e)
            {
                if (gen == _generation) Fail(e);
            }
            finally { _busy = false; }
        }

        void UpdatePosition(PlayerLife life, string code)
        {
            var pos = VoiceRules.ChannelName(code, VoiceChannel.Positional);
            if (life == null || !_joined.Contains(pos)) return;
            var head = life.transform.Find("HeadPivot");
            VivoxService.Instance.Set3DPosition(head != null ? head.gameObject : life.gameObject, pos);
        }

        void ReadSpeakers(PlayerLife life, string code, bool meeting)
        {
            _speaking.Clear();
            var channels = VivoxService.Instance.ActiveChannels;
            var positional = VoiceRules.ChannelName(code, VoiceChannel.Positional);
            var spawn = NetworkManager.Singleton != null ? NetworkManager.Singleton.SpawnManager : null;
            foreach (var name in _joined)
            {
                if (!channels.TryGetValue(name, out var participants)) continue;
                foreach (var p in participants)
                {
                    if (!p.SpeechDetected) continue;
                    if (p.IsSelf)
                    {
                        // Loud talking is a noise the Krasue hears (GDD 6.4); quiet talk is safe.
                        // Meetings pause the Krasue, so no noise reports then.
                        if (!meeting && name == _transmit && life != null && Time.realtimeSinceStartupAsDouble - _lastSend >= VoiceRules.ClientSendInterval)
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
                    if (!ulong.TryParse(p.DisplayName, out var id)) continue;
                    // Positional speech beyond earshot must not reveal who is talking across the house.
                    if (name == positional && life != null && spawn != null)
                    {
                        var other = spawn.GetPlayerNetworkObject(id);
                        if (other != null && Vector3.Distance(other.transform.position, life.transform.position) > VoiceRules.AudibleDistance) continue;
                    }
                    _speaking.Add(id);
                }
            }
        }
    }
}
