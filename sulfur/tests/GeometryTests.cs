using System;
using SulfurCraft.World;
using UnityEngine;

internal static class GeometryTests
{
    private static void Main()
    {
        var floor = new Triangle(new Vector3(-2, 0, -2), new Vector3(2, 0, -2), new Vector3(0, 0, 2));
        Check(CollisionGeometry.Overlaps(floor, Vector3.zero, .0625f), "Floor intersects voxel");
        Check(!CollisionGeometry.Overlaps(floor, new Vector3(0, .2f, 0), .0625f), "Floor outside voxel height");
        Check(!CollisionGeometry.Overlaps(floor, new Vector3(3, 0, 0), .0625f), "Separated X axis");
        var corner = new Triangle(new Vector3(0, .4f, .4f), new Vector3(.4f, 0, .4f), new Vector3(.4f, .4f, 0));
        Check(!CollisionGeometry.Overlaps(corner, Vector3.zero, .1f), "Separating plane despite overlapping AABB");
        Check(CollisionGeometry.Overlaps(floor, new Vector3(0, .0625f, 0), .0625f), "Touching boundary included");
        Check(CollisionGeometry.Overlaps(new Triangle(floor.C, floor.B, floor.A), Vector3.zero, .0625f), "Voxel coverage independent of winding");
        // The rasterizer performs many SAT checks; they must not allocate arrays per voxel.
        for (int i = 0; i < 10000; i++) CollisionGeometry.Overlaps(floor, Vector3.zero, .0625f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100000; i++) CollisionGeometry.Overlaps(floor, Vector3.zero, .0625f);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "100,000 SAT tests allocate zero bytes");
        Console.WriteLine("PASS: 7 collision geometry checks, including zero allocation rasterization");
    }
    private static void Check(bool value, string description) { if (!value) throw new Exception(description); }
}
