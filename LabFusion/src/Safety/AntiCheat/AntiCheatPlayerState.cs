using UnityEngine;

namespace LabFusion.Safety.AntiCheat;

internal sealed class PendingDamageState
{
    public float Baseline;
    public float Damage;
    public float EvaluateAfter;
    public int Hits;
}

internal sealed class AntiCheatPlayerState
{
    public int Strikes;
    public int Trust = 100;
    public float LastStrike;
    public bool ActionTaken;

    public int PacketCount;
    public int PacketBytes;
    public float PacketWindow;
    public int SdkCount;
    public float SdkWindow;

    public int SpawnCount;
    public float SpawnWindow;
    public int DamageCount;
    public float DamageWindow;
    public int DeathCount;
    public float DeathWindow;
    public readonly HashSet<ushort> Despawns = new();
    public float DespawnWindow;

    public bool HasPose;
    public float LastPoseAt;
    public float LastHealth;
    public float TeleportUntil;
    public Vector3 LastPelvis;
    public Vector3 LastHead;
    public int StatStrikes;
    public int RigStrikes;
    public int GodMisses;
    public PendingDamageState PendingDamage;
}
