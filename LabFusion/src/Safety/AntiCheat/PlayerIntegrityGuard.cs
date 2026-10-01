using LabFusion.Data;
using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Representation;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal static class PlayerIntegrityGuard
{
    public static bool ValidatePose(ReceivedMessage message, RigPose pose)
    {
        if (!AntiCheatContext.Inspect(message, out var player, out var state))
            return true;
        if (pose?.PelvisPose == null || pose.TrackedPoints == null ||
            pose.TrackedPoints.Length != RigAbstractor.TransformSyncCount)
            return AntiCheatContext.Reject(player, state, "malformed pose", true);

        float now = Time.realtimeSinceStartup;
        bool badStats = !AntiCheatContext.Finite(pose.Health) || !AntiCheatContext.Finite(pose.MaxHealth) ||
            pose.MaxHealth <= 0 || pose.MaxHealth > FusionAntiCheatSettings.MaximumHealth.Value ||
            pose.Health < -10 || pose.Health > pose.MaxHealth + FusionAntiCheatSettings.HealthTolerance.Value;
        state.StatStrikes = badStats ? state.StatStrikes + 1 : System.Math.Max(0, state.StatStrikes - 1);
        if (state.StatStrikes >= FusionAntiCheatSettings.IntegrityStrikesBeforeAction.Value)
            return AntiCheatContext.Reject(player, state,
                $"stat changer: health={pose.Health:0.##}/{pose.MaxHealth:0.##}", true);
        if (!AntiCheatContext.Finite(pose.PelvisPose.Position) ||
            !AntiCheatContext.Finite(pose.PelvisPose.Velocity))
            return AntiCheatContext.Reject(player, state, "non-finite pose", true);
        if (!MovementGuard.ValidatePose(player, state, pose.PelvisPose.Position, now))
            return false;

        bool badRig = pose.PelvisPose.Velocity.magnitude >
            FusionAntiCheatSettings.MaximumMovementSpeed.Value * 2;
        foreach (var point in pose.TrackedPoints)
        {
            if (point == null)
                return AntiCheatContext.Reject(player, state, "missing tracked-device pose", true);
            badRig |= !AntiCheatContext.Finite(point.position) ||
                !AntiCheatContext.Finite(point.rotation) ||
                point.position.magnitude > FusionAntiCheatSettings.MaximumRigReach.Value;
        }
        Vector3 head = pose.TrackedPoints[0].position;
        if (state.HasPose && now - state.LastPoseAt is >= 0.08f and < 5f)
            badRig |= Vector3.Distance(state.LastHead, head) > FusionAntiCheatSettings.MaximumHeadJump.Value;
        state.RigStrikes = badRig ? state.RigStrikes + 1 : System.Math.Max(0, state.RigStrikes - 1);
        if (state.RigStrikes >= FusionAntiCheatSettings.IntegrityStrikesBeforeAction.Value)
            return AntiCheatContext.Reject(player, state, "aim/freecam or impossible rig", true);

        if (state.PendingDamage is { } pending && now >= pending.EvaluateAfter)
        {
            bool ignored = pose.Health >= pending.Baseline - FusionAntiCheatSettings.HealthTolerance.Value;
            state.GodMisses = ignored ? state.GodMisses + 1 : 0;
            state.PendingDamage = null;
            if (state.GodMisses >= FusionAntiCheatSettings.GodModeMismatchWindows.Value)
                return AntiCheatContext.Reject(player, state,
                    $"god mode: health={pose.Health:0.##}, baseline={pending.Baseline:0.##}, damage={pending.Damage:0.##}", true);
        }
        if (pose.Health <= 0)
        {
            state.PendingDamage = null;
            state.GodMisses = 0;
        }
        state.HasPose = true;
        state.LastPoseAt = now;
        state.LastPelvis = pose.PelvisPose.Position;
        state.LastHead = head;
        state.LastHealth = pose.Health;
        AntiCheatContext.Decay(state, now);
        return true;
    }
}
