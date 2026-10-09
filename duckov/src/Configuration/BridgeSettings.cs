using System;
using System.IO;
using UnityEngine;

namespace DuckovCraft.Configuration
{
    [Serializable]
    internal sealed class BridgeSettings
    {
        public bool Enabled = true;
        public float UnitsPerBlock = 1f;
        public int CollisionRadius = 3;
        public int OverlayWidth = 1280;
        public float MouseSensitivity = .08f;
        public float MeleeDamageMultiplier = 5f;
        public float IncomingDamageMultiplier = .2f;
        public float NearClip = .05f;
        public bool LowLatency = true;
        public bool DisableDepthOfField = true;
        public bool Diagnostics = true;

        public static BridgeSettings Load(string directory)
        {
            string path = Path.Combine(directory, "settings.json");
            var result = new BridgeSettings();
            if (File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path), result);
            else File.WriteAllText(path, JsonUtility.ToJson(result, true));
            if (!Finite(result.UnitsPerBlock) || !Finite(result.MouseSensitivity) || !Finite(result.MeleeDamageMultiplier) || !Finite(result.IncomingDamageMultiplier) || !Finite(result.NearClip))
                throw new InvalidDataException("DuckovCraft settings contain a non-finite value.");
            result.UnitsPerBlock = Mathf.Clamp(result.UnitsPerBlock, .1f, 10f);
            result.CollisionRadius = Mathf.Clamp(result.CollisionRadius, 1, 6);
            result.OverlayWidth = Mathf.Clamp(result.OverlayWidth, 640, 3840);
            result.MouseSensitivity = Mathf.Clamp(result.MouseSensitivity, .001f, 1f);
            result.MeleeDamageMultiplier = Mathf.Clamp(result.MeleeDamageMultiplier, .01f, 100f);
            result.IncomingDamageMultiplier = Mathf.Clamp(result.IncomingDamageMultiplier, .01f, 100f);
            result.NearClip = Mathf.Clamp(result.NearClip, .01f, 1f);
            return result;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal sealed class BridgeLog
    {
        public void LogInfo(object message) => Debug.Log("[DuckovCraft] " + message);
        public void LogWarning(object message) => Debug.LogWarning("[DuckovCraft] " + message);
        public void LogError(object message) => Debug.LogError("[DuckovCraft] " + message);
    }
}
