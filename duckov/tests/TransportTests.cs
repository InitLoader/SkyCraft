using System;
using System.IO.MemoryMappedFiles;
using DuckovCraft.Link;
using DuckovCraft.World;
using UnityEngine;

internal static class TransportTests
{
    private static int assertions;
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        assertions++;
    }
    private static void Main()
    {
        string name = "Local\\DuckovCraft_test_" + Guid.NewGuid().ToString("N");
        using (var host = new SharedLink(name))
        using (var mapping = MemoryMappedFile.OpenExisting(name))
        using (var client = mapping.CreateViewAccessor())
        {
            Check(client.ReadUInt32(0) == Protocol.Magic && client.ReadUInt32(4) == 11, "Header layout");
            ulong beat = host.U64(16);
            try { using (var duplicate = new SharedLink(name)) { } throw new Exception("Duplicate host accepted"); }
            catch (InvalidOperationException) { Check(host.U64(16) == beat, "Duplicate host must not clear owner's heartbeat"); }
            client.Write(12, 123u); client.Write(24, SharedLink.Now);
            Check(host.Connected, "Client heartbeat handshake");
            client.Write(24, SharedLink.Now - 4000); Check(!host.Connected, "Heartbeat timeout");
            host.Input(Protocol.Key, 26, 1);
            Check(client.ReadUInt64(Protocol.Input) == 1 && client.ReadUInt16(Protocol.Input + 128) == Protocol.Key && client.ReadUInt16(Protocol.Input + 130) == 26 && client.ReadInt32(Protocol.Input + 132) == 1, "Input event layout");
            client.Write(Protocol.Mc, 1u); Check(!host.ReadMc(out _), "Odd seqlock rejected");
            client.Write(Protocol.Mc + 4, Protocol.McInWorld); client.Write(Protocol.Mc + 8, 8192.5); client.Write(Protocol.Mc + 48, 7u); client.Write(Protocol.Mc, 2u);
            Check(host.ReadMc(out McState state) && state.X == 8192.5 && state.TeleportAck == 7, "Minecraft snapshot layout");
            client.Write(Protocol.Mc + 168, 1.25f); client.Write(Protocol.Mc + 172, 1.5f); client.Write(Protocol.Mc + 176, .02f); client.Write(Protocol.Mc + 180, .03f);
            client.Write(Protocol.Mc + 200, .2f); client.Write(Protocol.Mc + 204, -.1f); client.Write(Protocol.Mc + 208, .6f); client.Write(Protocol.Mc + 212, 1.8f);
            Check(host.ReadMc(out state) && state.PreviousWalk == 1.25f && state.CurrentBob == .03f && state.MoveX == .2f && state.MoveZ == -.1f && state.BodyWidth == .6f && state.BodyHeight == 1.8f, "Walk history and SULFUR movement extension layout");
            client.Write(Protocol.Mc + 216, 123456789L); client.Write(Protocol.Mc + 224, .375f);
            Check(host.ReadMc(out state) && state.FrameQpc == 123456789L && state.FramePartial == .375f, "SULFUR render-frame clock layout");
            client.Write(Protocol.Events + 128, 1u); client.Write(Protocol.Events + 132, 42u); client.Write(Protocol.Events + 136, 7.5f); client.Write(Protocol.Events, 1ul);
            McEvent received = default; host.DrainEvents(ev => received = ev);
            Check(received.Id == 42 && received.A == 7.5f && client.ReadUInt64(Protocol.Events + 64) == 1, "Combat event drain");
            long capacity = Protocol.CollisionBytes - 128;
            client.Write(Protocol.Collision, (ulong)(capacity - 8)); client.Write(Protocol.Collision + 64, (ulong)(capacity - 8));
            Check(host.Collision(3, new byte[] { 1, 2, 3, 4 }), "Wrapped collision write");
            Check(client.ReadUInt32(Protocol.Collision + 128 + capacity - 8) == 0 && client.ReadUInt32(Protocol.Collision + 128) == 3 && client.ReadUInt32(Protocol.Collision + 132) == 4, "Collision ring padding and header");
            Check(client.ReadUInt64(Protocol.Collision) == (ulong)(capacity + 16), "Collision ring aligned head");
            client.Write(Protocol.Render + 128, 3u); client.Write(Protocol.Render + 132, 0u); client.Write(Protocol.Render, 8ul);
            uint renderType = 0; host.DrainRender((type, data) => renderType = type);
            Check(renderType == 3 && client.ReadUInt64(Protocol.Render + 64) == 8, "Render message consumption");
            client.Write(Protocol.OverlayHeaders + 64, 1); client.Write(Protocol.OverlayHeaders + 68, 2); client.Write(Protocol.OverlayHeaders + 72, 1u);
            client.WriteArray(Protocol.Pixels + Protocol.OverlaySlotBytes, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0, 8); client.Write(Protocol.Overlay, 5u);
            Check(host.OverlayFrame(out int w, out int h, out bool bottomUp, out byte[] pixels) && w == 1 && h == 2 && bottomUp && pixels[7] == 8, "Triple buffer acquisition");
            Check(client.ReadUInt32(Protocol.Overlay) == 2, "Triple buffer ownership swap");
            Check(!host.OverlayFrame(out _, out _, out _, out _), "No duplicate overlay frame");
        }
        // A client may retain the mapping while the old host exits.
        using (var oldHost = new SharedLink(name))
        using (var mapping = MemoryMappedFile.OpenExisting(name))
        {
            oldHost.Put(8, int.MaxValue); oldHost.Dispose();
            using (var replacement = new SharedLink(name)) Check(replacement.U32(0) == Protocol.Magic, "Dead host mapping recovery");
        }
        using (var host = new SharedLink(name + "_input"))
        {
            for (int i = 0; i < 4096; i++) Check(host.Input(Protocol.Hurt, 3, 500), "Damage input accepted while ring has capacity");
            ulong head = host.U64(Protocol.Input);
            Check(!host.Input(Protocol.Hurt, 3, 500) && host.U64(Protocol.Input) == head, "Full input ring reports failure without overwriting pending damage");
        }
        Check(!CollisionCoverage.Ready(4, 128, 4, .6, 1.8, (x, y, z) => false), "Unsent collision cannot permit movement");
        Check(CollisionCoverage.Ready(4, 128, 4, .6, 1.8, (x, y, z) => x == 0 && (y == 15 || y == 16) && z == 0), "Interior feet require ground and body regions");
        Check(!CollisionCoverage.Ready(7, 128, 4, .6, 1.8, (x, y, z) => x == 0), "Approaching a boundary waits for the next region");
        Check(!CollisionCoverage.Ready(4, 128, 4, .6, 1.8, (x, y, z) => y == 16), "Ground below a vertical region boundary must arrive");
        Check(!CollisionCoverage.Ready(-.1, 128, -.1, .6, 1.8, (x, y, z) => x >= 0 && z >= 0), "Negative coordinates must not truncate into a positive region");
        Check(CollisionCoverage.Ready(-.1, 128, -.1, .6, 1.8, (x, y, z) => (x == -1 || x == 0) && (z == -1 || z == 0) && (y == 15 || y == 16)), "Negative boundary coverage includes both sides");
        Check(!CollisionCoverage.Ready(4, 4, 4, .6, 9, (x, y, z) => y == 0), "Tall bodies require the upper region");
        var a = new Vector3(0, 0, 0); var b = new Vector3(1, 0, 0);
        var c = new Vector3(0, 0, -1); var d = new Vector3(1, 2, -1);
        Triangle[] cell = TerrainCell.Triangles(a, b, c, d, 1);
        Check(cell[0].C == d && cell[1].B == d, "Non-planar terrain must use the native raised diagonal");
        cell = TerrainCell.Triangles(a, b, c, d, 0);
        Check(cell[0].C == c && cell[1].A == b, "Non-planar terrain must preserve the native low diagonal");
        Check(Vector3.Cross(cell[0].B - cell[0].A, cell[0].C - cell[0].A).y > 0
            && Vector3.Cross(cell[1].B - cell[1].A, cell[1].C - cell[1].A).y > 0, "Terrain winding remains upward after world Z reflection");
        Check(!CollisionGeometry.Overlaps(cell[0], new Vector3(.5f, 1, -.5f), .1f), "Low terrain must not invent a floating floor");
        Check(CollisionGeometry.Overlaps(cell[0], new Vector3(.25f, 0, -.25f), .1f), "Terrain contact must remain on the real surface");
        Console.WriteLine($"PASS: {assertions} transport and collision checks");
    }
}
