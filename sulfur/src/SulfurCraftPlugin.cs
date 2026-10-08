using System;
using System.IO;
using BepInEx;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using SulfurCraft.Configuration;
using SulfurCraft.Game;
using SulfurCraft.Link;
using SulfurCraft.Rendering;
using SulfurCraft.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SulfurCraft
{
    [BepInPlugin("dev.skycraft.sulfur", "SulfurCraft", "0.1.4")]
    [DefaultExecutionOrder(10000)]
    public sealed class SulfurCraftPlugin : BaseUnityPlugin
    {
        private SharedLink link;
        private BridgeAssets assets;
        private BridgeSettings settings;
        private PlayerBridge player;
        private WorldMapping mapping;
        private WorldView world;
        private OverlayView overlay;
        private CollisionExporter collision;
        private LadderExporter ladders;
        private CombatBridge combat;
        private NativeInteractionBridge interaction;
        private PhysicsPushBridge physics;
        private FrameLatencyGuard latency;
        private Harmony harmony;
        private float nextDiagnostics;
        private bool connected;
        private bool failed;
        private readonly FrameTimings timings = new FrameTimings();

        private void Awake()
        {
            try
            {
                settings = new BridgeSettings(Config);
                assets = new BridgeAssets(Path.GetDirectoryName(Info.Location));
                link = new SharedLink();
                mapping = new WorldMapping(settings, Paths.ConfigPath);
                player = new PlayerBridge(link, mapping, settings);
                NativePresentation.Player = player;
                LadderExporter.Player = player;
                world = new WorldView(assets, mapping); overlay = new OverlayView(assets);
                collision = new CollisionExporter(mapping, settings, Logger);
                ladders = new LadderExporter(mapping);
                interaction = new NativeInteractionBridge(player, settings); physics = new PhysicsPushBridge(player, mapping, settings);
                latency = new FrameLatencyGuard(Logger);
                combat = new CombatBridge(link, mapping, player, settings); CombatBridge.Current = combat;
                harmony = new Harmony("dev.skycraft.sulfur"); harmony.PatchAll(typeof(SulfurCraftPlugin).Assembly);
                Logger.LogInfo("SulfurCraft ready: protocol 11, Minecraft movement; F9 toggle, F10 SULFUR menu.");
            }
            catch (Exception e) { Fail(e); }
        }

        private void Update()
        {
            if (failed || link == null) return;
            try
            {
                timings.Begin();
                link.Heartbeat();
                if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) settings.Enabled.Value = !settings.Enabled.Value;
                if (connected != link.Connected)
                {
                    connected = link.Connected; Logger.LogInfo("Minecraft link " + (connected ? "connected" : "disconnected"));
                    if (connected) { collision.Reset(link); ladders.Reset(); world.Clear(); }
                }
                GameManager manager = GameManager.Instance;
                player.Update(manager, settings.Enabled.Value);
                latency.Update(player.Active && settings.LowLatency.Value);
                interaction.Update(manager);
                timings.Mark(0);
                if (player.NeedsCollisionReset) { collision.Reset(link); ladders.Reset(); }
                if (player.Active) { collision.Update(link, player.Feet, manager.geometryMask); ladders.Update(link, player.Feet); }
                timings.Mark(1);
                if (connected) { link.DrainRender(world.Receive, 256); world.FinishFrame(); }
                timings.Mark(2);
                if (connected) { combat.Update(); world.UpdateEntities(link); }
                timings.Mark(3);
                world.Visible(player.Active);
                overlay.Update(link, player.Active);
                timings.Mark(4);
                if (settings.Diagnostics.Value && Time.unscaledTime >= nextDiagnostics)
                {
                    nextDiagnostics = Time.unscaledTime + 5;
                    Logger.LogInfo($"Bridge active={player.Active}, linked={connected}, overlayFrames={overlay.Frames}, sections={world.Sections}, cracks={world.Cracks}, collisionRegions={collision.RegionsSent}, triangles={collision.TrianglesSent}, hits={combat.Hits}, pushes={physics.Pushes}, ladders={ladders.Count}, nativeSmoothers={player.NativeSmoothers}, cameraDrift={player.CameraDrift:F4}/{player.CameraAngleDrift:F2}deg");
                    Logger.LogInfo(timings.Report());
                }
            }
            catch (Exception e) { Fail(e); }
        }
        private void LateUpdate()
        {
            if (failed) return;
            try { player?.LateUpdate(); if (player != null && player.Active) world.SetPlayerFeet(player.Feet); }
            catch (Exception e) { Fail(e); }
        }
        private void FixedUpdate()
        {
            if (failed) return;
            try { physics?.FixedUpdate(GameManager.Instance, collision); }
            catch (Exception e) { Fail(e); }
        }
        private void Fail(Exception e)
        {
            failed = true; Logger.LogError("SulfurCraft stopped and restored SULFUR controls: " + e);
            Cleanup();
        }
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            CombatBridge.Current = null; NativePresentation.Player = null; LadderExporter.Player = null; harmony?.UnpatchSelf(); harmony = null;
            interaction?.Dispose(); interaction = null; physics = null;
            latency?.Dispose(); latency = null;
            player?.Dispose(); player = null; overlay?.Dispose(); overlay = null; world?.Dispose(); world = null;
            link?.Dispose(); link = null; assets?.Dispose(); assets = null;
        }
    }
}
