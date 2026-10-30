using DuckovCraft.Link;
using PBZCraft.Trial;

internal static class InputCheck
{
    public static void Run()
    {
        var packet=new byte[8+IntPtr.Size*2+24];int header=8+IntPtr.Size*2;
        BitConverter.GetBytes((ushort)0x400).CopyTo(packet,header+4);BitConverter.GetBytes((short)-120).CopyTo(packet,header+6);
        BitConverter.GetBytes(17).CopyTo(packet,header+12);BitConverter.GetBytes(-9).CopyTo(packet,header+16);
        if(WindowInput.DecodeMouse(packet) is not {X:17,Y:-9,Wheel:-120})throw new InvalidOperationException("Raw mouse packet layout is incorrect.");
        packet[header]=1;if(WindowInput.DecodeMouse(packet) is not null)throw new InvalidOperationException("Absolute mouse packet was treated as relative movement.");
        var unfocused=new double[229];using(var windowInput=new WindowInput())windowInput.Read(unfocused);
        if(unfocused[218]!=0 || unfocused[219]!=0 || unfocused[220]!=0)throw new InvalidOperationException("Inactive input captured another window.");
        using var link=new SharedLink("Local\\PBZCraft.InputCheck-"+Guid.NewGuid().ToString("N"));
        var relay=new NativeInputRelay();
        var pose=new double[229];pose[218]=(1u<<0)|(1u<<23);pose[219]=10;pose[220]=-5;pose[224]=1;pose[225]=2560;pose[226]=1440;
        var mc=new McState{Flags=1,Sensitivity=0.5f};double yaw=30,pitch=10;
        relay.Step(link,pose,mc,2048,1152,ref yaw,ref pitch);
        long Record(int index)=>Protocol.Input+128+index*16;
        if(link.U64(Protocol.Input)!=2 || link.U32(Record(0))!=((26u<<16)|Protocol.Key) || link.I32(Record(0)+4)!=1
            || link.U32(Record(1))!=((1u<<16)|Protocol.MouseButton) || Math.Abs(yaw-31.5)>0.00001 || Math.Abs(pitch-10.75)>0.00001)
            throw new InvalidOperationException("Native keyboard/button/look signal did not reach the expected MC wire records.");
        pose[218]=0;pose[221]=1280;pose[222]=720;pose[224]=2;mc.Flags|=Protocol.McScreenOpen;
        relay.Step(link,pose,mc,2048,1152,ref yaw,ref pitch);
        if(link.U64(Protocol.Input)!=5 || link.I32(Record(2)+4)!=0 || link.I32(Record(3)+4)!=0
            || link.U32(Record(4))!=Protocol.Cursor || link.I32(Record(4)+4)!=1024 || link.I32(Record(4)+8)!=576)
            throw new InvalidOperationException("GUI cursor scaling or input release was incorrect.");
        pose[218]=1u<<26;pose[224]=3;
        relay.Step(link,pose,mc,2048,1152,ref yaw,ref pitch);
        if(!relay.ExitRequested)throw new InvalidOperationException("F9 did not request preview exit.");
        Console.WriteLine("PASS: Native signal maps to MC press/release, look, scaled GUI cursor, and F9 exit records.");
    }
}
