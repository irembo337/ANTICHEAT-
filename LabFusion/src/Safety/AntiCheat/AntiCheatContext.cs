using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Senders;
using LabFusion.Utilities;
using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal static class AntiCheatContext
{
    private static readonly Dictionary<ulong, AntiCheatPlayerState> _states = new();

    public static bool Inspect(ReceivedMessage message, out PlayerID player, out AntiCheatPlayerState state)
    {
        player = null;
        state = null;
        if (!message.IsServerHandled || !FusionAntiCheatSettings.Enabled.Value || !message.Sender.HasValue ||
            message.Sender.Value == PlayerIDManager.HostSmallID)
            return false;
        player = PlayerIDManager.GetPlayerID(message.Sender.Value);
        if (player == null)
            return false;
        state = Get(player);
        return true;
    }

    public static AntiCheatPlayerState Get(PlayerID player)
    {
        if (!_states.TryGetValue(player.PlatformID, out var state))
            _states[player.PlatformID] = state = new();
        return state;
    }

    public static bool Rate(PlayerID player, AntiCheatPlayerState state, ref float window,
        ref int count, int limit, string reason, bool immediate = false)
    {
        float now = Time.realtimeSinceStartup;
        if (now - window >= 1f)
        {
            window = now;
            count = 0;
        }
        count++;
        return count <= System.Math.Max(1, limit) || Reject(player, state, reason, immediate);
    }

    public static bool Reject(PlayerID player, AntiCheatPlayerState state, string reason, bool immediate = false)
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
                else
                {
                    NetworkConnectionManager.DisconnectUser(player.PlatformID);
                }
            }
            string action = FusionAntiCheatSettings.DryRun.Value ? "Dry-run" :
                FusionAntiCheatSettings.AutomaticBans.Value ? "Banned" : "Kicked";
            FusionLogger.Warn($"[Fusion Anti-Cheat] {action} {player.PlatformID}: {reason}.");
        }
        return false;
    }

    public static void Decay(AntiCheatPlayerState state, float now)
    {
        if (state.Strikes > 0 && now - state.LastStrike > FusionAntiCheatSettings.StrikeDecaySeconds.Value)
        {
            state.Strikes--;
            state.Trust = System.Math.Min(100, state.Trust + 5);
            state.LastStrike = now;
        }
    }

    public static void Forget(PlayerID player) => _states.Remove(player.PlatformID);
    public static void Reset() => _states.Clear();
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    public static bool Finite(Quaternion value) =>
        Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
}
