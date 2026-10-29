local Ray={}
local system=nil
local color={R=0,G=0,B=0,A=0}
function Ray.values(controller,pawn,intent)
    if not system then system=StaticFindObject("/Script/Engine.Default__KismetSystemLibrary") end
    local center=pawn:K2_GetActorLocation()
    local start={X=center.X,Y=center.Y,Z=center.Z-pawn.CapsuleComponent:GetScaledCapsuleHalfHeight()+162}
    if intent and intent.EyeWorld then start=intent.EyeWorld end
    local look=intent or controller:GetControlRotation()
    local yaw,pitch=math.rad(look.Yaw),math.rad(look.Pitch)
    local finish={X=start.X+math.cos(yaw)*math.cos(pitch)*500,Y=start.Y+math.sin(yaw)*math.cos(pitch)*500,Z=start.Z+math.sin(pitch)*500}
    local hit={}
    local found=system:LineTraceSingle(pawn,start,finish,0,true,{pawn},0,hit,true,color,color,0)
    if not found then return {0,0,0,0,0,0,0} end
    return {1,hit.ImpactPoint.X,hit.ImpactPoint.Y,hit.ImpactPoint.Z,hit.ImpactNormal.X,hit.ImpactNormal.Y,hit.ImpactNormal.Z}
end
return Ray
