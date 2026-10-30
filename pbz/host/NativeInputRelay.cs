using DuckovCraft.Link;

namespace PBZCraft.Trial;

internal sealed class NativeInputRelay
{
    private static readonly ushort[] Keys=[26,4,22,7,44,225,224,8,20,21,23,41,43,62,30,31,32,33,34,35,36,37,38];
    private long previousFrame=-1;
    private uint previousMask;
    public bool Initialized {get;private set;}
    public bool ExitRequested {get;private set;}
    private static void Send(SharedLink link,ushort type,ushort code=0,int a=0,int b=0)=>link.Input(type,code,a,b,unchecked((int)SharedLink.Now));

    public void Step(SharedLink link,double[] pose,McState mc,int width,int height,ref double yaw,ref double pitch)
    {
        if(pose.Length<227)return;
        long frame=(long)pose[224];
        if(frame==previousFrame)return;
        previousFrame=frame;Initialized=true;
        uint mask=(uint)pose[218],changed=mask^previousMask;
        if((changed&mask&(1u<<26))!=0)ExitRequested=true;
        for(int index=0;index<Keys.Length;index++)
            if((changed&(1u<<index))!=0)Send(link,Protocol.Key,Keys[index],(mask&(1u<<index))!=0?1:0);
        ushort[] buttons=[1,3,2];
        for(int index=0;index<buttons.Length;index++)
            if((changed&(1u<<(23+index)))!=0)Send(link,Protocol.MouseButton,buttons[index],(mask&(1u<<(23+index)))!=0?1:0);
        if((mc.Flags&Protocol.McScreenOpen)!=0)
        {
            int x=(int)Math.Round(pose[221]*width/Math.Max(1,pose[225]));
            int y=(int)Math.Round(pose[222]*height/Math.Max(1,pose[226]));
            Send(link,Protocol.Cursor,0,x,y);
        }
        else
        {
            double sensitivity=Math.Pow(mc.Sensitivity*0.6+0.2,3)*8*0.15;
            yaw=(yaw+pose[219]*sensitivity)%360;
            pitch=Math.Clamp(pitch-pose[220]*sensitivity,-89.9,89.9);
        }
        if(pose[223]!=0)Send(link,Protocol.Scroll,0,Math.Sign(pose[223])*120);
        previousMask=mask;
    }
}
