using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Senders;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal static class ClientAbuseGuard
{
    public static bool ValidateDamage(ReceivedMessage message, float damage)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (message.Route.Type != RelayType.ToTarget || !message.Route.Target.HasValue ||
            !PlayerIDManager.HasPlayerID(message.Route.Target.Value) || !AntiCheatContext.Finite(damage) ||
            damage < 0 || damage > FusionAntiCheatSettings.MaximumDamagePerHit.Value)
            return AntiCheatContext.Reject(player, state, "kill-all or malformed damage packet", true);
        if (!AntiCheatContext.Rate(player, state, ref state.DamageWindow, ref state.DamageCount,
            FusionAntiCheatSettings.MaximumDamageMessagesPerSecond.Value, "kill-all damage flood", true))
            return false;

        var target = PlayerIDManager.GetPlayerID(message.Route.Target.Value);
        var targetState = target == null ? null : AntiCheatContext.Get(target);
        if (targetState?.HasPose == true && targetState.LastHealth > 0 &&
            damage >= FusionAntiCheatSettings.MinimumGodModeDamage.Value)
        {
            targetState.PendingDamage ??= new PendingDamageState
            {
                Baseline = targetState.LastHealth,
                EvaluateAfter = Time.realtimeSinceStartup + 0.5f,
            };
            targetState.PendingDamage.Damage += damage;
            targetState.PendingDamage.Hits++;
        }
        return true;
    }

    public static bool ValidateDespawn(ReceivedMessage message, NetworkEntity entity)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (entity == null || !entity.IsRegistered || !entity.HasOwner ||
            entity.OwnerID.SmallID != message.Sender.Value)
            return AntiCheatContext.Reject(player, state, "clean-scene/foreign entity despawn", true);
        float now = Time.realtimeSinceStartup;
        if (now - state.DespawnWindow >= FusionAntiCheatSettings.MassDespawnWindowSeconds.Value)
        {
            state.DespawnWindow = now;
            state.Despawns.Clear();
        }
        state.Despawns.Add(entity.ID);
        return state.Despawns.Count < FusionAntiCheatSettings.MassDespawnLimit.Value ||
            AntiCheatContext.Reject(player, state, $"mass despawn: {state.Despawns.Count} entities", true);
    }

    public static bool ValidatePlayerAction(ReceivedMessage message, PlayerActionType type)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state) ||
            type != PlayerActionType.DEATH)
            return true;
        return AntiCheatContext.Rate(player, state, ref state.DeathWindow, ref state.DeathCount,
            FusionAntiCheatSettings.MaximumDeathActionsPerSecond.Value, "kill-all/death-action flood", true);
    }
}
