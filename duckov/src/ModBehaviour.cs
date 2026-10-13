using System;
using DuckovCraft.Configuration;
using DuckovCraft.Game;
using DuckovCraft.Link;
using DuckovCraft.Rendering;
using DuckovCraft.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace DuckovCraft
{
    [DefaultExecutionOrder(10000)]
    public sealed class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private readonly BridgeLog log = new BridgeLog();
        private SharedLink link;
        private BridgeAssets assets;
        private BridgeSettings settings;
        private PlayerBridge player;
        private WorldMapping mapping;
        private WorldView world;
        private OverlayView overlay;
        private CollisionExporter collision;
        private CombatBridge combat;
        private FrameLatencyGuard latency;
        private bool connected, failed;
        private float nextDiagnostics;

        protected override void OnAfterSetup()
        {
            try
            {
                settings = BridgeSettings.Load(info.path);
                assets = new BridgeAssets(info.path); link = new SharedLink();
                mapping = new WorldMapping(settings, info.path);
                player = new PlayerBridge(link, mapping, settings, gameObject);
                world = new WorldView(assets, mapping); overlay = new OverlayView(assets);
                collision = new CollisionExporter(mapping, settings, log);
                combat = new CombatBridge(link, mapping, player, settings); latency = new FrameLatencyGuard(log);
                RenderPipelineManager.beginCameraRendering += PrepareCamera;
                log.LogInfo("Ready through Duckov official ModManager; protocol 11. F9 toggle, F10 Duckov menu, F6 native inventory, R interact.");
            }
            catch (Exception e) { Fail(e); }
        }
        private void Update()
        {
            if (failed || link == null) return;
            try
            {
                link.Heartbeat();
                if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) settings.Enabled = !settings.Enabled;
                if (connected != link.Connected)
                {
                    connected = link.Connected; log.LogInfo("Minecraft link " + (connected ? "connected" : "disconnected"));
                    if (connected) { collision.Reset(link); world.Clear(); }
                }
                player.Update(); latency.Update(player.Active && settings.LowLatency);
                if (player.NeedsCollisionReset) collision.Reset(link);
                if (player.Active)
                {
                    collision.Update(link, player.Feet, player.CollisionMask);
                    player.SetCollisionReady(collision.Ready(link, player.Feet, player.BodyWidth, player.BodyHeight));
                }
                combat.Update();
                if (connected) { link.DrainRender(world.Receive, 256); world.FinishFrame(); world.UpdateEntities(link); }
                world.Visible(player.Active); overlay.Update(link, player.Active);
                if (settings.Diagnostics && Time.unscaledTime >= nextDiagnostics)
                {
                    nextDiagnostics = Time.unscaledTime + 5;
                    log.LogInfo($"active={player.Active}, linked={connected}, overlayFrames={overlay.Frames}, sections={world.Sections}, cracks={world.Cracks}, collisionRegions={collision.RegionsSent}, triangles={collision.TrianglesSent}, collisionReady={player.CollisionReady}, groundRecoveries={player.GroundRecoveries}, actors={combat.ActorCount}, hits={combat.Hits}, interactions={player.Interactions}, hiddenLasers={player.HiddenLasers}, disabledDepthOfField={player.DisabledDepthOfField}");
                    log.LogInfo(BridgeDiagnostics.Describe(player) + $", incomingHits={combat.IncomingHits}, directionalHits={combat.DirectionalHits}");
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
        private void PrepareCamera(ScriptableRenderContext context, Camera camera) => player?.PrepareCamera(camera);
        private void Fail(Exception e)
        {
            failed = true; log.LogError("Bridge stopped; restoring Duckov controls: " + e); Cleanup();
        }
        protected override void OnBeforeDeactivate() => Cleanup();
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= PrepareCamera;
            combat?.Dispose(); combat = null; latency?.Dispose(); latency = null;
            player?.Dispose(); player = null; overlay?.Dispose(); overlay = null; world?.Dispose(); world = null;
            link?.Dispose(); link = null; assets?.Dispose(); assets = null;
        }
    }
}
