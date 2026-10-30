using System.Numerics;
using DuckovCraft.Link;

namespace PBZCraft.Trial;

internal sealed class EntityCapture
{
    private uint previous;
    public int PeakCracks,PeakItems;
    public int Cracks {get;private set;}
    private static readonly Vector3[] Normals=[-Vector3.UnitY,Vector3.UnitY,-Vector3.UnitZ,Vector3.UnitZ,-Vector3.UnitX,Vector3.UnitX];
    private static readonly Vector3[] Across=[Vector3.UnitX,Vector3.UnitX,-Vector3.UnitX,Vector3.UnitX,Vector3.UnitZ,-Vector3.UnitZ];
    private static readonly Vector3[] Up=[Vector3.UnitZ,-Vector3.UnitZ,Vector3.UnitY,Vector3.UnitY,Vector3.UnitY,Vector3.UnitY];
    public byte[]? Read(SharedLink link)
    {
        uint seq=link.U32(Protocol.Entities);
        if(seq==previous || (seq&1)!=0)return null;
        int count=link.I32(Protocol.Entities+4);
        if(count<0 || count>160)throw new InvalidDataException("Invalid MC entity count");
        var data=new byte[count*96];link.Copy(Protocol.Entities+64,data,data.Length);
        if(seq!=link.U32(Protocol.Entities))return null;
        previous=seq;
        using var vertices=new MemoryStream();using var writer=new BinaryWriter(vertices);
        int cracks=0,items=0;
        for(int entity=0;entity<count;entity++)
        {
            int at=entity*96;uint kind=BitConverter.ToUInt32(data,at);
            if(kind is not (2 or 4 or 5))continue;
            float F(int offset)=>BitConverter.ToSingle(data,at+offset);
            var position=new Vector3(F(8),F(12),F(16));
            var extent=kind==5 ? new Vector3(F(32),F(36),F(40)):new Vector3(F(28));
            if(!float.IsFinite(position.X+position.Y+position.Z+extent.X+extent.Y+extent.Z) || extent.LengthSquared()>100)continue;
            if(kind==5)cracks++;else items++;
            var center=kind==5?extent*0.5f:Vector3.Zero;
            var rotation=Matrix4x4.CreateRotationY(-F(20)*MathF.PI/180);
            uint tint=BitConverter.ToUInt32(data,at+92);if(tint==0)tint=uint.MaxValue;
            for(int faceIndex=0;faceIndex<(kind==2?1:6);faceIndex++)
            {
                int face=kind==2?2:faceIndex;
                var c=center+Normals[face]*extent*0.5f;
                var u=Across[face]*extent*0.5f;var v=Up[face]*extent*0.5f;
                Vector3[] quad=[c-u-v,c+u-v,c+u+v,c-u+v];
                int rect=44+(kind==4 && face<2?(face==0?2:1)*16:0);
                Vector2[] uv=[new(F(rect),F(rect+12)),new(F(rect+8),F(rect+12)),new(F(rect+8),F(rect+4)),new(F(rect),F(rect+4))];
                foreach(int index in new[]{0,2,1,0,3,2})
                {
                    var p=Vector3.Transform(quad[index],rotation)+position;
                    writer.Write(p.X);writer.Write(p.Y);writer.Write(p.Z);writer.Write(uv[index].X);writer.Write(uv[index].Y);writer.Write(tint);writer.Write(0L);
                }
            }
        }
        Cracks=cracks;PeakCracks=Math.Max(PeakCracks,cracks);PeakItems=Math.Max(PeakItems,items);
        using var output=new MemoryStream();using var header=new BinaryWriter(output);
        int vertexCount=(int)vertices.Length/32;
        header.Write(vertexCount==0?0u:1u);header.Write((uint)vertexCount);
        if(vertexCount>0){header.Write(0u);header.Write(0u);header.Write((uint)vertexCount);header.Write(0u);header.Write(vertices.ToArray());}
        return output.ToArray();
    }
}
