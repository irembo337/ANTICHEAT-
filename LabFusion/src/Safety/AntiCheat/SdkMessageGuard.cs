using LabFusion.Network;

namespace LabFusion.Safety.AntiCheat;

internal static class SdkMessageGuard
{
    public static bool Validate(ReceivedMessage message, string family)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (message.Bytes == null ||
            message.Bytes.Length > FusionAntiCheatSettings.MaximumSdkPayloadBytes.Value)
            return AntiCheatContext.Reject(player, state, $"oversized {family} payload", true);
        return AntiCheatContext.Rate(player, state, ref state.SdkWindow, ref state.SdkCount,
            FusionAntiCheatSettings.MaximumSdkMessagesPerSecond.Value, $"{family} flood", true);
    }
}
