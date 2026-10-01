using LabFusion.Network;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal static class PacketFloodGuard
{
    public static bool Validate(ReceivedMessage message, byte tag)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        int bytes = message.Bytes?.Length ?? -1;
        if (bytes < 0 || bytes > FusionAntiCheatSettings.MaximumPacketBytes.Value)
            return AntiCheatContext.Reject(player, state, $"oversized/missing packet tag={tag}, bytes={bytes}", true);

        float now = Time.realtimeSinceStartup;
        if (now - state.PacketWindow >= 1f)
        {
            state.PacketWindow = now;
            state.PacketCount = 0;
            state.PacketBytes = 0;
        }
        state.PacketCount++;
        state.PacketBytes += bytes;
        if (state.PacketCount > FusionAntiCheatSettings.MaximumPacketsPerSecond.Value ||
            state.PacketBytes > FusionAntiCheatSettings.MaximumInboundBytesPerSecond.Value)
            return AntiCheatContext.Reject(player, state,
                $"lag-server flood: {state.PacketCount} packets/{state.PacketBytes} bytes per second", true);
        if (tag == NativeMessageTag.Module || tag is >= 209 and <= 215)
            return SdkMessageGuard.Validate(message, tag == NativeMessageTag.Module ? "module" : "RPC");
        return true;
    }
}
