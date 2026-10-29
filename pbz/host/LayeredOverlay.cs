using System.Diagnostics;
using System.Buffers;
using System.Runtime.InteropServices;

namespace PBZCraft.Trial;

internal sealed class LayeredOverlay : IDisposable
{
    private record Frame(int Width,int Height,bool BottomUp,byte[] Pixels);
    private Frame? pending;
    private readonly Thread thread;
    private volatile bool stopping;
    public int Presented;
    public string? Error;
    public int ViewportWidth=1280,ViewportHeight=720;

    public LayeredOverlay()
    {
        thread=new Thread(Run){IsBackground=true,Name="PBZCraft overlay"};
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    public void Publish(int width,int height,bool bottomUp,byte[] pixels)
    {
        var copy=ArrayPool<byte>.Shared.Rent(pixels.Length);
        Buffer.BlockCopy(pixels,0,copy,0,pixels.Length);
        var dropped=Interlocked.Exchange(ref pending,new Frame(width,height,bottomUp,copy));
        if(dropped is not null) ArrayPool<byte>.Shared.Return(dropped.Pixels);
    }
    public void Dispose()
    {
        stopping=true;thread.Join(2000);
        var dropped=Interlocked.Exchange(ref pending,null);
        if(dropped is not null) ArrayPool<byte>.Shared.Return(dropped.Pixels);
    }

    private void Run()
    {
        using var form=new OverlayForm();
        nint bitmap=0,bits=0,oldBitmap=0;
        nint screen=GetDC(0),dc=CreateCompatibleDC(screen);
        int width=0,height=0;
        nint owner=0;
        try
        {
            _=form.Handle;
            while(!stopping)
            {
                Application.DoEvents();
                if(owner==0 || !IsWindow(owner))
                {
                    owner=Process.GetProcessesByName("PBZ").Select(p=>p.MainWindowHandle).FirstOrDefault(h=>h!=0);
                    if(owner!=0) SetWindowLongPtr(form.Handle,-8,owner);
                }
                if(owner!=0 && GetClientRect(owner,out var viewport) && viewport.Right>0 && viewport.Bottom>0)
                { ViewportWidth=viewport.Right;ViewportHeight=viewport.Bottom; }
                var frame=Interlocked.Exchange(ref pending,null);
                if(frame is null) { Thread.Sleep(10);continue; }
                try
                {
                if(owner==0 || !IsWindowVisible(owner) || IsIconic(owner)) { Thread.Sleep(10);continue; }
                if(!GetClientRect(owner,out var client) || client.Right!=frame.Width || client.Bottom!=frame.Height) { Thread.Sleep(10);continue; }
                if(width!=frame.Width || height!=frame.Height)
                {
                    if(bitmap!=0) {SelectObject(dc,oldBitmap);DeleteObject(bitmap);}
                    width=frame.Width;height=frame.Height;
                    var info=new BitmapInfo{Size=40,Width=width,Height=-height,Planes=1,BitCount=32};
                    bitmap=CreateDIBSection(dc,ref info,0,out bits,0,0);
                    if(bitmap==0) throw new System.ComponentModel.Win32Exception();
                    oldBitmap=SelectObject(dc,bitmap);
                }
                unsafe
                {
                    var output=(byte*)bits;
                    fixed(byte* input=frame.Pixels)
                    {
                        for(int y=0;y<height;y++)
                        {
                            int src=(frame.BottomUp?height-1-y:y)*width*4,dst=y*width*4;
                            for(int x=0;x<width;x++,src+=4,dst+=4)
                            { output[dst]=input[src+2];output[dst+1]=input[src+1];output[dst+2]=input[src];output[dst+3]=input[src+3]; }
                        }
                    }
                }
                var position=new PointNative();ClientToScreen(owner,ref position);
                var size=new SizeNative{Width=width,Height=height};var source=new PointNative();
                var blend=new Blend{Operation=0,ConstantAlpha=255,AlphaFormat=1};
                if(!UpdateLayeredWindow(form.Handle,screen,ref position,ref size,dc,ref source,0,ref blend,2)) throw new System.ComponentModel.Win32Exception();
                if(!form.Visible) ShowWindow(form.Handle,4);
                Presented++;
                Thread.Sleep(1);
                }
                finally { ArrayPool<byte>.Shared.Return(frame.Pixels); }
            }
        }
        catch(Exception ex) { Error=ex.ToString();Console.WriteLine("[PBZCraftTrial] Overlay failed: "+ex.Message); }
        finally
        {
            if(bitmap!=0) {SelectObject(dc,oldBitmap);DeleteObject(bitmap);}
            DeleteDC(dc);ReleaseDC(0,screen);
        }
    }

    private sealed class OverlayForm : Form
    {
        public OverlayForm() {FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;Text="PBZCraft overlay";}
        protected override bool ShowWithoutActivation=>true;
        protected override CreateParams CreateParams { get {var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x8000000|0x80;return p;} }
    }
    [StructLayout(LayoutKind.Sequential)]private struct PointNative {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]private struct SizeNative {public int Width,Height;}
    [StructLayout(LayoutKind.Sequential)]private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,Pack=1)]private struct Blend {public byte Operation,Flags,ConstantAlpha,AlphaFormat;}
    [StructLayout(LayoutKind.Sequential)]private struct BitmapInfo {public uint Size;public int Width,Height;public ushort Planes,BitCount;public uint Compression,ImageBytes;public int XPixels,YPixels;public uint Used,Important;}
    [DllImport("user32.dll")]private static extern nint GetDC(nint window);
    [DllImport("user32.dll")]private static extern int ReleaseDC(nint window,nint dc);
    [DllImport("user32.dll")]private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]private static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")]private static extern bool ClientToScreen(nint window,ref PointNative point);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]private static extern nint SetWindowLongPtr(nint window,int index,nint value);
    [DllImport("user32.dll")]private static extern bool ShowWindow(nint window,int command);
    [DllImport("gdi32.dll")]private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")]private static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
    [DllImport("gdi32.dll")]private static extern nint SelectObject(nint dc,nint value);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool UpdateLayeredWindow(nint window,nint dest,ref PointNative position,ref SizeNative size,nint source,ref PointNative sourcePosition,uint key,ref Blend blend,uint flags);
}
