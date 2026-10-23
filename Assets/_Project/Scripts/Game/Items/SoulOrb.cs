using Unity.Netcode;

namespace BooTrayal.Game.Items
{
    /// <summary>The glowing soul a dead player leaves behind (GDD 5.1). Carry it to the shrine to revive its owner.</summary>
    public class SoulOrb : NetworkBehaviour
    {
        public readonly NetworkVariable<ulong> SoulOf =
            new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    }
}
