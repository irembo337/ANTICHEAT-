using LabFusion.Data;
using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;

using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

/// <summary>
/// Authoritative checks performed by the Fusion host before relaying client messages.
/// This layer deliberately uses existing Fusion packets so unmodified clients remain
/// wire-compatible with a protected Fusion host.
/// </summary>
public static class FusionAntiCheat
{
    private sealed class PlayerState
    {
        public int Strikes;
        public float LastStrikeTime;
        public float LastPoseTime;
        public Vector3 LastPelvisPosition;
        public bool HasPose;
        public float AuthorizedTeleportUntil;
        public float SpawnWindowStart;
        public int SpawnCount;
        public float DamageWindowStart;
        public int DamageCount;
        public bool ActionTaken;
    }

    private static readonly Dictionary<ulong, PlayerState> _states = new();
    private static readonly Dictionary<ulong, string> _initialAvatarByPlayer = new();

    public static bool ValidatePose(ReceivedMessage received, RigPose pose)
    {
        if (!ShouldInspect(received, out var player, out var state))
            return true;

        if (pose == null || pose.PelvisPose == null || pose.TrackedPoints == null || pose.TrackedPoints.Length != RigAbstractor.TransformSyncCount)
            return Reject(player, state, "malformed pose");

        if (!IsFinite(pose.Health) || !IsFinite(pose.MaxHealth) || pose.MaxHealth <= 0f ||
            pose.MaxHealth > FusionAntiCheatSettings.MaximumHealth.Value || pose.Health < -1f ||
            pose.Health > pose.MaxHealth + 1f)
            return Reject(player, state, $"invalid health values ({pose.Health:0.##}/{pose.MaxHealth:0.##})");

        if (!IsFinite(pose.PelvisPose.Position) || !IsFinite(pose.PelvisPose.Velocity) ||
            pose.PelvisPose.Velocity.magnitude > FusionAntiCheatSettings.MaximumMovementSpeed.Value * 2f)
            return Reject(player, state, "invalid pose position or velocity");

        float maxReach = FusionAntiCheatSettings.MaximumRigReach.Value;
        foreach (var point in pose.TrackedPoints)
        {
            if (point == null || !IsFinite(point.position) || !IsFinite(point.rotation) || point.position.magnitude > maxReach)
                return Reject(player, state, "freecam or impossible tracked-device offset");
        }

        float now = Time.realtimeSinceStartup;
        if (state.HasPose)
        {
            float elapsed = now - state.LastPoseTime;
            if (elapsed > 0.05f && elapsed < 5f && now > state.AuthorizedTeleportUntil)
            {
                float speed = Vector3.Distance(state.LastPelvisPosition, pose.PelvisPose.Position) / elapsed;
                if (speed > FusionAntiCheatSettings.MaximumMovementSpeed.Value)
                    return Reject(player, state, $"unauthorized teleport ({speed:0.0} m/s)");
            }
        }

        state.HasPose = true;
        state.LastPoseTime = now;
        state.LastPelvisPosition = pose.PelvisPose.Position;
        DecayStrikes(state, now);
        return true;
    }

    public static bool ValidateAvatar(ReceivedMessage received, SerializedAvatarStats stats, string barcode)
    {
        if (!ShouldInspect(received, out var player, out var state))
            return true;

        if (string.IsNullOrWhiteSpace(barcode))
            return Reject(player, state, "empty avatar barcode");

        if (FusionAntiCheatSettings.EnforceAvatarAllowlist.Value && !IsAvatarAllowed(barcode))
            return Reject(player, state, $"avatar is not on the allowlist ({barcode})");

        if (FusionAntiCheatSettings.BlockAvatarChanges.Value)
        {
            if (_initialAvatarByPlayer.TryGetValue(player.PlatformID, out var initialBarcode))
            {
                if (!string.Equals(initialBarcode, barcode, StringComparison.OrdinalIgnoreCase))
                    return Reject(player, state, $"avatar change blocked ({barcode})");
            }
            else
            {
                _initialAvatarByPlayer[player.PlatformID] = barcode;
            }
        }

        float limit = FusionAntiCheatSettings.MaximumAvatarStat.Value;
        float[] values =
        {
            stats.agility, stats.speed, stats.strengthUpper, stats.strengthLower, stats.vitality, stats.intelligence,
            stats.height, stats.massTotal, stats.localScale.x, stats.localScale.y, stats.localScale.z,
        };

        if (values.Any(value => !IsFinite(value)) ||
            MathF.Abs(stats.agility) > limit || MathF.Abs(stats.speed) > limit ||
            MathF.Abs(stats.strengthUpper) > limit || MathF.Abs(stats.strengthLower) > limit ||
            MathF.Abs(stats.vitality) > limit || MathF.Abs(stats.intelligence) > limit ||
            stats.height < 0.25f || stats.height > 8f || stats.massTotal < 0f || stats.massTotal > 2500f ||
            stats.localScale.x <= 0f || stats.localScale.y <= 0f || stats.localScale.z <= 0f ||
            stats.localScale.x > 10f || stats.localScale.y > 10f || stats.localScale.z > 10f)
            return Reject(player, state, "stat changer or invalid avatar dimensions");

        return true;
    }

