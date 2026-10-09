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
