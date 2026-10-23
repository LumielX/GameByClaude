using System.Collections.Generic;
using System.Linq;
using System.Text;
using BooTrayal.Core;
using BooTrayal.Core.Ghosts;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Ghosts;
using BooTrayal.Game.Items;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Player;
using BooTrayal.Game.World;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Night
{
    /// <summary>
    /// Runs the game in the House (GDD 4): Lobby → nights (roles, seals, ritual, gate, meetings, Spy powers,
    /// mini ghosts) → summary → … → game over → Lobby. All rules are Core NightSession/GameSession; this class only
    /// moves players and objects, syncs state and sends private data (roles) to the right client only.
    /// </summary>
    public class NightDirector : NetworkBehaviour
    {
        public const float SummarySeconds = 12f;
        public const float GameOverSeconds = 15f;
        public const float ReviveSeconds = 3f;
        public const float ReviveRadius = 3f;
        public const float EscapeZ = -22.6f;
        public const float EscapeHalfWidth = 4f;
        const float PublishInterval = 0.25f;

        public static NightDirector Instance { get; private set; }

        public GameObject SoulOrbPrefab;
        public GhostSpawner GhostSpawner;
        public RitualAltar Altar;
        public Transform Shrine;

        public readonly NetworkVariable<SessionPhase> Phase = Server(SessionPhase.Lobby);
        public readonly NetworkVariable<int> NightNumber = Server(0);
        public readonly NetworkVariable<int> NightsTotal = Server(0);
        public readonly NetworkVariable<float> SecondsLeft = Server(0f);
        public readonly NetworkVariable<int> SealsDone = Server(0);
        public readonly NetworkVariable<int> SealsTotal = Server(0);
        public readonly NetworkVariable<int> RitualDelivered = Server(0);
        public readonly NetworkVariable<int> RitualRequired = Server(0);
        public readonly NetworkVariable<bool> GateOpen = Server(false);
        public readonly NetworkVariable<MeetingPhase> MeetPhase = Server(MeetingPhase.None);
        public readonly NetworkVariable<float> MeetTimeLeft = Server(0f);

        static NetworkVariable<T> Server<T>(T value) =>
            new NetworkVariable<T>(value, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---- Client-side knowledge (this peer only) ----
        public bool HasRole { get; private set; }
        public Role MyRole { get; private set; }
        public IReadOnlyList<ulong> MyPartners { get; private set; } = new ulong[0];
        public string LastSummary { get; private set; } = "";
        public string LastMeetingResult { get; private set; } = "";
        public float MeetingResultUntil { get; private set; }
        /// <summary>Who showed the cold breath most recently, and how many traces this peer has seen (smoke bots use these).</summary>
        public ulong? LastSpyTraceId { get; private set; }
        public int SpyTraceCount { get; private set; }

        // ---- Server state ----
        GameSession _game;
        NightSession _night;
        IRandom _rng;
        SealStation[] _stations = new SealStation[0];
        readonly Dictionary<ulong, SoulOrb> _orbs = new Dictionary<ulong, SoulOrb>();
        readonly Dictionary<ulong, float> _reviveHold = new Dictionary<ulong, float>();
        float _phaseTimer;
        float _publishTimer;
        bool _autoStarted;
        MeetingPhase _lastMeetPhase;

        LaunchArgs Args => ConnectionService.Instance != null ? ConnectionService.Instance.Args : LaunchArgs.Parse(new string[0]);
        public bool InNight => Phase.Value == SessionPhase.InNight;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            GateOpen.OnValueChanged += OnGateChanged;
            ApplyGate(GateOpen.Value);
            Phase.OnValueChanged += OnPhaseChanged;
            if (!IsServer) return;

            NightHooks.PlayerKilled = OnKilled;
            NightHooks.Power = OnPower;
            NightHooks.Vote = OnVote;
            NightHooks.RingBell = OnRing;
            NetworkManager.OnClientDisconnectCallback += OnClientLeft;
            _stations = FindObjectsByType<SealStation>().OrderBy(s => s.StationIndex).ToArray();
            foreach (var s in _stations) s.SolvedServer += OnSealSolved;
            if (Altar != null)
            {
                Altar.DeliveredServer += OnItemDelivered;
                Altar.CompletedServer += OnRitualComplete;
            }
            SetPhase(SessionPhase.Lobby);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            GateOpen.OnValueChanged -= OnGateChanged;
            Phase.OnValueChanged -= OnPhaseChanged;
            if (!IsServer) return;
            NightHooks.PlayerKilled = null;
            NightHooks.Power = null;
            NightHooks.Vote = null;
            NightHooks.RingBell = null;
            GhostTargeting.IsHarmable = _ => true;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
            foreach (var s in _stations) if (s != null) s.SolvedServer -= OnSealSolved;
            if (Altar != null)
            {
                Altar.DeliveredServer -= OnItemDelivered;
                Altar.CompletedServer -= OnRitualComplete;
            }
            if (ConnectionService.Instance != null) ConnectionService.Instance.Phase = SessionPhase.Lobby;
        }

        void OnPhaseChanged(SessionPhase previous, SessionPhase phase)
        {
            if (phase == SessionPhase.Lobby) HasRole = false;
        }

        // ================= Server flow =================

        void SetPhase(SessionPhase phase)
        {
            Phase.Value = phase;
            if (ConnectionService.Instance != null) ConnectionService.Instance.Phase = phase;
            Debug.Log($"PHASE phase={phase} night={NightNumber.Value}");
        }

        /// <summary>Host only: start a game from the lobby (HUD button or -autostart).</summary>
        public void StartGame()
        {
            if (!IsServer || Phase.Value != SessionPhase.Lobby) return;
            var cfg = new GameConfig();
            int nights = Args.Nights > 0 ? Args.Nights : cfg.Nights;
            _game = new GameSession(nights);
            foreach (var id in NetworkManager.ConnectedClientsIds) _game.AddPlayer(id);
            _rng = new SeededRandom(Args.Seed ?? System.Environment.TickCount);
            NightsTotal.Value = nights;
            BeginNight();
        }

        void BeginNight()
        {
            var cfg = new GameConfig();
            var ids = NetworkManager.ConnectedClientsIds.OrderBy(i => i).ToList();
            foreach (var id in ids) _game.AddPlayer(id);
            int spies = Args.NoSpy ? 0 : RoomRules.SpyCount(ids.Count, cfg.SpyEnabled, Args.Spies > 0 ? Args.Spies : cfg.RequestedSpies);
            float seconds = Args.NightSecondsFor(_game.NightIndex);
            if (seconds <= 0f) seconds = cfg.NightMinutes * 60f;
            _night = new NightSession(ids, spies, seconds, _rng);
            GhostTargeting.IsHarmable = id => _night != null && _night.IsHarmable(id);
            NightNumber.Value = _game.NightIndex + 1;

            ResetWorld();
            var picks = SealPlanner.Pick(_rng, _stations.Select(s => s.TwoPerson).ToList(), RoomRules.SealCount(ids.Count), ids.Count <= 2);
            for (int i = 0; i < _stations.Length; i++) _stations[i].ResetForNight(_rng, picks.Contains(i));
            SealsTotal.Value = picks.Count;
            SealsDone.Value = 0;
            if (Altar != null)
            {
                Altar.ResetForNight();
                Altar.Accepting.Value = picks.Count == 0;
                RitualRequired.Value = Altar.RequiredIds.Length;
            }
            RitualDelivered.Value = 0;
            GateOpen.Value = false;
            _lastMeetPhase = MeetingPhase.None;
            if (!Args.NoGhost && GhostSpawner != null) GhostSpawner.SpawnGhost();

            foreach (var id in ids)
                RoleRpc(_night.RoleOf(id), _night.Roles.SpyPartnersOf(id).ToArray(), RpcTarget.Single(id, RpcTargetUse.Temp));
            Debug.Log($"NIGHT_START night={NightNumber.Value} players={ids.Count} seals={picks.Count} seconds={seconds:0}");
            if (Application.isBatchMode) Debug.Log($"NIGHT_ROLES spies={string.Join(",", _night.Roles.Spies)}");
            SetPhase(SessionPhase.InNight);
            Publish();
        }

        /// <summary>Everyone alive at their spawn with full stamina, items home, no orbs, no ghost.</summary>
        void ResetWorld()
        {
            if (GhostSpawner != null) GhostSpawner.DespawnGhost();
            foreach (var orb in _orbs.Values) if (orb != null && orb.IsSpawned) orb.NetworkObject.Despawn(true);
            _orbs.Clear();
            _reviveHold.Clear();
            foreach (var item in GrabbableItem.All.ToList())
                if (item.GetComponent<SoulOrb>() == null) item.ResetHome();
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null) continue;
                player.GetComponent<PlayerLife>().SetState(LifeState.Alive);
                var point = PlayerSpawnPlacer.PointFor(client.ClientId);
                if (point != null) player.GetComponent<PlayerMotor>().TeleportRpc(point.position, point.rotation, true);
            }
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            float dt = Time.deltaTime;
            switch (Phase.Value)
            {
                case SessionPhase.Lobby:
                    if (!_autoStarted && Args.AutoStart > 0 && NetworkManager.ConnectedClientsIds.Count >= Args.AutoStart)
                    {
                        _autoStarted = true;
                        StartGame();
                    }
                    break;
                case SessionPhase.InNight:
                    TickNight(dt);
                    break;
                case SessionPhase.Summary:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) BeginNight();
                    break;
                case SessionPhase.GameOver:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) ToLobby();
                    break;
            }
            _publishTimer -= dt;
            if (_publishTimer <= 0f)
            {
                _publishTimer = PublishInterval;
                Publish();
            }
        }

        void Publish()
        {
            if (_night == null) return;
            SecondsLeft.Value = Mathf.Max(0f, (float)_night.SecondsLeft);
            var m = _night.ActiveMeeting;
            MeetPhase.Value = m != null ? m.Phase : MeetingPhase.None;
            MeetTimeLeft.Value = m != null ? m.TimeLeft : 0f;
        }

        void TickNight(float dt)
        {
            var v = _night.Tick(dt);
            var ghost = GhostSpawner != null ? GhostSpawner.Current : null;
            if (ghost != null)
            {
                ghost.Paused = _night.ActiveMeeting != null;
                ghost.Brain.SpeedMultiplier = _night.GhostSpeedMultiplier;
                ghost.Brain.HearingMultiplier = GhostTuning.KrasueHearing * _night.GhostHearingMultiplier;
            }

            var phase = _night.ActiveMeeting != null ? _night.ActiveMeeting.Phase : MeetingPhase.None;
            if (phase == MeetingPhase.Vote && _lastMeetPhase != MeetingPhase.Vote) Debug.Log("MEETING_VOTE_OPEN");
            _lastMeetPhase = phase;
            var outcome = _night.TakeOutcome();
            if (outcome.HasValue) ApplyOutcome(outcome.Value);

            if (GateOpen.Value) CheckEscapes();
            CheckRevives(dt);
            if (v.Ended) EndNight(v);
        }

        void CheckEscapes()
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null || !_night.IsAlive(client.ClientId)) continue;
                var p = player.transform.position;
                if (p.z > EscapeZ || Mathf.Abs(p.x) > EscapeHalfWidth) continue;
                if (!_night.Escape(client.ClientId)) continue;
                player.GetComponent<PlayerLife>().SetState(LifeState.Escaped);
                Debug.Log($"PLAYER_ESCAPED id={client.ClientId}");
            }
        }

        void CheckRevives(float dt)
        {
            if (Shrine == null) return;
            foreach (var kv in _orbs.ToList())
            {
                var orb = kv.Value;
                if (orb == null || !orb.IsSpawned) continue;
                var item = orb.GetComponent<GrabbableItem>();
                bool holding = item.Holders.Count > 0 && _night.IsAlive(item.Holders[0]);
                bool atShrine = Vector3.Distance(Flat(orb.transform.position), Flat(Shrine.position)) <= ReviveRadius;
                float held = holding && atShrine ? (_reviveHold.TryGetValue(kv.Key, out var h) ? h : 0f) + dt : 0f;
                _reviveHold[kv.Key] = held;
                if (held >= ReviveSeconds) Revive(kv.Key, item.Holders[0]);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void Revive(ulong id, ulong by)
        {
            if (!_night.Revive(id)) return;
            DespawnOrb(id);
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
            if (player != null)
            {
                player.GetComponent<PlayerLife>().SetState(LifeState.Alive);
                var spot = MeetingSpotFor((int)(id % 8));
                player.GetComponent<PlayerMotor>().TeleportRpc(spot.position, spot.rotation, true);
            }
            Debug.Log($"PLAYER_REVIVED id={id} by={by}");
        }

        void EndNight(NightVerdict v)
        {
            var awards = _night.FinishAwards(v);
            _game.ApplyAwards(awards);
            int nightNo = NightNumber.Value;
            Debug.Log($"NIGHT_END night={nightNo} winner={v.Winner} reason={v.Reason} sabotage={_night.Sabotage.Successes}/{_night.Sabotage.Required}");
            var summary = new StringBuilder();
            summary.AppendLine($"คืนที่ {nightNo} จบแล้ว — {WinnerText(v.Winner)} ({v.Reason})");
            summary.AppendLine(_night.Roles.Spies.Count == 0
                ? "คืนนี้ไม่มีร่างทรง (co-op)"
                : $"ร่างทรง (Spy): {string.Join(", ", _night.Roles.Spies.Select(PlayerAvatar.NameOf))} — ป่วนสำเร็จ {_night.Sabotage.Successes}/{_night.Sabotage.Required}");
            foreach (var id in NetworkManager.ConnectedClientsIds.OrderBy(i => i))
            {
                int coins = awards.TryGetValue(id, out var c) ? c : 0;
                int total = _game.WalletOf(id).TotalScore;
                Debug.Log($"AWARD id={id} coins={coins} total={total}");
                summary.AppendLine($"{PlayerAvatar.NameOf(id)}: +{coins} (รวม {total})");
            }
            SummaryRpc(nightNo, summary.ToString());

            if (GhostSpawner != null) GhostSpawner.DespawnGhost();
            foreach (var client in NetworkManager.ConnectedClientsList)
                client.PlayerObject?.GetComponent<PlayerLife>()?.SetGrabbed(false);
            foreach (var id in _orbs.Keys.ToList()) DespawnOrb(id);
            foreach (var s in _stations) s.Active.Value = false;
            _night = null;
            GhostTargeting.IsHarmable = _ => true;
            MeetPhase.Value = MeetingPhase.None;

            _game.NextNight();
            if (_game.IsOver)
            {
                var ranking = _game.Ranking();
                Debug.Log($"GAME_OVER ranking={string.Join(",", ranking.Select(r => $"{r.Id}:{r.Score}"))}");
                var text = new StringBuilder("จบเกม! อันดับรวม\n");
                for (int i = 0; i < ranking.Count; i++) text.AppendLine($"{i + 1}. {PlayerAvatar.NameOf(ranking[i].Id)} — {ranking[i].Score}");
                GameOverRpc(text.ToString());
                _phaseTimer = GameOverSeconds;
                SetPhase(SessionPhase.GameOver);
            }
            else
            {
                _phaseTimer = SummarySeconds;
                SetPhase(SessionPhase.Summary);
            }
        }

        static string WinnerText(NightWinner w) =>
            w == NightWinner.Crew ? "ทีมชนะ (Crew win)" : w == NightWinner.Spies ? "ร่างทรงชนะ (Spy win)" : "ไม่มีใครชนะ (No winner)";

        void ToLobby()
        {
            _game = null;
            ResetWorld();
            foreach (var s in _stations) s.Active.Value = false;
            if (Altar != null) Altar.Accepting.Value = false;
            GateOpen.Value = false;
            NightNumber.Value = 0;
            SetPhase(SessionPhase.Lobby);
        }

        // ================= Events from the world =================

        bool OnKilled(ulong victim, Vector3 position)
        {
            if (!InNight || _night == null || !_night.Kill(victim)) return false;
            MakeMiniGhost(victim, position, "ghost");
            return true;
        }

        void MakeMiniGhost(ulong id, Vector3 position, string cause)
        {
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
            if (player != null) player.GetComponent<PlayerLife>().SetState(LifeState.MiniGhost);
            Debug.Log($"PLAYER_MINIGHOST id={id} cause={cause}");
            if (!_night.CanRevive(id) || SoulOrbPrefab == null) return;
            var orb = Instantiate(SoulOrbPrefab, position + Vector3.up * 0.6f, Quaternion.identity);
            orb.GetComponent<NetworkObject>().Spawn(true);
            var soul = orb.GetComponent<SoulOrb>();
            soul.SoulOf.Value = id;
            _orbs[id] = soul;
            Debug.Log($"SOUL_SPAWNED id={id} at={orb.transform.position}");
        }

        void DespawnOrb(ulong id)
        {
            if (_orbs.TryGetValue(id, out var orb) && orb != null && orb.IsSpawned)
            {
                orb.GetComponent<GrabbableItem>().ForceDropAll();
                orb.NetworkObject.Despawn(true);
            }
            _orbs.Remove(id);
            _reviveHold.Remove(id);
        }

        void OnClientLeft(ulong id)
        {
            if (_night == null) return;
            _night.PlayerLeft(id);
            DespawnOrb(id);
            Debug.Log($"NIGHT_PLAYER_LEFT id={id}");
        }

        void OnSealSolved(SealStation s)
        {
            if (_night == null) return;
            _night.SealClosed(s.Contributors);
            SealsDone.Value = _night.SealsClosed;
            Debug.Log($"SEAL_SOLVED station={s.StationIndex} kind={s.Kind} by={string.Join(",", s.Contributors)} done={SealsDone.Value}/{SealsTotal.Value}");
            if (SealsDone.Value >= SealsTotal.Value && Altar != null)
            {
                Altar.Accepting.Value = true;
                Debug.Log("RITUAL_OPEN");
            }
        }

        void OnItemDelivered(GrabbableItem item, IReadOnlyCollection<ulong> by)
        {
            if (_night == null) return;
            _night.ItemDelivered(by);
            RitualDelivered.Value = Altar.Delivered.Value;
        }

        void OnRitualComplete()
        {
            if (_night == null) return;
            _night.CompleteRitual();
            GateOpen.Value = true;
            Debug.Log("GATE_OPEN");
        }

        void OnRing(ulong caller)
        {
            if (!InNight || _night == null) return;
            var ghost = GhostSpawner != null ? GhostSpawner.Current : null;
            bool chased = ghost != null && ghost.IsChasing(caller);
            var scale = Args.MeetingScale;
            if (!_night.RingBell(caller, chased, MeetingRules.DiscussSeconds * scale, MeetingRules.VoteSeconds * scale))
            {
                Debug.Log($"BELL_DENIED by={caller} chased={chased}");
                return;
            }
            int i = 0;
            foreach (var id in _night.ActiveMeeting.Voters.OrderBy(x => x))
            {
                var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(id);
                if (player == null) continue;
                var spot = MeetingSpotFor(i++);
                player.GetComponent<PlayerMotor>().TeleportRpc(spot.position, spot.rotation, false);
            }
            Debug.Log($"MEETING_START caller={caller}");
            Publish();
        }

        void OnVote(ulong voter, ulong? target)
        {
            var m = _night?.ActiveMeeting;
            if (m == null || !m.Cast(voter, target)) return;
            Debug.Log($"VOTE_CAST by={voter} target={(target.HasValue ? target.Value.ToString() : "skip")}");
        }

        void ApplyOutcome(MeetingOutcome o)
        {
            Debug.Log($"MEETING_RESULT ejected={(o.Ejected ? o.Target.ToString() : "-")} spy={o.WasSpy}");
            string text;
            if (!o.Ejected) text = "ไม่มีใครถูกไล่ (No one ejected)";
            else
            {
                var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(o.Target);
                MakeMiniGhost(o.Target, player != null ? player.transform.position : Shrine.position, "vote");
                text = o.WasSpy
                    ? $"{PlayerAvatar.NameOf(o.Target)} คือร่างทรง! ไล่ถูก ผีอ่อนแรงลง +60 วิ (Correct!)"
                    : $"{PlayerAvatar.NameOf(o.Target)} ไม่ใช่ร่างทรง! ผีโกรธ 30 วิ (Wrong!)";
            }
            MeetingResultRpc(text);
        }

        void OnPower(ulong spy, SpyPower power)
        {
            if (!InNight || _night == null) return;
            if (!_night.TryUsePower(spy, power, out var reason))
            {
                Debug.Log($"POWER_DENIED by={spy} power={power} reason={reason}");
                return;
            }
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(spy);
            var pos = player != null ? player.transform.position : Vector3.zero;
            bool counted;
            if (power == SpyPower.SabotagePuzzle)
            {
                ISabotageable target = null;
                float best = SpyPowerRules.SabotageRange;
                foreach (var s in _stations.Cast<ISabotageable>().Append(Altar))
                {
                    if (s == null || !s.CanSabotage) continue;
                    float d = s.DistanceTo(pos);
                    if (d <= best) { best = d; target = s; }
                }
                bool had = target != null && target.Sabotage(_rng, spy);
                counted = _night.RecordSabotage(spy, had);
                if (target is SafeStation || target is FuseStation) Debug.Log($"SABOTAGE station={((SealStation)target).StationIndex} had={had}");
                if (target is RitualAltar) RitualDelivered.Value = Altar.Delivered.Value;
            }
            else
            {
                var zone = NightZone.Find(pos);
                bool crewInZone = zone != null && NetworkManager.ConnectedClientsList.Any(c =>
                    c.ClientId != spy && c.PlayerObject != null && _night.IsAlive(c.ClientId) &&
                    _night.RoleOf(c.ClientId) == Role.Crew && zone.Contains(c.PlayerObject.transform.position));
                counted = _night.RecordSabotage(spy, crewInZone);
                if (zone != null) BlackoutRpc(zone.Index, SpyPowerRules.BlackoutSeconds);
            }
            SpyTraceRpc(spy);
            Debug.Log($"POWER_USED by={spy} power={power} counted={counted} successes={_night.Sabotage.Successes}/{_night.Sabotage.Required}");
        }

        Transform MeetingSpotFor(int index)
        {
            var spots = FindObjectsByType<MeetingSpot>().OrderBy(s => s.Index).ToArray();
            return spots.Length > 0 ? spots[index % spots.Length].transform : Shrine;
        }

        // ================= Client RPCs =================

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void RoleRpc(Role role, ulong[] partners, RpcParams rpcParams)
        {
            HasRole = true;
            MyRole = role;
            MyPartners = partners ?? new ulong[0];
            Debug.Log($"ROLE_RECEIVED role={role} partners={string.Join(",", MyPartners)}");
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void SpyTraceRpc(ulong spy)
        {
            LastSpyTraceId = spy;
            SpyTraceCount++;
            if (Application.isBatchMode) Debug.Log($"SPY_TRACE id={spy}");
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(spy);
            if (player != null) NightEffects.ShowColdBreath(player.transform, SpyPowerRules.TraceSeconds);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void BlackoutRpc(int zoneIndex, float seconds)
        {
            var zone = NightZone.Ordered().FirstOrDefault(z => z.Index == zoneIndex);
            if (zone == null) return;
            if (Application.isBatchMode) Debug.Log($"BLACKOUT zone={zone.ZoneName}");
            StartCoroutine(NightEffects.Blackout(zone, seconds));
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void SummaryRpc(int night, string text)
        {
            LastSummary = text;
            if (Application.isBatchMode) Debug.Log($"SUMMARY_RECEIVED night={night}");
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void GameOverRpc(string text)
        {
            LastSummary = text;
            if (Application.isBatchMode) Debug.Log("GAME_OVER_RECEIVED");
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void MeetingResultRpc(string text)
        {
            LastMeetingResult = text;
            MeetingResultUntil = Time.time + 6f;
        }

        void OnGateChanged(bool previous, bool open) => ApplyGate(open);

        static void ApplyGate(bool open)
        {
            var gate = GameObject.Find("EscapeGate");
            if (gate == null) return;
            var col = gate.GetComponent<Collider>();
            if (col != null) col.enabled = !open;
            var r = gate.GetComponent<Renderer>();
            if (r != null) r.enabled = !open;
        }
    }
}
