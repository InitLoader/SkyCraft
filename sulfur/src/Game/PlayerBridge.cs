using System;
using System.Collections.Generic;
using System.Diagnostics;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using SulfurCraft.Configuration;
using SulfurCraft.Link;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SulfurCraft.Game
{
    internal sealed class PlayerBridge : IDisposable
    {
        private readonly SharedLink link;
        private readonly WorldMapping world;
        private readonly BridgeSettings settings;
        private readonly InputBridge input;
        private readonly MotionInterpolator motion = new MotionInterpolator(Stopwatch.Frequency);
        private readonly Dictionary<Behaviour, bool> behaviours = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        private Player player;
        private Rigidbody body;
        private RigidbodyInterpolation bodyInterpolation;
        private bool kinematic, gravity, weaponActive, onFootEnabled;
        private Vector3 rootFromFeet, cameraLocalPosition;
        private Quaternion cameraLocalRotation;
        private Vector3 controlsLocalPosition;
        private Quaternion controlsLocalRotation;
        private float fov, yaw, pitch;
        private uint teleportSequence;
        private Vector3 teleportPosition, lastPosition;
        private McState mc;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private Vector3 renderedPosition;
        private Quaternion renderedRotation;
        private bool cameraReady;
        public bool Active => player != null;
        public Player Player => player;
        public bool MotionReady => player != null && (mc.Flags & Protocol.McInWorld) != 0 && mc.TeleportAck == teleportSequence;
        public McState State => mc;
        public int NativeSmoothers { get; private set; }
        public float CameraDrift { get; private set; }
        public float CameraAngleDrift { get; private set; }
        public float BodyMass => body != null ? body.mass : 1;
        public bool ScreenOpen => (mc.Flags & Protocol.McScreenOpen) != 0;
        public Vector3 Feet => player != null ? player.transform.position - rootFromFeet : Vector3.zero;
        public bool NeedsCollisionReset { get; private set; }

        public PlayerBridge(SharedLink link, WorldMapping world, BridgeSettings settings)
        {
            this.link = link; this.world = world; this.settings = settings; input = new InputBridge(link);
        }

        public void Update(GameManager manager, bool enabled)
        {
            NeedsCollisionReset = false;
            Player next = manager != null ? manager.PlayerScript : null;
            bool running = manager != null && manager.gameState == GameState.Running;
            bool canTakeOver = enabled && link.Connected && running && next != null && next.playerUnit != null && next.playerUnit.IsAlive;
            if (player != null && (next != player || !canTakeOver)) Restore();
            if (canTakeOver && player == null) TakeOver(next);
            if (player != null)
            {
                foreach (var entry in behaviours) if (entry.Key != null && entry.Key.enabled) entry.Key.enabled = false;
                player.inputReader.inputActions.OnFoot.Disable();
                // Game-owned teleports (doors and level transitions) remain authoritative.
                if (world.Select(manager) || (player.transform.position - lastPosition).sqrMagnitude > 4f * world.Units * world.Units)
                {
                    teleportPosition = world.ToMc(Feet); teleportSequence++; NeedsCollisionReset = true;
                    motion.Reset();
                }
                link.ReadMc(out mc);
                if (!ScreenOpen && !manager.HasLock(GameManager.PlayerLocks.Camera) && Application.isFocused && Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    float factor = Mathf.Pow(mc.Sensitivity * .6f + .2f, 3) * 8;
                    yaw += delta.x * settings.MouseSensitivity.Value * factor;
                    pitch = Mathf.Clamp(pitch - delta.y * settings.MouseSensitivity.Value * factor, -89.9f, 89.9f);
                }
                bool menu = ScreenOpen || manager.HasLock(GameManager.PlayerLocks.Interaction);
                Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = menu;
                if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame) manager.PauseGame();
            }
            int width = settings.OverlayWidth.Value;
            int height = Mathf.Clamp(Mathf.RoundToInt(width * (float)Screen.height / Math.Max(1, Screen.width)), 360, 2160);
            bool nativeMovementLock = manager != null && manager.HasLock(GameManager.PlayerLocks.PlayerMovement);
            input.Update(player != null && running && !nativeMovementLock, ScreenOpen, width, height);
            uint seq = link.BeginWrite(Protocol.Sky);
            uint flags = player != null && !nativeMovementLock ? Protocol.InGame : Protocol.MenuOpen;
            if (manager != null && manager.gameState == GameState.Loading) flags |= Protocol.Loading;
            link.Put(Protocol.Sky + 4, flags); link.Put(Protocol.Sky + 8, world.WorldId); link.Put(Protocol.Sky + 12, world.Epoch);
            link.PutDouble(Protocol.Sky + 16, teleportPosition.x); link.PutDouble(Protocol.Sky + 24, teleportPosition.y); link.PutDouble(Protocol.Sky + 32, teleportPosition.z);
            link.PutFloat(Protocol.Sky + 40, yaw); link.PutFloat(Protocol.Sky + 44, pitch); link.Put(Protocol.Sky + 48, teleportSequence);
            link.Put(Protocol.Sky + 52, (uint)width); link.Put(Protocol.Sky + 56, (uint)height); link.PutFloat(Protocol.Sky + 60, 12);
            link.EndWrite(Protocol.Sky, seq);
        }

        private void TakeOver(Player next)
        {
            player = next;
            world.Select(GameManager.Instance);
            var collider = player.playerUnit.mainCollider;
            rootFromFeet = collider != null ? new Vector3(0, player.transform.position.y - collider.bounds.min.y, 0) : Vector3.zero;
            body = player.GetComponent<Rigidbody>();
            if (body != null) { kinematic = body.isKinematic; gravity = body.useGravity; bodyInterpolation = body.interpolation; body.linearVelocity = Vector3.zero; body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None; }
            onFootEnabled = player.inputReader.inputActions.OnFoot.enabled;
            player.inputReader.LockInput(true);
            Disable(player.movement); Disable(player.swimScript); Disable(player.playerCamController);
            Disable(player.cameraRootAnimator); Disable(player.weaponsAnchorAnimator);
            Disable(player.inputReader);
            Disable(player.GetComponent<CMF.Mover>());
            NativeSmoothers = 0;
            foreach (var smoother in player.GetComponentsInChildren<CMF.SmoothPosition>(true)) { Disable(smoother); NativeSmoothers++; }
            foreach (var smoother in player.GetComponentsInChildren<CMF.SmoothRotation>(true)) { Disable(smoother); NativeSmoothers++; }
            weaponActive = player.weaponsRoot != null && player.weaponsRoot.gameObject.activeSelf;
            if (player.weaponsRoot != null) player.weaponsRoot.gameObject.SetActive(false);
            foreach (var renderer in player.playerVisuals ?? Array.Empty<Renderer>())
                if (renderer != null) { renderers[renderer] = renderer.enabled; renderer.enabled = false; }
            cameraLocalPosition = player.playerCamera.transform.localPosition;
            cameraLocalRotation = player.playerCamera.transform.localRotation; fov = player.playerCamera.fieldOfView;
            if (player.cameraControls != null) { controlsLocalPosition = player.cameraControls.localPosition; controlsLocalRotation = player.cameraControls.localRotation; }
            Vector3 angles = player.playerCamera.transform.eulerAngles;
            yaw = angles.y + 180; pitch = Mathf.DeltaAngle(0, angles.x);
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            teleportPosition = world.ToMc(Feet); teleportSequence++; NeedsCollisionReset = true;
            lastPosition = player.transform.position;
            motion.Reset();
            cameraReady = false; CameraDrift = CameraAngleDrift = 0;
        }
        private void Disable(Behaviour behaviour)
        {
            if (behaviour == null || behaviours.ContainsKey(behaviour)) return;
            behaviours[behaviour] = behaviour.enabled; behaviour.enabled = false;
        }

        public void LateUpdate()
        {
            if (player == null) return;
            Quaternion look = Quaternion.Euler(pitch, yaw - 180, 0);
            Vector3 eyePosition = player.playerCamera.transform.position;
            float bobPhase = mc.BobPhase, bobAmount = mc.BobAmount;
            bool valid = (mc.Flags & Protocol.McInWorld) != 0 && mc.TeleportAck == teleportSequence;
            if (valid && Finite(mc.CurX) && Finite(mc.CurY) && Finite(mc.CurZ))
            {
                MotionPose pose = motion.Sample(mc, Stopwatch.GetTimestamp());
                Vector3 feet = world.FromMc(pose.X, pose.Y, pose.Z);
                player.transform.position = feet + rootFromFeet;
                player.playerUnit.isGrounded = (mc.Flags & 4) != 0;
                float eye = (float)pose.Eye;
                if (eye <= 0) eye = mc.EyeHeight > 0 ? mc.EyeHeight : 1.62f;
                eyePosition = feet + Vector3.up * (eye * world.Units);
                bobPhase = (float)pose.BobPhase; bobAmount = (float)pose.BobAmount;
                if (mc.Fov > 10 && mc.Fov < 170) player.playerCamera.fieldOfView = mc.Fov;
            }
            // The game's interaction rays and enemy aim use cameraControls.
            if (player.cameraControls != null && player.cameraControls != player.transform) player.cameraControls.SetPositionAndRotation(eyePosition, look);
            bool mirrored = mc.CameraMode == 2;
            float cameraYaw = yaw - 180 + (mirrored ? 180 : 0), cameraPitch = mirrored ? -pitch : pitch;
            Quaternion cameraLook = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            if (mc.CameraMode != 0) eyePosition -= cameraLook * Vector3.forward * (Mathf.Clamp(mc.CameraDistance, 0, 32) * world.Units);
            float phase = bobPhase * Mathf.PI, bob = bobAmount;
            Vector3 sway = cameraLook * Vector3.right * (-Mathf.Sin(phase) * bob * .5f * world.Units);
            sway += Vector3.up * (Mathf.Abs(Mathf.Cos(phase) * bob) * world.Units);
            float bobPitch = Mathf.Abs(Mathf.Cos(phase - .2f) * bob) * 5, roll = Mathf.Sin(phase) * bob * 3;
            player.playerCamera.transform.SetPositionAndRotation(eyePosition + sway, Quaternion.Euler(cameraPitch + bobPitch, cameraYaw, -roll));
            renderedPosition = player.playerCamera.transform.position; renderedRotation = player.playerCamera.transform.rotation; cameraReady = true;
            lastPosition = player.transform.position;
        }
        public void PrepareCamera(Camera camera)
        {
            if (player == null || !cameraReady || camera != player.playerCamera) return;
            CameraDrift = Mathf.Max(CameraDrift, Vector3.Distance(camera.transform.position, renderedPosition));
            CameraAngleDrift = Mathf.Max(CameraAngleDrift, Quaternion.Angle(camera.transform.rotation, renderedRotation));
            camera.transform.SetPositionAndRotation(renderedPosition, renderedRotation);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private void Restore()
        {
            link.Input(Protocol.ReleaseAll);
            foreach (var entry in behaviours) if (entry.Key != null) entry.Key.enabled = entry.Value;
            behaviours.Clear(); foreach (var entry in renderers) if (entry.Key != null) entry.Key.enabled = entry.Value; renderers.Clear();
            if (body != null) { body.isKinematic = kinematic; body.useGravity = gravity; body.interpolation = bodyInterpolation; if (!kinematic) body.linearVelocity = Vector3.zero; }
            if (player != null)
            {
                if (player.weaponsRoot != null) player.weaponsRoot.gameObject.SetActive(weaponActive);
                if (player.inputReader != null && onFootEnabled) player.inputReader.LockInput(false);
                if (player.cameraControls != null && player.cameraControls != player.transform) { player.cameraControls.localPosition = controlsLocalPosition; player.cameraControls.localRotation = controlsLocalRotation; }
                if (player.playerCamera != null) { player.playerCamera.transform.localPosition = cameraLocalPosition; player.playerCamera.transform.localRotation = cameraLocalRotation; player.playerCamera.fieldOfView = fov; }
            }
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            player = null; body = null;
            cameraReady = false;
            motion.Reset();
        }
        public void Dispose() { Restore(); input.Dispose(); }
    }
}
