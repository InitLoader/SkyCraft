local Probe={}
local result=nil
local color={R=0,G=0,B=0,A=0}
function Probe.values(pawn)
    if result then return result end
    local system=StaticFindObject("/Script/Engine.Default__KismetSystemLibrary")
    local point=pawn:K2_GetActorLocation()
    local start={X=point.X,Y=point.Y,Z=point.Z+20}
    local profile=pawn.CapsuleComponent:GetCollisionProfileName()
    local best=201
    result={0,0}
    for index=0,15 do
        local angle=index*math.pi/8
        local finish={X=start.X+math.cos(angle)*200,Y=start.Y+math.sin(angle)*200,Z=start.Z}
        local hit={}
        local found=system:CapsuleTraceSingleByProfile(pawn,start,finish,30,90,profile,false,{pawn},0,hit,true,color,color,0)
        if found and not hit.bStartPenetrating and math.abs(hit.Normal.Z)<0.4 and hit.Distance>30 and hit.Distance<best then
            best=hit.Distance;result={index*22.5,best}
        end
    end
    print(string.format("[PBZProbe] Nearby native obstacle direction=%.2fdeg distance=%.2fcm profile=%s\n",result[1],result[2],profile:ToString()))
    return result
end
return Probe
