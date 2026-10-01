using LabFusion.Data;
using LabFusion.Entities;
using LabFusion.Marrow.Serialization;
using LabFusion.Network;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;

namespace LabFusion.Safety.AntiCheat;

/// <summary>
/// Stable integration facade used by Fusion message handlers. Each protection is
/// implemented in its own guard so it can be audited and tuned independently.
/// </summary>
public static class FusionAntiCheat
{
    public static void OnInitialize()
    {
        MultiplayerHooking.OnPlayerLeft += player =>
        {
            AntiCheatContext.Forget(player);
            AvatarGuard.Forget(player);
        };
        MultiplayerHooking.OnDisconnected += () =>
        {
            AntiCheatContext.Reset();
            AvatarGuard.Reset();
        };
    }

    public static bool ValidateEnvelope(ReceivedMessage message, byte tag) =>
        PacketFloodGuard.Validate(message, tag);
    public static bool ValidatePose(ReceivedMessage message, RigPose pose) =>
        PlayerIntegrityGuard.ValidatePose(message, pose);
    public static bool ValidateAvatar(ReceivedMessage message, SerializedAvatarStats stats, string barcode) =>
        AvatarGuard.Validate(message, stats, barcode);
    public static bool AllowSpawn(ReceivedMessage message, SerializedSpawnData data) =>
        SpawnGuard.Validate(message, data);
    public static bool AllowDamage(ReceivedMessage message, float damage) =>
        ClientAbuseGuard.ValidateDamage(message, damage);
    public static bool AllowDespawn(ReceivedMessage message, NetworkEntity entity) =>
        ClientAbuseGuard.ValidateDespawn(message, entity);
    public static bool AllowPlayerAction(ReceivedMessage message, PlayerActionType type) =>
        ClientAbuseGuard.ValidatePlayerAction(message, type);
    public static bool AllowOwnershipRequest(ReceivedMessage message, EntityPlayerData data) =>
        OwnershipGuard.Validate(message, data);
    public static bool AllowSdkMessage(ReceivedMessage message, string family) =>
        SdkMessageGuard.Validate(message, family);
    public static bool AllowTeleportMessage(ReceivedMessage message) =>
        MovementGuard.ValidateTeleportPacket(message);
    public static void AuthorizeTeleport(byte target) => MovementGuard.AuthorizeTeleport(target);
}
