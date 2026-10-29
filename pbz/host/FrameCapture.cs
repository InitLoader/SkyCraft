using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace PBZCraft.Trial;

internal static class FrameCapture
{
    public static bool NativeProof(string path,string overlayPath)
    {
        using var process=Process.GetProcessesByName("PBZ").FirstOrDefault();
        if(process is null || process.MainWindowHandle==0 || process.MainModule?.FileName!="D:\\soft\\steam\\steamapps\\common\\PBZ\\platform\\steam\\PBZ.exe") return false;
        if(!GetClientRect(process.MainWindowHandle,out var rect) || rect.Right<1 || rect.Bottom<1) return false;
        using var image=new Bitmap(rect.Right,rect.Bottom,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(image))
        {
            nint dc=graphics.GetHdc();bool captured;
            try{captured=PrintWindow(process.MainWindowHandle,dc,3);}finally{graphics.ReleaseHdc(dc);}
            if(!captured)return false;
        }
        int visible=0;
        for(int y=0;y<image.Height;y+=Math.Max(1,image.Height/18)) for(int x=0;x<image.Width;x+=Math.Max(1,image.Width/32))
        {var color=image.GetPixel(x,y);if(color.R+color.G+color.B>24)visible++;}
        if(visible<40)return false;
        image.Save(path,ImageFormat.Png);
        using var overlay=Image.FromFile(overlayPath);
        if(overlay.Width!=image.Width || overlay.Height!=image.Height) return false;
        using(var graphics=Graphics.FromImage(image))graphics.DrawImageUnscaled(overlay,0,0);
        image.Save(Path.Combine(Path.GetDirectoryName(path)!,Path.GetFileNameWithoutExtension(path)+"-composite.png"),ImageFormat.Png);
        return true;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Rect{public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]private static extern bool GetClientRect(nint window,out Rect rect);
    [DllImport("user32.dll")]private static extern bool PrintWindow(nint window,nint dc,uint flags);
    public static int Save(string path, int width, int height, bool bottomUp, byte[] rgba)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var locked = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
        int visible = 0;
        try
        {
            var row = new byte[width * 4];
            for (int y = 0; y < height; y++)
            {
                int from = (bottomUp ? height - 1 - y : y) * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int p = from + x * 4, q = x * 4;
                    row[q] = rgba[p + 2]; row[q + 1] = rgba[p + 1]; row[q + 2] = rgba[p]; row[q + 3] = rgba[p + 3];
                    if (rgba[p + 3] != 0) visible++;
                }
                Marshal.Copy(row, 0, locked.Scan0 + y * locked.Stride, row.Length);
            }
        }
        finally { bitmap.UnlockBits(locked); }
        bitmap.Save(path, ImageFormat.Png);
        return visible;
    }
}
