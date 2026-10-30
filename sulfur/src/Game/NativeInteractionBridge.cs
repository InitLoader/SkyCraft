using System;
using PerfectRandom.Sulfur.Core;
using SulfurCraft.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SulfurCraft.Game
{
    internal sealed class NativeInteractionBridge : IDisposable
    {
        private readonly PlayerBridge player;
        private readonly BridgeSettings settings;
        private bool holding;
        public NativeInteractionBridge(PlayerBridge player, BridgeSettings settings) { this.player = player; this.settings = settings; }

        public void Update(GameManager manager)
        {
            InteractionManager interaction = InteractionManager.Instance;
            if (interaction == null) { holding = false; return; }
            bool enabled = player.Active && !player.ScreenOpen && Application.isFocused && manager != null && !manager.HasLock(GameManager.PlayerLocks.Interaction);
            var key = Keyboard.current != null ? Keyboard.current[settings.InteractionKey.Value] : null;
            if (enabled && key != null && key.wasPressedThisFrame) interaction.QueuePickup();
            bool pressed = enabled && key != null && key.isPressed;
            if (pressed || holding) interaction.HoldingPickup(pressed);
            holding = pressed;
        }
        public void Dispose() { if (holding && InteractionManager.Instance != null) InteractionManager.Instance.HoldingPickup(false); holding = false; }
    }
}
