using System;
using System.Collections.Generic;
using BooTrayal.Core.Net;
using BooTrayal.Core.Rules;
using BooTrayal.Game.Night;
using Unity.Netcode;
using UnityEngine;

namespace BooTrayal.Game.Player
{
    /// <summary>Lobby state per player (GDD 11): the Ready toggle and the text chat. The server checks everything.</summary>
    public class PlayerLobby : NetworkBehaviour
    {
        public const double ChatInterval = 1.0;
        public const int ChatKeep = 30;

        public readonly NetworkVariable<bool> Ready =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        static readonly List<(ulong From, string Text)> _log = new List<(ulong, string)>();
        /// <summary>The last chat lines on this peer, oldest first.</summary>
        public static IReadOnlyList<(ulong From, string Text)> ChatLog => _log;
        public static event Action ChatChanged;

        double _lastChat = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _log.Clear();
            ChatChanged = null;
        }

        static bool InLobby => NightDirector.Instance != null && NightDirector.Instance.Phase.Value == SessionPhase.Lobby;

        public void RequestReady(bool ready)
        {
            if (IsOwner) ReadyRpc(ready);
        }

        public void SendChat(string text)
        {
            if (IsOwner && !string.IsNullOrEmpty(text)) ChatRpc(text.Length > 4 * ChatRules.MaxLength ? text.Substring(0, 4 * ChatRules.MaxLength) : text);
        }

        /// <summary>Server: back to not-ready (after a game).</summary>
        public void ServerReset()
        {
            if (IsServer) Ready.Value = false;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ReadyRpc(bool ready)
        {
            if (!InLobby) return;
            Ready.Value = ready;
            Debug.Log($"LOBBY_READY id={OwnerClientId} ready={ready}");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ChatRpc(string raw)
        {
            double now = NetworkManager.ServerTime.Time;
            if (now - _lastChat < ChatInterval) return;
            var text = ChatRules.Sanitize(raw);
            if (text == null) return;
            _lastChat = now;
            ChatBroadcastRpc(text);
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
        void ChatBroadcastRpc(string text)
        {
            _log.Add((OwnerClientId, text));
            if (_log.Count > ChatKeep) _log.RemoveAt(0);
            if (Application.isBatchMode) Debug.Log($"CHAT_RECEIVED from={OwnerClientId} len={text.Length}");
            ChatChanged?.Invoke();
        }
    }
}
