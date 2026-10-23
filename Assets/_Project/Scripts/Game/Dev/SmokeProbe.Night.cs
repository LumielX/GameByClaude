using System.Collections;
using System.Linq;
using BooTrayal.Core.Night;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Items;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using BooTrayal.Game.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace BooTrayal.Game.Dev
{
    /// <summary>
    /// Night-loop bots for tools/unity/night-smoke.ps1.
    /// coop: two crew bots close every seal, deliver the ritual items and escape (night 1), then idle (night 2).
    /// spybot: five bots; night 1 the Spy sabotages a safe and blacks out the zone, the crew rings and votes it out;
    /// night 2 the crew wrongly votes out the host and revives it at the shrine while the Spy wins on time.
    /// </summary>
    public partial class SmokeProbe
    {
        static readonly Vector3 AltarCenter = new Vector3(-6f, 0f, -12f);

        static NightDirector D => NightDirector.Instance;
        static ulong LocalId => NetworkManager.Singleton.LocalClientId;

        IEnumerator WaitNight(int number)
        {
            while (D == null || !D.InNight || D.NightNumber.Value != number) yield return null;
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"PROBE_NIGHT night={number} role={D.MyRole}");
        }

        /// <summary>Follows the NavMesh path (run) so bots can use stairs and doorways.</summary>
        IEnumerator WalkPath(Vector3 target, float stopDistance, float timeout)
        {
            var player = LocalPlayer;
            var bot = EnableBot();
            var path = new NavMeshPath();
            Vector3[] corners = { target };
            if (NavMesh.SamplePosition(player.transform.position, out var a, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(target, out var b, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
                corners = path.corners.Skip(1).ToArray();
            float end = Time.time + timeout;
            bot.Run = true;
            for (int i = 0; i < corners.Length && Time.time < end; i++)
                yield return WalkTo(corners[i], i == corners.Length - 1 ? stopDistance : 0.4f, end - Time.time);
            bot.Run = false;
        }

        void Face(Vector3 point)
        {
            var d = point - LocalPlayer.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) LocalPlayer.GetComponent<PlayerLook>().SetYaw(Quaternion.LookRotation(d).eulerAngles.y);
        }

        static Interactable PartOf(NetworkBehaviour station, int part) =>
            station.GetComponentsInChildren<Interactable>().First(p => p.Part == part);

        IEnumerator Use(Interactable part)
        {
            LocalPlayer.GetComponent<PlayerActions>().RequestInteract(part);
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end) yield return null;
        }

        // ---------------- coop ----------------

        IEnumerator Coop()
        {
            yield return WaitNight(1);
            // The host (always id 0) leads: it solves the safes and drags the urn.
            bool leader = LocalId == NetworkManager.ServerClientId;
            float end = Time.time + 170f;
            while (Time.time < end)
            {
                var open = FindObjectsByType<SealStation>().Where(s => s.Active.Value && !s.Solved.Value).OrderBy(s => s.StationIndex).FirstOrDefault();
                if (open == null) break;
                Debug.Log($"PROBE_SEAL station={open.StationIndex} kind={open.Kind}");
                if (open is SafeStation safe)
                {
                    if (leader) yield return SolveSafe(safe);
                    else yield return WaitFor(() => open.Solved.Value, 60f);
                }
                else if (open is FuseStation fuse) yield return SolveFuse(fuse, leader ? 0 : 1);
                yield return null;
            }
            Debug.Log("PROBE_SEALS_DONE");

            var altar = FindAnyObjectByType<RitualAltar>();
            yield return WaitFor(() => altar.Accepting.Value, 20f);
            if (leader) yield return Deliver("urn", new Vector3(-6f, 0f, -13.4f));
            else yield return Deliver("incense", new Vector3(-4.7f, 0f, -12.4f));
            yield return WaitFor(() => D.GateOpen.Value, 60f);
            float x = leader ? -1f : 1f;
            yield return WalkPath(new Vector3(x, 0f, -20.5f), 0.3f, 40f);
            yield return WalkTo(new Vector3(x, 0f, -24f), 0.2f, 10f);
            Debug.Log("PROBE_ESCAPE_WALKED");
        }

        IEnumerator SolveSafe(SafeStation safe)
        {
            yield return WalkPath(PartOf(safe, 0).Approach.position, 0.3f, 45f);
            for (int attempt = 0; attempt < 3 && !safe.Solved.Value; attempt++)
            {
                foreach (var digit in safe.Digits) yield return Use(PartOf(safe, digit));
                yield return WaitFor(() => safe.Solved.Value, 1.5f);
            }
            Debug.Log($"PROBE_SAFE station={safe.StationIndex} solved={safe.Solved.Value}");
        }

        /// <summary>Both bots pull their lever at every even second of server time until the box closes (≤ 1 s apart).</summary>
        IEnumerator SolveFuse(FuseStation fuse, int lever)
        {
            var part = fuse.Lever(lever);
            yield return WalkPath(part.Approach.position, 0.3f, 45f);
            float end = Time.time + 60f;
            while (!fuse.Solved.Value && Time.time < end)
            {
                double next = System.Math.Ceiling(NetworkManager.Singleton.ServerTime.Time / 2.0) * 2.0;
                while (NetworkManager.Singleton.ServerTime.Time < next) yield return null;
                yield return Use(part);
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log($"PROBE_FUSE station={fuse.StationIndex} lever={lever} solved={fuse.Solved.Value}");
        }

        /// <summary>Carries (or drags) a ritual item to a spot by the altar, faces the altar and drops it there.</summary>
        IEnumerator Deliver(string itemId, Vector3 standPoint)
        {
            var hands = LocalPlayer.GetComponent<PlayerHands>();
            var item = GrabbableItem.All.First(i => i.ItemId == itemId);
            for (int attempt = 0; attempt < 4 && !item.Locked.Value; attempt++)
            {
                if (hands.HeldItem != item)
                {
                    yield return WalkPath(item.transform.position, 1.0f, 45f);
                    Face(item.transform.position);
                    for (int i = 0; i < 5 && hands.HeldItem != item; i++)
                    {
                        hands.RequestGrab(item);
                        yield return new WaitForSeconds(0.4f);
                    }
                    if (hands.HeldItem != item) continue;
                }
                yield return WalkPath(standPoint, 0.3f, 60f);
                Face(AltarCenter);
                yield return new WaitForSeconds(1.2f);
                EnableBot().Press(BotButton.Drop);
                yield return WaitFor(() => item.Locked.Value, 2f);
            }
            Debug.Log($"PROBE_DELIVERED item={itemId} locked={item.Locked.Value}");
        }

        // ---------------- spybot ----------------

        IEnumerator SpyBot()
        {
            var actions = LocalPlayer.GetComponent<PlayerActions>();
            bool host = LocalId == NetworkManager.ServerClientId;
            var bell = PartOf(FindAnyObjectByType<ShrineBell>(), 0);

            yield return WaitNight(1);
            bool spy = D.MyRole == Role.Spy;
            var safe = FindObjectsByType<SafeStation>().Where(s => s.Active.Value).OrderBy(s => s.StationIndex).First();
            var keyApproach = PartOf(safe, 0).Approach.position;
            if (host && !spy)
            {
                yield return WalkPath(keyApproach, 0.3f, 40f);
                var digits = safe.Digits;
                yield return Use(PartOf(safe, digits[0]));
                yield return Use(PartOf(safe, digits[1]));
                Debug.Log($"PROBE_DIGITS entered={safe.EnteredCount.Value}");
                yield return WaitFor(() => D.SpyTraceCount >= 2, 30f);
                yield return WalkPath(bell.Approach.position, 0.3f, 40f);
                yield return Use(bell);
            }
            else if (spy)
            {
                yield return WalkPath(keyApproach + new Vector3(0f, 0f, 1f), 0.3f, 40f);
                yield return WaitFor(() => safe.EnteredCount.Value >= 2, 40f);
                actions.RequestPower(SpyPower.SabotagePuzzle);
                yield return new WaitForSeconds(0.6f);
                actions.RequestPower(SpyPower.Blackout);
            }
            yield return WaitFor(() => D.MeetPhase.Value == MeetingPhase.Vote, 60f);
            actions.RequestVote(spy ? null : D.LastSpyTraceId);
            Debug.Log($"PROBE_VOTED target={(spy ? "skip" : D.LastSpyTraceId.ToString())}");

            yield return WaitNight(2);
            spy = D.MyRole == Role.Spy;
            if (host && !spy)
            {
                yield return WalkPath(bell.Approach.position, 0.3f, 40f);
                yield return Use(bell);
            }
            yield return WaitFor(() => D.MeetPhase.Value == MeetingPhase.Vote, 40f);
            actions.RequestVote(spy ? (ulong?)null : NetworkManager.ServerClientId);
            Debug.Log($"PROBE_VOTED target={(spy ? "skip" : "host")}");
            if (spy || host) yield break;

            // Crew: carry the host's soul to the shrine (the first bot to grab it wins, the others are refused).
            SoulOrb orb = null;
            yield return WaitFor(() => (orb = FindObjectsByType<SoulOrb>().FirstOrDefault(o => o.SoulOf.Value == NetworkManager.ServerClientId)) != null, 15f);
            if (orb == null) yield break;
            var hands = LocalPlayer.GetComponent<PlayerHands>();
            var item = orb.GetComponent<GrabbableItem>();
            yield return WalkPath(orb.transform.position, 1.0f, 15f);
            for (int i = 0; i < 6 && hands.HeldItem != item && item.Holders.Count == 0; i++)
            {
                Face(orb.transform.position);
                hands.RequestGrab(item);
                yield return new WaitForSeconds(0.4f);
            }
            if (hands.HeldItem != item) yield break;
            Debug.Log("PROBE_HOLDING_SOUL");
            var shrine = FindAnyObjectByType<ShrineBell>().transform.position;
            yield return WalkTo(shrine + new Vector3(0f, 0f, 1.6f), 0.3f, 8f);
            Face(shrine);
            yield return WaitFor(() => orb == null || !orb.IsSpawned, 10f);
            Debug.Log("PROBE_SOUL_DONE");
        }
    }
}
