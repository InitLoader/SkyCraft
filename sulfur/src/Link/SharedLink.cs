using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SulfurCraft.Link
{
    internal sealed unsafe class SharedLink : IDisposable
    {
        private IntPtr mapping;
        private byte* memory;
        private int front = 2;
        private byte[] overlayPixels;
        private readonly byte[] clear = new byte[0x100];
        public static ulong Now => GetTickCount64();
        public bool Connected => memory != null && U32(12) != 0 && U64(24) != 0 && Now - U64(24) < 3000;

        public SharedLink(string name = Protocol.MappingName)
        {
            mapping = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, 4, 0, (uint)Protocol.MappingBytes, name);
            if (mapping == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            bool existing = Marshal.GetLastWin32Error() == 183;
            memory = (byte*)MapViewOfFile(mapping, 0xF001F, 0, 0, new UIntPtr((ulong)Protocol.MappingBytes));
            if (memory == null) { CloseHandle(mapping); throw new System.ComponentModel.Win32Exception(); }
            uint previousClient = existing ? U32(12) : 0;
            if (existing)
            {
                bool alive = false;
                try { alive = !Process.GetProcessById((int)U32(8)).HasExited; } catch (ArgumentException) { }
                if (alive)
                {
                    UnmapViewOfFile(new IntPtr(memory)); memory = null;
                    CloseHandle(mapping); mapping = IntPtr.Zero;
                    throw new InvalidOperationException("Another SulfurCraft host is already running.");
                }
            }
            foreach (long offset in new[] { 0L, Protocol.Sky, Protocol.Mc, Protocol.Overlay, Protocol.Input, Protocol.Actors, Protocol.Events, Protocol.Entities, Protocol.Collision, Protocol.Render })
                Marshal.Copy(clear, 0, new IntPtr(memory + offset), clear.Length);
            Put(0, Protocol.Magic);
            Put(4, Protocol.Version);
            Put(8, (uint)Process.GetCurrentProcess().Id);
            if (previousClient != 0) Put(12, previousClient);
            Heartbeat();
        }

        public void Heartbeat() => Put64(16, Now);
        public uint U32(long offset) => unchecked((uint)Volatile.Read(ref *(int*)(memory + offset)));
        public ulong U64(long offset) => unchecked((ulong)Volatile.Read(ref *(long*)(memory + offset)));
        public int I32(long offset) => unchecked((int)U32(offset));
        public float F32(long offset) => *(float*)(memory + offset);
        public double F64(long offset) => *(double*)(memory + offset);
        public void Put(long offset, uint value) => Volatile.Write(ref *(int*)(memory + offset), unchecked((int)value));
        public void Put64(long offset, ulong value) => Volatile.Write(ref *(long*)(memory + offset), unchecked((long)value));
        public void PutFloat(long offset, float value) => *(float*)(memory + offset) = value;
        public void PutDouble(long offset, double value) => *(double*)(memory + offset) = value;
        public void Copy(long offset, byte[] destination, int length) => Marshal.Copy(new IntPtr(memory + offset), destination, 0, length);
        public void Write(long offset, byte[] source) => Marshal.Copy(source, 0, new IntPtr(memory + offset), source.Length);
        public uint BeginWrite(long offset) { uint sequence = U32(offset); Put(offset, sequence + 1); Thread.MemoryBarrier(); return sequence + 2; }
        public void EndWrite(long offset, uint sequence) { Thread.MemoryBarrier(); Put(offset, sequence); }

        public bool ReadMc(out McState state)
        {
            state = default;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long b = Protocol.Mc;
                uint sequence = U32(b);
                if ((sequence & 1) != 0) continue;
                state.Flags = U32(b + 4);
                state.X = F64(b + 8); state.Y = F64(b + 16); state.Z = F64(b + 24);
                state.Yaw = F32(b + 32); state.Pitch = F32(b + 36);
                state.EyeHeight = F32(b + 40); state.Sensitivity = F32(b + 44);
                state.TeleportAck = U32(b + 48); state.Fov = F32(b + 64);
                state.BobPhase = F32(b + 68); state.BobAmount = F32(b + 72);
                state.EyeX = F64(b + 80); state.EyeY = F64(b + 88); state.EyeZ = F64(b + 96);
                state.TickQpc = unchecked((long)U64(b + 104));
                state.PrevX = F64(b + 112); state.PrevY = F64(b + 120); state.PrevZ = F64(b + 128);
                state.CurX = F64(b + 136); state.CurY = F64(b + 144); state.CurZ = F64(b + 152);
                state.PreviousEye = F32(b + 160); state.CurrentEye = F32(b + 164); state.TickMs = F32(b + 184);
                state.PreviousWalk = F32(b + 168); state.CurrentWalk = F32(b + 172);
                state.PreviousBob = F32(b + 176); state.CurrentBob = F32(b + 180);
                state.CameraMode = I32(b + 192); state.CameraDistance = F32(b + 196);
                state.MoveX = F32(b + 200); state.MoveZ = F32(b + 204); state.BodyWidth = F32(b + 208); state.BodyHeight = F32(b + 212);
                Thread.MemoryBarrier();
                if (sequence == U32(b)) return sequence != 0;
            }
            return false;
        }

        public void Input(ushort type, ushort code = 0, int a = 0, int b = 0, int c = 0)
        {
            long ring = Protocol.Input;
            ulong head = U64(ring), tail = U64(ring + 64);
            if (head - tail >= 4096) return;
            long at = ring + 128 + (long)(head & 4095) * 16;
            *(ushort*)(memory + at) = type; *(ushort*)(memory + at + 2) = code;
            *(int*)(memory + at + 4) = a; *(int*)(memory + at + 8) = b; *(int*)(memory + at + 12) = c;
            Put64(ring, head + 1);
        }

        public void DrainEvents(Action<McEvent> receive)
        {
            ulong head = U64(Protocol.Events), tail = U64(Protocol.Events + 64);
            if (head < tail || head - tail > 512) throw new InvalidOperationException("Invalid Minecraft event ring.");
            while (tail < head)
            {
                long at = Protocol.Events + 128 + (long)(tail & 511) * 32;
                var ev = new McEvent { Type = U32(at), Id = U32(at + 4), A = F32(at + 8), B = F32(at + 12), C = F32(at + 16), D = F32(at + 20), Flags = U32(at + 24), Weapon = U32(at + 28) };
                receive(ev);
                Put64(Protocol.Events + 64, ++tail);
            }
        }

        public void DrainRender(Action<uint, byte[]> receive, int maxMessages = 48)
        {
            long capacity = Protocol.RenderBytes - 128;
            ulong head = U64(Protocol.Render), tail = U64(Protocol.Render + 64);
            if (head < tail || head - tail > (ulong)capacity) throw new InvalidOperationException("Invalid Minecraft render ring.");
            for (int i = 0; tail < head && i < maxMessages; i++)
            {
                long position = (long)(tail % (ulong)capacity), at = Protocol.Render + 128 + position;
                uint type = U32(at), length = U32(at + 4);
                if (type == 0) { tail += (ulong)(capacity - position); Put64(Protocol.Render + 64, tail); continue; }
                long size = (8L + length + 7) & ~7L;
                if (length > capacity - 8 || position + size > capacity || tail + (ulong)size > head)
                    throw new InvalidOperationException("Invalid Minecraft render message.");
                byte[] data = new byte[length];
                Copy(at + 8, data, data.Length);
                receive(type, data);
                tail += (ulong)size;
                Put64(Protocol.Render + 64, tail);
            }
        }

        public bool Collision(uint type, byte[] data)
        {
            long capacity = Protocol.CollisionBytes - 128;
            ulong head = U64(Protocol.Collision), tail = U64(Protocol.Collision + 64);
            long size = (8L + data.Length + 7) & ~7L, position = (long)(head % (ulong)capacity);
            long padding = position + size > capacity ? capacity - position : 0;
            if (head - tail + (ulong)(padding + size) > (ulong)capacity) return false;
            if (padding != 0) { Put(Protocol.Collision + 128 + position, 0); Put(Protocol.Collision + 132 + position, 0); head += (ulong)padding; position = 0; }
            long at = Protocol.Collision + 128 + position;
            Put(at, type); Put(at + 4, (uint)data.Length); Write(at + 8, data);
            Put64(Protocol.Collision, head + (ulong)size);
            return true;
        }

        public bool OverlayFrame(out int width, out int height, out bool bottomUp, out byte[] data)
        {
            width = height = 0; bottomUp = false; data = null;
            if ((U32(Protocol.Overlay) & 4) == 0) return false;
            front = Interlocked.Exchange(ref *(int*)(memory + Protocol.Overlay), front) & 3;
            if (front > 2) throw new InvalidOperationException("Invalid overlay slot.");
            long at = Protocol.OverlayHeaders + front * 64;
            width = I32(at); height = I32(at + 4); bottomUp = (U32(at + 8) & 1) != 0;
            if (width <= 0 || height <= 0 || width > 3840 || height > 2160) return false;
            int size = checked(width * height * 4);
            if (overlayPixels == null || overlayPixels.Length != size) overlayPixels = new byte[size];
            data = overlayPixels;
            Copy(Protocol.Pixels + front * Protocol.OverlaySlotBytes, data, data.Length);
            return true;
        }

        public void Dispose()
        {
            if (memory != null) { Put64(16, 0); UnmapViewOfFile(new IntPtr(memory)); memory = null; }
            if (mapping != IntPtr.Zero) { CloseHandle(mapping); mapping = IntPtr.Zero; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileMappingW")]
        private static extern IntPtr CreateFileMapping(IntPtr file, IntPtr security, uint protect, uint high, uint low, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint high, uint low, UIntPtr bytes);
        [DllImport("kernel32.dll")] private static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
    }
}
