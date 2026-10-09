using System;
using System.IO.MemoryMappedFiles;
using DuckovCraft.Link;

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
        Console.WriteLine($"PASS: {assertions} shared-memory transport checks");
    }
}
