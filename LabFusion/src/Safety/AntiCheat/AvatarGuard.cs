using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Player;

namespace LabFusion.Safety.AntiCheat;

internal static class AvatarGuard
{
    private static readonly Dictionary<ulong, string> _initialAvatars = new();

    public static bool Validate(ReceivedMessage message, SerializedAvatarStats stats, string barcode)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (string.IsNullOrWhiteSpace(barcode))
            return AntiCheatContext.Reject(player, state, "empty avatar barcode", true);
        if (FusionAntiCheatSettings.EnforceAvatarAllowlist.Value && !IsAllowed(barcode))
            return AntiCheatContext.Reject(player, state, $"avatar is not allowlisted: {barcode}", true);
        if (FusionAntiCheatSettings.BlockAvatarChanges.Value &&
            _initialAvatars.TryGetValue(player.PlatformID, out var initial) &&
            !string.Equals(initial, barcode, StringComparison.OrdinalIgnoreCase))
            return AntiCheatContext.Reject(player, state, $"avatar change blocked: {barcode}", true);
        _initialAvatars.TryAdd(player.PlatformID, barcode);

        float limit = FusionAntiCheatSettings.MaximumAvatarStat.Value;
        float[] values = { stats.agility, stats.speed, stats.strengthUpper, stats.strengthLower,
            stats.vitality, stats.intelligence, stats.height, stats.massTotal,
            stats.localScale.x, stats.localScale.y, stats.localScale.z };
        bool invalid = values.Any(v => !AntiCheatContext.Finite(v)) ||
            MathF.Abs(stats.agility) > limit || MathF.Abs(stats.speed) > limit ||
            MathF.Abs(stats.strengthUpper) > limit || MathF.Abs(stats.strengthLower) > limit ||
            MathF.Abs(stats.vitality) > limit || MathF.Abs(stats.intelligence) > limit ||
            stats.height is < 0.25f or > 8f || stats.massTotal is < 0f or > 2500f ||
            stats.localScale.x is <= 0f or > 10f || stats.localScale.y is <= 0f or > 10f ||
            stats.localScale.z is <= 0f or > 10f;
        return !invalid || AntiCheatContext.Reject(player, state, "OP avatar/stat changer", true);
    }

    private static bool IsAllowed(string barcode) => FusionAntiCheatSettings.AllowedAvatarBarcodes.Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(value => string.Equals(value, barcode, StringComparison.OrdinalIgnoreCase));

    public static void Forget(PlayerID player) => _initialAvatars.Remove(player.PlatformID);
    public static void Reset() => _initialAvatars.Clear();
}
