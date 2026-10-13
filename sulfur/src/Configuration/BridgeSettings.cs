using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace SulfurCraft.Configuration
{
    internal sealed class BridgeSettings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<float> UnitsPerBlock, MeleeDamageMultiplier, IncomingDamageMultiplier, MouseSensitivity;
        public readonly ConfigEntry<int> CollisionRadius, OverlayWidth;
        public readonly ConfigEntry<bool> Diagnostics;
        public readonly ConfigEntry<bool> LowLatency;
        public readonly ConfigEntry<Key> InteractionKey;
        public readonly ConfigEntry<float> PushStrength;

        public BridgeSettings(ConfigFile config)
        {
            Enabled = config.Bind("Bridge", "Enabled", true, "F9 toggles the bridge while playing.");
            UnitsPerBlock = config.Bind("Bridge", "UnitsPerBlock", 1f, new ConfigDescription("Unity world units per Minecraft block.", new AcceptableValueRange<float>(0.1f, 10f)));
            CollisionRadius = config.Bind("Bridge", "CollisionRadius", 3, new ConfigDescription("Eight-block collision regions around the player.", new AcceptableValueRange<int>(1, 6)));
            OverlayWidth = config.Bind("Bridge", "OverlayWidth", 1280, new ConfigDescription("Minecraft hand and inventory resolution; aspect ratio follows SULFUR.", new AcceptableValueRange<int>(640, 3840)));
            MouseSensitivity = config.Bind("Bridge", "MouseSensitivity", 0.08f, new ConfigDescription("Degrees per mouse pixel before Minecraft sensitivity scaling.", new AcceptableValueRange<float>(0.001f, 1f)));
            MeleeDamageMultiplier = config.Bind("Combat", "MeleeDamageMultiplier", 5f, new ConfigDescription("Minecraft damage to SULFUR damage.", new AcceptableValueRange<float>(0.01f, 100f)));
            IncomingDamageMultiplier = config.Bind("Combat", "IncomingDamageMultiplier", 0.2f, new ConfigDescription("SULFUR damage to Minecraft damage.", new AcceptableValueRange<float>(0.01f, 100f)));
            Diagnostics = config.Bind("Diagnostics", "Enabled", false, "Log frame, collision and mesh counts every five seconds.");
            InteractionKey = config.Bind("Interaction", "Key", Key.R, "SULFUR's native interaction and hold interaction; Minecraft inventory remains on E.");
            PushStrength = config.Bind("Interaction", "PushStrength", 1f, new ConfigDescription("Native rigidbody push impulse relative to the player's mass.", new AcceptableValueRange<float>(0.1f, 10f)));
            LowLatency = config.Bind("Input", "LowLatency", true, "Use dynamic input when the game used fixed input and limit queued graphics frames to one during takeover.");
        }
    }
}
