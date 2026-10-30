using DuckovCraft.Link;

namespace PBZCraft.Trial;

internal sealed class TrialActions
{
    private int stage;
    private long due;
    private ushort release;
    public bool LookingDown=>stage>=7 && stage<=9;
    public int Stage=>stage;
    public double ObstacleYaw,ObstacleDistance;
    public bool FacingObstacle=>stage>=18 && ObstacleDistance>0;
    public bool BlockAvailable;
    public bool MiningAim=>stage>=21 && stage<=25;
    public bool ThirdCaptureReady(long now)=>stage==3 && now>=due-12000;
    private void Key(SharedLink link,ushort key) {link.Input(Protocol.Key,key,1);release=key;}
    public void Step(SharedLink link,McState mc,bool inventoryCaptured,long now)
    {
        if(release!=0) {link.Input(Protocol.Key,release,0);release=0;}
        if(stage==0 && inventoryCaptured) {stage=1;due=now+1500;}
        if(now<due) return;
        switch(stage)
        {
            case 1: Key(link,8);stage=2;due=now+1000;Console.WriteLine("[PBZCraftTrial] Closing confirmed inventory via bridge.");break;
            case 2 when (mc.Flags&Protocol.McScreenOpen)==0:
                Key(link,62);stage=3;due=now+15000;Console.WriteLine("[PBZCraftTrial] Requested MC third-person back view via bridge.");break;
            case 3: Key(link,62);stage=4;due=now+2000;break;
            case 4: Key(link,62);stage=5;due=now+2000;break;
            case 5 when mc.CameraMode==0:
                Key(link,38);stage=7;due=now+1200;Console.WriteLine("[PBZCraftTrial] Selected MC slot9; looking down for one actual placement attempt.");break;
            case 7: link.Input(Protocol.MouseButton,3,1);stage=8;due=now+150;break;
            case 8: link.Input(Protocol.MouseButton,3,0);stage=9;due=now+1500;Console.WriteLine("[PBZCraftTrial] Released MC use button after placement attempt.");break;
            case 9: stage=10;due=now+1000;break;
            case 10: link.Input(Protocol.Key,26,1);stage=11;due=now+150;Console.WriteLine("[PBZCraftTrial] Bounded MC forward movement started through the bridge.");break;
            case 11: link.Input(Protocol.Key,26,0);stage=12;due=now+1500;break;
            case 12: link.Input(Protocol.Key,22,1);stage=13;due=now+150;break;
            case 13: link.Input(Protocol.Key,22,0);stage=14;due=now+1500;Console.WriteLine("[PBZCraftTrial] Bounded MC return movement released.");break;
            case 14: Key(link,62);stage=15;due=now+2000;break;
            case 15: link.Input(Protocol.Key,44,1);stage=16;due=now+150;Console.WriteLine("[PBZCraftTrial] Requested one MC jump against native collision.");break;
            case 16: link.Input(Protocol.Key,44,0);stage=17;due=now+3000;break;
            case 17 when ObstacleDistance>0 && ObstacleDistance<200: stage=18;due=now+1000;break;
            case 17: stage=20;due=now+1000;break;
            case 18: link.Input(Protocol.Key,26,1);stage=19;due=now+(long)Math.Clamp((ObstacleDistance+40)/0.43,200,700);Console.WriteLine($"[PBZCraftTrial] Walking toward measured native obstacle at {ObstacleYaw}deg / {ObstacleDistance:F2}cm.");break;
            case 19: link.Input(Protocol.Key,26,0);stage=20;due=now+1000;Console.WriteLine("[PBZCraftTrial] Native obstacle approach released.");break;
            case 20 when BlockAvailable && mc.CameraMode!=0: Key(link,62);due=now+500;break;
            case 20 when BlockAvailable: Key(link,31);stage=21;due=now+1000;break;
            case 21: link.Input(Protocol.MouseButton,1,1);stage=22;due=now+800;Console.WriteLine("[PBZCraftTrial] Mining the existing isolated MC test block with a diamond pickaxe.");break;
            case 22: link.Input(Protocol.MouseButton,1,0);Key(link,38);stage=23;due=now+1000;break;
            case 23: link.Input(Protocol.MouseButton,3,1);stage=24;due=now+150;break;
            case 24: link.Input(Protocol.MouseButton,3,0);stage=25;due=long.MaxValue;Console.WriteLine("[PBZCraftTrial] Released replacement placement on native terrain.");break;
        }
    }
}
