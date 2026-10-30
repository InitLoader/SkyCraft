using System.Collections.Generic;
using DuckovCraft.Game;
using UnityEngine;

namespace DuckovCraft.World
{
    internal static class TerrainCollision
    {
        public static List<Triangle> Collect(TerrainCollider collider, Bounds bounds, WorldMapping world)
        {
            var output = new List<Triangle>();
            TerrainData data = collider.terrainData;
            if (data == null) return output;
            Vector3 low = Vector3.one * float.PositiveInfinity, high = Vector3.one * float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 local = collider.transform.InverseTransformPoint(world.FromMc(corner));
                low = Vector3.Min(low, local); high = Vector3.Max(high, local);
            }
            Vector3 size = data.size, scale = data.heightmapScale;
            if (high.x < 0 || low.x > size.x || high.z < 0 || low.z > size.z) return output;
            int cells = data.heightmapResolution - 1;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(low.x / scale.x), 0, cells - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(low.z / scale.z), 0, cells - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(high.x / scale.x), x0 + 1, cells);
            int z1 = Mathf.Clamp(Mathf.CeilToInt(high.z / scale.z), z0 + 1, cells);
            float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            bool[,] surface = data.GetHoles(x0, z0, x1 - x0, z1 - z0);
            var vertices = new Vector3[z1 - z0 + 1, x1 - x0 + 1];
            for (int z = 0; z <= z1 - z0; z++)
                for (int x = 0; x <= x1 - x0; x++)
                    vertices[z, x] = world.ToMc(collider.transform.TransformPoint(new Vector3(
                        (x0 + x) * scale.x, heights[z, x] * size.y, (z0 + z) * scale.z)));
            Vector3 direction = collider.transform.TransformDirection(Vector3.down);
            float distance = (size.y + 2) * Mathf.Abs(collider.transform.lossyScale.y);
            for (int z = 0; z < z1 - z0; z++)
                for (int x = 0; x < x1 - x0; x++)
                {
                    if (!surface[z, x]) continue;
                    Vector3 origin = collider.transform.TransformPoint(new Vector3(
                        (x0 + x + .5f) * scale.x, size.y + 1, (z0 + z + .5f) * scale.z));
                    if (!collider.Raycast(new Ray(origin, direction), out RaycastHit hit, distance)) continue;
                    output.AddRange(TerrainCell.Triangles(vertices[z, x], vertices[z, x + 1],
                        vertices[z + 1, x], vertices[z + 1, x + 1], world.ToMc(hit.point).y));
                }
            return output;
        }
    }
}
