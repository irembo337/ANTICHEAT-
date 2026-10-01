# Fusion host-side anti-cheat

The anti-cheat layer adds host-authoritative validation to existing Fusion messages. It
does not add a new packet type, so it preserves the current Fusion wire protocol.

## Covered checks

- malformed, non-finite, out-of-range health and pose values;
- unauthorized high-speed movement and impossible headset/controller offsets;
- out-of-range avatar dimensions, mass, and avatar stats;
- optional avatar barcode allowlist and optional blocking of avatar changes;
- spawn floods and damage-message floods;
- multi-target or malformed damage messages used for kill-all attacks;
- attempts to despawn an entity not owned by the sender;
- forged teleport packets;
- a five-second exemption for teleports initiated by the host through Fusion.

Blocked packets and strikes are written to the MelonLoader/Fusion log. By default,
four strikes within the decay period produce a Fusion ban whose reason starts with
`Fusion Anti-Cheat:`. Values are stored as host-local entries in the `BONELAB Fusion`
MelonPreferences category:

- `Fusion Anti-Cheat Enabled`
- `Fusion Anti-Cheat Automatic Bans`
- `Fusion Anti-Cheat Strikes Before Action`
- `Fusion Anti-Cheat Maximum Movement Speed`
- `Fusion Anti-Cheat Maximum Rig Reach`
- `Fusion Anti-Cheat Maximum Health`
- `Fusion Anti-Cheat Maximum Avatar Stat`
- `Fusion Anti-Cheat Enforce Avatar Allowlist`
- `Fusion Anti-Cheat Block Avatar Changes`
- `Fusion Anti-Cheat Allowed Avatar Barcodes`
- `Fusion Anti-Cheat Maximum Spawns Per Second`
- `Fusion Anti-Cheat Maximum Damage Messages Per Second`

Set automatic bans to `false` while tuning thresholds; the host will disconnect the
offending player instead of persisting a ban.

## Trust boundary and limitations

These checks run where Fusion handles the host side of a message. They protect the
host and relay from the listed packet abuses, but no client-hosted game can prove
that an untrusted client is cheat-free. Checks that need authoritative game state
(for example, perfect aim detection or proving god mode after every possible damage
source) require additional server simulation and are intentionally not claimed here.

Normal Fusion clients can connect because this fork does not change message layouts.
The host must run the forked build for enforcement to exist.
