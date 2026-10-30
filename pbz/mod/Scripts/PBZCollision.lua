local Collision={}
local Origin=require("PBZOrigin")
local Movement=require("PBZMovement")
local original=nil
local radius,half=nil,nil
local requests,blocked,rejected=0,0,0
local maximum=0
local highest=-1000000

local function distance(a,b)
    return math.sqrt((a.X-b.X)^2+(a.Y-b.Y)^2+(a.Z-b.Z)^2)
end

local function sweep(pawn,delta)
    local remaining=delta
    local clipped={X=false,Y=false,Z=false}
    for _=1,3 do
        if remaining.X^2+remaining.Y^2+remaining.Z^2<0.00001 then break end
        local hit={}
        pawn:K2_AddActorWorldOffset(remaining,true,hit,false)
        if not hit.bBlockingHit then break end
        blocked=blocked+1
        local normal=hit.Normal
        local fraction=1-math.max(0,math.min(1,hit.Time))
        if normal.Z>=0.7 and remaining.Z<=0 then
            clipped.Z=true
            local x,y=remaining.X*fraction,remaining.Y*fraction
            remaining={X=x,Y=y,Z=-(x*normal.X+y*normal.Y)/normal.Z}
        else
            clipped.X=clipped.X or math.abs(normal.X)>0.0001
            clipped.Y=clipped.Y or math.abs(normal.Y)>0.0001
            clipped.Z=clipped.Z or math.abs(normal.Z)>0.0001
            local dot=(remaining.X*normal.X+remaining.Y*normal.Y+remaining.Z*normal.Z)*fraction
            remaining={X=remaining.X*fraction-normal.X*math.min(0,dot),Y=remaining.Y*fraction-normal.Y*math.min(0,dot),Z=remaining.Z*fraction-normal.Z*math.min(0,dot)}
        end
    end
    return clipped
end

function Collision.step(pawn)
    local origin=Origin.get(pawn)
    local data=PBZNative_CollisionPoll(1)
    if not data then return end
    local x,y,z,dx,dy,dz,width,height=string.unpack("<dddddddd",data)
    if x~=x or y~=y or z~=z or dx~=dx or dy~=dy or dz~=dz or not (width>=0.2 and width<=2 and height>=0.2 and height<=3)
        or x*x+z*z==math.huge or math.abs(y-128)==math.huge or dx*dx+dy*dy+dz*dz>64 then
        rejected=rejected+1;PBZNative_CollisionRespond(0,0,0,2);return
    end
    if not original then
        Movement.claim(pawn)
        local point=pawn:K2_GetActorLocation()
        original={X=point.X,Y=point.Y,Z=point.Z}
        radius=pawn.CapsuleComponent:GetScaledCapsuleRadius()
        half=pawn.CapsuleComponent:GetScaledCapsuleHalfHeight()
        print(string.format("[PBZProbe] Native collision queries started; original capsule %.2f,%.2fcm\n",radius,half))
    end
    local capsule=pawn.CapsuleComponent
    local targetHalf=height*50
    if math.abs(capsule:GetScaledCapsuleRadius()-width*50)>0.01 or math.abs(capsule:GetScaledCapsuleHalfHeight()-targetHalf)>0.01 then
        local before=pawn:K2_GetActorLocation()
        local oldHalf=capsule:GetScaledCapsuleHalfHeight()
        capsule:SetCapsuleSize(width*50,targetHalf,true)
        pawn:K2_SetActorLocation({X=before.X,Y=before.Y,Z=before.Z-oldHalf+targetHalf},false,{},true)
    end
    local start={X=origin.X+z*100,Y=origin.Y-x*100,Z=origin.Z+(y-128)*100+targetHalf}
    local current=pawn:K2_GetActorLocation()
    if distance(current,start)>0.05 then
        -- MC may discard a speculative step-up query. Reset the query body, then sweep only the requested movement.
        pawn:K2_SetActorLocation(start,false,{},true)
        if distance(pawn:K2_GetActorLocation(),start)>2 then
            rejected=rejected+1;PBZNative_CollisionRespond(0,0,0,3);return
        end
    end
    local clipped=sweep(pawn,{X=dz*100,Y=-dx*100,Z=dy*100})
    local actual=pawn:K2_GetActorLocation()
    local ax,ay,az=-(actual.Y-start.Y)/100,(actual.Z-start.Z)/100,(actual.X-start.X)/100
    if not clipped.Y then ax=dx end
    if not clipped.Z then ay=dy end
    if not clipped.X then az=dz end
    requests=requests+1
    maximum=math.max(maximum,math.sqrt((actual.X-origin.X)^2+(actual.Y-origin.Y)^2))
    highest=math.max(highest,actual.Z-targetHalf-origin.Z)
    if dy>0.01 then print(string.format("[PBZProbe] Native jump requested=%.6fm resolved=%.6fm feet=%.3fcm\n",dy,ay,actual.Z-targetHalf-origin.Z)) end
    PBZNative_CollisionRespond(ax,ay,az,1)
    if requests%40==0 then print(string.format("[PBZProbe] Native collision requests=%d blocked=%d rejected=%d maxTravel=%.3fcm feetHeight=%.3fcm\n",requests,blocked,rejected,maximum,actual.Z-targetHalf-origin.Z)) end
end

function Collision.restore(pawn)
    PBZNative_CollisionPoll(0)
    if original and pawn and pawn:IsValid() then
        pawn.CapsuleComponent:SetCapsuleSize(radius,half,true)
        pawn:K2_SetActorLocation(original,false,{},true)
        print(string.format("[PBZProbe] Collision trial restored native capsule/start: requests=%d blocked=%d rejected=%d maxTravel=%.3fcm maxHeight=%.3fcm\n",requests,blocked,rejected,maximum,highest))
    end
    original=nil
    Movement.restore()
end
return Collision
