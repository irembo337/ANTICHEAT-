using LabFusion.Network;

namespace LabFusion.Safety.AntiCheat;

internal static class OwnershipGuard
{
    public static bool Validate(ReceivedMessage message, EntityPlayerData data)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        var entity = data.Entity.GetEntity();
        bool valid = data.PlayerID == message.Sender.Value && entity?.IsRegistered == true &&
            !entity.IsOwnerLocked;
        return valid || AntiCheatContext.Reject(player, state,
            $"forged ownership request: player={data.PlayerID}, entity={data.Entity.ID}", true);
    }
}
