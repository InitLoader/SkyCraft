namespace SulfurCraft.Link
{
    internal static class Protocol
    {
        public const uint Magic = 0x43594B53;
        public const uint Version = 11;
        public const string MappingName = "Local\\SulfurCraft_v1";
        public const long Sky = 0x100, Mc = 0x200, Overlay = 0x300, OverlayHeaders = 0x340;
        public const long Input = 0x1000, Actors = 0x12000, Events = 0x17000, Entities = 0x1C000;
        public const long Collision = 0x20000, CollisionBytes = 32L << 20;
        public const long Pixels = Collision + CollisionBytes;
        public const long OverlaySlotBytes = 3840L * 2160 * 4;
        public const long Render = Pixels + OverlaySlotBytes * 3, RenderBytes = 64L << 20;
        public const long MappingBytes = Render + RenderBytes;
        public const uint InGame = 1, MenuOpen = 2, Loading = 4;
        public const uint McInWorld = 1, McScreenOpen = 2, McDead = 32;
        public const ushort Key = 1, MouseButton = 2, Scroll = 3, Cursor = 4, Text = 5, ReleaseAll = 6, Hurt = 7, OpenMenu = 8;
        public const uint CollisionLadders = 4;
    }

    internal struct McState
    {
        public uint Flags, TeleportAck;
        public double X, Y, Z, EyeX, EyeY, EyeZ;
        public float Yaw, Pitch, EyeHeight, Sensitivity, Fov, BobPhase, BobAmount;
        public long TickQpc;
        public double PrevX, PrevY, PrevZ, CurX, CurY, CurZ;
        public float TickMs, PreviousEye, CurrentEye, PreviousWalk, CurrentWalk, PreviousBob, CurrentBob;
        public int CameraMode;
        public float CameraDistance;
        public float MoveX, MoveZ, BodyWidth, BodyHeight;
    }

    internal struct McEvent
    {
        public uint Type, Id, Flags, Weapon;
        public float A, B, C, D;
    }
}
