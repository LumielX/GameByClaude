using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BooTrayal.Game.Flow;
using BooTrayal.Game.Items;
using BooTrayal.Game.Networking;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BooTrayal.Game.Dev
{
    /// <summary>
    /// Scripted bots for tools/unity/player-smoke.ps1. Does nothing unless the game runs with -smokeprobe ROLE.
    /// Roles: host (watches remote players), walker (walk, grab a box, throw it), lifter (grab the urn), holder (grab a box and keep it).
    /// </summary>
    public class SmokeProbe : MonoBehaviour
    {
        const float MovedThreshold = 2f;

        IEnumerator Start()
        {
            var role = ConnectionService.Instance != null ? ConnectionService.Instance.Args.SmokeProbe : null;
            if (string.IsNullOrEmpty(role)) yield break;
            Debug.Log($"SMOKE_PROBE role={role}");

            while (SceneManager.GetActiveScene().name != SessionFlow.PlayScene || LocalPlayer == null)
                yield return null;
            yield return new WaitForSeconds(1.5f);

            switch (role)
            {
                case "host": yield return HostWatch(); break;
                case "walker": yield return Walker(); break;
                case "lifter": yield return Grab(heavy: true); break;
                case "holder": yield return Grab(heavy: false); break;
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
        }

        IEnumerator Grab(bool heavy)
        {
            var player = LocalPlayer;
            var hands = player.GetComponent<PlayerHands>();
            var target = FindObjectsByType<GrabbableItem>(FindObjectsSortMode.None)
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
