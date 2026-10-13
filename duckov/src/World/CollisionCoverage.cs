using System;

namespace DuckovCraft.World
{
    internal static class CollisionCoverage
    {
        public static bool Ready(double x, double y, double z, double width, double height, Func<int, int, int, bool> consumed)
        {
            double radius = width * .5 + 1;
            int minX = (int)Math.Floor((x - radius) / 8), maxX = (int)Math.Floor((x + radius) / 8);
            int minY = (int)Math.Floor((y - 1) / 8), maxY = (int)Math.Floor((y + height + 1) / 8);
            int minZ = (int)Math.Floor((z - radius) / 8), maxZ = (int)Math.Floor((z + radius) / 8);
            for (int rx = minX; rx <= maxX; rx++)
                for (int ry = minY; ry <= maxY; ry++)
                    for (int rz = minZ; rz <= maxZ; rz++)
                        if (!consumed(rx, ry, rz)) return false;
            return true;
        }
    }
}
