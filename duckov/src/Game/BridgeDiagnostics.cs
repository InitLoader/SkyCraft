using UnityEngine;

namespace DuckovCraft.Game
{
    internal static class BridgeDiagnostics
    {
        public static string Describe(PlayerBridge bridge)
        {
            if (!bridge.Active) return "nativeView=" + (Duckov.UI.View.ActiveView?.GetType().Name ?? "none") + ", paused=" + GameManager.Paused;
            Vector3 feet = bridge.Feet;
            string ground = "none";
            if (Physics.Raycast(feet + Vector3.up * .05f, Vector3.down, out RaycastHit hit, 8, bridge.CollisionMask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.collider.name + ":" + hit.collider.GetType().Name + ", groundY=" + hit.point.y.ToString("F3") + ", gap=" + (feet.y - hit.point.y).ToString("F3");
                if (hit.collider is TerrainCollider terrain && terrain.terrainData != null)
                    ground += ", terrainResolution=" + terrain.terrainData.heightmapResolution + ", terrainScale=" + terrain.terrainData.heightmapScale.ToString("F3");
            }
            return "feet=" + feet.ToString("F3") + ", collisionMask=" + bridge.CollisionMask + ", ground=" + ground;
        }
    }
}
