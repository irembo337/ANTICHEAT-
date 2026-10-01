using LabFusion.Network;
using LabFusion.Player;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal static class MovementGuard
{
    public static bool ValidatePose(PlayerID player, AntiCheatPlayerState state, Vector3 pelvis, float now)
    {
        if (!state.HasPose)
            return true;
        float elapsed = now - state.LastPoseAt;
        if (elapsed < FusionAntiCheatSettings.MinimumMovementSampleSeconds.Value || elapsed >= 5f ||
            now <= state.TeleportUntil)
            return true;
        float distance = Vector3.Distance(state.LastPelvis, pelvis);
        float speed = distance / elapsed;
        return distance <= FusionAntiCheatSettings.MaximumTeleportDistance.Value &&
            speed <= FusionAntiCheatSettings.MaximumMovementSpeed.Value ||
            AntiCheatContext.Reject(player, state, $"unauthorized teleport: {distance:0.0}m, {speed:0.0}m/s");
    }

    public static bool ValidateTeleportPacket(ReceivedMessage message)
    {
        if (!message.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value)
            return true;
        if (message.Sender == PlayerIDManager.HostSmallID && message.Route.Type == RelayType.ToTarget)
            return true;
        if (message.Sender.HasValue && PlayerIDManager.GetPlayerID(message.Sender.Value) is { } player)
            AntiCheatContext.Reject(player, AntiCheatContext.Get(player), "forged teleport packet", true);
        return false;
    }

    public static void AuthorizeTeleport(byte target)
    {
        if (!NetworkInfo.IsHost || PlayerIDManager.GetPlayerID(target) is not { } player)
            return;
        var state = AntiCheatContext.Get(player);
        state.TeleportUntil = Time.realtimeSinceStartup + FusionAntiCheatSettings.TeleportGraceSeconds.Value;
        state.HasPose = false;
    }
}
