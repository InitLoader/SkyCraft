using DuckovCraft.Link;

namespace PBZCraft.Trial;

internal sealed class NativeSurface
{
    public int Hits { get; private set; }
    public int Triangles { get; private set; }
    private bool sent;
    private readonly Dictionary<(int X,int Y,int Z),List<float[]>> regions=[];
    private (int X,int Y,int Z)? rayRegion;
    private float[]? previousRay;

    public void Publish(SharedLink link, double[] native, double ox, double oy, double oz)
    {
        if(sent) {UpdateRay(link,native,ox,oy,oz);return;}
        if(native.Length<208 || native[11]!=49) return;
        var points = new (bool Hit, float X, float Y, float Z)[49];
        for (int i=0;i<49;i++)
        {
            int p=12+i*4;
            points[i]=(native[p]==1,(float)(-(native[p+2]-oy)/100),(float)(128+(native[p+3]-oz)/100),(float)((native[p+1]-ox)/100));
            if (points[i].Hit) Hits++;
        }
        var triangles = new List<float[]>();
        void Add(int a,int b,int c)
        {
            var p=points[a]; var q=points[b]; var r=points[c];
            if (!p.Hit || !q.Hit || !r.Hit || Math.Abs(p.Y-q.Y)>0.75 || Math.Abs(p.Y-r.Y)>0.75) return;
            triangles.Add([p.X,p.Y,p.Z,q.X,q.Y,q.Z,r.X,r.Y,r.Z]);
        }
        for (int y=0;y<6;y++) for(int x=0;x<6;x++)
        {
            int a=y*7+x; Add(a,a+7,a+1); Add(a+1,a+7,a+8);
        }
        link.Collision(1,BitConverter.GetBytes(1u));
        for (int rx=-1;rx<=0;rx++) for(int ry=14;ry<=17;ry++) for(int rz=-1;rz<=0;rz++)
        {
            using var buffer=new MemoryStream(); using var writer=new BinaryWriter(buffer);
            writer.Write(rx*8);writer.Write(ry*8);writer.Write(rz*8);
            writer.Write(rx*8+7);writer.Write(ry*8+7);writer.Write(rz*8+7);
            writer.Write(1u);
            var selected=triangles.Where(t => t.Where((v,i)=>i%3==0).Min()<=rx*8+8 && t.Where((v,i)=>i%3==0).Max()>=rx*8
                && t.Where((v,i)=>i%3==1).Min()<=ry*8+8 && t.Where((v,i)=>i%3==1).Max()>=ry*8
                && t.Where((v,i)=>i%3==2).Min()<=rz*8+8 && t.Where((v,i)=>i%3==2).Max()>=rz*8).ToArray();
            writer.Write(selected.Length);
            regions[(rx,ry,rz)]=selected.ToList();
            foreach(var tri in selected) { foreach(float value in tri) writer.Write(value); writer.Write(0u); }
            if (!link.Collision(3,buffer.ToArray())) throw new IOException("Native surface ring unexpectedly full");
            var known=buffer.ToArray()[..32];
            Array.Clear(known,28,4);
            if (!link.Collision(2,known)) throw new IOException("Native known-region ring unexpectedly full");
        }
        Triangles=triangles.Count;sent=true;
        Console.WriteLine($"[PBZCraftTrial] Local PBZ surface: {Hits}/49 native ray hits, {Triangles} triangles. No walls or full-map coverage.");
        UpdateRay(link,native,ox,oy,oz);
    }

    private void UpdateRay(SharedLink link,double[] native,double ox,double oy,double oz)
    {
        float[]? ray=null;
        (int X,int Y,int Z)? region=null;
        if(native.Length>=215 && native[208]==1)
        {
            var point=new System.Numerics.Vector3((float)(-(native[210]-oy)/100),(float)(128+(native[211]-oz)/100),(float)((native[209]-ox)/100));
            var normal=new System.Numerics.Vector3((float)-native[213],(float)native[214],(float)native[212]);
            if(normal.LengthSquared()<0.5)return;
            normal=System.Numerics.Vector3.Normalize(normal);
            var axis=Math.Abs(normal.Y)<0.9f ? System.Numerics.Vector3.UnitY:System.Numerics.Vector3.UnitX;
            var u=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(normal,axis))*0.15f;
            var v=System.Numerics.Vector3.Cross(normal,u);
            var a=point-u-v;var b=point+u-v;var c=point+u+v;var d=point-u+v;
            ray=[a.X,a.Y,a.Z,b.X,b.Y,b.Z,c.X,c.Y,c.Z,a.X,a.Y,a.Z,c.X,c.Y,c.Z,d.X,d.Y,d.Z];
            region=((int)Math.Floor(point.X/8),(int)Math.Floor(point.Y/8),(int)Math.Floor(point.Z/8));
        }
        if(region==rayRegion && ((ray is null && previousRay is null) || (ray is not null && previousRay is not null && ray.Zip(previousRay).All(pair=>Math.Abs(pair.First-pair.Second)<0.0001f))))return;
        void Send((int X,int Y,int Z) key,float[]? contact)
        {
            regions.TryGetValue(key,out var basis);
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);
            writer.Write(key.X*8);writer.Write(key.Y*8);writer.Write(key.Z*8);
            writer.Write(key.X*8+7);writer.Write(key.Y*8+7);writer.Write(key.Z*8+7);writer.Write(1u);
            writer.Write((basis?.Count??0)+(contact is null?0:2));
            if(basis is not null) foreach(var tri in basis) {foreach(var value in tri)writer.Write(value);writer.Write(0u);}
            if(contact is not null) for(int n=0;n<18;n+=9){for(int k=0;k<9;k++)writer.Write(contact[n+k]);writer.Write(0u);}
            if(!link.Collision(3,bytes.ToArray()))throw new IOException("Native ray contact ring full");
        }
        if(rayRegion is not null && rayRegion!=region)Send(rayRegion.Value,null);
        if(region is not null)Send(region.Value,ray);
        rayRegion=region;previousRay=ray;
    }
}
