using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PBZCraft.Trial;

internal sealed class RenderCapture
{
    private readonly string root;
    private byte[]? atlas;
    private int width,height;
    private bool dirty;
    private long nextFlush;
    private long nextAtlasFlush;
    private int atlasWrites;
    private System.Collections.BitArray? initialCoverage;
    private int initialMissing;
    private readonly HashSet<uint> textures=[];
    private readonly HashSet<string> sections=[];
    private byte[]? avatar;
    private byte[]? entities;
    public System.Numerics.Vector3? TestBlockTarget {get;private set;}
    private readonly Dictionary<string,byte[]> pending=[];
    public int SharingRetries {get;private set;}
    public int PendingWrites=>pending.Count;
    public int SectionUpdates,AvatarUpdates;
    public RenderCapture(string root) {this.root=root;Directory.CreateDirectory(root);}
    public void Receive(uint type,byte[] data)
    {
        int I(int offset)=>BitConverter.ToInt32(data,offset);
        switch(type)
        {
            case 1:
            case 12:
                if(data.Length<8) throw new InvalidDataException("Truncated atlas");
                width=I(0);height=I(4);
                if(width<1 || height<1 || width>8192 || height>8192) throw new InvalidDataException("Invalid atlas dimensions");
                atlas=new byte[checked(width*height*4)];
                initialMissing=type==12?checked(width*height):0;
                initialCoverage=type==12?new System.Collections.BitArray(initialMissing):null;
                Console.WriteLine($"[PBZCraftTrial] Atlas source type={type} size={width}x{height}; waitingPixels={initialMissing}");
                if(type==1) {if(data.Length!=atlas.Length+8)throw new InvalidDataException("Invalid atlas pixels");Buffer.BlockCopy(data,8,atlas,0,atlas.Length);}
                dirty=true;break;
            case 7:
                if(atlas is null || data.Length<16) return;
                int x=I(0),y=I(4),w=I(8),h=I(12);
                if(x<0 || y<0 || w<1 || h<1 || x+w>width || y+h>height || data.Length!=16L+w*(long)h*4) throw new InvalidDataException("Invalid atlas patch");
                for(int row=0;row<h;row++) Buffer.BlockCopy(data,16+row*w*4,atlas,((y+row)*width+x)*4,w*4);
                if(initialCoverage is not null)
                {
                    for(int row=0;row<h;row++) for(int col=0;col<w;col++) {int pixel=(y+row)*width+x+col;if(!initialCoverage[pixel]){initialCoverage[pixel]=true;initialMissing--;}}
                    if(initialMissing==0){initialCoverage=null;Console.WriteLine("[PBZCraftTrial] Initial atlas coverage complete.");}
                }
                dirty=true;break;
            case 4:
                if(data.Length<16)throw new InvalidDataException("Truncated entity texture");
                uint id=BitConverter.ToUInt32(data,0);
                if(!textures.Contains(id)) {SavePng($"texture-{id}.png",I(4),I(8),data,16);textures.Add(id);}
                break;
            case 2:
                if(data.Length<16 || I(12)<0 || data.Length!=16L+I(12)*32L)throw new InvalidDataException("Invalid section");
                string name=$"section-{I(0)}-{I(4)}-{I(8)}.bin";
                if(I(12)==36 && TestBlockTarget is null)
                {
                    var low=new System.Numerics.Vector3(float.MaxValue);
                    var high=new System.Numerics.Vector3(float.MinValue);
                    for(int n=0;n<36;n++)
                    {
                        int p=16+n*32;var point=new System.Numerics.Vector3(BitConverter.ToSingle(data,p)+I(0)*16,BitConverter.ToSingle(data,p+4)+I(4)*16,BitConverter.ToSingle(data,p+8)+I(8)*16);
                        low=System.Numerics.Vector3.Min(low,point);high=System.Numerics.Vector3.Max(high,point);
                    }
                    TestBlockTarget=(low+high)*0.5f;
                }
                Atomic(name,data);
                if(I(12)>0)sections.Add(name);else sections.Remove(name);
                Atomic("sections.txt",System.Text.Encoding.ASCII.GetBytes(string.Join("\n",sections.Order())));
                SectionUpdates++;break;
            case 5: avatar=data;break;
            case 3:
                sections.Clear();Atomic("sections.txt",[]);break;
        }
    }
    public void Flush(long now)
    {
        if(now<nextFlush) return;nextFlush=now+16;
        foreach(var item in pending.ToArray())Atomic(item.Key,item.Value);
        if(dirty && atlas is not null && initialMissing==0 && now>=nextAtlasFlush && atlasWrites<2) {SavePng("atlas.png",width,height,atlas,0);dirty=false;nextAtlasFlush=now+5000;atlasWrites++;}
        if(avatar is not null) {Atomic("avatar.bin",avatar);avatar=null;AvatarUpdates++;}
        if(entities is not null){Atomic("entities.bin",entities);entities=null;}
    }
    public void Entities(byte[]? data){if(data is not null)entities=data;}
    private void Atomic(string name,byte[] bytes)
    {
        string destination=Path.Combine(root,name),temporary=destination+".tmp";
        try { File.WriteAllBytes(temporary,bytes);File.Move(temporary,destination,true);pending.Remove(name); }
        catch(IOException error) when((error.HResult&0xffff) is 5 or 32 or 33) {pending[name]=bytes;SharingRetries++;}
        catch(UnauthorizedAccessException) {pending[name]=bytes;SharingRetries++;}
    }
    private void SavePng(string name,int w,int h,byte[] rgba,int offset)
    {
        if(w<1 || h<1 || w>8192 || h>8192 || rgba.Length-offset!=(long)w*h*4)throw new InvalidDataException("Invalid texture");
        using var bitmap=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        var data=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,bitmap.PixelFormat);
        try
        {
            byte[] row=new byte[w*4];
            for(int y=0;y<h;y++)
            {
                for(int x=0;x<w;x++)
                {
                    int from=offset+(y*w+x)*4,to=x*4;
                    row[to]=rgba[from+2];row[to+1]=rgba[from+1];row[to+2]=rgba[from];row[to+3]=rgba[from+3];
                    // The paired exporter appends ten 16px destroy textures. MC multiplies these;
                    // the cooked masked sprite material needs dark ink over transparent pixels.
                    if(name=="atlas.png" && x<10*16 && y>=h-16)
                    {
                        int light=(rgba[from]+rgba[from+1]+rgba[from+2])/3;
                        row[to]=0;row[to+1]=0;row[to+2]=0;row[to+3]=(byte)(rgba[from+3]*(255-light)/255);
                    }
                }
                Marshal.Copy(row,0,data.Scan0+y*data.Stride,row.Length);
            }
        }
        finally {bitmap.UnlockBits(data);}
        using var png=new MemoryStream();bitmap.Save(png,ImageFormat.Png);Atomic(name,png.ToArray());
    }
}
