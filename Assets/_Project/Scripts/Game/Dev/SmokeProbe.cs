using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core.Ghosts;
using BooTrayal.Game.Flow;
using BooTrayal.Game.Ghosts;
using BooTrayal.Game.Items;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Night;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BooTrayal.Game.Dev
{
    /// <summary>
    /// Scripted bots for tools/unity/player-smoke.ps1. Does nothing unless the game runs with -smokeprobe ROLE.
    /// Roles: host (watches remote players), walker (walk, grab a box, throw it), lifter (grab the urn), holder (grab a box and keep it),
    /// waller, pusher (player-smoke); victim (sneak behind the ghost and scream), lighter (shine a flashlight in its face) for ghost-smoke.
    /// </summary>
    public partial class SmokeProbe : MonoBehaviour
    {
        const float MovedThreshold = 2f;

        IEnumerator Start()
        {
            var role = ConnectionService.Instance != null ? ConnectionService.Instance.Args.SmokeProbe : null;
            if (string.IsNullOrEmpty(role)) yield break;
            Debug.Log($"SMOKE_PROBE role={role}");

            // Clients do not know the host's -playscene; any scene past the menu is the play scene.
            while (SceneManager.GetActiveScene().name == SessionFlow.MenuScene || SceneManager.GetActiveScene().name == "Bootstrap" || LocalPlayer == null)
                yield return null;
            yield return new WaitForSeconds(1.5f);
            if (role == "victim" || role == "lighter")
            {
                // House games start in the lobby: wait for the night (players are teleported to their spawns).
                while (NightDirector.Instance == null || !NightDirector.Instance.InNight) yield return null;
                yield return new WaitForSeconds(1.5f);
            }

            switch (role)
            {
                case "host": yield return HostWatch(); break;
                case "walker": yield return Walker(); break;
                case "lifter": yield return Grab(heavy: true); break;
                case "holder": yield return Grab(heavy: false); break;
                case "waller": yield return Waller(); break;
                case "pusher": yield return Pusher(); break;
                case "victim": yield return Victim(); break;
                case "lighter": yield return Lighter(); break;
                case "coop": yield return Coop(); break;
                case "spybot": yield return SpyBot(); break;
            }
        }

        static NetworkObject LocalPlayer =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;

        IEnumerator HostWatch()
        {
            var firstSeen = new Dictionary<ulong, float>();
            var start = new Dictionary<ulong, Vector3>();
            var reported = new HashSet<ulong>();
            var net = NetworkManager.Singleton;
            while (true)
            {
                foreach (var client in net.ConnectedClientsList)
                {
                    var po = client.PlayerObject;
                    if (po == null || client.ClientId == net.LocalClientId || reported.Contains(client.ClientId)) continue;
                    if (!firstSeen.ContainsKey(client.ClientId)) firstSeen[client.ClientId] = Time.time;
                    // Give the client time to be placed at its spawn point before measuring.
                    if (Time.time - firstSeen[client.ClientId] < 2f) continue;
                    if (!start.ContainsKey(client.ClientId))
                    {
                        start[client.ClientId] = po.transform.position;
                        Debug.Log($"PROBE_SEE id={client.ClientId} start={po.transform.position}");
                    }
                    float dist = Vector3.Distance(start[client.ClientId], po.transform.position);
                    if (dist > MovedThreshold)
                    {
                        reported.Add(client.ClientId);
                        Debug.Log($"PLAYER_MOVED id={client.ClientId} dist={dist:0.0}");
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        IEnumerator Walker()
        {
            var bot = EnableBot();
            bot.Move = new Vector2(0f, 1f);
            yield return new WaitForSeconds(2f);
            bot.Move = Vector2.zero;
            yield return Grab(heavy: false);
            yield return new WaitForSeconds(1f);
            bot.Press(BotButton.Throw);
            Debug.Log("PROBE_THROW");
            yield return new WaitForSeconds(1f);
            bot.Crouch = true;
            Debug.Log("PROBE_CROUCH");
            yield return new WaitForSeconds(1.5f);
            bot.Crouch = false;
        }

        /// <summary>Walks up to the host's player and pushes it twice (second push must hit the cooldown).</summary>
        IEnumerator Pusher()
        {
            var me = LocalPlayer;
            var target = FindObjectsByType<PlayerMotor>().FirstOrDefault(m => m.OwnerClientId == NetworkManager.ServerClientId);
            if (target == null)
            {
                Debug.Log("PROBE_NO_TARGET");
                yield break;
            }
            yield return WalkTo(target.transform.position, 1.2f, 10f);
            var push = me.GetComponent<PlayerPush>();
            push.RequestPush(target);
            Debug.Log("PROBE_PUSH");
            yield return null;
            push.RequestPush(target);
            Debug.Log("PROBE_PUSH_AGAIN");
        }

        /// <summary>Walks around the spirit house to a spot behind the waiting ghost, then screams once.</summary>
        IEnumerator Victim()
        {
            yield return WalkTo(new Vector3(6f, 0f, -14f), 0.3f, 15f);
            yield return WalkTo(new Vector3(2.5f, 0f, -6f), 0.3f, 15f);
            yield return new WaitForSeconds(1f);
            EnableBot().Press(BotButton.Scream);
            Debug.Log("PROBE_SCREAM");
        }

        /// <summary>Waits for the ghost's cooldown, walks up to it and keeps the flashlight on its face until it flees.</summary>
        IEnumerator Lighter()
        {
            var player = LocalPlayer;
            var bot = EnableBot();
            var look = player.GetComponent<PlayerLook>();
            var flash = player.GetComponent<PlayerFlashlight>();
            GhostController ghost = null;
            while (ghost == null || ghost.StateNet.Value != GhostState.Cooldown)
            {
                ghost = FindAnyObjectByType<GhostController>();
                yield return null;
            }
            Debug.Log("PROBE_LIGHTER_GO");
            yield return WalkTo(new Vector3(-4f, 0f, -14f), 0.3f, 10f);
            bot.Press(BotButton.Flashlight);
            float t = 0f;
            while (t < 30f && ghost != null && ghost.StateNet.Value != GhostState.Flee)
            {
                var head = ghost.transform.position + Vector3.up * GhostController.HeadHeight;
                var eye = player.transform.Find("HeadPivot").position;
                var d = head - eye;
                var flat = new Vector3(d.x, 0f, d.z);
                if (flat.sqrMagnitude > 1e-4f) look.SetYaw(Quaternion.LookRotation(flat).eulerAngles.y);
                look.SetPitch(-Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg);
                bot.Move = flat.magnitude > 6f ? new Vector2(0f, 1f) : Vector2.zero;
                t += Time.deltaTime;
                yield return null;
            }
            bot.Move = Vector2.zero;
            Debug.Log($"PROBE_LIGHTER_DONE flee={(ghost != null && ghost.StateNet.Value == GhostState.Flee)} light={flash.On.Value} t={t:0.0}");
        }

        /// <summary>Carries a box into the east wall and drops it there (it must stay inside the room).</summary>
        IEnumerator Waller()
        {
            yield return Grab(heavy: false);
            var player = LocalPlayer;
            var hands = player.GetComponent<PlayerHands>();
            if (hands.HeldItem == null) yield break;
            var item = hands.HeldItem;
            yield return WalkTo(new Vector3(20f, 0f, player.transform.position.z), 0.1f, 6f);
            yield return new WaitForSeconds(0.5f);
            EnableBot().Press(BotButton.Drop);
            Debug.Log($"PROBE_DROPPED item={item.ItemId}");
        }

        IEnumerator Grab(bool heavy)
        {
            var player = LocalPlayer;
            var hands = player.GetComponent<PlayerHands>();
            var target = FindObjectsByType<GrabbableItem>()
                .Where(g => g.Heavy == heavy && g.Holders.Count < (heavy ? 2 : 1))
                .OrderBy(g => Vector3.Distance(g.transform.position, player.transform.position))
                .FirstOrDefault();
            if (target == null)
            {
                Debug.Log("PROBE_NO_TARGET");
                yield break;
            }
            yield return WalkTo(target.transform.position, 1.1f, 10f);
            for (int attempt = 0; attempt < 5 && hands.HeldItem == null; attempt++)
            {
                hands.RequestGrab(target);
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log(hands.HeldItem != null ? $"PROBE_HOLDING item={target.ItemId}" : $"PROBE_GRAB_FAILED item={target.ItemId}");
            for (int i = 0; i < 3 && hands.HeldItem != null; i++)
            {
                yield return new WaitForSeconds(0.25f);
                var held = hands.HeldItem;
                Debug.Log($"PROBE_HELD item={held.ItemId} isOwner={held.IsOwner} owner={held.OwnerClientId} holders={held.Holders.Count} pos={held.transform.position} kinematic={held.GetComponent<Rigidbody>().isKinematic}");
            }
        }

        IEnumerator WalkTo(Vector3 target, float stopDistance, float timeout)
        {
            var player = LocalPlayer;
            var bot = EnableBot();
            var look = player.GetComponent<PlayerLook>();
            var from = player.transform.position;
            float t = 0f;
            while (t < timeout)
            {
                var d = target - player.transform.position;
                d.y = 0f;
                if (d.magnitude <= stopDistance) break;
                look.SetYaw(Quaternion.LookRotation(d).eulerAngles.y);
                bot.Move = new Vector2(0f, 1f);
                if (Mathf.FloorToInt(t) != Mathf.FloorToInt(t + Time.deltaTime))
                {
                    var motor = player.GetComponent<PlayerMotor>();
                    var cc = player.GetComponent<CharacterController>();
                    Debug.Log($"PROBE_TICK pos={player.transform.position} speed={motor.CurrentSpeed:0.00} cc={cc.enabled} grounded={cc.isGrounded} dt={Time.deltaTime:0.0000} owner={motor.IsOwner}");
                }
                t += Time.deltaTime;
                yield return null;
            }
            bot.Move = Vector2.zero;
            Debug.Log($"PROBE_WALKED from={from} to={player.transform.position} target={target} seconds={t:0.0}");
        }

        static BotInput EnableBot()
        {
            var bot = LocalPlayer.GetComponent<BotInput>();
            bot.enabled = true;
            return bot;
        }
    }
}
