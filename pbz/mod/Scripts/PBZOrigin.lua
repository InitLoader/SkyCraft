local Origin={}
local value=nil
function Origin.get(pawn)
    if not value then
        local point=pawn:K2_GetActorLocation()
        value={X=point.X,Y=point.Y,Z=point.Z-pawn.CapsuleComponent:GetScaledCapsuleHalfHeight()}
    end
    return value
end
return Origin
