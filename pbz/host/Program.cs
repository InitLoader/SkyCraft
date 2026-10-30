using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using DuckovCraft.Link;

namespace PBZCraft.Trial;

internal static class Program
{
    private static readonly DateTimeOffset Deadline = DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("PBZCRAFT_DEADLINE"),out var deadline) ? deadline : DateTimeOffset.MaxValue;
    public static async Task<int> Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : throw new ArgumentException("Trial root required");
        int seconds = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 150;
        bool nativeMode = args.Length > 2 && args[2] == "native";
        bool overlayMode = args.Length > 3 && args[3] == "overlay";
        bool interactive=args.Contains("interactive"),nativeCaptures=!args.Contains("nocapture");
        string resultFile=args.Length>4 ? args[4]+"-result.json" : "transport-result.json";
        if(Path.GetFileName(resultFile)!=resultFile) throw new ArgumentException("Result tag must be a filename");
        Directory.CreateDirectory(Path.Combine(root,"captures"));
        using var link = new SharedLink("Local\\PBZCraft_v1");
        using var native = new NativeIpc();
        using var overlay = overlayMode ? new LayeredOverlay() : null;
        var surface = new NativeSurface();
        string exchange=Environment.GetEnvironmentVariable("PBZCRAFT_EXCHANGE")??Path.Combine(root,"exchange","render");
        var renderCapture=new RenderCapture(exchange);
        var actions=new TrialActions();
        var input=new NativeInputRelay();
        var entities=new EntityCapture();
        var timer = Stopwatch.StartNew();
        int frames=0, renderMessages=0, events=0, visible=0, nativeSamples=0;
        bool firstSaved=false, inventorySaved=false, inventoryRequested=false, release=false, origin=false, diagnosticSaved=false;
        bool thirdSaved=false,jumpSaved=false,crackSaved=false,everWorld=false;
        double crackSince=0;
        double maxTravel=0,maxHeight=double.MinValue,minHeight=double.MaxValue;
        double ox=0,oy=0,oz=0, px=0,py=128,pz=0,yaw=0,pitch=0, stableSince=0, inventoryAt=0, nextReport=0;
        double previousLoop=0,maxLoop=0;
        double[] pose=[];
        McState mc=default;
        while((seconds<=0 || timer.Elapsed.TotalSeconds<seconds) && DateTimeOffset.Now<Deadline && !File.Exists(Path.Combine(exchange,"stop.request")))
        {
            double now=timer.Elapsed.TotalSeconds;
            if(previousLoop>0)maxLoop=Math.Max(maxLoop,(now-previousLoop)*1000);previousLoop=now;
            bool nativeReady=nativeMode && native.Read(out pose) && pose.Length>=218;
            if(nativeReady)
            {
                nativeSamples++;
                if(!origin) { ox=pose[215];oy=pose[216];oz=pose[217];origin=true; Console.WriteLine($"[PBZCraftTrial] Native origin {ox},{oy},{oz}; paused={pose[10]}"); }
                px=-(pose[1]-oy)/100; py=128+(pose[2]-pose[3]-oz)/100;pz=(pose[0]-ox)/100;
                if(!interactive || !input.Initialized) {yaw=pose[4];pitch=-pose[5];}
                if(actions.LookingDown)pitch=45;
                if(pose.Length>=229) {actions.ObstacleYaw=pose[227];actions.ObstacleDistance=pose[228];}
                if(!interactive && actions.FacingObstacle)yaw=actions.ObstacleYaw;
                actions.BlockAvailable=renderCapture.TestBlockTarget.HasValue;
                if(!interactive && actions.MiningAim && renderCapture.TestBlockTarget is {} block)
                {
                    double bx=block.X-mc.EyeX,bz=block.Z-mc.EyeZ,by=(actions.Stage>=23?block.Y-0.7:block.Y)-mc.EyeY;
                    yaw=Math.Atan2(-bx,bz)*180/Math.PI;pitch=-Math.Atan2(by,Math.Sqrt(bx*bx+bz*bz))*180/Math.PI;
                }
                surface.Publish(link,pose,ox,oy,oz);
            }
            int viewportWidth=Math.Clamp(overlay?.ViewportWidth??1280,1,3840),viewportHeight=Math.Clamp(overlay?.ViewportHeight??720,1,2160);
            if(interactive && nativeReady && overlay is not null)
            {
                overlay.Input.Enabled=link.Connected && (mc.Flags&Protocol.McInWorld)!=0 && mc.TeleportAck==1;
                overlay.Input.Read(pose);
                input.Step(link,pose,mc,viewportWidth,viewportHeight,ref yaw,ref pitch);
            }
            if(input.ExitRequested)break;
            link.Heartbeat();
            uint seq=link.BeginWrite(Protocol.Sky);
            link.Put(Protocol.Sky+4, !nativeMode || nativeReady ? Protocol.InGame : Protocol.Loading);
            link.Put(Protocol.Sky+8,1);link.Put(Protocol.Sky+12,1);
            link.PutDouble(Protocol.Sky+16,px);link.PutDouble(Protocol.Sky+24,py);link.PutDouble(Protocol.Sky+32,pz);
            link.PutFloat(Protocol.Sky+40,(float)yaw);link.PutFloat(Protocol.Sky+44,(float)pitch);
            link.Put(Protocol.Sky+48,1);link.Put(Protocol.Sky+52,(uint)viewportWidth);link.Put(Protocol.Sky+56,(uint)viewportHeight);link.PutFloat(Protocol.Sky+60,12);
            link.EndWrite(Protocol.Sky,seq);
            if(link.ReadMc(out var incoming)) mc=incoming;
            bool world=link.Connected && (mc.Flags&Protocol.McInWorld)!=0;
            if(world && (!nativeMode || nativeReady && mc.TeleportAck==1))everWorld=true;
            if(interactive && everWorld && !world)break;
            bool screen=(mc.Flags&Protocol.McScreenOpen)!=0;
            bool ready=world && !screen && mc.TeleportAck==1;
            if(ready && (interactive || actions.Stage>=10)) { maxTravel=Math.Max(maxTravel,Math.Sqrt(mc.X*mc.X+mc.Z*mc.Z));maxHeight=Math.Max(maxHeight,mc.Y);minHeight=Math.Min(minHeight,mc.Y); }
            if(ready) { if(stableSince==0)stableSince=now; } else stableSince=0;
            double horizontalFov=mc.Fov>0 ? 2*Math.Atan(Math.Tan(mc.Fov*Math.PI/360)*viewportWidth/viewportHeight)*180/Math.PI : 90;
            if(nativeMode) native.Write(nativeReady && world && mc.TeleportAck==1 ? 1:0,yaw,-pitch,horizontalFov,mc.CameraMode,mc.CameraDistance,
                ox+mc.EyeZ*100,oy-mc.EyeX*100,oz+(mc.EyeY-128)*100,ox+mc.Z*100,oy-mc.X*100,oz+(mc.Y-128)*100,screen?1:0);
            link.DrainRender((type,data)=>{renderMessages++;renderCapture.Receive(type,data);});
            renderCapture.Entities(entities.Read(link));
            renderCapture.Flush(Environment.TickCount64);
            link.DrainEvents(value=>events++);
            if(release) { link.Input(Protocol.Key,8,0);release=false; }
            if(!interactive)actions.Step(link,mc,inventorySaved,(long)timer.Elapsed.TotalMilliseconds);
            if(world && link.OverlayFrame(out int w,out int h,out bool bottomUp,out byte[] rgba))
            {
                frames++;
                if(mc.TeleportAck==1 && nativeReady) overlay?.Publish(w,h,bottomUp,rgba);
                if(!interactive && !firstSaved && stableSince>0 && now-stableSince>2 && now>20)
                {
                    visible=FrameCapture.Save(Path.Combine(root,"captures","minecraft-first-person.png"),w,h,bottomUp,rgba);
                    firstSaved=visible>100;
                    bool nativeCaptured=!nativeCaptures || FrameCapture.NativeProof(Path.Combine(root,"captures","native-first-person.png"),Path.Combine(root,"captures","minecraft-first-person.png"));
                    if(overlayMode && !nativeCaptured)firstSaved=false;
                }
                if(!inventoryRequested && firstSaved && ready)
                {
                    inventoryAt=now;link.Input(Protocol.Key,8,1);inventoryRequested=true;release=true;
                    Console.WriteLine("[PBZCraftTrial] Inventory requested after world-ready confirmation, via MC bridge only.");
                }
                if(!inventorySaved && inventoryRequested && screen && now-inventoryAt>1)
                {
                    FrameCapture.Save(Path.Combine(root,"captures","minecraft-inventory.png"),w,h,bottomUp,rgba);inventorySaved=true;
                    if(nativeCaptures)FrameCapture.NativeProof(Path.Combine(root,"captures","native-inventory.png"),Path.Combine(root,"captures","minecraft-inventory.png"));
                }
                if(!interactive && !thirdSaved && mc.CameraMode==1 && actions.ThirdCaptureReady((long)timer.Elapsed.TotalMilliseconds))
                {
                    FrameCapture.Save(Path.Combine(root,"captures","minecraft-third-person.png"),w,h,bottomUp,rgba);
                    thirdSaved=!nativeCaptures || FrameCapture.NativeProof(Path.Combine(root,"captures","native-third-person.png"),Path.Combine(root,"captures","minecraft-third-person.png"));
                }
                if(!interactive && !diagnosticSaved && now>40)
                {
                    FrameCapture.Save(Path.Combine(root,"captures","minecraft-diagnostic.png"),w,h,bottomUp,rgba);diagnosticSaved=true;
                }
                if(!interactive && !jumpSaved && actions.Stage>=16 && mc.Y>128.6)
                {
                    FrameCapture.Save(Path.Combine(root,"captures","minecraft-jump.png"),w,h,bottomUp,rgba);
                    jumpSaved=!nativeCaptures || FrameCapture.NativeProof(Path.Combine(root,"captures","native-jump.png"),Path.Combine(root,"captures","minecraft-jump.png"));
                }
                if(entities.Cracks>0 && crackSince==0)crackSince=now;
                if(!interactive && !crackSaved && crackSince>0 && entities.Cracks>0 && now-crackSince>0.15)
                {
                    FrameCapture.Save(Path.Combine(root,"captures","minecraft-cracks.png"),w,h,bottomUp,rgba);
                    crackSaved=!nativeCaptures || FrameCapture.NativeProof(Path.Combine(root,"captures","native-cracks.png"),Path.Combine(root,"captures","minecraft-cracks.png"));
                }
            }
            if(now>=nextReport)
            {
                nextReport=now+5;
                Console.WriteLine($"[PBZCraftTrial] inputQueued={link.U64(Protocol.Input)-link.U64(Protocol.Input+64)}, maxLoopMs={maxLoop:F1}, mcFrameAgeMs={(mc.FrameQpc>0?(Stopwatch.GetTimestamp()-mc.FrameQpc)*1000.0/Stopwatch.Frequency:-1):F1}, avatarWrites={renderCapture.AvatarUpdates}, paused={(nativeReady?pose[10]:-1)}, inputError={overlay?.Input.Error??"none"}");maxLoop=0;
                Console.WriteLine($"[PBZCraftTrial] native={nativeReady}, linked={link.Connected}, flags={mc.Flags}, ack={mc.TeleportAck}, frames={frames}, presented={overlay?.Presented??0}, messages={renderMessages}, sections={renderCapture.SectionUpdates}, mode={mc.CameraMode}, testStage={actions.Stage}, xyz={mc.X:F3},{mc.Y:F3},{mc.Z:F3}, inventory={inventorySaved}, focused={overlay?.Input.Focused??false}, keyMask={overlay?.Input.Mask??0}, mousePackets={overlay?.Input.MousePackets??0}");
                Console.Out.Flush();
            }
            await Task.Delay(12);
        }
        native.Write(-1,0,0,90,0,0);
        link.Input(Protocol.ReleaseAll);
        await Task.Delay(600);
        File.WriteAllText(Path.Combine(root,resultFile),JsonSerializer.Serialize(new { interactive,nativeCaptures,frames,renderMessages,events,firstSaved,inventorySaved,thirdSaved,jumpSaved,crackSaved,visible,nativeSamples,surface.Hits,surface.Triangles,maxTravel,maxHeight,minHeight,renderCapture.SharingRetries,renderCapture.PendingWrites,renderCapture.SectionUpdates,entities.PeakCracks,entities.PeakItems,testStage=actions.Stage,presented=overlay?.Presented??0,overlayError=overlay?.Error,seconds=timer.Elapsed.TotalSeconds,mcFlags=mc.Flags,ack=mc.TeleportAck,scope="Native world running, MC movement uses native capsule sweeps without a fixed arena boundary. No full-map or combat acceptance." },new JsonSerializerOptions{WriteIndented=true}));
        return (interactive?everWorld:firstSaved && inventorySaved) && (!nativeMode || nativeSamples>0) ? 0:2;
    }
}