    private static bool IsAvatarAllowed(string barcode)
    {
        return FusionAntiCheatSettings.AllowedAvatarBarcodes.Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(allowed => string.Equals(allowed, barcode, StringComparison.OrdinalIgnoreCase));
    }

    public static bool AllowSpawn(ReceivedMessage received)
    {
        if (!ShouldInspect(received, out var player, out var state))
            return true;

        return CheckRateLimit(player, state, ref state.SpawnWindowStart, ref state.SpawnCount,
            FusionAntiCheatSettings.MaximumSpawnsPerSecond.Value, "spawn/lag-server flood");
    }

    public static bool AllowDamage(ReceivedMessage received, float damage)
    {
        if (!ShouldInspect(received, out var player, out var state))
            return true;

        if (received.Route.Type != RelayType.ToTarget || !received.Route.Target.HasValue ||
            !PlayerIDManager.HasPlayerID(received.Route.Target.Value) || !IsFinite(damage) || damage < 0f || damage > 5000f)
            return Reject(player, state, "kill-all or malformed damage packet");

        return CheckRateLimit(player, state, ref state.DamageWindowStart, ref state.DamageCount,
            FusionAntiCheatSettings.MaximumDamageMessagesPerSecond.Value, "damage packet flood");
    }

    public static bool AllowDespawn(ReceivedMessage received, NetworkEntity entity)
    {
        if (!ShouldInspect(received, out var player, out var state))
            return true;

        if (entity == null || !entity.IsRegistered || !entity.HasOwner || entity.OwnerID.SmallID != received.Sender.Value)
            return Reject(player, state, "despawn-all or foreign entity despawn");

        return true;
    }

    public static bool AllowTeleportMessage(ReceivedMessage received)
    {
        if (!received.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value)
            return true;

        if (received.Sender == PlayerIDManager.HostSmallID && received.Route.Type == RelayType.ToTarget)
            return true;

        if (received.Sender.HasValue && PlayerIDManager.GetPlayerID(received.Sender.Value) is { } player)
            Reject(player, GetState(player), "forged teleport packet");

        return false;
    }

    public static void AuthorizeTeleport(byte target)
    {
        if (!NetworkInfo.IsHost)
            return;

        var player = PlayerIDManager.GetPlayerID(target);
        if (player == null)
            return;

        var state = GetState(player);
        state.AuthorizedTeleportUntil = Time.realtimeSinceStartup + 5f;
        state.HasPose = false;
    }

    private static bool CheckRateLimit(PlayerID player, PlayerState state, ref float windowStart, ref int count, int limit, string reason)
    {
        float now = Time.realtimeSinceStartup;
        if (now - windowStart >= 1f)
        {
            windowStart = now;
            count = 0;
        }

        count++;
        return count <= System.Math.Max(1, limit) || Reject(player, state, reason);
    }

    private static bool ShouldInspect(ReceivedMessage received, out PlayerID player, out PlayerState state)
    {
        player = null;
        state = null;

        if (!received.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value || !received.Sender.HasValue ||
            received.Sender.Value == PlayerIDManager.HostSmallID)
            return false;

        player = PlayerIDManager.GetPlayerID(received.Sender.Value);
        if (player == null)
            return false;

        state = GetState(player);
        return true;
    }

    private static PlayerState GetState(PlayerID player)
    {
        if (!_states.TryGetValue(player.PlatformID, out var state))
        {
            state = new PlayerState();
            _states[player.PlatformID] = state;
        }

        return state;
    }

    private static bool Reject(PlayerID player, PlayerState state, string reason)
    {
        float now = Time.realtimeSinceStartup;
        DecayStrikes(state, now);
        state.Strikes++;
        state.LastStrikeTime = now;

        FusionLogger.Warn($"[Fusion Anti-Cheat] Blocked {player.PlatformID} ({player.SmallID}): {reason}; strike {state.Strikes}/{FusionAntiCheatSettings.StrikesBeforeAction.Value}.");

        if (!state.ActionTaken && state.Strikes >= System.Math.Max(1, FusionAntiCheatSettings.StrikesBeforeAction.Value))
        {
            state.ActionTaken = true;
            string actionReason = $"Fusion Anti-Cheat: {reason}";

            if (FusionAntiCheatSettings.AutomaticBans.Value)
            {
                BanManager.Ban(new PlayerInfo(player), actionReason);
                ConnectionSender.SendDisconnect(player, actionReason);
                FusionLogger.Warn($"[Fusion Anti-Cheat] Banned {player.PlatformID}: {reason}.");
            }
            else
            {
                NetworkConnectionManager.DisconnectUser(player.PlatformID);
                FusionLogger.Warn($"[Fusion Anti-Cheat] Kicked {player.PlatformID}: {reason}.");
            }
        }

        return false;
    }

    private static void DecayStrikes(PlayerState state, float now)
    {
        if (state.Strikes > 0 && now - state.LastStrikeTime > 30f)
            state.Strikes--;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
}
