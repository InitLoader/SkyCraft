local Resume=require("PBZResume")
local Ipc=require("PBZIpcProbe")
local View=require("PBZViewBridge")
local MeshView=require("PBZMeshView")
local Collision=require("PBZCollision")
local meshFailed=false
local controller=nil
local failures=0
local started=os.time()
local duration=tonumber(os.getenv("PBZCRAFT_PROBE_SECONDS")) or 180
local deadline=tonumber(os.getenv("PBZCRAFT_DEADLINE_UNIX")) or math.huge
local lastState=""
local handle=nil
print("[PBZProbe] Single game-thread loop bridge trial loaded.\n")
handle=LoopInGameThreadWithDelay(16,function()
    if os.time()>=deadline or (duration>0 and os.time()-started>duration) or failures>=3 then
        if controller and controller:IsValid() then pcall(View.restore,controller,controller.Pawn) end
        CancelDelayedAction(handle)
        return
    end
    local ok,err=pcall(function()
        if not controller or not controller:IsValid() then controller=FindFirstOf("PlayerController") end
        if not controller or not controller:IsValid() then return end
        Resume.step(controller)
        local pawn=controller.Pawn
        if not pawn or not pawn:IsValid() or not pawn:IsA("/Script/Engine.Character") then return end
        Collision.step(pawn)
        Ipc.publish(controller,pawn,View.intent(),View.active())
        View.step(controller,pawn)
        if not meshFailed then
            local drawn,message=pcall(MeshView.step,pawn,View.active(),View.intent())
            if not drawn then meshFailed=true;print("[PBZProbe] Mesh test disabled after error: "..tostring(message).."\n") end
        end
        local state=pawn:GetFullName()
        if state~=lastState then lastState=state;print("[PBZProbe] Bridge player="..state.."\n") end
    end)
    if not ok then
        failures=failures+1
        print("[PBZProbe] Native bridge call failed: "..tostring(err).."\n")
        if controller and controller:IsValid() then pcall(View.restore,controller,controller.Pawn) end
    end
end)
