using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BooTrayal.Core;
using BooTrayal.Core.Net;
using BooTrayal.Core.Rules;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace BooTrayal.Game.Networking
{
    public enum NetStatus { Offline, SigningIn, Hosting, Joining, Connected, Failed }

    /// <summary>
    /// Creates/joins rooms through Unity Multiplayer Sessions (Lobby + Relay) or plain local NGO,
    /// approves connections with Core rules and turns disconnects into reason keys (see NetMessages).
    /// Log lines starting with NET_ are read by tools/unity/net-smoke.ps1.
    /// </summary>
    public sealed class ConnectionService : MonoBehaviour
    {
        public const string LocalAddress = "127.0.0.1";
        public const ushort LocalPort = 7777;
        /// <summary>RoomCode of a localhost room (Solo / same-PC tests): there is no code to share.</summary>
        public const string LocalCode = "LOCAL";
        const string VersionProperty = "ver";

        readonly ConnectionTracker _tracker = new ConnectionTracker();
        ISession _session;
        int _maxPlayers = RoomRules.MaxPlayers;
        bool _hooked;
        bool _busy;

        public static ConnectionService Instance { get; private set; }
        public LaunchArgs Args { get; private set; }
        public string PlayerDisplayName { get; private set; }
        public NetStatus Status { get; private set; } = NetStatus.Offline;
        public string LastMessageKey
        {
            get => _lastMessageKey;
            private set
            {
                _lastMessageKey = value;
                if (value != null) MessageSerial++;
            }
        }
        string _lastMessageKey;
        /// <summary>Goes up every time a new message key is reported, even the same one twice (the UI shows each).</summary>
        public int MessageSerial { get; private set; }
        public string RoomCode { get; private set; }
        public int ConnectedCount { get; private set; }
        /// <summary>Host-side game phase used by the join rules. M5's NightDirector drives it.</summary>
        public SessionPhase Phase { get; set; } = SessionPhase.Lobby;
        /// <summary>Room options chosen on the Create Room screen (host only; used by the NightDirector).</summary>
        public GameConfig HostConfig { get; set; } = new GameConfig();

        public event Action Changed;

        NetworkManager Net => NetworkManager.Singleton;

        bool IsActive => _busy || Status == NetStatus.Hosting || Status == NetStatus.Connected || Status == NetStatus.Joining;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Args = LaunchArgs.Parse(Environment.GetCommandLineArgs());
            PlayerDisplayName = PlayerName.Sanitize(Args.PlayerName, UnityEngine.Random.Range(1, 10000));
        }

        /// <summary>Host only, in the lobby: remove a player; they see the localized "kicked" message.</summary>
        public void Kick(ulong clientId)
        {
            if (Net == null || !Net.IsServer || clientId == Net.LocalClientId || Phase != SessionPhase.Lobby) return;
            Debug.Log($"KICK id={clientId}");
            Net.DisconnectClient(clientId, BooTrayal.Core.Net.DisconnectClassifier.Kicked);
        }

        /// <summary>The name typed on the Play screen (sanitized like every other name; used for the next connection).</summary>
        public void SetPlayerName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            PlayerDisplayName = PlayerName.Sanitize(raw, UnityEngine.Random.Range(1, 10000));
        }

        /// <summary>Applies -autohost/-autojoin flags. Called by SessionFlow once the Menu scene is up.</summary>
        public void RunLaunchArgs()
        {
            if (Args.AutoHostLocal) StartLocalHost();
            else if (Args.AutoJoinLocal) StartLocalClient();
            else if (Args.AutoHost) _ = CreateRoomAsync(RoomRules.MaxPlayers);
            else if (Args.AutoJoinCode != null) _ = JoinRoomAsync(Args.AutoJoinCode);
        }

        // ------------------------------------------------------------------ public API

        public async Task<bool> CreateRoomAsync(int maxPlayers)
        {
            int token = BeginOperation(NetStatus.SigningIn);
            if (token == 0) return false;
            try
            {
                if (!await EnsureServicesAsync() || !_tracker.IsCurrent(token)) return false;
                PrepareNetwork(maxPlayers);
                var options = new SessionOptions
                {
                    MaxPlayers = _maxPlayers,
                    IsPrivate = true,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { VersionProperty, new SessionProperty(GameVersion.Current) }
                    }
                }.WithRelayNetwork();
                var session = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (!_tracker.IsCurrent(token))
                {
                    await LeaveLateSessionAsync(session);
                    return false;
                }
                _session = session;
                OnHosting(session.Code);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Create failed: {e}");
                if (_tracker.IsCurrent(token)) await FailOnceAsync(MapError(e));
                return false;
            }
            finally
            {
                if (_tracker.IsCurrent(token)) _busy = false;
            }
        }

        public async Task<bool> JoinRoomAsync(string rawCode)
        {
            if (IsActive) return false;
            var code = JoinCode.Normalize(rawCode);
            if (!JoinCode.IsValid(code))
            {
                // Nothing is running yet, so there is nothing to tear down.
                Debug.Log("NET_FAILED reason=code_invalid");
                LastMessageKey = "code_invalid";
                Changed?.Invoke();
                return false;
            }

            int token = BeginOperation(NetStatus.SigningIn);
            if (token == 0) return false;
            try
            {
                if (!await EnsureServicesAsync() || !_tracker.IsCurrent(token)) return false;
                PrepareNetwork(RoomRules.MaxPlayers);
                SetStatus(NetStatus.Joining);
                var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                if (!_tracker.IsCurrent(token))
                {
                    await LeaveLateSessionAsync(session);
                    return false;
                }
                _session = session;
                RoomCode = code;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Join failed: {e}");
                if (_tracker.IsCurrent(token)) await FailOnceAsync(_tracker.RejectionKey ?? MapError(e));
                return false;
            }
            finally
            {
                if (_tracker.IsCurrent(token)) _busy = false;
            }
        }

        /// <summary>Solo mode and offline tests: plain NGO host on localhost, no Unity Services.</summary>
        public bool StartLocalHost()
        {
            // Hosting is announced by OnHosting once NGO is actually running as server.
            if (BeginOperation(NetStatus.SigningIn) == 0) return false;
            _busy = false;
            PrepareNetwork(RoomRules.MaxPlayers);
            Net.GetComponent<UnityTransport>().SetConnectionData(LocalAddress, LocalPort);
            if (!Net.StartHost())
            {
                _ = FailOnceAsync("connect_failed");
                return false;
            }
            OnHosting(LocalCode);
            return true;
        }

        public bool StartLocalClient()
        {
            if (BeginOperation(NetStatus.Joining) == 0) return false;
            _busy = false;
            PrepareNetwork(RoomRules.MaxPlayers);
            Net.GetComponent<UnityTransport>().SetConnectionData(LocalAddress, LocalPort);
            if (!Net.StartClient())
            {
                _ = FailOnceAsync("connect_failed");
                return false;
            }
            return true;
        }

        /// <summary>Leaves the room. Also cancels a create/join that is still in flight.</summary>
        public async Task LeaveAsync()
        {
            _tracker.Cancel();
            _busy = false;
            await CleanupAsync();
            LastMessageKey = null;
            SetStatus(NetStatus.Offline);
        }

        // ------------------------------------------------------------------ internals

        /// <summary>Returns the operation token, or 0 when another operation or a room is active.</summary>
        int BeginOperation(NetStatus status)
        {
            if (IsActive) return 0;
            int token = _tracker.Begin();
            _tracker.ResetClients();
            _busy = true;
            LastMessageKey = null;
            SetStatus(status);
            return token;
        }

        async Task<bool> EnsureServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(Args.Profile));
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Services unavailable: {e}");
                await FailOnceAsync("services_unavailable");
                return false;
            }
        }

        void PrepareNetwork(int maxPlayers)
        {
            _maxPlayers = Math.Max(RoomRules.MinPlayers, Math.Min(RoomRules.MaxPlayers, maxPlayers));
            if (!_hooked)
            {
                Net.ConnectionApprovalCallback = Approve;
                Net.OnClientConnectedCallback += OnClientConnected;
                Net.OnClientDisconnectCallback += OnClientDisconnected;
                _hooked = true;
            }
            var version = string.IsNullOrEmpty(Args.FakeVersion) ? GameVersion.Current : Args.FakeVersion;
            Net.NetworkConfig.ConnectionApproval = true;
            Net.NetworkConfig.ConnectionData = ConnectionPayload.Encode(version, PlayerDisplayName);
        }

        void OnHosting(string code)
        {
            RoomCode = code;
            ConnectedCount = Math.Max(1, _tracker.ClientCount);
            // Write the code file before logging the marker: the smoke script reads it right after.
            if (!string.IsNullOrEmpty(Args.CodeFile))
            {
                var dir = Path.GetDirectoryName(Args.CodeFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Args.CodeFile, code);
            }
            SetStatus(NetStatus.Hosting);
            Debug.Log($"NET_HOSTING code={code}");
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int connected = Net.ConnectedClientsIds.Count;
            var decision = ApprovalRules.Evaluate(request.Payload, GameVersion.Current, Phase, connected, _maxPlayers);
            response.Approved = decision.Approved;
            response.Reason = decision.Reason;
            response.CreatePlayerObject = decision.Approved;
            response.Pending = false;
            if (!decision.Approved) Debug.Log($"NET_REFUSED_CLIENT reason={decision.Reason}");
        }

        void OnClientConnected(ulong clientId)
        {
            if (Net.IsServer)
            {
                _tracker.ClientConnected(clientId);
                ConnectedCount = Math.Max(1, _tracker.ClientCount);
                if (clientId != Net.LocalClientId)
                    Debug.Log($"NET_CLIENT_JOINED id={clientId} count={ConnectedCount}");
                Changed?.Invoke();
                return;
            }
            if (clientId == Net.LocalClientId)
            {
                SetStatus(NetStatus.Connected);
                Debug.Log("NET_CONNECTED");
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (Net.IsServer)
            {
                // NGO also reports refused pending clients here; only count players who really joined.
                if (clientId == Net.LocalClientId || !_tracker.ClientDisconnected(clientId)) return;
                ConnectedCount = Math.Max(1, _tracker.ClientCount);
                Debug.Log($"NET_CLIENT_LEFT id={clientId}");
                Changed?.Invoke();
                return;
            }

            var raw = Net.DisconnectReason;
            _tracker.RecordRejection(raw);
            var key = DisconnectClassifier.Classify(Status == NetStatus.Connected, raw);
            if (!string.IsNullOrEmpty(raw) && raw != key) Debug.Log($"[ConnectionService] Disconnect detail: {raw}");
            if (key == "host_left")
            {
                if (!_tracker.TryMarkFailed()) return;
                Debug.Log("NET_HOST_LOST");
                _ = FailAsync(key, logMarker: false);
            }
            else
            {
                _ = FailOnceAsync(key);
            }
        }

        /// <summary>Reports the current operation's failure only once (disconnect callback and join catch can both see it).</summary>
        Task FailOnceAsync(string reasonKey)
        {
            return _tracker.TryMarkFailed() ? FailAsync(reasonKey) : Task.CompletedTask;
        }

        async Task FailAsync(string reasonKey, bool logMarker = true)
        {
            if (logMarker)
                Debug.Log(DisconnectClassifier.IsRejection(reasonKey) ? $"NET_REJECTED reason={reasonKey}" : $"NET_FAILED reason={reasonKey}");
            LastMessageKey = reasonKey;
            await CleanupAsync();
            SetStatus(NetStatus.Offline);
        }

        /// <summary>A create/join finished after the player pressed Leave: leave that session again.</summary>
        async Task LeaveLateSessionAsync(ISession session)
        {
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Late leave failed: {e.Message}");
            }
            if (Net != null && Net.IsListening && !Net.ShutdownInProgress)
                Net.Shutdown();
        }

        async Task CleanupAsync()
        {
            var session = _session;
            _session = null;
            if (session != null)
            {
                try
                {
                    await session.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ConnectionService] Leave failed: {e.Message}");
                }
            }
            if (Net != null && Net.IsListening && !Net.ShutdownInProgress)
                Net.Shutdown();
            _tracker.ResetClients();
            RoomCode = null;
            ConnectedCount = 0;
        }

        static string MapError(Exception e)
        {
            for (var x = e; x != null; x = x.InnerException)
            {
                if (x is SessionException se)
                {
                    switch (se.Error)
                    {
                        case SessionError.SessionNotFound:
                        case SessionError.SessionDeleted:
                            return "room_not_found";
                        case SessionError.NotAuthorized:
                        case SessionError.Forbidden:
                        case SessionError.RateLimitExceeded:
                            return "services_unavailable";
                    }
                }
                var key = NetErrorText.Classify(x.Message);
                if (key != null) return key;
            }
            return "connect_failed";
        }

        void SetStatus(NetStatus status)
        {
            Status = status;
            Changed?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
