using System;
using UnityEngine;

namespace DuckovCraft.World
{
    internal static class TerrainCell
    {
        public static Triangle[] Triangles(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float centerHeight)
        {
            // Match the native heightfield's diagonal rather than averaging a non-planar cell.
            bool ad = Math.Abs((a.y + d.y) * .5f - centerHeight) < Math.Abs((b.y + c.y) * .5f - centerHeight);
            return ad ? new[] { new Triangle(a, b, d), new Triangle(a, d, c) }
                : new[] { new Triangle(a, b, c), new Triangle(b, d, c) };
        }
    }
}
