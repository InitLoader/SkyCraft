local Movement={}
local component,mode,custom=nil,nil,nil

function Movement.claim(pawn)
    if component then return end
    component=pawn.CharacterMovement
    assert(component and component:IsValid(),"Native character movement is unavailable")
    mode=component.MovementMode;custom=component.CustomMovementMode
    component:SetMovementMode(0,0)
    print("[PBZProbe] MC owns player movement; native world is not paused.\n")
end

function Movement.restore()
    if component and component:IsValid() then component:SetMovementMode(mode,custom) end
    component=nil
end
return Movement
