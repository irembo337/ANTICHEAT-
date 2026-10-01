using LabFusion.Preferences;

using MelonLoader;

namespace LabFusion.Safety.AntiCheat;

/// <summary>
/// Host-local settings for the anti-cheat layer. These values are intentionally
/// not synchronized to clients, so a remote player cannot learn or alter enforcement.
/// </summary>
public static class FusionAntiCheatSettings
{
    public static FusionPref<bool> Enabled { get; private set; }
    public static FusionPref<bool> AutomaticBans { get; private set; }
    public static FusionPref<int> StrikesBeforeAction { get; private set; }
    public static FusionPref<float> MaximumMovementSpeed { get; private set; }
    public static FusionPref<float> MaximumRigReach { get; private set; }
    public static FusionPref<float> MaximumHealth { get; private set; }
    public static FusionPref<float> MaximumAvatarStat { get; private set; }
    public static FusionPref<bool> EnforceAvatarAllowlist { get; private set; }
    public static FusionPref<bool> BlockAvatarChanges { get; private set; }
    public static FusionPref<string> AllowedAvatarBarcodes { get; private set; }
    public static FusionPref<int> MaximumSpawnsPerSecond { get; private set; }
    public static FusionPref<int> MaximumDamageMessagesPerSecond { get; private set; }

    public static void OnInitialize(MelonPreferences_Category category)
    {
        Enabled = new FusionPref<bool>(category, "Fusion Anti-Cheat Enabled", true);
        AutomaticBans = new FusionPref<bool>(category, "Fusion Anti-Cheat Automatic Bans", true);
        StrikesBeforeAction = new FusionPref<int>(category, "Fusion Anti-Cheat Strikes Before Action", 4);
        MaximumMovementSpeed = new FusionPref<float>(category, "Fusion Anti-Cheat Maximum Movement Speed", 45f);
        MaximumRigReach = new FusionPref<float>(category, "Fusion Anti-Cheat Maximum Rig Reach", 6f);
        MaximumHealth = new FusionPref<float>(category, "Fusion Anti-Cheat Maximum Health", 500f);
        MaximumAvatarStat = new FusionPref<float>(category, "Fusion Anti-Cheat Maximum Avatar Stat", 50f);
        EnforceAvatarAllowlist = new FusionPref<bool>(category, "Fusion Anti-Cheat Enforce Avatar Allowlist", false);
        BlockAvatarChanges = new FusionPref<bool>(category, "Fusion Anti-Cheat Block Avatar Changes", false);
        AllowedAvatarBarcodes = new FusionPref<string>(category, "Fusion Anti-Cheat Allowed Avatar Barcodes", string.Empty);
        MaximumSpawnsPerSecond = new FusionPref<int>(category, "Fusion Anti-Cheat Maximum Spawns Per Second", 15);
        MaximumDamageMessagesPerSecond = new FusionPref<int>(category, "Fusion Anti-Cheat Maximum Damage Messages Per Second", 30);
    }
}
