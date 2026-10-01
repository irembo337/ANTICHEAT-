using LabFusion.Marrow.Serialization;
using LabFusion.Network;
using LabFusion.Utilities;

namespace LabFusion.Safety.AntiCheat;

internal static class SpawnGuard
{
    private static readonly HashSet<string> DeveloperTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "c1534c5a-5747-42a2-bd08-ab3b47616467",
        "c1534c5a-6b38-438a-a324-d7e147616467",
        "c1534c5a-3813-49d6-a98c-f595436f6e73",
        "c1534c5a-e777-4d15-b0c1-3195426f6172",
        "c1534c5a-c6a8-45d0-aaa2-2c954465764d",
        "SLZ.BONELAB.Content.Spawnable.SpawnGunUI",
    };
    private static readonly HashSet<string> ExploitSpawnables = new(StringComparer.OrdinalIgnoreCase)
    {
        "marks.BlackHole.Spawnable.BlackHoleAnchor2m",
        "MrAssBurgers.WristHub.Spawnable.VoiceAnchor",
        "SLZ.BONELAB.Content.Spawnable.DungeonLargeBrick",
        "SLZ.BONELAB.Content.Spawnable.DungeonSmallBrick",
        "SLZ.BONELAB.Content.Spawnable.ProjectileVoidBall",
    };

    public static bool Validate(ReceivedMessage message, SerializedSpawnData data)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (data == null || string.IsNullOrWhiteSpace(data.Barcode))
            return AntiCheatContext.Reject(player, state, "malformed spawn", true);
        if (DeveloperTools.Contains(data.Barcode))
        {
            FusionLogger.Warn($"[Fusion Anti-Cheat] Removed developer tool from {player.PlatformID}: {data.Barcode}; no strike.");
            return false;
        }
        if (ExploitSpawnables.Contains(data.Barcode))
            return AntiCheatContext.Reject(player, state, $"known exploit spawnable: {data.Barcode}", true);
        if (data.SpawnEffect || (byte)data.SpawnSource > 1)
            return AntiCheatContext.Reject(player, state,
                $"Spawn Lab packet: source={(byte)data.SpawnSource}, effect={data.SpawnEffect}");
        return AntiCheatContext.Rate(player, state, ref state.SpawnWindow, ref state.SpawnCount,
            FusionAntiCheatSettings.MaximumSpawnsPerSecond.Value, "spawn/lag-server flood");
    }
}
