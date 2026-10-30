package.path=(arg[1] or "pbz/mod/Scripts").."/?.lua;"..package.path
local point={X=0,Y=0,Z=90}
local sweepCount=0
local pawn={CapsuleComponent={}}
local movement={MovementMode=1,CustomMovementMode=0}
function movement:IsValid() return true end
function movement:SetMovementMode(mode,custom) self.MovementMode=mode;self.CustomMovementMode=custom end
pawn.CharacterMovement=movement
function pawn.CapsuleComponent:GetScaledCapsuleRadius() return 30 end
function pawn.CapsuleComponent:GetScaledCapsuleHalfHeight() return 90 end
function pawn:K2_GetActorLocation() return {X=point.X,Y=point.Y,Z=point.Z} end
function pawn:K2_SetActorLocation(target,sweep,hit,teleport)
    -- A speculative endpoint is separated from the next query origin by an obstacle.
    if not sweep then assert(teleport);point={X=target.X,Y=target.Y,Z=target.Z} end
end
function pawn:K2_AddActorWorldOffset(delta,sweep,hit,teleport)
    assert(sweep and not teleport,"Requested movement must retain collision sweeps")
    sweepCount=sweepCount+1
    point={X=point.X+delta.X,Y=point.Y+delta.Y,Z=point.Z+delta.Z}
    hit.bBlockingHit=false
end
local request,response
function PBZNative_CollisionPoll() local value=request;request=nil;return value end
function PBZNative_CollisionRespond(x,y,z,status) response={x,y,z,status} end
local collision=require("PBZCollision")
local function query(x,z,dx,dz)
    request=string.pack("<ddddddddd",x,128,z,dx,0,dz,0.6,1.8,1)
    response=nil;collision.step(pawn)
    assert(response and response[4]==1,"Query remained stuck at a discarded speculative endpoint")
    return response[1],response[3]
end
local function close(actual,expected) assert(math.abs(actual-expected)<0.000001,string.format("%.9f ~= %.9f",actual,expected)) end
close(query(0,0,0.5,0),0.5)
close(query(0.5,0,0.4,0),0.4)
close(query(0.5,0,-0.1,0),-0.1)
close(query(2.99,0,0.2,0),0.2)
close(query(3.17,0,-0.2,0),-0.2)
close(query(20,10,0.2,0),0.2)
assert(sweepCount>=5)
assert(movement.MovementMode==0)
require("PBZMovement").restore()
assert(movement.MovementMode==1)
print("PASS: speculative query reset does not trap movement; movement beyond 3m stays swept, and native movement mode is restored.")
