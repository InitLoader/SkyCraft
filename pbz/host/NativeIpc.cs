using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace PBZCraft.Trial;

internal sealed class NativeIpc : IDisposable
{
    private MemoryMappedFile? file;
    private MemoryMappedViewAccessor? view;
    private double[]? previous;
    private ulong previousStamp;

    public bool Read(out double[] values)
    {
        values = [];
        if (view is null)
        {
            try { file = MemoryMappedFile.OpenExisting("Local\\PBZCraft.Native_v1"); view = file.CreateViewAccessor(0, 4096); }
            catch (FileNotFoundException) { return false; }
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int seq = view.ReadInt32(0);
            int length = view.ReadInt32(4);
            if (seq == 0 || (seq & 1) != 0 || length < 80 || length > 2032 || (length & 7) != 0) continue;
            ulong stamp=view.ReadUInt64(8);
            if(GetTickCount64()-stamp>3000) return false;
            var data = new double[length / 8];
            view.ReadArray(16, data, 0, data.Length);
            Thread.MemoryBarrier();
            if (seq != view.ReadInt32(0)) continue;
            if (data.Any(value => !double.IsFinite(value))) return false;
            values = data;
            previous=data;previousStamp=stamp;
            return true;
        }
        if(previous is not null && GetTickCount64()-previousStamp<=3000) {values=previous;return true;}
        return false;
    }

    public void Write(params double[] values)
    {
        if (view is null || values.Length > 254) return;
        int seq = view.ReadInt32(2048);
        view.Write(2048, seq + 1);
        view.Write(2052, values.Length * 8);
        view.Write(2056, GetTickCount64());
        view.WriteArray(2064, values, 0, values.Length);
        Thread.MemoryBarrier();
        view.Write(2048, seq + 2);
    }

    public void Dispose() { view?.Dispose(); file?.Dispose(); }
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
}
