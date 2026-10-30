local Ground = {}
local samples = {}
local index = 0
local base = nil
local system = nil
local color = {R=0,G=0,B=0,A=0}

function Ground.step(pawn)
    if index >= 49 then return end
    if not base then
        local pos = pawn:K2_GetActorLocation()
        base = {X=pos.X,Y=pos.Y,Z=pos.Z-pawn.CapsuleComponent:GetScaledCapsuleHalfHeight()}
        system = StaticFindObject("/Script/Engine.Default__KismetSystemLibrary")
    end
    local x = base.X + ((index % 7)-3)*100
    local y = base.Y + (math.floor(index/7)-3)*100
    local hit = {}
    local found = system:LineTraceSingle(pawn, {X=x,Y=y,Z=base.Z+50}, {X=x,Y=y,Z=base.Z-300}, 0, true, {pawn}, 0, hit, true, color, color, 0)
    samples[#samples+1] = found and 1 or 0
    samples[#samples+1] = found and hit.ImpactPoint.X or x
    samples[#samples+1] = found and hit.ImpactPoint.Y or y
    samples[#samples+1] = found and hit.ImpactPoint.Z or base.Z
    index = index + 1
    if index == 49 then
        local valid = 0
        for at=1,#samples,4 do if samples[at] == 1 then valid=valid+1 end end
        print(string.format("[PBZProbe] Ground sampling complete: %d/49 actual native ray hits; local surface only.\n",valid))
    end
end

function Ground.values()
    if index < 49 then return {} end
    return samples
end
return Ground
