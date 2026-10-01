# Fusion host-side anti-cheat

The anti-cheat layer adds host-authoritative validation to existing Fusion messages. It
does not add a new packet type, so it preserves the current Fusion wire protocol.

## Source layout

The implementation is split into auditable guards under
`LabFusion/src/Safety/AntiCheat`:

- `FusionAntiCheat.cs` — stable integration facade used by Fusion message handlers;
- `AntiCheatContext.cs` — trust, strikes, kick/ban enforcement and shared validation;
- `AntiCheatPlayerState.cs` — per-player counters, baselines and pending evidence;
- `PacketFloodGuard.cs` — packet size, packet-rate and inbound-byte limits;
- `MovementGuard.cs` — unauthorized teleport detection and host teleport grace;
- `PlayerIntegrityGuard.cs` — god mode, stat changer and aim/freecam rig checks;
- `AvatarGuard.cs` — OP avatar limits, allowlist and avatar-change policy;
- `SpawnGuard.cs` — Spawn Lab, exploit spawnables, developer tools and spawn floods;
- `ClientAbuseGuard.cs` — kill-all, damage flood, clean-scene and mass despawn;
- `OwnershipGuard.cs` — forged and locked ownership requests;
- `SdkMessageGuard.cs` — module/RPC payload and rate limits;
- `FusionAntiCheatSettings.cs` — host-local configuration.

## Covered checks

- malformed, non-finite, out-of-range health and pose values;
- unauthorized high-speed movement and impossible headset/controller offsets;
- out-of-range avatar dimensions, mass, and avatar stats;
- optional avatar barcode allowlist and optional blocking of avatar changes;
- spawn floods and damage-message floods;
- multi-target or malformed damage messages used for kill-all attacks;
- generic packet/byte floods and oversized packets before relay;
- SDK module/RPC floods and oversized SDK payloads;
- Spawn Lab-style packets and known exploit spawnables;
- vanilla developer tools are removed with a warning and do not cause a ban;
- attempts to despawn an entity not owned by the sender and mass clean-scene attempts;
- forged or locked entity ownership requests;
- repeated remote death actions used for kill-all attacks;
- repeated ignored damage windows used for god-mode detection;
- forged teleport packets;
- a five-second exemption for teleports initiated by the host through Fusion.

Blocked packets, trust changes and enforcement actions are written to the
MelonLoader/Fusion log. High-confidence protocol abuse is acted on immediately;
movement and Spawn Lab heuristics use the configured strike threshold. Values are
stored as host-local entries in the `BONELAB Fusion` MelonPreferences category.
The complete list is generated in the preferences file and includes:

- `Fusion Anti-Cheat Enabled`
- `Fusion Anti-Cheat Automatic Bans`
- `Fusion Anti-Cheat Dry Run`
- `Fusion Anti-Cheat Strikes Before Action`
- `Fusion Anti-Cheat Maximum Movement Speed`
- `Fusion Anti-Cheat Maximum Teleport Distance`
- `Fusion Anti-Cheat Maximum Rig Reach`
- `Fusion Anti-Cheat Maximum Health`
- `Fusion Anti-Cheat Maximum Avatar Stat`
- `Fusion Anti-Cheat Enforce Avatar Allowlist`
- `Fusion Anti-Cheat Block Avatar Changes`
- `Fusion Anti-Cheat Allowed Avatar Barcodes`
- `Fusion Anti-Cheat Maximum Spawns Per Second`
- `Fusion Anti-Cheat Maximum Damage Messages Per Second`
- packet count, inbound byte and packet-size limits;
- SDK message and payload-size limits;
- mass-despawn, death-action and integrity thresholds.

Enable dry-run while tuning thresholds to block and log packets without banning or
disconnecting the player. Setting automatic bans to `false` changes enforcement to
a kick.

## Trust boundary and limitations

These checks run where Fusion handles the host side of a message. They protect the
host and relay from the listed packet abuses, but no client-hosted game can prove
that an untrusted client is cheat-free. Checks that need authoritative game state
(for example, proving aim assistance or god mode after damage that never travels
through Fusion) require authoritative simulation and are intentionally not claimed.

Normal Fusion clients can connect because this fork does not change message layouts.
The host must run the forked build for enforcement to exist.
