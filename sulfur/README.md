# SulfurCraft experimental host

Runs Minecraft's movement, inventory and renderer alongside the Windows Mono build of SULFUR.
The BepInEx plugin exports SULFUR collision geometry and NPC proxies, forwards input and combat
events, and displays Minecraft's hand/UI, block meshes, models, skin and equipment in Unity.
The Skyrim host remains the default when building the Fabric mod without `-PbridgeHost=sulfur`.

The current host version is **0.1.5**, paired with Fabric mod **0.1.2-sulfur.4**. Use that client
version or a compatible later build. This is an experimental integration; successful builds and transport tests
do not establish complete gameplay parity with Minecraft or SULFUR.

## Installation

Tested host: Windows x64, SULFUR Unity 6000.3.22f1, BepInEx 5.4.23.5 (Mono).
Client: Minecraft 26.3, Fabric Loader 0.19.5, Fabric API for 26.3, Java 25.

1. Put `SulfurCraft.dll` and `sulfurcraft-assets` together in
   `SULFUR/BepInEx/plugins/SulfurCraft/`.
2. Put the SULFUR Fabric jar in the selected Minecraft instance's `mods` directory. Keep only
   one SkyCraft/SulfurCraft jar in that instance. The jar embeds its SULFUR host selection.
3. Start SULFUR through Steam and launch the Minecraft instance through its usual launcher.
   Minecraft requires `--enable-native-access=ALL-UNNAMED` for the shared-memory bridge.
   Once connected, Minecraft hides its window and opens the isolated `SulfurCraft` world.
4. Enter a SULFUR save. Its player takes over Minecraft's physics once the surrounding collision
   has been received. Skin selection follows the Minecraft account/skin loaded by that client.

Minecraft saves its inventory and placed blocks in its own `saves/SulfurCraft` directory.
`BepInEx/config/SulfurCraft.worlds.tsv` assigns separate Minecraft coordinates to SULFUR
environment/level/seed/scene combinations. Keep both files when moving the setup.

## Controls and configuration

| Control | Action |
| --- | --- |
| Minecraft's normal bindings | Move, jump, crouch, sprint, attack, use/place and select items |
| E | Minecraft inventory, unless rebound in Minecraft |
| F5 | Minecraft first person, rear third person and front third person |
| R | SULFUR interaction; hold R for native hold interactions |
| F9 | Toggle the bridge and restore SULFUR control |
| F10 | SULFUR pause menu |

The native interaction key, rigidbody push strength, unit scale, collision radius, overlay
resolution and combat damage multipliers are stored in `BepInEx/config/dev.skycraft.sulfur.cfg`.
Use Minecraft's own options for its inventory bindings, skin, equipment and sensitivity.
`[Input] LowLatency` limits the host graphics queue to one frame and changes fixed-update input
processing to dynamic input when needed; both previous settings are restored on bridge exit.

## Implementation and current scope

- Uses protocol 11 over a separate Windows mapping, `Local\SulfurCraft_v1`, so it cannot attach
  to the Skyrim host. Reserved McState fields carry SULFUR movement intent and body dimensions;
  collision message 4 carries native ladder trigger volumes.
- Native collider triangles feed SkyCraft's precise terrain collision. An asynchronous SAT
  rasterizer also supplies coarse shapes for Minecraft's world logic. Moving doors invalidate
  their old and new regions instead of waiting for the static refresh interval.
- Minecraft owns normal locomotion. Native walking/camera smoothing is disabled during takeover
  and restored on exit. Position, walking bob and eye height use Minecraft's published render
  phase and its QPC timestamp.
  Host frames advance within that already collision-resolved tick, with no extra history delay
  and no extrapolation past its endpoint. Tick data and render phase are published together
  after rendering. Mouse look uses the current host input.
  The owned camera pose is reapplied just before that camera renders, with drift diagnostics.
- SULFUR's ladder trigger volumes make Minecraft players climbable on both client and server.
  The native ladder velocity updater is suspended during takeover to prevent competing movement.
- Native interactions retain the game's interaction selection, key requirements and hold timers.
  Rigidbody props receive contact impulses based on Minecraft's requested movement and the native
  player's mass. Their practical behavior still requires testing on actual doors.
- The actual `PlayerHUD` is hidden after its native update. Minecraft's supplied RGBA overlay is
  displayed with its proper image origin and premultiplied blending.
- Animated atlas patches are applied once per frame. Skin/equipment/avatar and block-entity mesh
  batches are rendered separately. Block-breaking entities use their native stage UV rectangle
  and the exported inflated bounds, including shapes smaller than a full cube.
- Melee events and incoming damage are routed between the games. Basic block occupancy updates
  native colliders and debounces A* navigation updates. Full projectile rendering, lighting,
  water integration, native progression, death parity and multiplayer acceptance remain unfinished.
- Mining SULFUR's original terrain is deliberately disabled in this phase. Placed Minecraft
  blocks remain mineable. The Skyrim renderer's terrain excavation pipeline has not been ported.

## Build and checks

Requires .NET SDK, the installed game's managed assemblies/BepInEx, Java 25, and Unity Editor
6000.4.3f1 for the saved material bundle. Game DLLs are references only and are not redistributed.

```powershell
# From the repository root
dotnet build sulfur/SulfurCraft.csproj -c Release '-p:SulfurDir=C:\Games\SULFUR'
dotnet run --project sulfur/tests/TransportTests.csproj -c Release
dotnet run --project sulfur/tests/GeometryTests.csproj -c Release '-p:SulfurDir=C:\Games\SULFUR'
dotnet run --project sulfur/tests/MotionTests.csproj -c Release

# With JAVA_HOME pointing to Java 25
cd fabric
./gradlew.bat build '-PbridgeHost=sulfur' '-Pversion=0.1.2-sulfur.4' --no-daemon
./gradlew.bat runClient '-PbridgeHost=sulfur' '-Pversion=0.1.2-sulfur.4' --no-daemon
```

To build `sulfur/build/assets/sulfurcraft-assets`, open `sulfur/assets` in Unity and run
`BuildBridgeAssets.Build`, or invoke that method with Unity's batch-mode `-executeMethod`.
Materials are saved assets. The builder creates missing materials only, preserving existing
Inspector settings; it does not rebuild a SULFUR scene.

Checks cover named shared memory, ownership recovery, rings/seqlocks, overlay buffering, raw
movement layout, SAT geometry/allocation, camera timing under jitter, ray picking and terrain
movement. The SULFUR build also runs a native-ladder volume/epoch regression. Runtime logs have
confirmed plugin loading, the hidden client, native collision transport and skin/armor export.
Gameplay feedback on 0.1.5 confirms that walking animation and player position now start
together. General movement feel, native ladder traversal, each door type, crack appearance and
third-person visual acceptance still need gameplay assessment; high frame rates alone are insufficient evidence.

Set `[Diagnostics] Enabled = true` to report frame rate, bridge CPU stages, mesh/crack/ladder
counts, disabled native camera smoothers, camera drift and contact pushes every five seconds.
