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
        const string VersionProperty = "ver";

        static readonly HashSet<string> RejectionReasons = new HashSet<string>
        {
            "version_mismatch", "room_full", "game_in_progress", "bad_payload"
        };

        ISession _session;
        int _maxPlayers = RoomRules.MaxPlayers;
        bool _hooked;
        bool _busy;

        public static ConnectionService Instance { get; private set; }
        public LaunchArgs Args { get; private set; }
        public string PlayerDisplayName { get; private set; }
        public NetStatus Status { get; private set; } = NetStatus.Offline;
        public string LastMessageKey { get; private set; }
        public string RoomCode { get; private set; }
        public int ConnectedCount { get; private set; }
        /// <summary>Host-side game phase used by the join rules. M5's NightDirector drives it.</summary>
        public SessionPhase Phase { get; set; } = SessionPhase.Lobby;

        public event Action Changed;

        NetworkManager Net => NetworkManager.Singleton;

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

        void Start()
        {
            if (Args.AutoHostLocal) StartLocalHost();
            else if (Args.AutoJoinLocal) StartLocalClient();
            else if (Args.AutoHost) _ = CreateRoomAsync(RoomRules.MaxPlayers);
            else if (Args.AutoJoinCode != null) _ = JoinRoomAsync(Args.AutoJoinCode);
        }

        // ------------------------------------------------------------------ public API

        public async Task<bool> CreateRoomAsync(int maxPlayers)
        {
            if (!BeginOperation(NetStatus.SigningIn)) return false;
            try
            {
                if (!await EnsureServicesAsync()) return false;
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
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                OnHosting(_session.Code);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Create failed: {e}");
                await FailAsync(MapError(e));
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        public async Task<bool> JoinRoomAsync(string rawCode)
        {
            var code = JoinCode.Normalize(rawCode);
            if (!JoinCode.IsValid(code))
            {
                await FailAsync("code_invalid");
                return false;
            }
            if (!BeginOperation(NetStatus.SigningIn)) return false;
            try
            {
                if (!await EnsureServicesAsync()) return false;
                PrepareNetwork(RoomRules.MaxPlayers);
                SetStatus(NetStatus.Joining);
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                RoomCode = code;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConnectionService] Join failed: {e}");
                var reason = Net != null && !string.IsNullOrEmpty(Net.DisconnectReason) ? Net.DisconnectReason : MapError(e);
                await FailAsync(reason);
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Solo mode and offline tests: plain NGO host on localhost, no Unity Services.</summary>
        public bool StartLocalHost()
        {
            if (!BeginOperation(NetStatus.Hosting)) return false;
            _busy = false;
            PrepareNetwork(RoomRules.MaxPlayers);
            Net.GetComponent<UnityTransport>().SetConnectionData(LocalAddress, LocalPort);
            if (!Net.StartHost())
            {
                _ = FailAsync("connect_failed");
                return false;
            }
            OnHosting("LOCAL");
            return true;
        }

        public bool StartLocalClient()
        {
            if (!BeginOperation(NetStatus.Joining)) return false;
            _busy = false;
            PrepareNetwork(RoomRules.MaxPlayers);
            Net.GetComponent<UnityTransport>().SetConnectionData(LocalAddress, LocalPort);
            if (!Net.StartClient())
            {
                _ = FailAsync("connect_failed");
                return false;
            }
            return true;
        }

        public async Task LeaveAsync()
        {
            await CleanupAsync();
            SetStatus(NetStatus.Offline);
        }

        // ------------------------------------------------------------------ internals

        bool BeginOperation(NetStatus status)
        {
            if (_busy || Status == NetStatus.Hosting || Status == NetStatus.Connected || Status == NetStatus.Joining)
                return false;
            _busy = true;
            LastMessageKey = null;
            SetStatus(status);
            return true;
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
                await FailAsync("services_unavailable");
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
            ConnectedCount = 1;
            SetStatus(NetStatus.Hosting);
            Debug.Log($"NET_HOSTING code={code}");
            if (!string.IsNullOrEmpty(Args.CodeFile))
            {
                var dir = Path.GetDirectoryName(Args.CodeFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Args.CodeFile, code);
            }
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
                ConnectedCount = Net.ConnectedClientsIds.Count;
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
                if (clientId == Net.LocalClientId) return;
                ConnectedCount = Math.Max(1, Net.ConnectedClientsIds.Count - 1);
                Debug.Log($"NET_CLIENT_LEFT id={clientId}");
                Changed?.Invoke();
                return;
            }

            var reason = Net.DisconnectReason;
            if (Status == NetStatus.Connected && string.IsNullOrEmpty(reason))
            {
                Debug.Log("NET_HOST_LOST");
                _ = FailAsync("host_left", logMarker: false);
            }
            else
            {
                _ = FailAsync(string.IsNullOrEmpty(reason) ? "connect_failed" : reason);
            }
        }

        async Task FailAsync(string reasonKey, bool logMarker = true)
        {
            if (logMarker)
                Debug.Log(RejectionReasons.Contains(reasonKey) ? $"NET_REJECTED reason={reasonKey}" : $"NET_FAILED reason={reasonKey}");
            LastMessageKey = reasonKey;
            await CleanupAsync();
            SetStatus(NetStatus.Offline);
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
                var msg = x.Message ?? "";
                if (msg.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0) return "room_full";
                if (msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0) return "room_not_found";
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
