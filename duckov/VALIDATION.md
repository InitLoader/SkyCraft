# 2026-10-09 startup failure and rollback

Duckov 0.1.4 is not accepted for gameplay. The initial shield-test reply was corrected by the user: the game froze with a blank window during startup, before shield combat. The installed bridge and paired Minecraft client were restored to the saved 0.1.3 files, preserving both games' saves, bridge settings and world mappings. The user subsequently confirmed Minecraft view returned and the game did not black-screen again.

## Evidence

- The failed 0.1.4 native log stopped immediately after connection diagnostics with `active=False`, `incomingHits=0` and `directionalHits=0`. The first texture failure was a 2048 x 2576 atlas with HRESULT `0x887A0005`; subsequent small textures and `Graphics.CopyTexture` also failed.
- Microsoft defines `0x887A0005` as `DXGI_ERROR_DEVICE_REMOVED`. This identifies a failed graphics device, not the initiating fault: [DXGI error reference](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-error).
- Windows recorded NVIDIA `nvlddmkm` events 153 and 13 at the same startup, including graphics SM warp/MMU exceptions. Hardware failure or a particular driver defect has not been established.
- A second 0.1.4 run started Duckov before Minecraft. Native updates stopped and NVIDIA graphics exceptions occurred before Minecraft started. Simultaneous initialization is therefore not a supported explanation for this failure.
- ZIP-entry comparison of the previously deployed client and the rebuilt 0.1.4 client found exactly one changed class, `SkyCombat.class`. Other differences were the mod version and resource line endings. All renderer classes were byte-identical, so a missing previous rendering patch was ruled out.
- The 0.1.3 rollback run continuously received overlay frames with `active=True`, `linked=True` and `collisionReady=True`. More than 7,000 frames were observed with no repeated D3D11 texture failure. NVIDIA event 153 was also present during its startup, so that event alone does not prove a black-screen failure.
- The user's temporary native-view screenshot preceded the Minecraft connection. The bridge then enabled automatically; user confirmation established the visible recovery.

## Boundaries

The earlier zero-warning native build, 4,131 transport/math assertions and 22 Minecraft tests remain build/static evidence only. They do not validate a working 0.1.4 startup or native melee/projectile shield blocking. The exact startup failure trigger remains unknown. A healthy rollback session does not establish a permanent graphics fix.

The current 0.1.3 installation retains the user's previously confirmed improvements to outdoor invisible walls/hovering and native F9 restoration. Shield integration is not present in this rollback. Existing 0.1.4 package artifacts must not be delivered as a verified repair.

## Recurrence and external renderer isolation

The user subsequently reported another startup freeze on the restored 0.1.3. The installed DLL hash still matched the saved 0.1.3 DLL, so the earlier successful session did not establish a permanent fix and the failure was not exclusive to 0.1.4.

The preserved ReShade log provides a closer trigger: at 14:19:25, RenoDX DLSS initialized its direct neural-rendering path and private output, followed immediately by `DXGI_ERROR_DEVICE_REMOVED` with removal reason `DXGI_ERROR_DEVICE_HUNG`. Windows recorded NVIDIA SM warp/MMU exceptions at the same time; Minecraft reported the host link down at 14:19:33. The official DuckovDLSS5 Mod was marked inactive, but the root ReShade addon was still running. The underlying fault inside the addon/driver is not established by these logs.

With both bridge processes already closed, only `renodx-dlss.addon64` was moved out of the root addon-loading path to `DuckovCraft/backups/render-conflict-20261009-142436/`; its SHA256 was preserved and ReShade.ini was copied beside it. ReShade itself, the official Mods, Minecraft files and saves/settings were retained. No registry or driver settings were changed.

The ordinary launcher then connected both games. ReShade no longer logged RenoDX evaluation or device loss, native updates continued and the user supplied a working first-person screenshot with Minecraft items and placed blocks, confirming recovery. This supports the external neural-rendering path as the practical trigger for this installation; prolonged/repeated-start stability and the separate 0.1.4 shield feature remain unverified. Keep the backup addon outside the loading path while using this recovered configuration.
