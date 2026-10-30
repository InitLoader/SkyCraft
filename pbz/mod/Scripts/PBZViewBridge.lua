local Bridge = {}
local Effects=require("PBZCameraEffects")
local Collision=require("PBZCollision")
local Input=require("PBZInput")
local camera, original, hidden, hud, hudVisible = nil,nil,nil,nil,nil
local updates = 0
local intent=nil
local cursorBefore=false
local screenBefore=false

function Bridge.restore(controller,pawn)
    if not camera then return end
    Collision.restore(pawn)
    Input.restore()
    if original and original:IsValid() then controller:SetViewTargetWithBlend(original,0,0,0,false) end
    if pawn and pawn:IsValid() then pawn:SetActorHiddenInGame(hidden) end
    if hud and hud:IsValid() then hud.bShowHUD=hudVisible end
    if screenBefore then StaticFindObject("/Script/UMG.Default__WidgetBlueprintLibrary"):SetInputMode_GameOnly(controller,false) end
    controller.bShowMouseCursor=cursorBefore
    screenBefore=false
    if camera:IsValid() then camera:K2_DestroyActor() end
    camera=nil
    intent=nil
    print("[PBZProbe] Native view restored after bridge stop.\n")
end

function Bridge.step(controller,pawn)
    local data = PBZNative_Receive()
    if not data or (#data~=48 and #data~=96 and #data~=104) then Bridge.restore(controller,pawn); return end
    local active,yaw,pitch,fov,mode,distance = string.unpack("<dddddd",data)
    if active==-1 then
        Bridge.restore(controller,pawn)
        local system=StaticFindObject("/Script/Engine.Default__KismetSystemLibrary")
        print("[PBZProbe] Requesting normal native game exit after the bounded trial.\n")
        system:QuitGame(controller,controller,0,false)
        return
    end
    if active ~= 1 then Bridge.restore(controller,pawn); return end
    if #data~=96 and #data~=104 then return end
    local screen=#data==104 and string.unpack("<d",data,97)==1
    local _,_,_,_,_,_,ex,ey,ez,fx,fy,fz=string.unpack("<dddddddddddd",data)
    if math.abs(yaw)>10000 or math.abs(pitch)>90 or fov<40 or fov>120 or distance<0 or distance>8 then return end
    local center=pawn:K2_GetActorLocation()
    if math.abs(ex-center.X)>800 or math.abs(ey-center.Y)>800 or math.abs(ez-center.Z)>800 then return end
    local eye={X=ex,Y=ey,Z=ez}
    local rotation={Pitch=pitch,Yaw=yaw,Roll=0}
    intent={Pitch=pitch,Yaw=yaw,Roll=0,EyeWorld={X=ex,Y=ey,Z=ez},FeetWorld={X=fx,Y=fy,Z=fz}}
    if mode ~= 0 then
        local r,p=math.rad(yaw),math.rad(pitch)
        local sign=mode==2 and 1 or -1
        eye.X=eye.X+math.cos(r)*math.cos(p)*distance*100*sign
        eye.Y=eye.Y+math.sin(r)*math.cos(p)*distance*100*sign
        eye.Z=eye.Z+math.sin(p)*distance*100*sign
        if mode==2 then rotation.Yaw=yaw+180; rotation.Pitch=-pitch end
    end
    if not camera or not camera:IsValid() then
        original=controller:GetViewTarget()
        cursorBefore=controller.bShowMouseCursor
        hidden=pawn.bHidden
        hud=controller:GetHUD()
        if hud and hud:IsValid() then hudVisible=hud.bShowHUD; hud.bShowHUD=false end
        camera=controller:GetWorld():SpawnActor(StaticFindObject("/Script/Engine.CameraActor"),eye,rotation)
        assert(camera and camera:IsValid(),"Cannot create bridge camera")
        Effects.apply(camera)
        pawn:SetActorHiddenInGame(true)
        controller:SetViewTargetWithBlend(camera,0,0,0,false)
        print("[PBZProbe] Native view now follows the MC bridge camera.\n")
    end
    camera.CameraComponent.FieldOfView=fov
    if screen~=screenBefore then
        local widgets=StaticFindObject("/Script/UMG.Default__WidgetBlueprintLibrary")
        if screen then widgets:SetInputMode_GameAndUIEx(controller,nil,0,false,false)
        else widgets:SetInputMode_GameOnly(controller,false) end
        controller.bShowMouseCursor=screen
        screenBefore=screen
    end
    camera:K2_SetActorLocationAndRotation(eye,rotation,false,{},true)
    updates=updates+1
    if updates%120==0 then
        local actual=controller.PlayerCameraManager:GetCameraLocation()
        local dx,dy,dz=actual.X-eye.X,actual.Y-eye.Y,actual.Z-eye.Z
        print(string.format("[PBZProbe] Bridge camera updates=%d error=%.3fcm yaw=%.2f mode=%d\n",updates,math.sqrt(dx*dx+dy*dy+dz*dz),yaw,mode))
    end
end
function Bridge.active() return camera and camera:IsValid() end
function Bridge.intent() return intent end
return Bridge
