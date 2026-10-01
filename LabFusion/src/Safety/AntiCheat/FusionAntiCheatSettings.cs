using LabFusion.Preferences;
using MelonLoader;

namespace LabFusion.Safety.AntiCheat;

public static class FusionAntiCheatSettings
{
    public static FusionPref<bool> Enabled { get; private set; }
    public static FusionPref<bool> AutomaticBans { get; private set; }
    public static FusionPref<bool> DryRun { get; private set; }
    public static FusionPref<int> StrikesBeforeAction { get; private set; }
    public static FusionPref<float> StrikeDecaySeconds { get; private set; }
    public static FusionPref<float> MaximumMovementSpeed { get; private set; }
    public static FusionPref<float> MaximumTeleportDistance { get; private set; }
    public static FusionPref<float> MinimumMovementSampleSeconds { get; private set; }
    public static FusionPref<float> TeleportGraceSeconds { get; private set; }
    public static FusionPref<float> MaximumRigReach { get; private set; }
    public static FusionPref<float> MaximumHeadJump { get; private set; }
    public static FusionPref<float> MaximumHealth { get; private set; }
    public static FusionPref<float> HealthTolerance { get; private set; }
    public static FusionPref<float> MinimumGodModeDamage { get; private set; }
    public static FusionPref<int> GodModeMismatchWindows { get; private set; }
    public static FusionPref<int> IntegrityStrikesBeforeAction { get; private set; }
    public static FusionPref<float> MaximumAvatarStat { get; private set; }
    public static FusionPref<bool> EnforceAvatarAllowlist { get; private set; }
    public static FusionPref<bool> BlockAvatarChanges { get; private set; }
    public static FusionPref<string> AllowedAvatarBarcodes { get; private set; }
    public static FusionPref<int> MaximumSpawnsPerSecond { get; private set; }
    public static FusionPref<float> MaximumDamagePerHit { get; private set; }
    public static FusionPref<int> MaximumDamageMessagesPerSecond { get; private set; }
    public static FusionPref<int> MaximumPacketsPerSecond { get; private set; }
    public static FusionPref<int> MaximumInboundBytesPerSecond { get; private set; }
    public static FusionPref<int> MaximumPacketBytes { get; private set; }
    public static FusionPref<int> MaximumSdkPayloadBytes { get; private set; }
    public static FusionPref<int> MaximumSdkMessagesPerSecond { get; private set; }
    public static FusionPref<int> MassDespawnLimit { get; private set; }
    public static FusionPref<float> MassDespawnWindowSeconds { get; private set; }
    public static FusionPref<int> MaximumDeathActionsPerSecond { get; private set; }

    public static void OnInitialize(MelonPreferences_Category category)
    {
        Enabled = new(category, "Fusion Anti-Cheat Enabled", true);
        AutomaticBans = new(category, "Fusion Anti-Cheat Automatic Bans", true);
        DryRun = new(category, "Fusion Anti-Cheat Dry Run", false);
        StrikesBeforeAction = new(category, "Fusion Anti-Cheat Strikes Before Action", 4);
        StrikeDecaySeconds = new(category, "Fusion Anti-Cheat Strike Decay Seconds", 30f);
        MaximumMovementSpeed = new(category, "Fusion Anti-Cheat Maximum Movement Speed", 45f);
        MaximumTeleportDistance = new(category, "Fusion Anti-Cheat Maximum Teleport Distance", 35f);
        MinimumMovementSampleSeconds = new(category, "Fusion Anti-Cheat Minimum Movement Sample Seconds", 0.08f);
        TeleportGraceSeconds = new(category, "Fusion Anti-Cheat Teleport Grace Seconds", 5f);
        MaximumRigReach = new(category, "Fusion Anti-Cheat Maximum Rig Reach", 6f);
        MaximumHeadJump = new(category, "Fusion Anti-Cheat Maximum Head Jump", 4f);
        MaximumHealth = new(category, "Fusion Anti-Cheat Maximum Health", 500f);
        HealthTolerance = new(category, "Fusion Anti-Cheat Health Tolerance", 1f);
        MinimumGodModeDamage = new(category, "Fusion Anti-Cheat Minimum God Mode Damage", 5f);
        GodModeMismatchWindows = new(category, "Fusion Anti-Cheat God Mode Mismatch Windows", 3);
        IntegrityStrikesBeforeAction = new(category, "Fusion Anti-Cheat Integrity Strikes Before Action", 3);
        MaximumAvatarStat = new(category, "Fusion Anti-Cheat Maximum Avatar Stat", 50f);
        EnforceAvatarAllowlist = new(category, "Fusion Anti-Cheat Enforce Avatar Allowlist", false);
        BlockAvatarChanges = new(category, "Fusion Anti-Cheat Block Avatar Changes", false);
        AllowedAvatarBarcodes = new(category, "Fusion Anti-Cheat Allowed Avatar Barcodes", string.Empty);
        MaximumSpawnsPerSecond = new(category, "Fusion Anti-Cheat Maximum Spawns Per Second", 15);
        MaximumDamagePerHit = new(category, "Fusion Anti-Cheat Maximum Damage Per Hit", 5000f);
        MaximumDamageMessagesPerSecond = new(category, "Fusion Anti-Cheat Maximum Damage Messages Per Second", 30);
        MaximumPacketsPerSecond = new(category, "Fusion Anti-Cheat Maximum Packets Per Second", 240);
        MaximumInboundBytesPerSecond = new(category, "Fusion Anti-Cheat Maximum Inbound Bytes Per Second", 1048576);
        MaximumPacketBytes = new(category, "Fusion Anti-Cheat Maximum Packet Bytes", 262144);
        MaximumSdkPayloadBytes = new(category, "Fusion Anti-Cheat Maximum SDK Payload Bytes", 65536);
        MaximumSdkMessagesPerSecond = new(category, "Fusion Anti-Cheat Maximum SDK Messages Per Second", 90);
        MassDespawnLimit = new(category, "Fusion Anti-Cheat Mass Despawn Limit", 24);
        MassDespawnWindowSeconds = new(category, "Fusion Anti-Cheat Mass Despawn Window Seconds", 2f);
        MaximumDeathActionsPerSecond = new(category, "Fusion Anti-Cheat Maximum Death Actions Per Second", 4);
    }
}
