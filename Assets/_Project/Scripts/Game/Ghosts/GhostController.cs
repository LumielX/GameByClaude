using System.Linq;
using BooTrayal.Core;
using BooTrayal.Core.Ghosts;
using BooTrayal.Core.Player;
using BooTrayal.Game.Items;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace BooTrayal.Game.Ghosts
{
    /// <summary>
    /// Server-run ghost: gathers senses (sight raycasts, flashlights, NoiseBus), ticks the Core GhostBrain and
    /// drives a NavMeshAgent. Clients only see the synced transform and state.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GhostController : NetworkBehaviour
    {
        public const float HeadHeight = 1.4f;
        const float ArriveDistance = 0.8f;
        const float ChaseRepathSeconds = 0.25f;
        const float FleeDistance = 8f;
        const float NavSampleRadius = 3f;

        public readonly NetworkVariable<GhostState> StateNet =
            new NetworkVariable<GhostState>(GhostState.Wander, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Current target's client id, or ulong.MaxValue for none.</summary>
        public readonly NetworkVariable<ulong> TargetNet =
            new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly GhostBrain _brain = new GhostBrain();
        NavMeshAgent _agent;
        IRandom _rng;
        GhostWaypoint[] _waypoints = new GhostWaypoint[0];
        int _waypoint = -1;
        Vector3 _noisePoint;
        Vector3 _lightSource;
        float _repath;
        bool _waiting;
        GhostState _destinationFor = (GhostState)255;

        public GhostBrain Brain => _brain;
        Vector3 Head => transform.position + Vector3.up * HeadHeight;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public override void OnNetworkSpawn()
        {
            GhostRegistry.Register(transform);
            if (!IsServer)
            {
                _agent.enabled = false;
                return;
            }
            _rng = new SeededRandom(System.Environment.TickCount);
            _waypoints = FindObjectsByType<GhostWaypoint>().OrderBy(w => w.Index).ToArray();
            var args = ConnectionService.Instance != null ? ConnectionService.Instance.Args : null;
            _waiting = args != null && args.GhostWait;
            NoiseBus.Raised += OnNoise;
            Debug.Log($"GHOST_SPAWNED at={transform.position} waypoints={_waypoints.Length} wait={_waiting}");
        }

        public override void OnNetworkDespawn()
        {
            GhostRegistry.Unregister(transform);
            if (IsServer) NoiseBus.Raised -= OnNoise;
        }

        void OnNoise(Vector3 position, Noise noise)
        {
            if (!IsSpawned) return;
            float distance = Vector3.Distance(Head, position);
            if (!_brain.OnNoise(distance, noise.Radius)) return;
            _noisePoint = OnNavMesh(position);
            _waiting = false;
            _destinationFor = (GhostState)255;
            Debug.Log($"GHOST_HEARD at={position} d={distance:0.0} t={Time.time:0.00}");
            Publish();
        }

        void Update()
        {
            if (!IsSpawned || !IsServer) return;
            float dt = Time.deltaTime;
            var senses = Gather();
            var e = _brain.Tick(dt, senses);
            Handle(e);
            Publish();
            Move(dt);
        }

        void Handle(GhostEvent e)
        {
            switch (e)
            {
                case GhostEvent.Alerted:
                    _waiting = false;
                    break;
                case GhostEvent.Grabbed:
                {
                    var life = LifeOf(_brain.TargetId);
                    if (life != null)
                    {
                        life.SetGrabbed(true);
                        life.GetComponent<PlayerHands>()?.ForceRelease("grabbed");
                    }
                    Debug.Log($"GHOST_GRAB target={_brain.TargetId} t={Time.time:0.00}");
                    break;
                }
                case GhostEvent.Killed:
                    LifeOf(_brain.TargetId)?.Kill();
                    break;
                case GhostEvent.Spared:
                    Debug.Log($"GHOST_SPARED target={TargetNet.Value}");
                    break;
            }
            if (e != GhostEvent.None) _destinationFor = (GhostState)255;
        }

        void Publish()
        {
            ulong target = _brain.HasTarget ? _brain.TargetId : ulong.MaxValue;
            if (StateNet.Value == _brain.State && TargetNet.Value == target) return;
            StateNet.Value = _brain.State;
            TargetNet.Value = target;
            Debug.Log($"GHOST_STATE state={_brain.State} target={(_brain.HasTarget ? _brain.TargetId.ToString() : "-")} t={Time.time:0.00}");
        }

        // ---- Senses ----

        GhostSenses Gather()
        {
            var s = new GhostSenses();
            var eye = Head;
            var forward = transform.forward;
            float nearest = float.MaxValue;
            float bestLight = float.MaxValue;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null) continue;
                var life = player.GetComponent<PlayerLife>();
                if (life == null || !life.IsAlive) continue;
                var head = HeadOf(player);
                var toPlayer = head - eye;
                float dist = toPlayer.magnitude;
                bool clear = dist <= GhostTuning.SightRange && LineClear(eye, head);

                if (_brain.HasTarget && client.ClientId == _brain.TargetId)
                {
                    s.TargetValid = true;
                    s.TargetVisible = clear;
                    s.TargetDistance = Vector3.Distance(transform.position, player.transform.position);
                    s.TargetHarmable = GhostTargeting.IsHarmable(client.ClientId);
                }

                if (!_brain.IsIgnoring(client.ClientId) && dist < nearest &&
                    GhostPerception.CanSee(dist, Angle(forward, toPlayer), clear))
                {
                    nearest = dist;
                    s.SeesPlayer = true;
                    s.SeenId = client.ClientId;
                }

                var flash = player.GetComponent<PlayerFlashlight>();
                if (flash == null || !flash.Shining) continue;
                var beam = player.transform.Find("HeadPivot");
                var toGhost = eye - beam.position;
                float lightDist = toGhost.magnitude;
                if (lightDist < bestLight && GhostPerception.IsLit(lightDist, Angle(beam.forward, toGhost), LineClear(beam.position, eye)))
                {
                    bestLight = lightDist;
                    s.Lit = true;
                    _lightSource = beam.position;
                }
            }

            if (_brain.State == GhostState.Investigate)
            {
                // Measured to the noise point itself, not the agent's path (which may still be the previous one).
                var off = _noisePoint - transform.position;
                s.ReachedPoint = Mathf.Abs(off.y) < 1.5f && new Vector2(off.x, off.z).magnitude <= ArriveDistance;
            }
            return s;
        }

        static Vector3 HeadOf(NetworkObject player)
        {
            var head = player.transform.Find("HeadPivot");
            return head != null ? head.position : player.transform.position + Vector3.up * 1.5f;
        }

        static float Angle(Vector3 a, Vector3 b) => GhostPerception.AngleDeg(a.x, a.y, a.z, b.x, b.y, b.z);

        /// <summary>No world geometry between a and b (players, items and triggers do not block).</summary>
        static bool LineClear(Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            foreach (var hit in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.attachedRigidbody == null && !(hit.collider is CharacterController)) return false;
            return true;
        }

        PlayerLife LifeOf(ulong clientId)
        {
            var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            return player != null ? player.GetComponent<PlayerLife>() : null;
        }

        // ---- Movement ----

        void Move(float dt)
        {
            if (!_agent.isOnNavMesh) return;
            var state = _brain.State;
            _agent.speed = _waiting && state == GhostState.Wander ? 0f : _brain.Speed;
            _agent.isStopped = _agent.speed <= 0f;
            bool entered = _destinationFor != state;
            _destinationFor = state;

            switch (state)
            {
                case GhostState.Wander:
                case GhostState.Cooldown:
                    if (_waiting) break;
                    if (entered || _waypoint < 0 || (!_agent.pathPending && _agent.remainingDistance <= ArriveDistance))
                        NextWaypoint();
                    break;
                case GhostState.Investigate:
                    if (entered) _agent.SetDestination(_noisePoint);
                    break;
                case GhostState.Chase:
                    _repath -= dt;
                    if (entered || _repath <= 0f)
                    {
                        _repath = ChaseRepathSeconds;
                        var target = NetworkManager.SpawnManager.GetPlayerNetworkObject(_brain.TargetId);
                        if (target != null) _agent.SetDestination(OnNavMesh(target.transform.position));
                    }
                    break;
                case GhostState.Flee:
                    if (entered)
                    {
                        var away = transform.position - _lightSource;
                        away.y = 0f;
                        if (away.sqrMagnitude < 1e-3f) away = -transform.forward;
                        _agent.SetDestination(OnNavMesh(transform.position + away.normalized * FleeDistance));
                    }
                    break;
                case GhostState.Alert:
                case GhostState.Grab:
                    FaceTarget(dt);
                    break;
            }
        }

        void NextWaypoint()
        {
            if (_waypoints.Length == 0) return;
            int next = _rng.Next(0, _waypoints.Length);
            if (next == _waypoint && _waypoints.Length > 1) next = (next + 1) % _waypoints.Length;
            _waypoint = next;
            _agent.SetDestination(OnNavMesh(_waypoints[next].transform.position));
        }

        /// <summary>The warning stop: turn to look at the target (GDD 6.1 "ผีชะงัก").</summary>
        void FaceTarget(float dt)
        {
            var target = NetworkManager.SpawnManager.GetPlayerNetworkObject(_brain.TargetId);
            if (target == null) return;
            var dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * dt);
        }

        Vector3 OnNavMesh(Vector3 p) =>
            NavMesh.SamplePosition(p, out var hit, NavSampleRadius, NavMesh.AllAreas) ? hit.position : transform.position;
    }
}
