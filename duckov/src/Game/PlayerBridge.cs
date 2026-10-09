using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dialogues;
using Duckov.Scenes;
using Duckov.UI;
using DuckovCraft.Configuration;
using DuckovCraft.Link;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DuckovCraft.Game
{
    internal sealed class PlayerBridge : IDisposable
    {
        private readonly SharedLink link;
        private readonly WorldMapping world;
        private readonly BridgeSettings settings;
        private readonly GameObject owner;
        private readonly InputBridge input;
        private readonly MotionInterpolator motion = new MotionInterpolator(Stopwatch.Frequency);
        private readonly Dictionary<Behaviour, bool> behaviours = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        private readonly List<InputAction> actions = new List<InputAction>();
        private CharacterMainControl player;
        private ECM2.CharacterMovement motor;
        private Camera camera;
        private Rigidbody body;
        private bool kinematic, gravity, movementEnabled, colliderEnabled;
        private RigidbodyInterpolation interpolation;
        private Vector3 rootFromFeet, cameraPosition, lastPosition, teleportPosition;
        private Quaternion cameraRotation;
        private float fov, nearClip, yaw, pitch;
        private uint teleportSequence;
        private McState mc;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private Vector3 renderedPosition;
        private Quaternion renderedRotation;
        private bool cameraReady;
        public bool Active => player != null;
        public CharacterMainControl Player => player;
        public Vector3 Feet => player != null ? player.transform.position - rootFromFeet : Vector3.zero;
        public bool ScreenOpen => (mc.Flags & Protocol.McScreenOpen) != 0;
        public bool NeedsCollisionReset { get; private set; }

        public PlayerBridge(SharedLink link, WorldMapping world, BridgeSettings settings, GameObject owner)
        {
            this.link = link; this.world = world; this.settings = settings; this.owner = owner;
            input = new InputBridge(link);
        }
        public void Update()
        {
            NeedsCollisionReset = false;
            var keyboard = Keyboard.current;
            if (Active && !ScreenOpen && keyboard != null)
            {
                if (keyboard.f10Key.wasPressedThisFrame) PauseMenu.Show();
                else if (keyboard.f6Key.wasPressedThisFrame) InventoryView.Show();
                else if (keyboard.rKey.wasPressedThisFrame) { player.RefreshInteractTarget(); player.Interact(); }
            }
            LevelManager level = LevelManager.Instance;
            CharacterMainControl next = CharacterMainControl.Main;
            bool loading = LevelManager.LevelInitializing || (MultiSceneCore.Instance != null && MultiSceneCore.Instance.IsLoading);
            bool nativeUi = GameManager.Paused || View.ActiveView != null || DialogueUI.Active || CameraMode.Active;
            bool takeOver = settings.Enabled && link.Connected && !loading && !nativeUi && LevelManager.LevelInited && next != null && next.Health != null && !next.Health.IsDead
                && level != null && level.ControllingCharacter == next && level.GameCamera != null;
            if (player != null && (next != player || !takeOver)) Restore();
            if (takeOver && player == null) TakeOver(next, level);
            if (player != null)
            {
                foreach (var entry in behaviours) if (entry.Key != null) entry.Key.enabled = false;
                foreach (InputAction action in actions) if (action.enabled) action.Disable();
                if (world.Select() || (player.transform.position - lastPosition).sqrMagnitude > 4f * world.Units * world.Units)
                {
                    teleportPosition = world.ToMc(Feet); teleportSequence++; NeedsCollisionReset = true;
                }
                if (link.ReadMc(out McState state)) mc = state;
                if (!ScreenOpen && Application.isFocused && Mouse.current != null)
                {
                    Vector2 delta = Mouse.current.delta.ReadValue();
                    float factor = Mathf.Pow(mc.Sensitivity * .6f + .2f, 3) * 8;
                    yaw += delta.x * settings.MouseSensitivity * factor;
                    pitch = Mathf.Clamp(pitch - delta.y * settings.MouseSensitivity * factor, -89.9f, 89.9f);
                }
                Cursor.lockState = ScreenOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = ScreenOpen;
            }
            int width = settings.OverlayWidth;
            int height = Mathf.Clamp(Mathf.RoundToInt(width * (float)Screen.height / Math.Max(1, Screen.width)), 360, 2160);
            input.Update(Active, ScreenOpen, width, height);
            uint seq = link.BeginWrite(Protocol.Sky);
            link.Put(Protocol.Sky + 4, Active ? Protocol.InGame : Protocol.MenuOpen | (loading ? Protocol.Loading : 0));
            link.Put(Protocol.Sky + 8, world.WorldId); link.Put(Protocol.Sky + 12, world.Epoch);
            link.PutDouble(Protocol.Sky + 16, teleportPosition.x); link.PutDouble(Protocol.Sky + 24, teleportPosition.y); link.PutDouble(Protocol.Sky + 32, teleportPosition.z);
            link.PutFloat(Protocol.Sky + 40, yaw); link.PutFloat(Protocol.Sky + 44, pitch); link.Put(Protocol.Sky + 48, teleportSequence);
            link.Put(Protocol.Sky + 52, (uint)width); link.Put(Protocol.Sky + 56, (uint)height); link.PutFloat(Protocol.Sky + 60, 12);
            link.EndWrite(Protocol.Sky, seq);
        }
        private void TakeOver(CharacterMainControl next, LevelManager level)
        {
            player = next; world.Select(); motor = player.GetComponent<ECM2.CharacterMovement>();
            if (motor == null) throw new InvalidOperationException("Duckov player has no ECM2 CharacterMovement.");
            rootFromFeet = new Vector3(0, player.transform.position.y - motor.collider.bounds.min.y, 0);
            body = motor.rigidbody;
            if (body != null)
            {
                kinematic = body.isKinematic; gravity = body.useGravity; interpolation = body.interpolation;
                if (!kinematic) body.linearVelocity = Vector3.zero;
                body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.None;
            }
            camera = level.GameCamera.renderCamera;
            cameraPosition = camera.transform.position; cameraRotation = camera.transform.rotation;
            fov = camera.fieldOfView; nearClip = camera.nearClipPlane; camera.nearClipPlane = settings.NearClip;
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            if (GameManager.MainPlayerInput != null)
                foreach (InputAction action in GameManager.MainPlayerInput.actions)
                    if (action.enabled) { actions.Add(action); action.Disable(); }
            player.SetMoveInput(Vector3.zero); player.Trigger(false, false, false);
            motor.velocity = Vector3.zero;
            Disable(CharacterInputControl.Instance); Disable(level.InputManager); Disable(player.movementControl); Disable(motor);
            movementEnabled = player.movementControl.MovementEnabled; colliderEnabled = motor.collider.enabled;
            player.movementControl.MovementEnabled = false;
            motor.collider.enabled = colliderEnabled;
            Disable(level.GameCamera); Disable(level.GameCamera.brain);
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) { renderers[renderer] = renderer.enabled; renderer.enabled = false; }
            HUDManager.RegisterHideToken(owner);
            yaw = player.modelRoot.eulerAngles.y + 180; pitch = 0;
            teleportPosition = world.ToMc(Feet); teleportSequence++; NeedsCollisionReset = true;
            lastPosition = player.transform.position; cameraReady = false;
        }
        private void Disable(Behaviour value)
        {
            if (value == null || behaviours.ContainsKey(value)) return;
            behaviours[value] = value.enabled; value.enabled = false;
        }
        public void LateUpdate()
        {
            if (player == null || camera == null) return;
            Vector3 eyePosition = Feet + Vector3.up * (1.62f * world.Units);
            float bobPhase = mc.BobPhase, bobAmount = mc.BobAmount;
            if ((mc.Flags & Protocol.McInWorld) != 0 && mc.TeleportAck == teleportSequence && Finite(mc.CurX) && Finite(mc.CurY) && Finite(mc.CurZ))
            {
                MotionPose pose = motion.Sample(mc, Stopwatch.GetTimestamp());
                Vector3 feet = world.FromMc(pose.X, pose.Y, pose.Z);
                Vector3 position = feet + rootFromFeet;
                if (body != null) body.position = position;
                player.transform.position = position;
                eyePosition = feet + Vector3.up * ((pose.Eye > 0 ? (float)pose.Eye : 1.62f) * world.Units);
                bobPhase = (float)pose.BobPhase; bobAmount = (float)pose.BobAmount;
                if (mc.Fov > 10 && mc.Fov < 170) camera.fieldOfView = mc.Fov;
            }
            Quaternion look = Quaternion.Euler(pitch, yaw - 180, 0);
            player.modelRoot.rotation = Quaternion.Euler(0, yaw - 180, 0);
            player.SetAimPoint(eyePosition + look * Vector3.forward * 50 * world.Units);
            bool mirrored = mc.CameraMode == 2;
            float cameraYaw = yaw - 180 + (mirrored ? 180 : 0), cameraPitch = mirrored ? -pitch : pitch;
            Quaternion cameraLook = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            if (mc.CameraMode != 0) eyePosition -= cameraLook * Vector3.forward * (Mathf.Clamp(mc.CameraDistance, 0, 32) * world.Units);
            float phase = bobPhase * Mathf.PI, bob = bobAmount;
            Vector3 sway = cameraLook * Vector3.right * (-Mathf.Sin(phase) * bob * .5f * world.Units);
            sway += Vector3.up * (Mathf.Abs(Mathf.Cos(phase) * bob) * world.Units);
            renderedPosition = eyePosition + sway;
            renderedRotation = Quaternion.Euler(cameraPitch + Mathf.Abs(Mathf.Cos(phase - .2f) * bob) * 5, cameraYaw, -Mathf.Sin(phase) * bob * 3);
            cameraReady = true; PrepareCamera(camera); lastPosition = player.transform.position;
        }
        public void PrepareCamera(Camera value)
        {
            if (Active && cameraReady && value == camera) camera.transform.SetPositionAndRotation(renderedPosition, renderedRotation);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private void Restore()
        {
            if (player == null && behaviours.Count == 0) return;
            link.Input(Protocol.ReleaseAll); HUDManager.UnregisterHideToken(owner);
            if (motor != null) motor.velocity = Vector3.zero;
            if (player != null) player.movementControl.MovementEnabled = movementEnabled;
            if (motor != null) motor.collider.enabled = colliderEnabled;
            foreach (var entry in behaviours) if (entry.Key != null) entry.Key.enabled = entry.Value;
            behaviours.Clear();
            foreach (var entry in renderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
            renderers.Clear();
            foreach (InputAction action in actions) action.Enable(); actions.Clear();
            if (body != null) { body.isKinematic = kinematic; body.useGravity = gravity; body.interpolation = interpolation; }
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
                camera.fieldOfView = fov; camera.nearClipPlane = nearClip;
            }
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            player = null; motor = null; body = null; camera = null; cameraReady = false;
        }
        public void Dispose() { Restore(); input.Dispose(); }
    }
}
