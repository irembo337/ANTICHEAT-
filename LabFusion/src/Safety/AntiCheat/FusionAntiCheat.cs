using LabFusion.Data;
using LabFusion.Entities;
using LabFusion.Marrow.Serialization;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

/// <summary>Host-side protection for ordinary Fusion lobbies; clients need no extra plugin.</summary>
public static class FusionAntiCheat
{
    private sealed class PendingDamage
    {
        public float Baseline;
        public float Damage;
        public float EvaluateAfter;
        public int Hits;
    }

    private sealed class State
    {
        public int Strikes, Trust = 100, SpawnCount, DamageCount, PacketCount, PacketBytes;
        public int DeathCount, SdkCount, StatStrikes, RigStrikes, GodMisses;
        public float LastStrike, LastPoseAt, TeleportUntil, SpawnWindow, DamageWindow;
        public float PacketWindow, DeathWindow, SdkWindow, DespawnWindow, LastHealth;
        public Vector3 LastPelvis, LastHead;
        public bool HasPose, ActionTaken;
        public readonly HashSet<ushort> Despawns = new();
        public PendingDamage Damage;
    }

    private static readonly Dictionary<ulong, State> _states = new();
    private static readonly Dictionary<ulong, string> _avatars = new();
    private static readonly HashSet<string> _developerTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "c1534c5a-5747-42a2-bd08-ab3b47616467", "c1534c5a-6b38-438a-a324-d7e147616467",
        "c1534c5a-3813-49d6-a98c-f595436f6e73", "c1534c5a-e777-4d15-b0c1-3195426f6172",
        "c1534c5a-c6a8-45d0-aaa2-2c954465764d", "SLZ.BONELAB.Content.Spawnable.SpawnGunUI",
    };
    private static readonly HashSet<string> _exploitSpawnables = new(StringComparer.OrdinalIgnoreCase)
    {
        "marks.BlackHole.Spawnable.BlackHoleAnchor2m", "MrAssBurgers.WristHub.Spawnable.VoiceAnchor",
        "SLZ.BONELAB.Content.Spawnable.DungeonLargeBrick", "SLZ.BONELAB.Content.Spawnable.DungeonSmallBrick",
        "SLZ.BONELAB.Content.Spawnable.ProjectileVoidBall",
    };

    public static void OnInitialize()
    {
        MultiplayerHooking.OnPlayerLeft += Forget;
        MultiplayerHooking.OnDisconnected += Reset;
    }

    public static bool ValidateEnvelope(ReceivedMessage message, byte tag)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        int bytes = message.Bytes?.Length ?? -1;
        if (bytes < 0 || bytes > FusionAntiCheatSettings.MaximumPacketBytes.Value)
            return Reject(player, state, $"oversized/missing packet tag={tag}, bytes={bytes}", true);
        float now = Time.realtimeSinceStartup;
        ResetWindow(now, ref state.PacketWindow, ref state.PacketCount, ref state.PacketBytes);
        state.PacketCount++;
        state.PacketBytes += bytes;
        if (state.PacketCount > FusionAntiCheatSettings.MaximumPacketsPerSecond.Value ||
            state.PacketBytes > FusionAntiCheatSettings.MaximumInboundBytesPerSecond.Value)
            return Reject(player, state, $"lag-server flood: {state.PacketCount} packets/{state.PacketBytes} bytes per second", true);
        if (tag == NativeMessageTag.Module || tag is >= 209 and <= 215)
            return AllowSdkMessage(message, tag == NativeMessageTag.Module ? "module" : "RPC");
        return true;
    }

    public static bool ValidatePose(ReceivedMessage message, RigPose pose)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (pose?.PelvisPose == null || pose.TrackedPoints == null ||
            pose.TrackedPoints.Length != RigAbstractor.TransformSyncCount)
            return Reject(player, state, "malformed pose", true);

        float now = Time.realtimeSinceStartup;
        bool badStats = !Finite(pose.Health) || !Finite(pose.MaxHealth) || pose.MaxHealth <= 0 ||
            pose.MaxHealth > FusionAntiCheatSettings.MaximumHealth.Value || pose.Health < -10 ||
            pose.Health > pose.MaxHealth + FusionAntiCheatSettings.HealthTolerance.Value;
        state.StatStrikes = badStats ? state.StatStrikes + 1 : System.Math.Max(0, state.StatStrikes - 1);
        if (state.StatStrikes >= FusionAntiCheatSettings.IntegrityStrikesBeforeAction.Value)
            return Reject(player, state, $"stat changer: health={pose.Health:0.##}/{pose.MaxHealth:0.##}", true);
        if (!Finite(pose.PelvisPose.Position) || !Finite(pose.PelvisPose.Velocity))
            return Reject(player, state, "non-finite pose", true);

        bool badRig = pose.PelvisPose.Velocity.magnitude > FusionAntiCheatSettings.MaximumMovementSpeed.Value * 2;
        foreach (var point in pose.TrackedPoints)
        {
            if (point == null)
                return Reject(player, state, "missing tracked-device pose", true);
            badRig |= !Finite(point.position) || !Finite(point.rotation) ||
                point.position.magnitude > FusionAntiCheatSettings.MaximumRigReach.Value;
        }
        Vector3 head = pose.TrackedPoints[0].position;
        if (state.HasPose)
        {
            float elapsed = now - state.LastPoseAt;
            if (elapsed >= FusionAntiCheatSettings.MinimumMovementSampleSeconds.Value && elapsed < 5 &&
                now > state.TeleportUntil)
            {
                float distance = Vector3.Distance(state.LastPelvis, pose.PelvisPose.Position);
                float speed = distance / elapsed;
                if (distance > FusionAntiCheatSettings.MaximumTeleportDistance.Value ||
                    speed > FusionAntiCheatSettings.MaximumMovementSpeed.Value)
                    return Reject(player, state, $"unauthorized teleport: {distance:0.0}m, {speed:0.0}m/s");
                badRig |= Vector3.Distance(state.LastHead, head) > FusionAntiCheatSettings.MaximumHeadJump.Value;
            }
        }
        state.RigStrikes = badRig ? state.RigStrikes + 1 : System.Math.Max(0, state.RigStrikes - 1);
        if (state.RigStrikes >= FusionAntiCheatSettings.IntegrityStrikesBeforeAction.Value)
            return Reject(player, state, "aim/freecam or impossible rig", true);

        if (state.Damage is { } pending && now >= pending.EvaluateAfter)
        {
            bool ignored = pose.Health >= pending.Baseline - FusionAntiCheatSettings.HealthTolerance.Value;
            state.GodMisses = ignored ? state.GodMisses + 1 : 0;
            state.Damage = null;
            if (state.GodMisses >= FusionAntiCheatSettings.GodModeMismatchWindows.Value)
                return Reject(player, state,
                    $"god mode: health={pose.Health:0.##}, baseline={pending.Baseline:0.##}, damage={pending.Damage:0.##}", true);
        }
        if (pose.Health <= 0) { state.Damage = null; state.GodMisses = 0; }
        state.HasPose = true;
        state.LastPoseAt = now;
        state.LastPelvis = pose.PelvisPose.Position;
        state.LastHead = head;
        state.LastHealth = pose.Health;
        Decay(state, now);
        return true;
    }

    public static bool ValidateAvatar(ReceivedMessage message, SerializedAvatarStats stats, string barcode)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (string.IsNullOrWhiteSpace(barcode)) return Reject(player, state, "empty avatar barcode", true);
        if (FusionAntiCheatSettings.EnforceAvatarAllowlist.Value && !AllowedAvatar(barcode))
            return Reject(player, state, $"avatar is not allowlisted: {barcode}", true);
        if (FusionAntiCheatSettings.BlockAvatarChanges.Value && _avatars.TryGetValue(player.PlatformID, out var initial) &&
            !string.Equals(initial, barcode, StringComparison.OrdinalIgnoreCase))
            return Reject(player, state, $"avatar change blocked: {barcode}", true);
        _avatars.TryAdd(player.PlatformID, barcode);

        float limit = FusionAntiCheatSettings.MaximumAvatarStat.Value;
        float[] values = { stats.agility, stats.speed, stats.strengthUpper, stats.strengthLower, stats.vitality,
            stats.intelligence, stats.height, stats.massTotal, stats.localScale.x, stats.localScale.y, stats.localScale.z };
        if (values.Any(v => !Finite(v)) || MathF.Abs(stats.agility) > limit || MathF.Abs(stats.speed) > limit ||
            MathF.Abs(stats.strengthUpper) > limit || MathF.Abs(stats.strengthLower) > limit ||
            MathF.Abs(stats.vitality) > limit || MathF.Abs(stats.intelligence) > limit ||
            stats.height is < 0.25f or > 8f || stats.massTotal is < 0f or > 2500f ||
            stats.localScale.x is <= 0f or > 10f || stats.localScale.y is <= 0f or > 10f ||
            stats.localScale.z is <= 0f or > 10f)
            return Reject(player, state, "OP avatar/stat changer", true);
        return true;
    }

    public static bool AllowSpawn(ReceivedMessage message, SerializedSpawnData data)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (data == null || string.IsNullOrWhiteSpace(data.Barcode)) return Reject(player, state, "malformed spawn", true);
        if (_developerTools.Contains(data.Barcode))
        {
            FusionLogger.Warn($"[Fusion Anti-Cheat] Removed developer tool from {player.PlatformID}: {data.Barcode}; no strike.");
            return false;
        }
        if (_exploitSpawnables.Contains(data.Barcode))
            return Reject(player, state, $"known exploit spawnable: {data.Barcode}", true);
        if (data.SpawnEffect || (byte)data.SpawnSource > 1)
            return Reject(player, state, $"Spawn Lab packet: source={(byte)data.SpawnSource}, effect={data.SpawnEffect}");
        return Rate(player, state, ref state.SpawnWindow, ref state.SpawnCount,
            FusionAntiCheatSettings.MaximumSpawnsPerSecond.Value, "spawn/lag-server flood");
    }

    public static bool AllowDamage(ReceivedMessage message, float damage)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (message.Route.Type != RelayType.ToTarget || !message.Route.Target.HasValue ||
            !PlayerIDManager.HasPlayerID(message.Route.Target.Value) || !Finite(damage) || damage < 0 ||
            damage > FusionAntiCheatSettings.MaximumDamagePerHit.Value)
            return Reject(player, state, "kill-all or malformed damage packet", true);
        if (!Rate(player, state, ref state.DamageWindow, ref state.DamageCount,
            FusionAntiCheatSettings.MaximumDamageMessagesPerSecond.Value, "kill-all damage flood", true)) return false;
        var target = PlayerIDManager.GetPlayerID(message.Route.Target.Value);
        var targetState = target == null ? null : Get(target);
        if (targetState?.HasPose == true && targetState.LastHealth > 0 &&
            damage >= FusionAntiCheatSettings.MinimumGodModeDamage.Value)
        {
            targetState.Damage ??= new PendingDamage { Baseline = targetState.LastHealth,
                EvaluateAfter = Time.realtimeSinceStartup + 0.5f };
            targetState.Damage.Damage += damage;
            targetState.Damage.Hits++;
        }
        return true;
    }

    public static bool AllowDespawn(ReceivedMessage message, NetworkEntity entity)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (entity == null || !entity.IsRegistered || !entity.HasOwner || entity.OwnerID.SmallID != message.Sender.Value)
            return Reject(player, state, "clean-scene/foreign entity despawn", true);
        float now = Time.realtimeSinceStartup;
        if (now - state.DespawnWindow >= FusionAntiCheatSettings.MassDespawnWindowSeconds.Value)
        { state.DespawnWindow = now; state.Despawns.Clear(); }
        state.Despawns.Add(entity.ID);
        return state.Despawns.Count < FusionAntiCheatSettings.MassDespawnLimit.Value ||
            Reject(player, state, $"mass despawn: {state.Despawns.Count} entities", true);
    }

    public static bool AllowPlayerAction(ReceivedMessage message, PlayerActionType type)
    {
        if (!Inspect(message, out var player, out var state) || type != PlayerActionType.DEATH) return true;
        return Rate(player, state, ref state.DeathWindow, ref state.DeathCount,
            FusionAntiCheatSettings.MaximumDeathActionsPerSecond.Value, "kill-all/death-action flood", true);
    }

    public static bool AllowOwnershipRequest(ReceivedMessage message, EntityPlayerData data)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        var entity = data.Entity.GetEntity();
        return data.PlayerID == message.Sender.Value && entity?.IsRegistered == true && !entity.IsOwnerLocked ||
            Reject(player, state, $"forged ownership request: player={data.PlayerID}, entity={data.Entity.ID}", true);
    }

    public static bool AllowSdkMessage(ReceivedMessage message, string family)
    {
        if (!Inspect(message, out var player, out var state)) return true;
        if (message.Bytes == null || message.Bytes.Length > FusionAntiCheatSettings.MaximumSdkPayloadBytes.Value)
            return Reject(player, state, $"oversized {family} payload", true);
        return Rate(player, state, ref state.SdkWindow, ref state.SdkCount,
            FusionAntiCheatSettings.MaximumSdkMessagesPerSecond.Value, $"{family} flood", true);
    }

    public static bool AllowTeleportMessage(ReceivedMessage message)
    {
        if (!message.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value) return true;
        if (message.Sender == PlayerIDManager.HostSmallID && message.Route.Type == RelayType.ToTarget) return true;
        if (message.Sender.HasValue && PlayerIDManager.GetPlayerID(message.Sender.Value) is { } player)
            Reject(player, Get(player), "forged teleport packet", true);
        return false;
    }

    public static void AuthorizeTeleport(byte target)
    {
        if (!NetworkInfo.IsHost || PlayerIDManager.GetPlayerID(target) is not { } player) return;
        var state = Get(player);
        state.TeleportUntil = Time.realtimeSinceStartup + FusionAntiCheatSettings.TeleportGraceSeconds.Value;
        state.HasPose = false;
    }

    private static bool Rate(PlayerID player, State state, ref float start, ref int count,
        int limit, string reason, bool immediate = false)
    {
        float now = Time.realtimeSinceStartup;
        if (now - start >= 1) { start = now; count = 0; }
        count++;
        return count <= System.Math.Max(1, limit) || Reject(player, state, reason, immediate);
    }

    private static bool Inspect(ReceivedMessage message, out PlayerID player, out State state)
    {
        player = null; state = null;
        if (!message.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value || !message.Sender.HasValue ||
            message.Sender.Value == PlayerIDManager.HostSmallID) return false;
        player = PlayerIDManager.GetPlayerID(message.Sender.Value);
        if (player == null) return false;
        state = Get(player);
        return true;
    }

    private static State Get(PlayerID player)
    {
        if (!_states.TryGetValue(player.PlatformID, out var state)) _states[player.PlatformID] = state = new();
        return state;
    }

    private static bool Reject(PlayerID player, State state, string reason, bool immediate = false)
    {
        float now = Time.realtimeSinceStartup;
        Decay(state, now);
        state.Strikes++;
        state.Trust = System.Math.Max(0, state.Trust - (immediate ? 35 : 15));
        state.LastStrike = now;
        int threshold = System.Math.Max(1, FusionAntiCheatSettings.StrikesBeforeAction.Value);
        FusionLogger.Warn($"[Fusion Anti-Cheat] Blocked {player.PlatformID}: {reason}; strike {state.Strikes}/{threshold}; trust={state.Trust}.");
        if (!state.ActionTaken && (immediate || state.Strikes >= threshold))
        {
            state.ActionTaken = true;
            string actionReason = $"Fusion Anti-Cheat: {reason}";
            if (!FusionAntiCheatSettings.DryRun.Value)
            {
                if (FusionAntiCheatSettings.AutomaticBans.Value)
                {
                    BanManager.Ban(new PlayerInfo(player), actionReason);
                    ConnectionSender.SendDisconnect(player, actionReason);
                }
                else NetworkConnectionManager.DisconnectUser(player.PlatformID);
            }
            FusionLogger.Warn($"[Fusion Anti-Cheat] {(FusionAntiCheatSettings.DryRun.Value ? "Dry-run" : FusionAntiCheatSettings.AutomaticBans.Value ? "Banned" : "Kicked")} {player.PlatformID}: {reason}.");
        }
        return false;
    }

    private static void ResetWindow(float now, ref float start, ref int count, ref int bytes)
    { if (now - start >= 1) { start = now; count = 0; bytes = 0; } }
    private static void Decay(State state, float now)
    {
        if (state.Strikes > 0 && now - state.LastStrike > FusionAntiCheatSettings.StrikeDecaySeconds.Value)
        { state.Strikes--; state.Trust = System.Math.Min(100, state.Trust + 5); state.LastStrike = now; }
    }
    private static bool AllowedAvatar(string barcode) => FusionAntiCheatSettings.AllowedAvatarBarcodes.Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(value => string.Equals(value, barcode, StringComparison.OrdinalIgnoreCase));
    private static void Forget(PlayerID player) { _states.Remove(player.PlatformID); _avatars.Remove(player.PlatformID); }
    private static void Reset() { _states.Clear(); _avatars.Clear(); }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
}
