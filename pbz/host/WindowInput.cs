using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace PBZCraft.Trial;

internal sealed class WindowInput : IDisposable
{
    private static readonly int[] Keys=[0x57,0x41,0x53,0x44,0x20,0xA0,0xA2,0x45,0x51,0x52,0x54,0x1B,0x09,0x74,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x01,0x02,0x04,0x78];
    private nint owner;
    private int dx,dy,wheel;
    private long frame;
    private Thread? thread;
    private uint threadId;
    public string? Error {get;private set;}
    public volatile bool Enabled=false;
    public bool Focused {get;private set;}
    public uint Mask {get;private set;}
    public long MousePackets;

    public void Start()
    {
        thread=new Thread(() =>
        {
            try
            {
                using var window=new InputWindow(this);
                threadId=GetCurrentThreadId();
                Register(window.Handle);
                Application.Run();
            }
            catch(Exception error){Error=error.ToString();Console.WriteLine("[PBZCraftTrial] Input receiver failed: "+error.Message);}
        }){IsBackground=true,Name="PBZCraft raw input"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public void Dispose(){if(threadId!=0)PostThreadMessage(threadId,0x12,0,0);thread?.Join(2000);}
    private sealed class InputWindow : NativeWindow,IDisposable
    {
        private readonly WindowInput input;
        public InputWindow(WindowInput input){this.input=input;CreateHandle(new CreateParams{Caption="PBZCraft input",Parent=(nint)(-3)});}
        protected override void WndProc(ref Message message){if(message.Msg==0xff)input.Receive(message.LParam);base.WndProc(ref message);}
        public void Dispose()=>DestroyHandle();
    }

    public void SetOwner(nint window)=>Interlocked.Exchange(ref owner,window);
    public void Register(nint window)
    {
        var device=new RawDevice{UsagePage=1,Usage=2,Flags=0x100,Window=window};
        if(!RegisterRawInputDevices([device],1,(uint)Marshal.SizeOf<RawDevice>()))throw new System.ComponentModel.Win32Exception();
    }
    private bool GameFocused()
    {
        nint window=Interlocked.CompareExchange(ref owner,0,0),foreground=GetForegroundWindow();
        if(!Enabled || window==0 || foreground==0)return false;
        GetWindowThreadProcessId(window,out uint game);GetWindowThreadProcessId(foreground,out uint active);
        return game!=0 && game==active;
    }
    internal static (int X,int Y,int Wheel)? DecodeMouse(ReadOnlySpan<byte> data)
    {
        int header=8+IntPtr.Size*2;
        if(data.Length<header+24 || BinaryPrimitives.ReadUInt32LittleEndian(data)!=0)return null;
        data=data[header..];
        if((BinaryPrimitives.ReadUInt16LittleEndian(data)&1)!=0)return null;
        int scroll=(BinaryPrimitives.ReadUInt16LittleEndian(data[4..])&0x400)!=0?BinaryPrimitives.ReadInt16LittleEndian(data[6..]):0;
        return (BinaryPrimitives.ReadInt32LittleEndian(data[12..]),BinaryPrimitives.ReadInt32LittleEndian(data[16..]),scroll);
    }
    public unsafe void Receive(nint handle)
    {
        if(!GameFocused())return;
        uint size=0,header=(uint)(8+IntPtr.Size*2);
        if(GetRawInputData(handle,0x10000003,0,ref size,header)==uint.MaxValue || size>256 || size<header+24)return;
        Span<byte> data=stackalloc byte[(int)size];
        fixed(byte* pointer=data)
            if(GetRawInputData(handle,0x10000003,(nint)pointer,ref size,header)==uint.MaxValue)return;
        if(DecodeMouse(data) is {} value)
        {
            Interlocked.Add(ref dx,value.X);Interlocked.Add(ref dy,value.Y);Interlocked.Add(ref wheel,value.Wheel);Interlocked.Increment(ref MousePackets);
        }
    }
    public void Read(double[] pose)
    {
        Focused=GameFocused();Mask=0;
        int x=Interlocked.Exchange(ref dx,0),y=Interlocked.Exchange(ref dy,0),scroll=Interlocked.Exchange(ref wheel,0);
        if(Focused)for(int index=0;index<Keys.Length;index++)if((GetAsyncKeyState(Keys[index])&0x8000)!=0)Mask|=1u<<index;
        var cursor=new Point();var rect=new Rect();nint window=Interlocked.CompareExchange(ref owner,0,0);
        bool positioned=Focused && GetCursorPos(out cursor) && ScreenToClient(window,ref cursor);
        GetClientRect(window,out rect);
        pose[218]=Mask;pose[219]=Focused?x:0;pose[220]=Focused?-y:0;
        pose[221]=positioned?cursor.X:0;pose[222]=positioned?cursor.Y:0;pose[223]=Focused?scroll:0;
        pose[224]=++frame;pose[225]=Math.Max(1,rect.Right);pose[226]=Math.Max(1,rect.Bottom);
    }
    [StructLayout(LayoutKind.Sequential)]private struct RawDevice{public ushort UsagePage,Usage;public uint Flags;public nint Window;}
    [StructLayout(LayoutKind.Sequential)]private struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll",SetLastError=true)]private static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll")]private static extern uint GetRawInputData(nint handle,uint command,nint data,ref uint size,uint headerSize);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]private static extern bool ScreenToClient(nint window,ref Point point);
    [DllImport("user32.dll")]private static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]private static extern bool PostThreadMessage(uint thread,uint message,nint wparam,nint lparam);
}
